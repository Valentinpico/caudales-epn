using System.Globalization;
using CaudalesPlatform.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CaudalesPlatform.Api.Infrastructure;

/// <summary>
/// First-run load: parses the raw INAMHI files, bulk-inserts the daily observations
/// with PostgreSQL binary COPY (~2M rows) and registers the seed documents.
/// Runs only when the tables are empty, so restarts are cheap.
/// </summary>
public static class DataSeeder
{
    private static readonly (string File, Variable Variable)[] RawFiles =
    [
        ("INAMHI_precipitacion_CRUDA.csv", Variable.Precipitation),
        ("INAMHI_temperatura_CRUDA.csv", Variable.Temperature),
    ];

    public static async Task SeedAsync(AppDbContext db, string rawDir, string dataRoot, ILogger log)
    {
        if (!await db.Observations.AnyAsync()) await LoadSeriesAsync(db, rawDir, log);
        await ApplyStationMetadataAsync(db, Path.Combine(dataRoot, "estaciones.csv"), log);
        if (!await db.Documents.AnyAsync()) await SeedDocumentsAsync(db);
    }

    private static async Task LoadSeriesAsync(AppDbContext db, string rawDir, ILogger log)
    {
        var stations = new Dictionary<string, Station>();
        foreach (var (file, variable) in RawFiles)
        {
            var path = Path.Combine(rawDir, file);
            if (!File.Exists(path))
            {
                log.LogWarning("Raw file not found: {Path}", path);
                continue;
            }

            using var reader = new StreamReader(path);
            var r = InamhiCsvParser.Parse(reader, variable);
            foreach (var s in r.Stations.Where(s => !stations.ContainsKey(s.Code)))
            {
                stations[s.Code] = s;
                db.Stations.Add(s);
            }
            await db.SaveChangesAsync();

            await CopyObservationsAsync(db, r.Observations);
            db.QualityIssues.AddRange(r.Issues);
            await db.SaveChangesAsync();
            log.LogInformation("{File}: {Obs} observations, {Issues} quality issues",
                file, r.Observations.Count, r.Issues.Count);
        }
    }

    private static async Task CopyObservationsAsync(AppDbContext db, List<Observation> rows)
    {
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();

        await using var w = await conn.BeginBinaryImportAsync(
            "COPY observations (\"StationCode\", \"Variable\", \"Date\", \"Value\", \"Suspect\", \"SourceLine\") FROM STDIN (FORMAT BINARY)");
        foreach (var o in rows)
        {
            await w.StartRowAsync();
            await w.WriteAsync(o.StationCode, NpgsqlDbType.Text);
            await w.WriteAsync(o.Variable.ToString(), NpgsqlDbType.Text);
            await w.WriteAsync(o.Date, NpgsqlDbType.Date);
            await w.WriteAsync(o.Value, NpgsqlDbType.Double);
            await w.WriteAsync(o.Suspect, NpgsqlDbType.Boolean);
            await w.WriteAsync(o.SourceLine, NpgsqlDbType.Integer);
        }
        await w.CompleteAsync();
    }

    /// <summary>Optional data/estaciones.csv: codigo,latitud,longitud,altitud,provincia.</summary>
    private static async Task ApplyStationMetadataAsync(AppDbContext db, string path, ILogger log)
    {
        if (!File.Exists(path)) return;

        double? Num(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

        var updated = 0;
        foreach (var line in (await File.ReadAllLinesAsync(path)).Skip(1))
        {
            var c = line.Split(',');
            if (c.Length < 5) continue;
            var station = await db.Stations.FindAsync(c[0].Trim());
            if (station is null) continue;
            (station.Latitude, station.Longitude, station.Altitude, station.Province) =
                (Num(c[1]), Num(c[2]), Num(c[3]), c[4].Trim());
            updated++;
        }
        await db.SaveChangesAsync();
        log.LogInformation("Station metadata applied to {Count} stations", updated);
    }

    private static async Task SeedDocumentsAsync(AppDbContext db)
    {
        db.Documents.AddRange(
            new Document
            {
                Title = "Postulación del proyecto de investigación",
                Category = "propuesta",
                Description = "Propuesta aprobada: objetivos, metodología, cronograma, equipo y presupuesto.",
                Authors = "A. Uquillas, M. Flores, X. Zapata, R. Mollineda, S. Torres",
                FilePath = "documentos/propuesta/postulacion-proyecto-caudales.pdf",
            },
            new Document
            {
                Title = "Base de datos INAMHI — descripción de archivos crudos",
                Category = "datos",
                Objective = "OE1",
                Description = "Estructura de los archivos de precipitación y temperatura extraídos de la data nacional del INAMHI.",
                FilePath = "documentos/datos/README-base-datos-inamhi.md",
            });
        await db.SaveChangesAsync();
    }
}
