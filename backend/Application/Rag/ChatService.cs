using System.Runtime.CompilerServices;
using System.Text;
using CaudalesPlatform.Api.Shared;

namespace CaudalesPlatform.Api.Application.Rag;

public record AskRequest(string Question, string? SessionId = null, List<ChatTurn>? History = null);

/// <summary>Source cited as [N] in the answer.</summary>
public record ChatSource(int N, string DocId, string Title, string Section, string? Url, double Score, bool FromSession);

public abstract record ChatEvent;
public record SourcesEvent(string Kind, List<ChatSource> Sources) : ChatEvent;
public record TokenEvent(string Text) : ChatEvent;
public record DoneEvent(string Kind, string Answer, List<ChatSource> Sources, double TopScore) : ChatEvent;

public static class AnswerKind
{
    public const string Rag = "rag";
    public const string Abstained = "abstained";
}

/// <summary>
/// Retrieve, apply the grounding floor, then stream a grounded answer. When nothing clears the
/// floor the model is never called: the abstention costs one embedding and no generation. When
/// the model itself finds no support it replies with a marker, which also becomes an abstention.
/// </summary>
public class ChatService(IEmbedder embedder, IChatModel chat, IChunkStore store, RagOptions options)
{
    public async IAsyncEnumerable<ChatEvent> AskAsync(AskRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var (question, sessionId) = Validate(request);
        var (grounded, topScore) = await RetrieveAsync(question, sessionId, request.History, ct);

        if (grounded.Count == 0)
        {
            foreach (var e in Abstain(topScore, sourcesSent: false)) yield return e;
            yield break;
        }

        var sources = grounded.Select((h, i) => new ChatSource(i + 1, h.DocId, h.Title, h.Section, h.Url,
            Math.Round(h.Score, 4), h.FromSession)).ToList();
        yield return new SourcesEvent(AnswerKind.Rag, sources);

        var messages = RagPrompt.Build(question, grounded, request.History, options.MaxContextChars);
        var answer = new StringBuilder();
        var state = MarkerState.Undecided;
        await foreach (var token in chat.StreamAsync(RagPrompt.System, messages, ct))
        {
            answer.Append(token);
            if (state == MarkerState.Undecided)
            {
                // Hold the first tokens back until they either spell the marker or diverge from it.
                state = RagPrompt.Classify(answer.ToString());
                if (state == MarkerState.Marker) break;
                if (state == MarkerState.Answer) yield return new TokenEvent(answer.ToString());
                continue;
            }
            yield return new TokenEvent(token);
        }

        if (state == MarkerState.Marker)
        {
            foreach (var e in Abstain(topScore, sourcesSent: true)) yield return e;
            yield break;
        }
        // A stream that ended while still ambiguous (e.g. "[") is a plain answer.
        if (state == MarkerState.Undecided && answer.Length > 0) yield return new TokenEvent(answer.ToString());
        yield return new DoneEvent(AnswerKind.Rag, answer.ToString(), sources, topScore);
    }

    /// <summary>
    /// The grounding floor is judged on the question alone, so history cannot carry an unrelated
    /// question over it. The history-augmented query (embedded in the same call) then ranks the
    /// context; only a short follow-up, too thin to score by itself, may clear the floor through it.
    /// </summary>
    private async Task<(List<ScoredChunk> Grounded, double TopScore)> RetrieveAsync(string question, string? sessionId,
        IReadOnlyList<ChatTurn>? history, CancellationToken ct)
    {
        var augmented = RagPrompt.RetrievalQuery(question, history);
        var texts = augmented == question ? new[] { question } : [question, augmented];
        var vectors = await embedder.EmbedAsync(texts, isQuery: true, ct);

        var hits = await SearchAsync(vectors[0], sessionId, ct);
        var grounded = RagPrompt.Grounded(hits, options.MinScore);
        var topScore = TopScore(hits);
        if (vectors.Length == 1 || (grounded.Count == 0 && !RagPrompt.IsFollowUp(question))) return (grounded, topScore);

        var augmentedHits = await SearchAsync(vectors[1], sessionId, ct);
        var augmentedGrounded = RagPrompt.Grounded(augmentedHits, options.MinScore);
        return augmentedGrounded.Count == 0 ? (grounded, topScore) : (augmentedGrounded, Math.Max(topScore, TopScore(augmentedHits)));
    }

    private async Task<List<ScoredChunk>> SearchAsync(float[] vector, string? sessionId, CancellationToken ct) =>
        RagPrompt.Diversify(await store.SearchAsync(vector, embedder.Model, sessionId, options.TopK * RagPrompt.SearchOverfetch, ct), options.TopK);

    private static double TopScore(List<ScoredChunk> hits) => hits.Count == 0 ? 0 : Math.Round(hits.Max(h => h.Score), 4);

    private static IEnumerable<ChatEvent> Abstain(double topScore, bool sourcesSent)
    {
        if (!sourcesSent) yield return new SourcesEvent(AnswerKind.Abstained, []);
        yield return new TokenEvent(RagPrompt.Abstention);
        yield return new DoneEvent(AnswerKind.Abstained, RagPrompt.Abstention, [], topScore);
    }

    /// <summary>Checked before the stream opens, so a bad request still gets a plain 400.</summary>
    public (string Question, string? SessionId) Validate(AskRequest request)
    {
        var question = request.Question?.Trim() ?? "";
        if (question.Length == 0) throw new ValidationError("La pregunta no puede estar vacía.");
        if (question.Length > options.MaxQuestionChars)
            throw new ValidationError($"La pregunta supera los {options.MaxQuestionChars} caracteres.");
        var sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? null : SessionFileService.ValidateSessionId(request.SessionId);
        return (question, sessionId);
    }
}
