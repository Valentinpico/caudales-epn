using System.Text;
using CaudalesPlatform.Api.Application;
using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;
using CaudalesPlatform.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace CaudalesPlatform.Tests;

public class RepositoryAndGraphTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("caudales-tests-").FullName;
    private readonly AppDbContext _db = new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private RepositoryService Repo() => new(_db, new KnowledgeGraphService(_db));

    private static UploadedFile File(string name, string content = "x") =>
        new(name, new MemoryStream(Encoding.UTF8.GetBytes(content)));

    [Fact]
    public async Task Rebuild_projects_proposal_stations_and_documents()
    {
        _db.Stations.Add(new Station { Code = "M0001", Name = "INGUINCHO" });
        _db.Observations.Add(new Observation { StationCode = "M0001", Variable = Variable.Precipitation, Date = new DateOnly(1970, 1, 1) });
        _db.Documents.Add(new Document { Title = "Protocolo Climatol", Category = "protocolo", Objective = "OE2" });
        await _db.SaveChangesAsync();

        var graph = new KnowledgeGraphService(_db);
        await graph.RebuildAsync();
        var g = await graph.GetGraphAsync();

        Assert.Contains(g.Nodes, n => n.Id == "station:M0001" && n.Kind == "Station");
        Assert.Contains(g.Edges, e => e.SourceId == "dataset:precipitation" && e.TargetId == "station:M0001" && e.Relation == "CONTIENE");
        Assert.Contains(g.Edges, e => e.SourceId == "station:M0001" && e.TargetId == "variable:precipitation" && e.Relation == "REGISTRA");
        Assert.Contains(g.Edges, e => e.SourceId.StartsWith("document:") && e.TargetId == "objective:OE2" && e.Relation == "DOCUMENTA");
        Assert.Contains(g.Edges, e => e.SourceId == "researcher:uquillas" && e.Relation == "DIRIGE");
        Assert.Contains(g.Edges, e => e.SourceId == "project:caudales" && e.TargetId == "line:LPI-DM-2023-04" && e.Relation == "EN_LINEA");
        Assert.Contains(g.Edges, e => e.SourceId == "line:LPI-DM-2023-04" && e.TargetId == "institution:epn-dm" && e.Relation == "DEFINIDA_POR");
        Assert.DoesNotContain(g.Nodes, n => n.Id == "dataset:temperature"); // no temperature data loaded
    }

    [Fact]
    public async Task Rebuild_is_idempotent()
    {
        var graph = new KnowledgeGraphService(_db);
        await graph.RebuildAsync();
        var first = await _db.GraphEdges.CountAsync();
        await graph.RebuildAsync();

        Assert.Equal(first, await _db.GraphEdges.CountAsync());
    }

    [Fact]
    public async Task Neighborhood_returns_node_with_adjacent_nodes()
    {
        var graph = new KnowledgeGraphService(_db);
        await graph.RebuildAsync();

        var n = await graph.GetNeighborhoodAsync("objective:OE2");

        Assert.Equal("objective:OE2", n.Node.Id);
        Assert.Contains(n.Neighbors, x => x.Id == "activity:A2.2");
        Assert.Contains(n.Neighbors, x => x.Id == ProjectKnowledge.ProjectId);
    }

    [Fact]
    public async Task Unknown_node_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundError>(() => new KnowledgeGraphService(_db).GetNeighborhoodAsync("x:y"));
    }

    [Fact]
    public async Task Create_document_stores_file_and_links_it_in_the_graph()
    {
        var doc = await Repo().CreateAsync(
            new DocumentInput("Informe de calidad", "informe", "OE1", null, "Equipo"), File("informe.pdf", "pdf"), _root);

        Assert.Equal("pdf", await System.IO.File.ReadAllTextAsync(Path.Combine(_root, doc.FilePath!)));
        Assert.StartsWith("documentos/", doc.FilePath);
        Assert.True(await _db.GraphEdges.AnyAsync(e => e.SourceId == $"document:{doc.Id}" && e.TargetId == "objective:OE1"));
    }

    [Theory]
    [InlineData("", "informe", null, "a.pdf")]
    [InlineData("T", "otra", null, "a.pdf")]
    [InlineData("T", "informe", "OE9", "a.pdf")]
    [InlineData("T", "informe", null, "a.exe")]
    public async Task Invalid_document_is_rejected(string title, string category, string? objective, string file)
    {
        await Assert.ThrowsAsync<ValidationError>(() =>
            Repo().CreateAsync(new DocumentInput(title, category, objective, null, null), File(file), _root));
    }

    [Fact]
    public async Task Delete_removes_document_file_and_graph_node()
    {
        var repo = Repo();
        var doc = await repo.CreateAsync(new DocumentInput("Datos", "datos", null, null, null), File("d.csv"), _root);

        await repo.DeleteAsync(doc.Id, _root);

        Assert.False(System.IO.File.Exists(Path.Combine(_root, doc.FilePath!)));
        Assert.False(await _db.GraphNodes.AnyAsync(n => n.Id == $"document:{doc.Id}"));
        await Assert.ThrowsAsync<NotFoundError>(() => repo.DeleteAsync(doc.Id, _root));
    }

    [Fact]
    public async Task List_filters_by_category_objective_and_text()
    {
        _db.Documents.AddRange(
            new Document { Title = "Postulación", Category = "propuesta" },
            new Document { Title = "Protocolo Climatol", Category = "protocolo", Objective = "OE2" });
        await _db.SaveChangesAsync();

        Assert.Single(await Repo().ListAsync("protocolo", null, null));
        Assert.Single(await Repo().ListAsync(null, "OE2", null));
        Assert.Equal("Postulación", Assert.Single(await Repo().ListAsync(null, null, "postul")).Title);
    }
}
