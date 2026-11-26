using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using CaudalesPlatform.Api.Application.Rag;
using CaudalesPlatform.Api.Shared;

namespace CaudalesPlatform.Api.Infrastructure.Rag;

/// <summary>Picks the embedding and chat implementations from Rag:Provider. Clients are created once and reused.</summary>
public record RagModels(IEmbedder Embedder, IChatModel Chat)
{
    /// <summary>False when the provider could not be configured; /api/chat then answers 503.</summary>
    public bool Available { get; init; } = true;

    public const string UnavailableMessage = "El asistente no está disponible en este momento.";

    public static RagModels Unavailable()
    {
        var model = new UnavailableModel();
        return new(model, model) { Available = false };
    }

    private sealed class UnavailableModel : IEmbedder, IChatModel
    {
        public string Model => "no disponible";
        public Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct) =>
            throw new ServiceUnavailableError(UnavailableMessage);
        public IAsyncEnumerable<string> StreamAsync(string system, IReadOnlyList<ChatTurn> messages, CancellationToken ct) =>
            throw new ServiceUnavailableError(UnavailableMessage);
    }
}

public static class RagProviders
{
    public static RagModels Create(
        RagOptions o, Func<IAmazonBedrockRuntime> bedrock, Func<HttpClient> http)
    {
        switch (o.Provider.Trim().ToLowerInvariant())
        {
            case "bedrock":
                var client = bedrock();
                return new(new BedrockEmbedder(client, o.BedrockEmbeddingModel),
                        new BedrockChat(client, o.BedrockChatModel, o.MaxTokens));
            case "openai":
                if (string.IsNullOrWhiteSpace(o.OpenAiApiKey))
                    throw new InvalidOperationException("Rag:Provider=openai requiere OPENAI_API_KEY.");
                var httpClient = http();
                return new(new OpenAiEmbedder(httpClient, o.OpenAiEmbeddingModel, o.OpenAiApiKey),
                        new OpenAiChat(httpClient, o.OpenAiChatModel, o.OpenAiApiKey, o.MaxTokens));
            default:
                throw new InvalidOperationException($"Proveedor RAG desconocido '{o.Provider}'. Use 'bedrock' u 'openai'.");
        }
    }

    /// <summary>
    /// Same as Create, but a misconfigured provider (missing key, unresolvable credentials) is
    /// logged and turned into an unavailable assistant instead of breaking the rest of the API.
    /// </summary>
    public static RagModels CreateOrUnavailable(
        RagOptions o, Func<IAmazonBedrockRuntime> bedrock, Func<HttpClient> http, ILogger logger)
    {
        try { return Create(o, bedrock, http); }
        catch (Exception e)
        {
            logger.LogError(e, "No se pudo configurar el proveedor RAG '{Provider}'; el asistente queda deshabilitado", o.Provider);
            return RagModels.Unavailable();
        }
    }

    public static IAmazonBedrockRuntime BedrockClient(RagOptions o) => new AmazonBedrockRuntimeClient(BedrockConfig(o));

    /// <summary>
    /// Standard retries with backoff absorb throttled bursts of embedding calls. Not Adaptive: its
    /// client-side rate limiter, shared by every request of this singleton client, kept refusing
    /// chat calls ("capacity could not be obtained") for minutes after Bedrock had recovered.
    /// </summary>
    public static AmazonBedrockRuntimeConfig BedrockConfig(RagOptions o) => new()
    {
        RegionEndpoint = RegionEndpoint.GetBySystemName(o.Region),
        RetryMode = Amazon.Runtime.RequestRetryMode.Standard,
        MaxErrorRetry = 6,
    };

    public static HttpClient OpenAiHttpClient(RagOptions o) =>
        new() { BaseAddress = new Uri(o.OpenAiBaseUrl), Timeout = TimeSpan.FromMinutes(2) };
}

/// <summary>Cohere Embed v3 on Bedrock: 1024 dims, separate document/query input types, 96 texts per call.</summary>
public sealed class BedrockEmbedder(IAmazonBedrockRuntime client, string model) : IEmbedder
{
    public const int MaxBatch = 96;
    public string Model => model;

    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct)
    {
        var result = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(MaxBatch))
        {
            var response = await client.InvokeModelAsync(new InvokeModelRequest
            {
                ModelId = model, ContentType = "application/json", Accept = "application/json",
                Body = new MemoryStream(Encoding.UTF8.GetBytes(CohereBody(batch, isQuery))),
            }, ct);
            result.AddRange(ParseCohere(response.Body));
        }
        return [.. result];
    }

    public static string CohereBody(IEnumerable<string> texts, bool isQuery) => JsonSerializer.Serialize(new
    {
        texts, input_type = isQuery ? "search_query" : "search_document", truncate = "END",
    });

    public static float[][] ParseCohere(Stream body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("embeddings").EnumerateArray()
            .Select(v => v.EnumerateArray().Select(x => x.GetSingle()).ToArray()).ToArray();
    }
}

/// <summary>Bedrock Converse streaming (Claude Haiku 4.5 by default).</summary>
public sealed class BedrockChat(IAmazonBedrockRuntime client, string model, int maxTokens) : IChatModel
{
    public string Model => model;

    public async IAsyncEnumerable<string> StreamAsync(string system, IReadOnlyList<ChatTurn> messages,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var response = await client.ConverseStreamAsync(new ConverseStreamRequest
        {
            ModelId = model,
            System = [new SystemContentBlock { Text = system }],
            Messages = messages.Select(m => new Message
            {
                Role = m.Role == "assistant" ? ConversationRole.Assistant : ConversationRole.User,
                Content = [new ContentBlock { Text = m.Content }],
            }).ToList(),
            InferenceConfig = new InferenceConfiguration { MaxTokens = maxTokens, Temperature = 0.2f },
        }, ct);

        // Disposing the event stream closes the HTTP response, so a client that stops reading
        // (disconnect or the no-support marker) also stops the generation being billed.
        using var stream = response.Stream;
        await foreach (var e in stream.WithCancellation(ct))
            if (e is ContentBlockDeltaEvent { Delta.Text: { Length: > 0 } text }) yield return text;
    }
}

/// <summary>OpenAI embeddings, shortened to 1024 dims so both providers share the pgvector column.</summary>
public sealed class OpenAiEmbedder(HttpClient http, string model, string apiKey) : IEmbedder
{
    public const int MaxBatch = 512;
    public string Model => model;

    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct)
    {
        var result = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(MaxBatch))
        {
            using var request = OpenAi.Request("embeddings", apiKey,
                new { model, input = batch, dimensions = RagOptions.Dimensions });
            using var response = await http.SendAsync(request, ct);
            await OpenAi.EnsureSuccess(response, ct);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            result.AddRange(doc.RootElement.GetProperty("data").EnumerateArray()
                .OrderBy(d => d.GetProperty("index").GetInt32())
                .Select(d => d.GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray()));
        }
        return [.. result];
    }
}

/// <summary>OpenAI chat completions with server-sent events.</summary>
public sealed class OpenAiChat(HttpClient http, string model, string apiKey, int maxTokens) : IChatModel
{
    public string Model => model;

    public async IAsyncEnumerable<string> StreamAsync(string system, IReadOnlyList<ChatTurn> messages,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var all = new[] { new { role = "system", content = system } }
            .Concat(messages.Select(m => new { role = m.Role, content = m.Content }));
        using var request = OpenAi.Request("chat/completions", apiKey,
            new { model, stream = true, temperature = 0.2, max_tokens = maxTokens, messages = all });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await OpenAi.EnsureSuccess(response, ct);

        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(ct));
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (!line.StartsWith("data:")) continue;
            var data = line[5..].Trim();
            if (data == "[DONE]") yield break;
            using var doc = JsonDocument.Parse(data);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0) continue;
            if (choices[0].GetProperty("delta").TryGetProperty("content", out var c) && c.GetString() is { Length: > 0 } text)
                yield return text;
        }
    }
}

internal static class OpenAi
{
    public static HttpRequestMessage Request(string path, string apiKey, object body) => new(HttpMethod.Post, path)
    {
        Headers = { Authorization = new AuthenticationHeaderValue("Bearer", apiKey) },
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    public static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException($"OpenAI respondió {(int)response.StatusCode}: {body[..Math.Min(body.Length, 300)]}");
    }
}
