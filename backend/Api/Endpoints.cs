using CaudalesPlatform.Api.Application;
using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Shared;

namespace CaudalesPlatform.Api.Api;

public static class Endpoints
{
    public static void MapApi(this WebApplication app, string dataRoot)
    {
        var g = app.MapGroup("/api");

        g.MapGet("/overview", (SeriesService s) => s.GetOverviewAsync());

        // Hydrometeorological series
        g.MapGet("/stations", (string? variable, SeriesService s) => s.GetStationsAsync(ParseOptional(variable)));
        g.MapGet("/stations/{code}/series", (string code, string variable, string? resolution,
                DateOnly? from, DateOnly? to, SeriesService s) =>
            s.GetSeriesAsync(code, Parse(variable), resolution ?? "monthly", from, to));
        g.MapGet("/stations/{code}/completeness", (string code, string variable, SeriesService s) =>
            s.GetCompletenessAsync(code, Parse(variable)));
        g.MapGet("/stations/{code}/export", async (string code, string variable, SeriesService s) =>
            Results.File(System.Text.Encoding.UTF8.GetBytes(await s.ExportCsvAsync(code, Parse(variable))),
                "text/csv", $"{code}_{variable}.csv"));

        // Quality control
        g.MapGet("/quality/summary", (SeriesService s) => s.GetIssueSummaryAsync());
        g.MapGet("/quality/issues", (string? station, string? variable, string? kind, SeriesService s) =>
            s.GetIssuesAsync(station, ParseOptional(variable), kind));

        // Digital repository
        g.MapGet("/documents", (string? category, string? objective, string? search, RepositoryService r) =>
            r.ListAsync(category, objective, search));
        g.MapPost("/documents", async (HttpRequest req, RepositoryService r) =>
        {
            var form = await req.ReadFormAsync();
            string? Opt(string k) => string.IsNullOrWhiteSpace(form[k]) ? null : form[k].ToString();
            var input = new DocumentInput(form["title"].ToString(), form["category"].ToString(),
                Opt("objective"), Opt("description"), Opt("authors"));
            var file = form.Files.GetFile("file");
            var doc = await r.CreateAsync(input,
                file is null ? null : new UploadedFile(file.FileName, file.OpenReadStream()), dataRoot);
            return Results.Created($"/api/documents/{doc.Id}", doc);
        }).DisableAntiforgery();
        g.MapDelete("/documents/{id:int}", async (int id, RepositoryService r) =>
        {
            await r.DeleteAsync(id, dataRoot);
            return Results.NoContent();
        });

        // Knowledge graph
        g.MapGet("/graph", (KnowledgeGraphService k) => k.GetGraphAsync());
        g.MapGet("/graph/nodes/{id}", (string id, KnowledgeGraphService k) => k.GetNeighborhoodAsync(id));
    }

    private static Variable Parse(string value) =>
        ParseOptional(value) ?? throw new ValidationError("La variable debe ser 'precipitation' o 'temperature'.");

    private static Variable? ParseOptional(string? value) =>
        value is null ? null
        : Enum.TryParse<Variable>(value, ignoreCase: true, out var v) ? v
        : throw new ValidationError("La variable debe ser 'precipitation' o 'temperature'.");
}
