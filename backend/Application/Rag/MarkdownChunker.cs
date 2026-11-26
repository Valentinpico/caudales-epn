using System.Text;
using System.Text.RegularExpressions;

namespace CaudalesPlatform.Api.Application.Rag;

/// <summary>One piece of a document. Section is the heading trail ("Metodología > Fase 2").</summary>
public sealed record TextChunk(string Section, string Text, int Index);

/// <summary>
/// Splits Markdown on its headings and cuts long sections into overlapping windows at line
/// boundaries. Every chunk carries its heading trail, so a piece cut from the middle of a
/// section still says where it comes from. Pure and deterministic.
/// </summary>
public static partial class MarkdownChunker
{
    public const int TargetSize = 1000;
    public const int MaxSize = 1200;
    public const int Overlap = 150;

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*\s*$")]
    private static partial Regex Heading();

    // Image alt text sometimes carries the only copy of a value; keep it as prose, drop the path.
    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\([^)]*\)")]
    private static partial Regex Image();

    public static List<TextChunk> Chunk(string markdown, string title)
    {
        var chunks = new List<TextChunk>();
        if (string.IsNullOrWhiteSpace(markdown)) return chunks;

        markdown = Image().Replace(markdown, m =>
            m.Groups["alt"].Value.Trim() is { Length: > 0 } alt ? $"Figura: {alt}" : "");

        var trail = new string?[6];
        var body = new StringBuilder();
        var inFence = false;

        void Flush()
        {
            var text = body.ToString().Trim();
            body.Clear();
            if (text.Length == 0) return;
            var parts = trail.Where(h => h is not null && h != title).ToList();
            var section = parts.Count == 0 ? title : string.Join(" > ", parts);
            foreach (var piece in Split(text))
                chunks.Add(new TextChunk(section, piece, chunks.Count));
        }

        foreach (var line in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.TrimStart().StartsWith("```")) inFence = !inFence;
            var m = inFence ? Match.Empty : Heading().Match(line);
            if (m.Success)
            {
                Flush();
                var level = m.Groups[1].Value.Length;
                trail[level - 1] = m.Groups[2].Value.Trim();
                for (var i = level; i < trail.Length; i++) trail[i] = null;
                continue;
            }
            body.Append(line).Append('\n');
        }
        Flush();
        return chunks;
    }

    private static IEnumerable<string> Split(string text)
    {
        if (text.Length <= MaxSize)
        {
            yield return text;
            yield break;
        }

        var start = 0;
        while (start < text.Length)
        {
            var end = Math.Min(start + TargetSize, text.Length);
            if (end < text.Length)
            {
                // Cut after the last full line that fits; a single overlong line is cut by size.
                var lastBreak = text.LastIndexOf('\n', end - 1, end - start);
                if (lastBreak > start) end = lastBreak + 1;
            }
            var piece = text[start..end].Trim();
            if (piece.Length > 0) yield return piece;
            if (end >= text.Length) yield break;

            // Overlap: step back, then forward to the next line start so the window opens clean.
            var next = Math.Max(start + 1, end - Overlap);
            var nl = text.IndexOf('\n', next, end - next);
            start = nl >= 0 && nl + 1 < end ? nl + 1 : next;
        }
    }
}
