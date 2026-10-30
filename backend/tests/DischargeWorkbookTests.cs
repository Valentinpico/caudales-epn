using CaudalesPlatform.Api.Domain.Entities;
using CaudalesPlatform.Api.Infrastructure;

namespace CaudalesPlatform.Tests;

public class DischargeWorkbookTests
{
    private static readonly DateTime Jan1 = new(1985, 1, 1);

    [Fact]
    public void Expands_station_columns_into_daily_observations_and_skips_gaps()
    {
        object?[][] rows =
        [
            ["fechas", "mes", "H0011", "H0016"],
            [Jan1, 1.0, 6.365, null],
            [Jan1.AddDays(1), 1.0, "NA", 5.5],
            [null, null, null, null],
        ];

        var obs = DischargeWorkbook.Parse(rows, Variable.DischargeObserved).ToList();

        Assert.Equal(
            [("H0011", new DateOnly(1985, 1, 1), 6.365, 2), ("H0016", new DateOnly(1985, 1, 2), 5.5, 3)],
            obs.Select(o => (o.StationCode, o.Date, o.Value, o.SourceLine)));
        Assert.All(obs, o => Assert.Equal((Variable.DischargeObserved, (string?)null), (o.Variable, o.Origin)));
    }

    [Fact]
    public void Final_sheet_pairs_each_value_with_its_origin_column()
    {
        object?[][] rows =
        [
            ["fechas", "mes", "H0011", "origen", "H0045", "origen"],
            [Jan1, 1.0, 284.04, "GR6J", 5.0, "RPA"],
            [Jan1.AddDays(1), 1.0, "NA", "NA", 4.5, "RPA"],
        ];

        var obs = DischargeWorkbook.Parse(rows, Variable.DischargeFinal).ToList();

        Assert.Equal([("H0011", 284.04, "GR6J"), ("H0045", 5.0, "RPA"), ("H0045", 4.5, "RPA")],
            obs.Select(o => (o.StationCode, o.Value, o.Origin)));
        Assert.Equal(["H0011", "H0045"], DischargeWorkbook.StationCodes(rows));
    }
}
