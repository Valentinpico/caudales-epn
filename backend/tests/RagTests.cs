using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using Amazon.BedrockRuntime;
using Amazon.Runtime;
using CaudalesPlatform.Api.Application;
using CaudalesPlatform.Api.Application.Rag;
using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;
using CaudalesPlatform.Api.Infrastructure.Rag;
using CaudalesPlatform.Api.Shared;

namespace CaudalesPlatform.Tests;

public class MarkdownChunkerTests
{
    [Fact]
    public void Splits_on_headings_and_keeps_the_heading_trail()
    {
        var md = "# Informe\n\nIntro.\n\n## Metodología\n\n### Fase 2\n\nClimatol.\n\n## Equipo\n\nDirectora.";
        var chunks = MarkdownChunker.Chunk(md, "Informe");

        Assert.Equal(["Informe", "Metodología > Fase 2", "Equipo"], chunks.Select(c => c.Section));
        Assert.Equal("Climatol.", chunks[1].Text);
        Assert.Equal([0, 1, 2], chunks.Select(c => c.Index));
    }

    [Fact]
    public void Empty_input_and_heading_only_sections_produce_no_chunks()
    {
        Assert.Empty(MarkdownChunker.Chunk("  \n", "x"));
        Assert.Single(MarkdownChunker.Chunk("# A\n## B\n## C\ntexto", "A"));
    }

    [Fact]
    public void Long_sections_are_cut_at_line_boundaries_with_overlap()
    {
        var lines = Enumerable.Range(1, 80).Select(i => $"Línea número {i:000} con algo de contenido.");
        var chunks = MarkdownChunker.Chunk("## Larga\n" + string.Join("\n", lines), "Doc");

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Text.Length <= MarkdownChunker.MaxSize));
        Assert.All(chunks, c => Assert.StartsWith("Línea número", c.Text));
        Assert.All(chunks, c => Assert.EndsWith("contenido.", c.Text));
        // Consecutive windows share at least one line.
        var lastLineOfFirst = chunks[0].Text.Split('\n')[^1];
        Assert.Contains(lastLineOfFirst, chunks[1].Text);
        Assert.All(chunks, c => Assert.Equal("Larga", c.Section));
    }

    [Fact]
    public void Image_alt_text_becomes_prose_and_fenced_hashes_are_not_headings()
    {
        var md = "![Mapa de estaciones del INAMHI](img/mapa.png)\n```\n# comentario\n```";
        var chunk = Assert.Single(MarkdownChunker.Chunk(md, "Doc"));
        Assert.Contains("Figura: Mapa de estaciones del INAMHI", chunk.Text);
        Assert.Contains("# comentario", chunk.Text);
    }
}

public class RagPromptTests
{
    private static ScoredChunk Hit(double score, bool session = false, string content = "texto") =>
        new("doc", "Título", "Sección", null, content, score, session);

    [Fact]
    public void Grounding_floor_drops_weak_corpus_hits_but_keeps_session_hits()
    {
        var grounded = RagPrompt.Grounded([Hit(0.6), Hit(0.2), Hit(0.1, session: true)], 0.35);
        Assert.Equal([0.6, 0.1], grounded.Select(h => h.Score));
    }

    [Fact]
    public void Context_is_numbered_fenced_and_bounded()
    {
        var ctx = RagPrompt.Context([Hit(0.9, content: "uno"), Hit(0.8, content: new string('x', 50))], 20);
        Assert.Contains("[1] Título — Sección", ctx);
        Assert.Contains("<documento n=\"1\">\nuno\n</documento>", ctx);
        Assert.Contains(new string('x', 17), ctx);
        Assert.DoesNotContain(new string('x', 18), ctx);
    }

    [Fact]
    public void Build_keeps_short_alternating_history_and_ends_with_the_question()
    {
        List<ChatTurn> history =
        [
            new("assistant", "saludo suelto"),          // dropped: history must start with the user
            new("user", "¿Quién dirige el proyecto?"),
            new("assistant", "Adriana Uquillas [1]."),
            new("user", "pregunta sin respuesta"),       // dropped: two user turns in a row otherwise
        ];
        var turns = RagPrompt.Build("¿Y el codirector?", [Hit(0.9)], history, 1000);

        Assert.Equal(["user", "assistant", "user"], turns.Select(t => t.Role));
        Assert.EndsWith("Pregunta: ¿Y el codirector?", turns[^1].Content);
        Assert.Contains("<documento", turns[^1].Content);
    }

    [Fact]
    public void Retrieval_query_prepends_the_previous_user_turn()
    {
        Assert.Equal("¿Y el codirector?", RagPrompt.RetrievalQuery("¿Y el codirector?", null));
        Assert.Equal("¿Quién dirige?\n¿Y el codirector?",
            RagPrompt.RetrievalQuery("¿Y el codirector?", [new("user", "¿Quién dirige?"), new("assistant", "X")]));
    }

    [Fact]
    public void Diversify_caps_chunks_per_section_and_keeps_rank_order()
    {
        ScoredChunk H(string section, double score) => new("d", "T", section, null, section, score, false);
        var hits = new[] { H("Estaciones", 0.70), H("Estaciones", 0.69), H("Estaciones", 0.68), H("Estaciones", 0.67), H("Cronograma", 0.60), H("Resumen", 0.50) };

        var picked = RagPrompt.Diversify(hits, k: 4);

        Assert.Equal(RagPrompt.MaxChunksPerSection, picked.Count(h => h.Section == "Estaciones"));
        Assert.Equal([0.70, 0.69, 0.68, 0.60], picked.Select(h => h.Score));
    }

    [Theory]
    [InlineData("¿Y quién la codirige?", true)]
    [InlineData("¿Y el presupuesto?", true)]
    [InlineData("¿Cuál es la capital de Francia?", false)]
    [InlineData("¿Qué método de homogeneización se aplica a las series?", false)]
    public void Short_questions_count_as_follow_ups(string question, bool expected) =>
        Assert.Equal(expected, RagPrompt.IsFollowUp(question));

    [Theory]
    [InlineData("", MarkerState.Undecided)]
    [InlineData("  \n", MarkerState.Undecided)]
    [InlineData("[[SIN_RES", MarkerState.Undecided)]
    [InlineData(" [[SIN_RESPALDO]]", MarkerState.Marker)]
    [InlineData("[[SIN_RESPALDO]] extra", MarkerState.Marker)]
    [InlineData("El proyecto", MarkerState.Answer)]
    [InlineData("[1", MarkerState.Answer)]
    [InlineData("[[OTRO", MarkerState.Answer)]
    public void Classifies_the_start_of_the_stream_against_the_marker(string start, MarkerState expected) =>
        Assert.Equal(expected, RagPrompt.Classify(start));

    [Fact]
    public void System_prompt_pins_grounding_citations_and_language()
    {
        Assert.Contains("ÚNICAMENTE", RagPrompt.System);
        Assert.Contains("[2]", RagPrompt.System);
        Assert.Contains("sin voseo", RagPrompt.System);
        Assert.Contains("usted", RagPrompt.System);
        Assert.Contains(RagPrompt.NoSupportMarker, RagPrompt.System);
        Assert.Contains("Intente reformular", RagPrompt.Abstention);
    }
}

public class ChatServiceTests
{
    private readonly FakeEmbedder _embedder = new();
    private readonly FakeChat _chat = new("El proyecto ", "usa Climatol [1].");
    private readonly FakeStore _store = new();
    private readonly RagOptions _options = new() { MinScore = 0.35, TopK = 4 };

    private ChatService Service() => new(_embedder, _chat, _store, _options);

    private async Task<List<ChatEvent>> Ask(AskRequest req)
    {
        var events = new List<ChatEvent>();
        await foreach (var e in Service().AskAsync(req, CancellationToken.None)) events.Add(e);
        return events;
    }

    [Fact]
    public async Task Abstains_without_calling_the_model_when_every_hit_is_below_the_floor()
    {
        _store.Hits = [new("d", "T", "S", null, "x", 0.21, false)];
        var events = await Ask(new("¿Cuál es la capital de Francia?"));

        Assert.Empty(_chat.Calls);
        var done = Assert.IsType<DoneEvent>(events[^1]);
        Assert.Equal(AnswerKind.Abstained, done.Kind);
        Assert.Equal(RagPrompt.Abstention, done.Answer);
        Assert.Equal(0.21, done.TopScore);
        Assert.Equal(AnswerKind.Abstained, Assert.IsType<SourcesEvent>(events[0]).Kind);
    }

    [Fact]
    public async Task Streams_sources_then_tokens_then_done_with_numbered_sources()
    {
        _store.Hits =
        [
            new("propuesta", "Postulación", "Metodología", "/files/p.pdf", "Climatol...", 0.62, false),
            new("informe", "Informe", "Datos", null, "INAMHI...", 0.30, false),
        ];
        var events = await Ask(new("¿Qué método de homogeneización usa?"));

        var sources = Assert.IsType<SourcesEvent>(events[0]);
        Assert.Equal(AnswerKind.Rag, sources.Kind);
        var only = Assert.Single(sources.Sources);
        Assert.Equal((1, "propuesta", "/files/p.pdf"), (only.N, only.DocId, only.Url));
        Assert.Equal(["El proyecto ", "usa Climatol [1]."], events.OfType<TokenEvent>().Select(t => t.Text));
        var done = Assert.IsType<DoneEvent>(events[^1]);
        Assert.Equal("El proyecto usa Climatol [1].", done.Answer);

        var call = Assert.Single(_chat.Calls);
        Assert.Equal(RagPrompt.System, call.System);
        Assert.DoesNotContain("INAMHI...", call.Messages[^1].Content);
        Assert.True(Assert.Single(_embedder.Calls).IsQuery);
    }

    private static readonly List<ChatTurn> PriorTurn =
        [new("user", "¿Qué método de homogeneización usa el proyecto?"), new("assistant", "Climatol [1].")];

    private static ScoredChunk Hit(string doc, double score) => new(doc, "T", "S", null, doc, score, false);

    [Fact]
    public async Task Gate_uses_the_question_only_score_even_when_history_lifts_the_augmented_query()
    {
        const string question = "¿Cuál es la capital de Francia?";
        _store.HitsFor = v => v[0] == question.Length ? [Hit("q", 0.30)] : [Hit("aug", 0.60)];

        var events = await Ask(new(question, History: PriorTurn));

        var embed = Assert.Single(_embedder.Calls);
        Assert.Equal(2, embed.Texts.Count);
        Assert.Equal(question, embed.Texts[0]);
        Assert.Empty(_chat.Calls);
        var done = Assert.IsType<DoneEvent>(events[^1]);
        Assert.Equal(AnswerKind.Abstained, done.Kind);
        Assert.Equal(0.30, done.TopScore);
    }

    [Fact]
    public async Task Augmented_query_ranks_the_context_once_the_question_clears_the_floor()
    {
        const string question = "¿Qué software se usa para la homogeneización de series?";
        _store.HitsFor = v => v[0] == question.Length ? [Hit("q", 0.45)] : [Hit("aug", 0.62)];

        var events = await Ask(new(question, History: PriorTurn));

        Assert.Equal(2, _store.Searches.Count);
        Assert.Equal("aug", Assert.Single(Assert.IsType<SourcesEvent>(events[0]).Sources).DocId);
        Assert.Equal(AnswerKind.Rag, Assert.IsType<DoneEvent>(events[^1]).Kind);
    }

    [Fact]
    public async Task Short_follow_up_borrows_the_previous_turn_to_clear_the_floor()
    {
        const string question = "¿Y quién la codirige?";
        _store.HitsFor = v => v[0] == question.Length ? [Hit("q", 0.20)] : [Hit("aug", 0.58)];

        var events = await Ask(new(question, History: PriorTurn));

        Assert.Equal(AnswerKind.Rag, Assert.IsType<DoneEvent>(events[^1]).Kind);
        Assert.Single(_chat.Calls);
    }

    [Fact]
    public async Task Without_history_only_the_question_is_embedded_and_searched()
    {
        _store.Hits = [Hit("q", 0.5)];
        await Ask(new("¿Qué método de homogeneización usa?"));

        Assert.Single(Assert.Single(_embedder.Calls).Texts);
        Assert.Single(_store.Searches);
    }

    [Fact]
    public async Task Model_marker_split_across_tokens_turns_the_answer_into_an_abstention()
    {
        var chat = new FakeChat(" [[SIN", "_RESP", "ALDO]]", " no consta.");
        _store.Hits = [Hit("q", 0.5)];
        var events = new List<ChatEvent>();
        await foreach (var e in new ChatService(_embedder, chat, _store, _options).AskAsync(new("¿Quién ganó el mundial?"), CancellationToken.None))
            events.Add(e);

        Assert.DoesNotContain(events.OfType<TokenEvent>(), t => t.Text.Contains("[["));
        Assert.Equal(RagPrompt.Abstention, Assert.Single(events.OfType<TokenEvent>()).Text);
        var done = Assert.IsType<DoneEvent>(events[^1]);
        Assert.Equal((AnswerKind.Abstained, RagPrompt.Abstention), (done.Kind, done.Answer));
        Assert.Empty(done.Sources);
    }

    [Fact]
    public async Task Answers_that_do_not_start_with_the_marker_stream_unchanged()
    {
        var chat = new FakeChat("[", "1] indica ", "que ", "se usa Climatol.");
        _store.Hits = [Hit("q", 0.5)];
        var events = new List<ChatEvent>();
        await foreach (var e in new ChatService(_embedder, chat, _store, _options).AskAsync(new("¿Qué usa?"), CancellationToken.None))
            events.Add(e);

        var tokens = events.OfType<TokenEvent>().Select(t => t.Text).ToList();
        Assert.Equal("[1] indica que se usa Climatol.", string.Concat(tokens));
        Assert.Equal(["que ", "se usa Climatol."], tokens[^2..]);
        var done = Assert.IsType<DoneEvent>(events[^1]);
        Assert.Equal((AnswerKind.Rag, "[1] indica que se usa Climatol."), (done.Kind, done.Answer));
        Assert.Single(done.Sources);
    }

    [Fact]
    public async Task Session_id_is_passed_to_retrieval_and_validated()
    {
        _store.Hits = [new("sesion:1", "mio", "S", null, "propio", 0.05, true)];
        var events = await Ask(new("Resume mi archivo", "sesion-abc-123"));

        Assert.Equal(("sesion-abc-123", 4 * RagPrompt.SearchOverfetch), _store.LastSearch);
        Assert.Equal(AnswerKind.Rag, Assert.IsType<DoneEvent>(events[^1]).Kind);
        Assert.Throws<ValidationError>(() => Service().Validate(new("hola", "../../etc")));
    }

    [Fact]
    public void Rejects_empty_and_overlong_questions()
    {
        Assert.Throws<ValidationError>(() => Service().Validate(new("   ")));
        Assert.Throws<ValidationError>(() => Service().Validate(new(new string('a', 1001))));
    }
}

public class CorpusServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("rag-corpus-").FullName;
    private readonly FakeEmbedder _embedder = new();
    private readonly FakeStore _store = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private CorpusService Service() => new(null!, _store, _embedder, new RagOptions(), null!,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<CorpusService>.Instance);

    private static readonly List<CorpusDoc> Docs =
        [new("a", "Doc A", "/grafo", "# Doc A\n## Uno\nalpha\n## Dos\nbeta")];

    [Fact]
    public async Task Indexes_once_then_skips_until_content_or_model_changes()
    {
        var first = await Service().IndexDocsAsync(Docs, force: false, CancellationToken.None);
        var second = await Service().IndexDocsAsync(Docs, force: false, CancellationToken.None);
        Assert.True(first.Reindexed);
        Assert.False(second.Reindexed);
        Assert.Equal(1, _store.ReplaceCalls);
        Assert.Equal(["Uno", "Dos"], _store.Corpus.Select(c => c.Section));
        Assert.All(_store.Corpus, c => Assert.Equal("/grafo", c.Url));
        // Headings travel into the embedded text.
        Assert.Contains("Doc A\nUno\n\nalpha", _embedder.Calls[0].Texts);

        _embedder.Model = "otro-modelo";
        Assert.True((await Service().IndexDocsAsync(Docs, false, CancellationToken.None)).Reindexed);
        Assert.True((await Service().IndexDocsAsync(Docs, force: true, CancellationToken.None)).Reindexed);
        Assert.Equal(3, _store.ReplaceCalls);
    }

    [Fact]
    public void Reads_markdown_title_and_source_comment()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "propuesta.md"),
            "<!-- source: /files/documentos/propuesta/p.pdf -->\n# Postulación\n\ntexto");
        File.WriteAllText(Path.Combine(_root, "sin-titulo.md"), "solo texto");
        File.WriteAllText(Path.Combine(_root, "evaluacion.md"), "<!-- rag: exclude -->\n# Evaluación\n\ncapital de Francia");

        var docs = CorpusService.ReadMarkdown([_root, Path.Combine(_root, "no-existe")]);

        Assert.Equal(["sin-titulo", "propuesta"], docs.Select(d => d.DocId));
        Assert.Equal(("Postulación", "/files/documentos/propuesta/p.pdf"), (docs[1].Title, docs[1].Url));
        Assert.Equal(("sin-titulo", null), (docs[0].Title, docs[0].Url));
    }
}

public class SessionFileServiceTests
{
    private readonly FakeEmbedder _embedder = new();
    private readonly FakeStore _store = new();
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    private SessionFileService Service(RagOptions? o = null) =>
        new(_embedder, _store, o ?? new RagOptions { SessionTtlMinutes = 120 }, new FixedClock(Now));

    private static MemoryStream Text(string s) => new(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task Upload_goes_to_the_session_table_only()
    {
        var result = await Service().IngestAsync("sesion-123", "notas.md", Text("# Notas\n## Parte\ncontenido propio"), default);

        Assert.Equal(("sesion-123", "notas", 1), (result.SessionId, result.Title, result.Chunks));
        Assert.Equal(Now.AddHours(2), result.ExpiresAt);
        Assert.Empty(_store.Corpus);
        Assert.Equal("contenido propio", Assert.Single(_store.Sessions["sesion-123"]).Content);
        Assert.False(_embedder.Calls[0].IsQuery);
    }

    [Fact]
    public async Task Rejects_bad_session_ids_extensions_sizes_and_empty_files()
    {
        var s = Service(new RagOptions { MaxUploadBytes = 10 });
        await Assert.ThrowsAsync<ValidationError>(() => s.IngestAsync("corto", "a.md", Text("x"), default));
        await Assert.ThrowsAsync<ValidationError>(() => s.IngestAsync("sesion-123", "a.docx", Text("x"), default));
        await Assert.ThrowsAsync<ValidationError>(() => s.IngestAsync("sesion-123", "a.txt", Text(new string('x', 11)), default));
        await Assert.ThrowsAsync<ValidationError>(() => s.IngestAsync("sesion-123", "a.txt", Text("   "), default));
        await Assert.ThrowsAsync<ValidationError>(() => s.IngestAsync("sesion-123", "a.pdf", Text("no es pdf"), default));
    }

    [Fact]
    public async Task Purge_removes_chunks_older_than_the_ttl()
    {
        await Service().PurgeExpiredAsync(default);
        Assert.Equal(Now.AddMinutes(-120), _store.LastPurgeCutoff);
    }

    [Fact]
    public async Task Delete_session_removes_its_chunks()
    {
        await Service().IngestAsync("sesion-123", "a.txt", Text("hola"), default);
        Assert.Equal(1, await Service().DeleteSessionAsync("sesion-123", default));
        Assert.Empty(_store.Sessions);
    }
}

public class RagProviderTests
{
    private static IAmazonBedrockRuntime FakeBedrock() =>
        new AmazonBedrockRuntimeClient(new BasicAWSCredentials("x", "y"), Amazon.RegionEndpoint.USEast1);

    private static HttpClient NoHttp() => throw new InvalidOperationException("no debe crearse");

    [Fact]
    public void Bedrock_is_the_default_provider()
    {
        var o = new RagOptions();
        var models = RagProviders.Create(o, FakeBedrock, NoHttp);
        Assert.IsType<BedrockEmbedder>(models.Embedder);
        Assert.IsType<BedrockChat>(models.Chat);
        Assert.Equal("cohere.embed-multilingual-v3", models.Embedder.Model);
        Assert.Equal("us.anthropic.claude-haiku-4-5-20251001-v1:0", models.Chat.Model);
        Assert.Equal(o.EmbeddingModel, models.Embedder.Model);
    }

    [Fact]
    public void OpenAi_needs_a_key_and_unknown_providers_fail_clearly()
    {
        var o = new RagOptions { Provider = "OpenAI", OpenAiApiKey = "sk-test" };
        var models = RagProviders.Create(o, () => throw new InvalidOperationException(), () => new HttpClient());
        Assert.IsType<OpenAiEmbedder>(models.Embedder);
        Assert.Equal(("text-embedding-3-small", "gpt-4o-mini"), (models.Embedder.Model, models.Chat.Model));
        Assert.Equal("gpt-4o-mini", o.ChatModel);

        Assert.Throws<InvalidOperationException>(() =>
            RagProviders.Create(new RagOptions { Provider = "openai" }, FakeBedrock, NoHttp));
        Assert.Throws<InvalidOperationException>(() =>
            RagProviders.Create(new RagOptions { Provider = "ollama" }, FakeBedrock, NoHttp));
    }

    [Fact]
    public void Cohere_body_and_response_round_trip()
    {
        Assert.Equal("""{"texts":["a","b"],"input_type":"search_query","truncate":"END"}""",
            BedrockEmbedder.CohereBody(["a", "b"], isQuery: true));
        Assert.Contains("search_document", BedrockEmbedder.CohereBody(["a"], isQuery: false));

        var parsed = BedrockEmbedder.ParseCohere(new MemoryStream(Encoding.UTF8.GetBytes(
            """{"id":"1","embeddings":[[0.5,-1.0],[2.0,0.25]],"response_type":"embeddings_floats"}""")));
        Assert.Equal([0.5f, -1f], parsed[0]);
        Assert.Equal([2f, 0.25f], parsed[1]);
    }

    [Fact]
    public async Task OpenAi_embeddings_request_1024_dims_and_keep_input_order()
    {
        var handler = new StubHandler(_ => """{"data":[{"index":1,"embedding":[2.0]},{"index":0,"embedding":[1.0]}]}""");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example/v1/") };

        var vectors = await new OpenAiEmbedder(http, "text-embedding-3-small", "sk").EmbedAsync(["a", "b"], false, default);

        Assert.Equal([[1f], [2f]], vectors);
        Assert.Equal("https://api.example/v1/embeddings", handler.Requests[0].Url);
        Assert.Contains("\"dimensions\":1024", handler.Requests[0].Body);
        Assert.Equal("Bearer sk", handler.Requests[0].Auth);
    }

    [Fact]
    public async Task OpenAi_chat_parses_the_event_stream()
    {
        var sse = "data: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n\n" +
                  "data: {\"choices\":[{\"delta\":{\"content\":\"Hola\"}}]}\n\n" +
                  "data: {\"choices\":[{\"delta\":{\"content\":\" mundo\"}}]}\n\n" +
                  "data: [DONE]\n\n";
        var handler = new StubHandler(_ => sse);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example/v1/") };

        var tokens = new List<string>();
        await foreach (var t in new OpenAiChat(http, "gpt-4o-mini", "sk", 100).StreamAsync("sys", [new("user", "hola")], default))
            tokens.Add(t);

        Assert.Equal(["Hola", " mundo"], tokens);
        Assert.Contains("\"stream\":true", handler.Requests[0].Body);
        Assert.Contains("\"role\":\"system\"", handler.Requests[0].Body);
    }

    [Fact]
    public async Task OpenAi_errors_surface_the_status()
    {
        var handler = new StubHandler(_ => """{"error":"bad key"}""", HttpStatusCode.Unauthorized);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.example/v1/") };
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new OpenAiEmbedder(http, "m", "sk").EmbedAsync(["a"], false, default));
        Assert.Contains("401", ex.Message);
    }

    [Fact]
    public void Vector_literal_uses_invariant_decimals()
    {
        Assert.Equal("[0.5,-1.25,3]", PgVectorChunkStore.Literal([0.5f, -1.25f, 3f]));
    }

    private sealed class StubHandler(Func<string, string> respond, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<(string Url, string Body, string? Auth)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.RequestUri!.ToString(), body, request.Headers.Authorization?.ToString()));
            return new HttpResponseMessage(status) { Content = new StringContent(respond(body)) };
        }
    }
}

public class CatalogProjectionTests
{
    [Fact]
    public async Task Projects_graph_repository_and_stations_from_the_database()
    {
        var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Stations.Add(new Station { Code = "H0001", Name = "RIO A", Province = "Pichincha" });
        db.Observations.AddRange(
            new Observation { StationCode = "H0001", Variable = Variable.DischargeFinal, Date = new DateOnly(1990, 1, 1), Value = 1 },
            new Observation { StationCode = "H0001", Variable = Variable.DischargeFinal, Date = new DateOnly(1990, 1, 2), Value = 2 });
        db.Documents.Add(new Document
            { Title = "Protocolo Climatol", Category = "protocolo", Objective = "OE2", Authors = "Equipo", FilePath = "documentos/p.pdf" });
        await db.SaveChangesAsync();
        await new KnowledgeGraphService(db).RebuildAsync();

        var docs = await CatalogProjection.BuildAsync(db, new SeriesService(db));

        var graph = docs.Single(d => d.DocId == "catalogo-grafo");
        Assert.Equal("/grafo", graph.Url);
        Assert.Contains("## Equipo de investigación", graph.Markdown);
        Assert.Contains("Integrantes del equipo de investigación del proyecto (5)", graph.Markdown);
        Assert.Contains("**Adriana Uquillas Andrade**: Directora", graph.Markdown);
        Assert.Contains("dirige Regionalización de caudales", graph.Markdown);
        Assert.Contains("## Métodos y técnicas", graph.Markdown);
        Assert.DoesNotContain("station:", graph.Markdown);

        var repo = docs.Single(d => d.DocId == "catalogo-repositorio");
        Assert.Contains("contiene 1 documento(s)", repo.Markdown);
        Assert.Contains("**Protocolo Climatol** (categoría: protocolo; objetivo OE2). Autores: Equipo. Archivo: /files/documentos/p.pdf.", repo.Markdown);

        var stations = docs.Single(d => d.DocId == "catalogo-estaciones");
        Assert.Contains("La plataforma tiene 1 estaciones del INAMHI con datos y 2 observaciones", stations.Markdown);
        Assert.Contains("- Caudal final rellenado (m³/s): 1 estaciones, 2 observaciones, periodo 1990-01-01 a 1990-01-02.", stations.Markdown);
        Assert.Contains("- H0001 RIO A (Pichincha): 1990-01-01 a 1990-01-02, 2 observaciones, completitud 100 %.", stations.Markdown);

        // Each catalog document chunks into sections the retriever can hit on their own.
        Assert.Contains(MarkdownChunker.Chunk(graph.Markdown, graph.Title), c => c.Section == "Equipo de investigación");
    }
}
