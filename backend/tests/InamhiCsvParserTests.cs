using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;

namespace CaudalesPlatform.Tests;

public class InamhiCsvParserTests
{
    private static readonly string Header =
        "﻿linea_original,codigo,estacion,anio,mes," +
        string.Join(",", Enumerable.Range(1, 31).Select(d => $"dia_{d}")) + ",texto_original";

    private static string Row(int line, string code, string name, int year, int month, params string[] days)
    {
        var cells = Enumerable.Range(0, 31).Select(i => i < days.Length ? days[i] : "");
        return $"{line},{code},{name},{year},{month},{string.Join(",", cells)},raw text";
    }

    private static InamhiParseResult Parse(Variable variable, params string[] rows) =>
        InamhiCsvParser.Parse(new StringReader(string.Join("\n", rows.Prepend(Header))), variable);

    [Fact]
    public void Expands_wide_month_row_into_daily_observations()
    {
        var r = Parse(Variable.Precipitation, Row(13, "M0001", "INGUINCHO", 1964, 2, "0.4", "", "2.2"));

        Assert.Equal(2, r.Observations.Count);
        var first = r.Observations[0];
        Assert.Equal(("M0001", new DateOnly(1964, 2, 1), 0.4, 13, false),
            (first.StationCode, first.Date, first.Value, first.SourceLine, first.Suspect));
        Assert.Equal(new DateOnly(1964, 2, 3), r.Observations[1].Date);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void Collects_each_station_once()
    {
        var r = Parse(Variable.Temperature,
            Row(1, "M0001", "INGUINCHO", 1977, 6, "9.9"),
            Row(2, "M0001", "INGUINCHO", 1977, 7, "9.3"),
            Row(3, "M0003", "IZOBAMBA", 1977, 6, "11.0"));

        Assert.Equal(["M0001", "M0003"], r.Stations.Select(s => s.Code));
        Assert.Equal("IZOBAMBA", r.Stations[1].Name);
    }

    [Fact]
    public void Repeated_station_month_keeps_first_row_and_reports_duplicate()
    {
        var r = Parse(Variable.Precipitation,
            Row(10, "M0001", "INGUINCHO", 1964, 2, "1.0"),
            Row(11, "M0001", "INGUINCHO", 1964, 2, "1.0"));

        Assert.Single(r.Observations);
        var issue = Assert.Single(r.Issues);
        Assert.Equal((IssueKind.DuplicateMonth, 11), (issue.Kind, issue.SourceLine));
    }

    [Fact]
    public void Invalid_month_row_is_rejected()
    {
        var r = Parse(Variable.Precipitation, Row(40793, "M1103", "X", 2013, 0, "0.0"));

        Assert.Empty(r.Observations);
        Assert.Equal(IssueKind.InvalidMonth, Assert.Single(r.Issues).Kind);
    }

    [Fact]
    public void Value_after_month_end_is_rejected()
    {
        var days = Enumerable.Repeat("1.0", 30).ToArray(); // February 1965 has 28 days
        var r = Parse(Variable.Precipitation, Row(5, "M0001", "INGUINCHO", 1965, 2, days));

        Assert.Equal(28, r.Observations.Count);
        Assert.All(r.Issues, i => Assert.Equal(IssueKind.InvalidDay, i.Kind));
        Assert.Equal([29, 30], r.Issues.Select(i => i.Day!.Value));
    }

    [Fact]
    public void Precipitation_sentinel_is_treated_as_missing()
    {
        var r = Parse(Variable.Precipitation, Row(7, "M0001", "INGUINCHO", 1970, 1, "999.9", "3.0"));

        Assert.Single(r.Observations);
        Assert.Equal(IssueKind.Sentinel, Assert.Single(r.Issues).Kind);
    }

    [Theory]
    [InlineData(Variable.Precipitation, "350.0")]
    [InlineData(Variable.Temperature, "0.0")]
    [InlineData(Variable.Temperature, "41.0")]
    public void Out_of_range_value_is_kept_as_suspect(Variable variable, string value)
    {
        var r = Parse(variable, Row(8, "M0001", "INGUINCHO", 1970, 1, value));

        Assert.True(Assert.Single(r.Observations).Suspect);
        Assert.Equal(IssueKind.OutOfRange, Assert.Single(r.Issues).Kind);
    }

    [Fact]
    public void Precipitation_zero_is_a_valid_dry_day()
    {
        var r = Parse(Variable.Precipitation, Row(9, "M0001", "INGUINCHO", 1970, 1, "0.0"));

        Assert.False(Assert.Single(r.Observations).Suspect);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void Unparseable_value_is_reported()
    {
        var r = Parse(Variable.Precipitation, Row(9, "M0001", "INGUINCHO", 1970, 1, "abc"));

        Assert.Empty(r.Observations);
        Assert.Equal(IssueKind.Unparseable, Assert.Single(r.Issues).Kind);
    }
}
