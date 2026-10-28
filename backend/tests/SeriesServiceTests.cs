using CaudalesPlatform.Api.Application;
using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;
using CaudalesPlatform.Api.Shared;
using Microsoft.EntityFrameworkCore;

namespace CaudalesPlatform.Tests;

public class SeriesServiceTests
{
    private static AppDbContext Db()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Stations.Add(new Station { Code = "M0001", Name = "INGUINCHO" });
        db.SaveChanges();
        return db;
    }

    private static void Add(AppDbContext db, Variable v, string date, double value, bool suspect = false)
    {
        db.Observations.Add(new Observation
        {
            StationCode = "M0001", Variable = v, Date = DateOnly.Parse(date), Value = value, Suspect = suspect,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Monthly_precipitation_is_the_sum_of_daily_values()
    {
        var db = Db();
        Add(db, Variable.Precipitation, "1970-01-01", 2.0);
        Add(db, Variable.Precipitation, "1970-01-15", 3.5);
        Add(db, Variable.Precipitation, "1970-02-01", 1.0);

        var points = await new SeriesService(db).GetSeriesAsync("M0001", Variable.Precipitation, "monthly", null, null);

        Assert.Equal([5.5, 1.0], points.Select(p => p.Value));
        Assert.Equal([2, 1], points.Select(p => p.Days));
        Assert.Equal(new DateOnly(1970, 1, 1), points[0].Date);
    }

    [Fact]
    public async Task Monthly_temperature_is_the_mean_of_daily_values()
    {
        var db = Db();
        Add(db, Variable.Temperature, "1970-01-01", 10.0);
        Add(db, Variable.Temperature, "1970-01-02", 12.0);

        var points = await new SeriesService(db).GetSeriesAsync("M0001", Variable.Temperature, "monthly", null, null);

        Assert.Equal(11.0, Assert.Single(points).Value);
    }

    [Fact]
    public async Task Daily_series_respects_date_range_and_suspect_flag()
    {
        var db = Db();
        Add(db, Variable.Precipitation, "1970-01-01", 1.0);
        Add(db, Variable.Precipitation, "1970-01-02", 350.0, suspect: true);
        Add(db, Variable.Precipitation, "1970-01-03", 2.0);

        var points = await new SeriesService(db).GetSeriesAsync(
            "M0001", Variable.Precipitation, "daily", new DateOnly(1970, 1, 2), new DateOnly(1970, 1, 2));

        var p = Assert.Single(points);
        Assert.True(p.Suspect);
    }

    [Fact]
    public async Task Unknown_resolution_is_rejected()
    {
        await Assert.ThrowsAsync<ValidationError>(() =>
            new SeriesService(Db()).GetSeriesAsync("M0001", Variable.Precipitation, "hourly", null, null));
    }

    [Fact]
    public async Task Unknown_station_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundError>(() =>
            new SeriesService(Db()).GetSeriesAsync("X9999", Variable.Precipitation, "daily", null, null));
    }

    [Fact]
    public async Task Completeness_reports_days_with_data_per_month()
    {
        var db = Db();
        Add(db, Variable.Precipitation, "1970-02-01", 1.0);
        Add(db, Variable.Precipitation, "1970-02-02", 1.0);

        var months = await new SeriesService(db).GetCompletenessAsync("M0001", Variable.Precipitation);

        var feb = Assert.Single(months);
        Assert.Equal((1970, 2, 2, 28), (feb.Year, feb.Month, feb.Days, feb.DaysInMonth));
    }

    [Fact]
    public async Task Station_summary_computes_period_and_completeness()
    {
        var db = Db();
        Add(db, Variable.Precipitation, "1970-01-01", 1.0);
        Add(db, Variable.Precipitation, "1970-01-04", 1.0); // 2 of 4 days in span
        db.QualityIssues.Add(new QualityIssue { StationCode = "M0001", Variable = Variable.Precipitation, Kind = IssueKind.Sentinel });
        db.SaveChanges();

        var s = Assert.Single(await new SeriesService(db).GetStationsAsync(Variable.Precipitation));

        Assert.Equal(("M0001", 2, new DateOnly(1970, 1, 1), new DateOnly(1970, 1, 4), 0.5, 1),
            (s.Code, s.Observations, s.First, s.Last, s.Completeness, s.Issues));
    }

    [Fact]
    public async Task Csv_export_lists_daily_rows()
    {
        var db = Db();
        Add(db, Variable.Precipitation, "1970-01-01", 1.5);

        var csv = await new SeriesService(db).ExportCsvAsync("M0001", Variable.Precipitation);

        Assert.Equal("codigo,fecha,valor,sospechoso\nM0001,1970-01-01,1.5,false\n", csv);
    }

    [Fact]
    public async Task Coverage_counts_days_per_station_and_year()
    {
        var db = Db();
        Add(db, Variable.Precipitation, "1970-01-01", 1.0);
        Add(db, Variable.Precipitation, "1970-06-01", 1.0);
        Add(db, Variable.Precipitation, "1971-01-01", 1.0);
        Add(db, Variable.Temperature, "1971-01-01", 9.0);

        var c = await new SeriesService(db).GetCoverageAsync(Variable.Precipitation);

        Assert.Equal([("M0001", 1970, 2), ("M0001", 1971, 1)], c.Select(x => (x.Code, x.Year, x.Days)));
    }
}
