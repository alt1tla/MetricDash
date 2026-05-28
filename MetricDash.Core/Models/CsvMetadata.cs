namespace MetricDash.Core.Models;

public record CsvMetadata(
    string FileName,
    List<string> Columns,
    int RowCount,
    string? DateColumn = null,
    char Separator = ',');