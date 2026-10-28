namespace CaudalesPlatform.Api.Domain.Entities;

/// <summary>Project document stored in the digital repository.</summary>
public class Document
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Category { get; set; }  // propuesta | datos | informe | protocolo | codigo | articulo
    public string? Objective { get; set; }         // OE1..OE4 of the proposal
    public string? Description { get; set; }
    public string? Authors { get; set; }
    public string? FilePath { get; set; }          // relative to the data directory
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Node of the knowledge graph. Id is a stable slug such as "method:climatol".</summary>
public class GraphNode
{
    public required string Id { get; set; }
    public required string Kind { get; set; }      // Project | Objective | Activity | Method | Dataset | Variable | Station | Document | Researcher | Institution
    public required string Label { get; set; }
    public string? Description { get; set; }
}

public class GraphEdge
{
    public int Id { get; set; }
    public required string SourceId { get; set; }
    public required string TargetId { get; set; }
    public required string Relation { get; set; }
}
