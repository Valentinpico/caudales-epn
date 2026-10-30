using CaudalesPlatform.Api.Domain.Entities;
using ExcelDataReader;

namespace CaudalesPlatform.Api.Infrastructure;

/// <summary>
/// Reads caudales_rellenos.xlsx: one sheet per processing stage of the daily streamflow,
/// laid out as fechas | mes | H0011 | H0016 | ... The final sheet pairs every station
/// column with an "origen" column (H, GR4J, GR6J, RPA, PDM). Empty and "NA" cells are gaps.
/// </summary>
public static class DischargeWorkbook
{
    public const string FileName = "caudales_rellenos.xlsx";

    // ExcelDataReader needs code page 1252 even for .xlsx; .NET ships it but does not register it.
    static DischargeWorkbook() => System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

    public static readonly (string Sheet, Variable Variable)[] Sheets =
    [
        ("Caudal_Crudo", Variable.DischargeRaw),
        ("Caudal_Observado", Variable.DischargeObserved),
        ("Caudal_Hydrac", Variable.DischargeHydraccess),
        ("Caudal_simulado", Variable.DischargeSimulated),
        ("Caudal_flags_Finales", Variable.DischargeFinal),
    ];

    /// <summary>Station codes in the header row of a sheet.</summary>
    public static List<string> StationCodes(IEnumerable<object?[]> rows) =>
        rows.Take(1).SelectMany(Header).Where(IsStation).Distinct().ToList();

    public static IEnumerable<Observation> Parse(IEnumerable<object?[]> rows, Variable variable)
    {
        string[]? header = null;
        var line = 0;
        foreach (var r in rows)
        {
            line++;
            if (header is null) { header = Header(r); continue; }
            if (r.Length == 0 || r[0] is not DateTime date) continue;

            for (var i = 2; i < Math.Min(header.Length, r.Length); i++)
            {
                if (!IsStation(header[i]) || Number(r[i]) is not double value) continue;
                var origin = i + 1 < Math.Min(header.Length, r.Length) && header[i + 1] == "origen"
                    ? r[i + 1]?.ToString()?.Trim() : null;
                yield return new Observation
                {
                    StationCode = header[i], Variable = variable, Date = DateOnly.FromDateTime(date),
                    Value = value, SourceLine = line, Origin = string.IsNullOrEmpty(origin) ? null : origin,
                };
            }
        }
    }

    /// <summary>Rows of one sheet, streamed. Row 1 is the header.</summary>
    public static IEnumerable<object?[]> ReadSheet(string path, string sheet)
    {
        using var stream = File.OpenRead(path);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        do
        {
            if (reader.Name != sheet) continue;
            while (reader.Read())
            {
                var row = new object?[reader.FieldCount];
                for (var i = 0; i < row.Length; i++) row[i] = reader.GetValue(i);
                yield return row;
            }
            yield break;
        } while (reader.NextResult());
        throw new InvalidDataException($"Sheet '{sheet}' not found in {path}");
    }

    private static string[] Header(object?[] row) => row.Select(c => c?.ToString()?.Trim() ?? "").ToArray();

    private static bool IsStation(string column) => column.Length > 1 && column[0] == 'H' && char.IsDigit(column[1]);

    private static double? Number(object? cell) => cell switch
    {
        double d => d,
        int n => n,
        long n => n,
        _ => null,
    };
}
