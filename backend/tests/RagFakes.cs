using System.Runtime.CompilerServices;
using CaudalesPlatform.Api.Application.Rag;

namespace CaudalesPlatform.Tests;

internal sealed class FakeEmbedder : IEmbedder
{
    public string Model { get; set; } = "fake-embed";
    public List<(IReadOnlyList<string> Texts, bool IsQuery)> Calls { get; } = [];

    public Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, bool isQuery, CancellationToken ct)
    {
        Calls.Add((texts, isQuery));
        return Task.FromResult(texts.Select(t => new[] { t.Length, 1f }).ToArray());
    }
}

internal sealed class FakeChat(params string[] tokens) : IChatModel
{
    public string Model => "fake-chat";
    public List<(string System, IReadOnlyList<ChatTurn> Messages)> Calls { get; } = [];

    public async IAsyncEnumerable<string> StreamAsync(string system, IReadOnlyList<ChatTurn> messages,
        [EnumeratorCancellation] CancellationToken ct)
    {
        Calls.Add((system, messages));
        foreach (var t in tokens)
        {
            await Task.Yield();
            yield return t;
        }
    }
}

internal sealed class FakeStore : IChunkStore
{
    public List<ScoredChunk> Hits { get; set; } = [];
    public List<ChunkRecord> Corpus { get; } = [];
    public Dictionary<string, List<ChunkRecord>> Sessions { get; } = [];
    public IndexState State { get; set; } = new(0, null, null, null);
    public int ReplaceCalls { get; private set; }
    public DateTimeOffset? LastPurgeCutoff { get; private set; }
    public (string? SessionId, int K)? LastSearch { get; private set; }
    /// <summary>Optional per-query hits; FakeEmbedder encodes the text length in the first component.</summary>
    public Func<float[], List<ScoredChunk>>? HitsFor { get; set; }
    public List<float[]> Searches { get; } = [];

    public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<IndexState> GetStateAsync(CancellationToken ct) => Task.FromResult(State);

    public Task ReplaceCorpusAsync(IReadOnlyList<ChunkRecord> chunks, string model, string fingerprint, CancellationToken ct)
    {
        ReplaceCalls++;
        Corpus.Clear();
        Corpus.AddRange(chunks);
        State = new IndexState(chunks.Count, model, fingerprint, DateTimeOffset.UnixEpoch);
        return Task.CompletedTask;
    }

    public Task<List<ScoredChunk>> SearchAsync(float[] query, string model, string? sessionId, int k, CancellationToken ct)
    {
        LastSearch = (sessionId, k);
        Searches.Add(query);
        return Task.FromResult((HitsFor?.Invoke(query) ?? Hits).Where(h => !h.FromSession || sessionId is not null).Take(k).ToList());
    }

    public Task AddSessionChunksAsync(string sessionId, IReadOnlyList<ChunkRecord> chunks, string model, CancellationToken ct)
    {
        Sessions.TryAdd(sessionId, []);
        Sessions[sessionId].AddRange(chunks);
        return Task.CompletedTask;
    }

    public Task<int> DeleteSessionAsync(string sessionId, CancellationToken ct) =>
        Task.FromResult(Sessions.Remove(sessionId, out var removed) ? removed.Count : 0);

    public Task<int> PurgeSessionsOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        LastPurgeCutoff = cutoff;
        return Task.FromResult(0);
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
