using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;
using CaudalesPlatform.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace CaudalesPlatform.Api.Application;

public record Graph(List<GraphNode> Nodes, List<GraphEdge> Edges);
public record Neighborhood(GraphNode Node, List<GraphNode> Neighbors, List<GraphEdge> Edges);

/// <summary>
/// Knowledge graph persisted in PostgreSQL (graph_nodes / graph_edges). It is a
/// projection: rebuilt from the proposal knowledge plus the stations, datasets and
/// documents currently in the database, so it never drifts from the repository.
/// </summary>
public class KnowledgeGraphService(AppDbContext db)
{
    private static readonly (Variable Variable, string Slug, string Label)[] Datasets =
    [
        (Variable.Precipitation, "precipitation", "INAMHI · Precipitación diaria (cruda)"),
        (Variable.Temperature, "temperature", "INAMHI · Temperatura diaria (cruda)"),
    ];

    public async Task RebuildAsync()
    {
        db.GraphEdges.RemoveRange(db.GraphEdges);
        db.GraphNodes.RemoveRange(db.GraphNodes);
        await db.SaveChangesAsync();

        var nodes = ProjectKnowledge.Nodes
            .Select(n => new GraphNode { Id = n.Id, Kind = n.Kind, Label = n.Label, Description = n.Description })
            .ToList();
        var edges = ProjectKnowledge.Edges.Select(e => (e.Source, e.Target, e.Relation)).ToList();

        var stationVariables = await db.Observations
            .Select(o => new { o.StationCode, o.Variable }).Distinct().ToListAsync();
        var stations = await db.Stations.ToListAsync();

        foreach (var (variable, slug, label) in Datasets)
        {
            var codes = stationVariables.Where(x => x.Variable == variable).Select(x => x.StationCode).ToHashSet();
            if (codes.Count == 0) continue;

            var datasetId = $"dataset:{slug}";
            nodes.Add(new GraphNode { Id = datasetId, Kind = "Dataset", Label = label,
                Description = $"{codes.Count} estaciones. Fuente: INAMHI." });
            edges.Add((datasetId, $"variable:{slug}", "MIDE"));
            edges.Add((datasetId, "institution:inamhi", "PROVISTO_POR"));
            edges.Add((datasetId, "activity:A1.1", "INSUMO_DE"));
            foreach (var code in codes)
            {
                edges.Add((datasetId, $"station:{code}", "CONTIENE"));
                edges.Add(($"station:{code}", $"variable:{slug}", "REGISTRA"));
            }
        }

        var withData = stationVariables.Select(x => x.StationCode).ToHashSet();
        nodes.AddRange(stations.Where(s => withData.Contains(s.Code)).Select(s => new GraphNode
        {
            Id = $"station:{s.Code}", Kind = "Station", Label = $"{s.Code} · {s.Name}", Description = s.Province,
        }));

        foreach (var d in await db.Documents.ToListAsync())
        {
            var id = $"document:{d.Id}";
            nodes.Add(new GraphNode { Id = id, Kind = "Document", Label = d.Title, Description = d.Description });
            edges.Add((id, ProjectKnowledge.ProjectId, "PERTENECE_A"));
            if (d.Objective is not null) edges.Add((id, $"objective:{d.Objective}", "DOCUMENTA"));
        }

        db.GraphNodes.AddRange(nodes);
        db.GraphEdges.AddRange(edges.Distinct().Select(e =>
            new GraphEdge { SourceId = e.Item1, TargetId = e.Item2, Relation = e.Item3 }));
        await db.SaveChangesAsync();
    }

    public async Task<Graph> GetGraphAsync() =>
        new(await db.GraphNodes.AsNoTracking().ToListAsync(), await db.GraphEdges.AsNoTracking().ToListAsync());

    public async Task<Neighborhood> GetNeighborhoodAsync(string id)
    {
        var node = await db.GraphNodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new NotFoundError($"Nodo '{id}' no encontrado.");
        var edges = await db.GraphEdges.AsNoTracking()
            .Where(e => e.SourceId == id || e.TargetId == id).ToListAsync();
        var ids = edges.Select(e => e.SourceId == id ? e.TargetId : e.SourceId).ToHashSet();
        var neighbors = await db.GraphNodes.AsNoTracking().Where(n => ids.Contains(n.Id)).ToListAsync();
        return new Neighborhood(node, neighbors, edges);
    }
}
