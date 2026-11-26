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
/// floor the model is never called: the abstention costs one embedding and no generation.
/// </summary>
public class ChatService(IEmbedder embedder, IChatModel chat, IChunkStore store, RagOptions options)
{
    public async IAsyncEnumerable<ChatEvent> AskAsync(AskRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        var (question, sessionId) = Validate(request);

        var query = RagPrompt.RetrievalQuery(question, request.History);
        var vector = (await embedder.EmbedAsync([query], isQuery: true, ct))[0];
        var hits = await store.SearchAsync(vector, embedder.Model, sessionId, options.TopK, ct);
        var topScore = hits.Count == 0 ? 0 : Math.Round(hits.Max(h => h.Score), 4);
        var grounded = RagPrompt.Grounded(hits, options.MinScore);

        if (grounded.Count == 0)
        {
            yield return new SourcesEvent(AnswerKind.Abstained, []);
            yield return new TokenEvent(RagPrompt.Abstention);
            yield return new DoneEvent(AnswerKind.Abstained, RagPrompt.Abstention, [], topScore);
            yield break;
        }

        var sources = grounded.Select((h, i) => new ChatSource(i + 1, h.DocId, h.Title, h.Section, h.Url,
            Math.Round(h.Score, 4), h.FromSession)).ToList();
        yield return new SourcesEvent(AnswerKind.Rag, sources);

        var messages = RagPrompt.Build(question, grounded, request.History, options.MaxContextChars);
        var answer = new StringBuilder();
        await foreach (var token in chat.StreamAsync(RagPrompt.System, messages, ct))
        {
            answer.Append(token);
            yield return new TokenEvent(token);
        }
        yield return new DoneEvent(AnswerKind.Rag, answer.ToString(), sources, topScore);
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
