using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using CaudalesPlatform.Api.Infrastructure;

namespace CaudalesPlatform.Api.Application.Rag;

public record IndexResult(bool Reindexed, int Documents, int Chunks, string Model, DateTimeOffset? IndexedAt);

/// <summary>
/// Builds the permanent corpus (Markdown roots + database catalog), chunks and embeds it.
/// A fingerprint of model + chunk texts is stored with the index: when nothing changed the run
/// is a no-op, and switching the embedding model always forces a full reindex.
/// </summary>
public partial class CorpusService(
    IServiceScopeFactory scopes, IChunkStore store, IEmbedder embedder, RagOptions options,
    IHostEnvironment env, ILogger<CorpusService> logger)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public bool Indexing => Gate.CurrentCount == 0;
    public string? LastError { get; private set; }

    [GeneratedRegex(@"^\s*<!--\s*source:\s*(?<url>\S+)\s*-->", RegexOptions.Multiline)]
    private static partial Regex SourceComment();

    [GeneratedRegex(@"^#\s+(?<title>.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex FirstH1();

    public async Task<IndexResult> IndexAsync(bool force, CancellationToken ct)
    {
        List<CorpusDoc> docs;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var series = scope.ServiceProvider.GetRequiredService<SeriesService>();
            docs = [.. ReadMarkdown(ResolveRoots()), .. await CatalogProjection.BuildAsync(db, series)];
        }
        return await IndexDocsAsync(docs, force, ct);
    }

    public async Task<IndexResult> IndexDocsAsync(IReadOnlyList<CorpusDoc> docs, bool force, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            var pieces = docs.SelectMany(d => MarkdownChunker.Chunk(d.Markdown, d.Title).Select(c => (Doc: d, Chunk: c))).ToList();
            var fingerprint = Fingerprint(embedder.Model, pieces.Select(p => $"{p.Doc.DocId}|{p.Doc.Url}|{p.Chunk.Section}|{p.Chunk.Text}"));

            var state = await store.GetStateAsync(ct);
            if (!force && state.Fingerprint == fingerprint && state.Model == embedder.Model)
                return new IndexResult(false, docs.Count, state.Chunks, embedder.Model, state.IndexedAt);

            logger.LogInformation("Indexando corpus RAG: {Docs} documentos, {Chunks} fragmentos, modelo {Model}",
                docs.Count, pieces.Count, embedder.Model);
            var vectors = await embedder.EmbedAsync(pieces.Select(p => EmbedText(p.Doc.Title, p.Chunk)).ToList(), false, ct);
            var records = pieces.Select((p, i) => new ChunkRecord(p.Doc.DocId, p.Doc.Title, p.Chunk.Section, p.Doc.Url,
                p.Chunk.Index, p.Chunk.Text, vectors[i])).ToList();
            await store.ReplaceCorpusAsync(records, embedder.Model, fingerprint, ct);
            LastError = null;

            var after = await store.GetStateAsync(ct);
            return new IndexResult(true, docs.Count, records.Count, embedder.Model, after.IndexedAt);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LastError = e.Message;
            throw;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Title and heading trail travel with every chunk into the embedding.</summary>
    public static string EmbedText(string title, TextChunk chunk) => $"{title}\n{chunk.Section}\n\n{chunk.Text}";

    public static string Fingerprint(string model, IEnumerable<string> parts)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha.AppendData(Encoding.UTF8.GetBytes(model));
        foreach (var p in parts) sha.AppendData(Encoding.UTF8.GetBytes("\n\u0000" + p));
        return Convert.ToHexString(sha.GetHashAndReset());
    }

    /// <summary>Every *.md under the roots, sorted for a stable fingerprint. DocId is the file name.</summary>
    public static List<CorpusDoc> ReadMarkdown(IEnumerable<string> roots)
    {
        var docs = new List<CorpusDoc>();
        foreach (var root in roots.Where(Directory.Exists))
        foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var text = File.ReadAllText(file);
            var docId = Path.GetFileNameWithoutExtension(file);
            var title = FirstH1().Match(text) is { Success: true } m ? m.Groups["title"].Value : docId;
            var url = SourceComment().Match(text) is { Success: true } s ? s.Groups["url"].Value : null;
            docs.Add(new CorpusDoc(docId, title, url, text));
        }
        return docs;
    }

    /// <summary>Relative roots are looked up under the content root, then under its parent (repo root in dev).</summary>
    private IEnumerable<string> ResolveRoots()
    {
        foreach (var raw in options.CorpusRoots.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Path.IsPathRooted(raw)) { yield return raw; continue; }
            var candidates = new[] { Path.Combine(env.ContentRootPath, raw), Path.Combine(env.ContentRootPath, "..", raw) };
            var found = candidates.FirstOrDefault(Directory.Exists);
            if (found is null) logger.LogWarning("Raíz de corpus no encontrada: {Root}", raw);
            else yield return Path.GetFullPath(found);
        }
    }
}
