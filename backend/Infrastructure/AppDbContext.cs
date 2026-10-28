using CaudalesPlatform.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CaudalesPlatform.Api.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Observation> Observations => Set<Observation>();
    public DbSet<QualityIssue> QualityIssues => Set<QualityIssue>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<GraphNode> GraphNodes => Set<GraphNode>();
    public DbSet<GraphEdge> GraphEdges => Set<GraphEdge>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Station>(e =>
        {
            e.ToTable("stations");
            e.HasKey(s => s.Code);
        });

        b.Entity<Observation>(e =>
        {
            e.ToTable("observations");
            e.HasKey(o => new { o.StationCode, o.Variable, o.Date });
            e.Property(o => o.Variable).HasConversion<string>();
            e.HasOne<Station>().WithMany().HasForeignKey(o => o.StationCode);
        });

        b.Entity<QualityIssue>(e =>
        {
            e.ToTable("quality_issues");
            e.Property(q => q.Variable).HasConversion<string>();
            e.HasIndex(q => new { q.StationCode, q.Variable });
        });

        b.Entity<Document>().ToTable("documents");

        b.Entity<GraphNode>().ToTable("graph_nodes");
        b.Entity<GraphEdge>(e =>
        {
            e.ToTable("graph_edges");
            e.HasOne<GraphNode>().WithMany().HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<GraphNode>().WithMany().HasForeignKey(x => x.TargetId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.SourceId, x.TargetId, x.Relation }).IsUnique();
        });
    }
}
