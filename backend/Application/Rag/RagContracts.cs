namespace CaudalesPlatform.Api.Application.Rag;

/// <summary>Configuration section "Rag" (env RAG__*). Defaults target AWS Bedrock in us-east-1.</summary>
public class RagOptions
{
    /// <summary>pgvector column size; both providers are asked for this many dimensions.</summary>
    public const int Dimensions = 1024;

    public string Provider { get; set; } = "bedrock";          // bedrock | openai
    public string Region { get; set; } = "us-east-1";
    public string BedrockEmbeddingModel { get; set; } = "cohere.embed-multilingual-v3";
    public string BedrockChatModel { get; set; } = "us.anthropic.claude-haiku-4-5-20251001-v1:0";
    public string OpenAiEmbeddingModel { get; set; } = "text-embedding-3-small";
    public string OpenAiChatModel { get; set; } = "gpt-4o-mini";
    public string? OpenAiApiKey { get; set; }
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1/";

    /// <summary>Comma-separated Markdown roots, relative to the repo root or the content root.</summary>
    public string CorpusRoots { get; set; } = "docs/informes,docs/corpus";
    public bool IndexOnStartup { get; set; } = true;

    public int TopK { get; set; } = 8;
    /// <summary>Grounding floor: when every corpus hit scores below it, answer is abstention, no LLM call.</summary>
    public double MinScore { get; set; } = 0.35;
    public int MaxContextChars { get; set; } = 12_000;
    public int MaxTokens { get; set; } = 1024;
    public int MaxQuestionChars { get; set; } = 1000;
    public int RateLimitPerMinute { get; set; } = 10;

    public int SessionTtlMinutes { get; set; } = 120;
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxChunksPerUpload { get; set; } = 300;

    public string EmbeddingModel => IsOpenAi ? OpenAiEmbeddingModel : BedrockEmbeddingModel;
    public string ChatModel => IsOpenAi ? OpenAiChatModel : BedrockChatModel;
    private bool IsOpenAi => Provider.Equals("openai", StringComparison.OrdinalIgnoreCase);
}

public record ChatTurn(string Role, string Content);    // role: user | assistant

public interface IEmbedder
{
    string Model { get; }
    /// <summary>One vector per text, same order. isQuery selects the query-side embedding when the model has one.</summary>
    Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct);
}

public interface IChatModel
{
    string Model { get; }
    IAsyncEnumerable<string> StreamAsync(string system, IReadOnlyList<ChatTurn> messages, CancellationToken ct);
}

/// <summary>Chunk ready to store. Embedding already computed.</summary>
public record ChunkRecord(string DocId, string Title, string Section, string? Url, int Index, string Content, float[] Embedding);

public record ScoredChunk(string DocId, string Title, string Section, string? Url, string Content, double Score, bool FromSession);

public record IndexState(int Chunks, string? Model, string? Fingerprint, DateTimeOffset? IndexedAt);

/// <summary>pgvector storage: permanent corpus plus per-session temporary chunks.</summary>
public interface IChunkStore
{
    Task EnsureSchemaAsync(CancellationToken ct);
    Task<IndexState> GetStateAsync(CancellationToken ct);
    Task ReplaceCorpusAsync(IReadOnlyList<ChunkRecord> chunks, string model, string fingerprint, CancellationToken ct);
    Task<List<ScoredChunk>> SearchAsync(float[] query, string model, string? sessionId, int k, CancellationToken ct);
    Task AddSessionChunksAsync(string sessionId, IReadOnlyList<ChunkRecord> chunks, string model, CancellationToken ct);
    Task<int> DeleteSessionAsync(string sessionId, CancellationToken ct);
    Task<int> PurgeSessionsOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct);
}
