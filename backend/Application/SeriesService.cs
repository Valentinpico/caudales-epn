using System.Globalization;
using System.Text;
using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;
using CaudalesPlatform.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace CaudalesPlatform.Api.Application;

public record StationSummary(
    string Code, string Name, Variable Variable, int Observations,
    DateOnly First, DateOnly Last, double Completeness, int Suspect, int Issues,
    double? Latitude, double? Longitude, double? Altitude, string? Province);

public record SeriesPoint(DateOnly Date, double Value, int Days, bool Suspect);
public record MonthCoverage(int Year, int Month, int Days, int DaysInMonth);
public record YearCoverage(string Code, int Year, int Days);
public record IssueCount(Variable Variable, string Kind, int Count);
public record Overview(int Stations, int Observations, int Issues, int Documents, int GraphNodes, int GraphEdges,
    DateOnly? First, DateOnly? Last);

/// <summary>Read-side queries over the hydrometeorological series.</summary>
public class SeriesService(AppDbContext db)
{
    // Note: stats are aggregated on every call (~2M rows, PK-indexed). Cache or
    // materialize a summary table if the station list becomes slow.
    public async Task<List<StationSummary>> GetStationsAsync(Variable? variable)
    {
        var q = db.Observations.AsQueryable();
        if (variable is not null) q = q.Where(o => o.Variable == variable);

        var stats = await q.GroupBy(o => new { o.StationCode, o.Variable })
            .Select(g => new
            {
                g.Key.StationCode, g.Key.Variable, Count = g.Count(),
                First = g.Min(o => o.Date), Last = g.Max(o => o.Date),
                Suspect = g.Count(o => o.Suspect),
            }).ToListAsync();

        var issues = await db.QualityIssues
            .GroupBy(i => new { i.StationCode, i.Variable })
            .Select(g => new { g.Key.StationCode, g.Key.Variable, Count = g.Count() })
            .ToDictionaryAsync(x => (x.StationCode, x.Variable), x => x.Count);

        var stations = await db.Stations.ToDictionaryAsync(s => s.Code);

        return stats
            .Select(s =>
            {
                var st = stations[s.StationCode];
                var span = s.Last.DayNumber - s.First.DayNumber + 1;
                return new StationSummary(
                    s.StationCode, st.Name, s.Variable, s.Count, s.First, s.Last,
                    Math.Round((double)s.Count / span, 4), s.Suspect,
                    issues.GetValueOrDefault((s.StationCode, s.Variable)),
                    st.Latitude, st.Longitude, st.Altitude, st.Province);
            })
            .OrderBy(s => s.Code).ThenBy(s => s.Variable)
            .ToList();
    }

    public async Task<List<SeriesPoint>> GetSeriesAsync(
        string code, Variable variable, string resolution, DateOnly? from, DateOnly? to)
    {
        await EnsureStationAsync(code);
        if (resolution is not ("daily" or "monthly"))
            throw new ValidationError("La resolución debe ser 'daily' o 'monthly'.");

        var q = db.Observations.Where(o => o.StationCode == code && o.Variable == variable);
        if (from is not null) q = q.Where(o => o.Date >= from);
        if (to is not null) q = q.Where(o => o.Date <= to);

        if (resolution == "daily")
            return await q.OrderBy(o => o.Date)
                .Select(o => new SeriesPoint(o.Date, o.Value, 1, o.Suspect))
                .ToListAsync();

        // Monthly: precipitation accumulates, temperature averages.
        var months = await q.GroupBy(o => new { o.Date.Year, o.Date.Month })
            .Select(g => new
            {
                g.Key.Year, g.Key.Month, Sum = g.Sum(o => o.Value), Avg = g.Average(o => o.Value),
                Days = g.Count(), Suspect = g.Any(o => o.Suspect),
            }).ToListAsync();

        return months
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .Select(m => new SeriesPoint(
                new DateOnly(m.Year, m.Month, 1),
                Math.Round(variable == Variable.Precipitation ? m.Sum : m.Avg, 2),
                m.Days, m.Suspect))
            .ToList();
    }

    public async Task<List<MonthCoverage>> GetCompletenessAsync(string code, Variable variable)
    {
        await EnsureStationAsync(code);
        var months = await db.Observations
            .Where(o => o.StationCode == code && o.Variable == variable)
            .GroupBy(o => new { o.Date.Year, o.Date.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Days = g.Count() })
            .ToListAsync();

        return months
            .OrderBy(m => m.Year).ThenBy(m => m.Month)
            .Select(m => new MonthCoverage(m.Year, m.Month, m.Days, DateTime.DaysInMonth(m.Year, m.Month)))
            .ToList();
    }

    /// <summary>Days with data per station and year: the network-wide coverage matrix.</summary>
    public async Task<List<YearCoverage>> GetCoverageAsync(Variable variable) =>
        (await db.Observations.Where(o => o.Variable == variable)
            .GroupBy(o => new { o.StationCode, o.Date.Year })
            .Select(g => new { g.Key.StationCode, g.Key.Year, Days = g.Count() }).ToListAsync())
        .Select(x => new YearCoverage(x.StationCode, x.Year, x.Days))
        .OrderBy(x => x.Code).ThenBy(x => x.Year).ToList();

    public async Task<List<QualityIssue>> GetIssuesAsync(string? code, Variable? variable, string? kind)
    {
        var q = db.QualityIssues.AsQueryable();
        if (code is not null) q = q.Where(i => i.StationCode == code);
        if (variable is not null) q = q.Where(i => i.Variable == variable);
        if (kind is not null) q = q.Where(i => i.Kind == kind);
        return await q.OrderBy(i => i.StationCode).ThenBy(i => i.Year).ThenBy(i => i.Month).Take(1000).ToListAsync();
    }

    public async Task<List<IssueCount>> GetIssueSummaryAsync() =>
        (await db.QualityIssues.GroupBy(i => new { i.Variable, i.Kind })
            .Select(g => new { g.Key.Variable, g.Key.Kind, Count = g.Count() }).ToListAsync())
        .Select(x => new IssueCount(x.Variable, x.Kind, x.Count))
        .OrderBy(x => x.Variable).ThenByDescending(x => x.Count).ToList();

    public async Task<string> ExportCsvAsync(string code, Variable variable)
    {
        var rows = await GetSeriesAsync(code, variable, "daily", null, null);
        var sb = new StringBuilder("codigo,fecha,valor,sospechoso\n");
        foreach (var r in rows)
            sb.Append(code).Append(',').Append(r.Date.ToString("yyyy-MM-dd")).Append(',')
              .Append(r.Value.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.Suspect ? "true" : "false").Append('\n');
        return sb.ToString();
    }

    public async Task<Overview> GetOverviewAsync()
    {
        var hasData = await db.Observations.AnyAsync();
        return new Overview(
            await db.Stations.CountAsync(), await db.Observations.CountAsync(),
            await db.QualityIssues.CountAsync(), await db.Documents.CountAsync(),
            await db.GraphNodes.CountAsync(), await db.GraphEdges.CountAsync(),
            hasData ? await db.Observations.MinAsync(o => o.Date) : null,
            hasData ? await db.Observations.MaxAsync(o => o.Date) : null);
    }

    private async Task EnsureStationAsync(string code)
    {
        if (!await db.Stations.AnyAsync(s => s.Code == code))
            throw new NotFoundError($"Estación '{code}' no encontrada.");
    }
}
