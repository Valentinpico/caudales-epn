using System.Globalization;
using CaudalesPlatform.Api.Domain.Entities;

namespace CaudalesPlatform.Api.Infrastructure;

public record InamhiParseResult(List<Station> Stations, List<Observation> Observations, List<QualityIssue> Issues);

/// <summary>
/// Parses the raw INAMHI exports (one row per station-month, columns dia_1..dia_31)
/// into daily observations, raising quality-control issues instead of failing.
/// Files carry no quoted fields, so a plain comma split is enough.
/// </summary>
public static class InamhiCsvParser
{
    private const int FirstDayColumn = 5;
    private const double PrecipitationSentinel = 999.9;

    // Plausible daily ranges; values outside are kept but flagged as suspect.
    private static (double Min, double Max) Range(Variable v) =>
        v == Variable.Precipitation ? (0, 300) : (0.1, 40);

    public static InamhiParseResult Parse(TextReader reader, Variable variable)
    {
        var stations = new List<Station>();
        var seenStations = new HashSet<string>();
        var seenMonths = new HashSet<(string, int, int)>();
        var observations = new List<Observation>();
        var issues = new List<QualityIssue>();
        var (min, max) = Range(variable);

        reader.ReadLine(); // header
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            var c = line.Split(',');
            var sourceLine = int.Parse(c[0]);
            var code = c[1];
            var year = int.Parse(c[3]);
            var month = int.Parse(c[4]);

            QualityIssue Issue(string kind, string detail, int? day = null) => new()
            {
                StationCode = code, Variable = variable, SourceLine = sourceLine,
                Year = year, Month = month, Day = day, Kind = kind, Detail = detail,
            };

            if (seenStations.Add(code)) stations.Add(new Station { Code = code, Name = c[2] });

            if (month is < 1 or > 12)
            {
                issues.Add(Issue(IssueKind.InvalidMonth, $"Mes inválido ({month}); fila descartada."));
                continue;
            }
            if (!seenMonths.Add((code, year, month)))
            {
                issues.Add(Issue(IssueKind.DuplicateMonth, "Mes repetido; se conserva la primera fila."));
                continue;
            }

            var daysInMonth = DateTime.DaysInMonth(year, month);
            for (var day = 1; day <= 31; day++)
            {
                var cell = c[FirstDayColumn + day - 1];
                if (cell.Length == 0) continue;

                if (!double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    issues.Add(Issue(IssueKind.Unparseable, $"Valor no numérico '{cell}'.", day));
                    continue;
                }
                if (day > daysInMonth)
                {
                    issues.Add(Issue(IssueKind.InvalidDay, $"Día {day} fuera del mes.", day));
                    continue;
                }
                if (variable == Variable.Precipitation && value >= PrecipitationSentinel)
                {
                    issues.Add(Issue(IssueKind.Sentinel, $"Valor centinela {value}; tratado como faltante.", day));
                    continue;
                }

                var suspect = value < min || value > max;
                if (suspect)
                    issues.Add(Issue(IssueKind.OutOfRange, $"Valor {value} fuera de [{min}, {max}].", day));

                observations.Add(new Observation
                {
                    StationCode = code, Variable = variable, Date = new DateOnly(year, month, day),
                    Value = value, Suspect = suspect, SourceLine = sourceLine,
                });
            }
        }

        return new InamhiParseResult(stations, observations, issues);
    }
}
