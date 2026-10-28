namespace CaudalesPlatform.Api.Domain.Entities;

public enum Variable { Precipitation, Temperature }

/// <summary>INAMHI station. Coordinates are optional: the raw files only carry code and name.</summary>
public class Station
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }
    public string? Province { get; set; }
}

/// <summary>One daily value. SourceLine points back to the row of the raw INAMHI file.</summary>
public class Observation
{
    public string StationCode { get; set; } = "";
    public Variable Variable { get; set; }
    public DateOnly Date { get; set; }
    public double Value { get; set; }
    public bool Suspect { get; set; }
    public int SourceLine { get; set; }
}

public static class IssueKind
{
    public const string DuplicateMonth = "DuplicateMonth";
    public const string InvalidMonth = "InvalidMonth";
    public const string InvalidDay = "InvalidDay";
    public const string Sentinel = "Sentinel";
    public const string OutOfRange = "OutOfRange";
    public const string Unparseable = "Unparseable";
}

/// <summary>Quality-control finding raised while loading the raw files.</summary>
public class QualityIssue
{
    public int Id { get; set; }
    public string StationCode { get; set; } = "";
    public Variable Variable { get; set; }
    public int SourceLine { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int? Day { get; set; }
    public string Kind { get; set; } = "";
    public string Detail { get; set; } = "";
}
