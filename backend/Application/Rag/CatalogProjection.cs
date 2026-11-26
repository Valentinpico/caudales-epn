using System.Globalization;
using System.Text;
using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CaudalesPlatform.Api.Application.Rag;

/// <summary>Document of the RAG corpus, as Markdown. Url is where a source citation links to.</summary>
public record CorpusDoc(string DocId, string Title, string? Url, string Markdown);

/// <summary>
/// Projects the database catalog (knowledge graph, repository, stations and series) into
/// Markdown documents, so the assistant can answer "who is on the team", "which methods" or
/// "how many stations" from the same data the platform shows.
/// </summary>
public static class CatalogProjection
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-EC");

    private static readonly (string Kind, string Heading, string Intro)[] GraphSections =
    [
        ("Project", "Proyecto", "Datos generales del proyecto de investigación."),
        ("Objective", "Objetivos específicos", "Objetivos específicos del proyecto"),
        ("Activity", "Actividades del cronograma", "Actividades del cronograma del proyecto"),
        ("Method", "Métodos y técnicas", "Métodos y técnicas que aplica el proyecto"),
        ("Researcher", "Equipo de investigación", "Integrantes del equipo de investigación del proyecto"),
        ("Institution", "Instituciones participantes", "Instituciones que participan en el proyecto"),
        ("ResearchLine", "Líneas de investigación", "Líneas de investigación en las que se inscribe el proyecto"),
        ("SDG", "Objetivos de Desarrollo Sostenible", "Objetivos de Desarrollo Sostenible a los que contribuye el proyecto"),
        ("Variable", "Variables hidrometeorológicas", "Variables hidrometeorológicas que estudia el proyecto"),
        ("Dataset", "Conjuntos de datos", "Conjuntos de datos cargados en la plataforma"),
    ];

    private static readonly Dictionary<string, string> Relations = new()
    {
        ["DIRIGE"] = "dirige", ["CODIRIGE"] = "codirige", ["COLABORA"] = "colabora en",
        ["PERTENECE_A"] = "pertenece a", ["TIENE_OBJETIVO"] = "tiene el objetivo",
        ["TIENE_ACTIVIDAD"] = "incluye la actividad", ["APLICA_METODO"] = "aplica el método",
        ["USA"] = "usa", ["ANALIZA"] = "analiza", ["EN_LINEA"] = "se inscribe en la línea",
        ["DEFINIDA_POR"] = "definida por", ["CONTRIBUYE_A"] = "contribuye a", ["MIDE"] = "mide",
        ["PROVISTO_POR"] = "provisto por", ["INSUMO_DE"] = "es insumo de", ["DOCUMENTA"] = "documenta",
    };

    private static readonly Dictionary<Variable, string> VariableNames = new()
    {
        [Variable.Precipitation] = "Precipitación diaria (mm)",
        [Variable.Temperature] = "Temperatura diaria (°C)",
        [Variable.DischargeRaw] = "Caudal diario crudo (m³/s)",
        [Variable.DischargeObserved] = "Caudal observado procesado con Climatol (m³/s)",
        [Variable.DischargeHydraccess] = "Caudal analizado con Hydraccess (m³/s)",
        [Variable.DischargeSimulated] = "Caudal simulado con GR4J/GR6J (m³/s)",
        [Variable.DischargeFinal] = "Caudal final rellenado (m³/s)",
    };

    public static async Task<List<CorpusDoc>> BuildAsync(AppDbContext db, SeriesService series)
    {
        var nodes = await db.GraphNodes.AsNoTracking().ToListAsync();
        var edges = await db.GraphEdges.AsNoTracking().ToListAsync();
        var documents = await db.Documents.AsNoTracking().OrderBy(d => d.Title).ToListAsync();
        var stations = await series.GetStationsAsync(null);
        return [Graph(nodes, edges), Repository(documents), Stations(stations)];
    }

    public static CorpusDoc Graph(List<GraphNode> nodes, List<GraphEdge> edges)
    {
        var labels = nodes.ToDictionary(n => n.Id, n => n.Label);
        var sb = new StringBuilder("# Catálogo del proyecto (grafo de conocimiento)\n\n");
        foreach (var (kind, heading, intro) in GraphSections)
        {
            var ofKind = nodes.Where(n => n.Kind == kind).OrderBy(n => n.Id, StringComparer.Ordinal).ToList();
            if (ofKind.Count == 0) continue;
            sb.Append($"## {heading}\n\n");
            if (kind != "Project") sb.Append($"{intro} ({ofKind.Count}):\n\n");
            foreach (var n in ofKind)
            {
                var related = edges.Where(e => e.SourceId == n.Id && labels.ContainsKey(e.TargetId)
                                               && !e.TargetId.StartsWith("station:"))
                    .Select(e => $"{Relations.GetValueOrDefault(e.Relation, e.Relation.ToLowerInvariant())} {labels[e.TargetId]}")
                    .Distinct().ToList();
                var line = new StringBuilder($"- **{n.Label}**");
                if (!string.IsNullOrWhiteSpace(n.Description)) line.Append($": {n.Description.Trim().TrimEnd('.')}.");
                if (related.Count > 0) line.Append($" Relaciones: {string.Join("; ", related)}.");
                sb.Append(line).Append('\n');
            }
            sb.Append('\n');
        }
        return new CorpusDoc("catalogo-grafo", "Catálogo del proyecto (grafo de conocimiento)", "/grafo", sb.ToString());
    }

    public static CorpusDoc Repository(List<Document> documents)
    {
        var sb = new StringBuilder("# Repositorio digital del proyecto\n\n## Documentos del repositorio\n\n");
        sb.Append($"El repositorio digital contiene {documents.Count} documento(s):\n\n");
        foreach (var d in documents)
        {
            sb.Append($"- **{d.Title}** (categoría: {d.Category}");
            if (d.Objective is not null) sb.Append($"; objetivo {d.Objective}");
            sb.Append(')');
            if (!string.IsNullOrWhiteSpace(d.Authors)) sb.Append($". Autores: {d.Authors.Trim()}");
            if (!string.IsNullOrWhiteSpace(d.Description)) sb.Append($". {d.Description.Trim().TrimEnd('.')}");
            if (d.FilePath is not null) sb.Append($". Archivo: /files/{d.FilePath.Replace('\\', '/')}");
            sb.Append(".\n");
        }
        return new CorpusDoc("catalogo-repositorio", "Repositorio digital del proyecto", "/repositorio", sb.ToString());
    }

    public static CorpusDoc Stations(List<StationSummary> summaries)
    {
        var sb = new StringBuilder("# Estaciones y series hidrometeorológicas\n\n## Resumen de estaciones y series\n\n");
        var stationCount = summaries.Select(s => s.Code).Distinct().Count();
        sb.Append($"La plataforma tiene {stationCount} estaciones del INAMHI con datos y " +
                  $"{N(summaries.Sum(s => s.Observations))} observaciones diarias en total.\n\n");
        foreach (var g in summaries.GroupBy(s => s.Variable).OrderBy(g => g.Key))
        {
            sb.Append($"- {VariableNames[g.Key]}: {g.Count()} estaciones, {N(g.Sum(s => s.Observations))} observaciones, " +
                      $"periodo {g.Min(s => s.First):yyyy-MM-dd} a {g.Max(s => s.Last):yyyy-MM-dd}.\n");
        }
        foreach (var g in summaries.GroupBy(s => s.Variable).OrderBy(g => g.Key))
        {
            sb.Append($"\n## Estaciones de {VariableNames[g.Key]}\n\n");
            foreach (var s in g.OrderBy(s => s.Code, StringComparer.Ordinal))
            {
                var province = s.Province is null ? "" : $" ({s.Province})";
                sb.Append($"- {s.Code} {s.Name}{province}: {s.First:yyyy-MM-dd} a {s.Last:yyyy-MM-dd}, " +
                          $"{N(s.Observations)} observaciones, completitud {(s.Completeness * 100).ToString("0.#", Es)} %.\n");
            }
        }
        return new CorpusDoc("catalogo-estaciones", "Estaciones y series hidrometeorológicas", "/estaciones", sb.ToString());
    }

    private static string N(int n) => n.ToString("#,0", Es);
}
