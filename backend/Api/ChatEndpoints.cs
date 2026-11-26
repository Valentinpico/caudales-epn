using System.Text.Json;
using CaudalesPlatform.Api.Application.Rag;
using CaudalesPlatform.Api.Shared;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace CaudalesPlatform.Api.Api;

/// <summary>
/// Document assistant. /ask, /upload and DELETE /session are public and rate limited per IP
/// ("chat" policy); /reindex goes through the admin gate like every other mutation.
/// </summary>
public static class ChatEndpoints
{
    public const string RateLimitPolicy = "chat";

    public static void MapChat(this WebApplication app)
    {
        var g = app.MapGroup("/api/chat");

        g.MapGet("/health", async (RagOptions o, IChunkStore store, CorpusService corpus, CancellationToken ct) =>
        {
            IndexState? state = null;
            string? error = corpus.LastError;
            try { state = await store.GetStateAsync(ct); }
            catch (Exception e) when (e is not OperationCanceledException) { error ??= e.Message; }
            return Results.Ok(new
            {
                provider = o.Provider.ToLowerInvariant(),
                embeddingModel = o.EmbeddingModel,
                chatModel = o.ChatModel,
                chunks = state?.Chunks ?? 0,
                indexedModel = state?.Model,
                indexedAt = state?.IndexedAt,
                indexing = corpus.Indexing,
                minScore = o.MinScore,
                topK = o.TopK,
                sessionTtlMinutes = o.SessionTtlMinutes,
                lastError = error,
            });
        });

        g.MapPost("/reindex", (bool? force, CorpusService corpus, CancellationToken ct) =>
            corpus.IndexAsync(force ?? true, ct));

        // Server-sent events: sources, token*, done (or error if generation fails midway).
        g.MapPost("/ask", async (AskRequest body, ChatService chat, HttpContext ctx, CancellationToken ct) =>
        {
            chat.Validate(body);
            var response = ctx.Response;
            response.ContentType = "text/event-stream";
            response.Headers.CacheControl = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";
            ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            var json = ctx.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

            async Task Send(string evt, object data)
            {
                await response.WriteAsync($"event: {evt}\ndata: {JsonSerializer.Serialize(data, json)}\n\n", ct);
                await response.Body.FlushAsync(ct);
            }

            try
            {
                await foreach (var e in chat.AskAsync(body, ct))
                {
                    switch (e)
                    {
                        case SourcesEvent s: await Send("sources", new { kind = s.Kind, sources = s.Sources }); break;
                        case TokenEvent t: await Send("token", new { text = t.Text }); break;
                        case DoneEvent d: await Send("done", new { kind = d.Kind, answer = d.Answer, sources = d.Sources, topScore = d.TopScore }); break;
                    }
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ctx.RequestServices.GetRequiredService<ILogger<ChatService>>().LogError(e, "Falló la respuesta del asistente");
                await Send("error", new { message = "No se pudo generar la respuesta. Inténtelo de nuevo en unos minutos." });
            }
        }).RequireRateLimiting(RateLimitPolicy);

        g.MapPost("/upload", async (HttpRequest req, SessionFileService files, RagOptions o, CancellationToken ct) =>
        {
            if (req.ContentLength > o.MaxUploadBytes + 64 * 1024)
                throw new ValidationError($"El archivo supera el tamaño máximo permitido ({o.MaxUploadBytes / (1024 * 1024)} MB).");
            if (!req.HasFormContentType) throw new ValidationError("Envíe el archivo como multipart/form-data.");
            var form = await req.ReadFormAsync(ct);
            var file = form.Files.GetFile("file") ?? throw new ValidationError("Falta el archivo ('file').");
            await using var stream = file.OpenReadStream();
            return Results.Ok(await files.IngestAsync(form["sessionId"].ToString(), file.FileName, stream, ct));
        }).DisableAntiforgery().RequireRateLimiting(RateLimitPolicy);

        g.MapDelete("/session/{sessionId}", async (string sessionId, SessionFileService files, CancellationToken ct) =>
            Results.Ok(new { sessionId, deletedChunks = await files.DeleteSessionAsync(sessionId, ct) }))
            .RequireRateLimiting(RateLimitPolicy);
    }

    /// <summary>Chat routes open to visitors; everything else under /api/chat stays admin-only for mutations.</summary>
    public static bool IsPublic(string path) =>
        path.Equals("/api/chat/ask", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/chat/upload", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/api/chat/session/", StringComparison.OrdinalIgnoreCase);
}
