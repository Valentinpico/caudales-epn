using System.Text;
using System.Text.RegularExpressions;
using CaudalesPlatform.Api.Shared;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CaudalesPlatform.Api.Application.Rag;

public record UploadResult(string SessionId, string DocId, string Title, int Chunks, DateTimeOffset ExpiresAt);

/// <summary>
/// The user's own file, queried for one session only: chunked and embedded into the session
/// table, never into the permanent corpus, and purged once the TTL passes.
/// </summary>
public partial class SessionFileService(IEmbedder embedder, IChunkStore store, RagOptions options, TimeProvider clock)
{
    public static readonly string[] Extensions = [".pdf", ".md", ".txt"];

    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$")]
    private static partial Regex SessionIdPattern();

    public static string ValidateSessionId(string? sessionId) =>
        sessionId is not null && SessionIdPattern().IsMatch(sessionId)
            ? sessionId
            : throw new ValidationError("El identificador de sesión no es válido (8 a 64 letras, dígitos, '-' o '_').");

    public async Task<UploadResult> IngestAsync(string? sessionId, string fileName, Stream content, CancellationToken ct)
    {
        var session = ValidateSessionId(sessionId);
        var name = Path.GetFileName(fileName);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (!Extensions.Contains(ext)) throw new ValidationError("Solo se aceptan archivos PDF, Markdown (.md) o texto (.txt).");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length > options.MaxUploadBytes)
            throw new ValidationError($"El archivo supera el tamaño máximo permitido ({options.MaxUploadBytes / (1024 * 1024)} MB).");

        var text = ext == ".pdf" ? ExtractPdf(buffer.ToArray()) : Encoding.UTF8.GetString(buffer.ToArray());
        var title = Path.GetFileNameWithoutExtension(name);
        var pieces = MarkdownChunker.Chunk(text, title);
        if (pieces.Count == 0) throw new ValidationError("No se pudo extraer texto del archivo.");
        if (pieces.Count > options.MaxChunksPerUpload)
            throw new ValidationError("El archivo es demasiado extenso para consultarlo en el chat.");

        var docId = $"sesion:{Guid.NewGuid():N}";
        var vectors = await embedder.EmbedAsync(pieces.Select(p => $"{title}\n{p.Section}\n\n{p.Text}").ToList(), false, ct);
        var records = pieces.Select((p, i) => new ChunkRecord(docId, title, p.Section, null, p.Index, p.Text, vectors[i])).ToList();
        await store.AddSessionChunksAsync(session, records, embedder.Model, ct);

        return new UploadResult(session, docId, title, records.Count,
            clock.GetUtcNow().AddMinutes(options.SessionTtlMinutes));
    }

    public Task<int> DeleteSessionAsync(string sessionId, CancellationToken ct) =>
        store.DeleteSessionAsync(ValidateSessionId(sessionId), ct);

    /// <summary>Deletes every session chunk uploaded more than the TTL ago.</summary>
    public Task<int> PurgeExpiredAsync(CancellationToken ct) =>
        store.PurgeSessionsOlderThanAsync(clock.GetUtcNow().AddMinutes(-options.SessionTtlMinutes), ct);

    private static string ExtractPdf(byte[] bytes)
    {
        try
        {
            using var pdf = PdfDocument.Open(bytes);
            return string.Join("\n\n", pdf.GetPages().Select(p => ContentOrderTextExtractor.GetText(p)));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            throw new ValidationError("No se pudo leer el PDF. Verifica que no esté dañado ni protegido.");
        }
    }
}
