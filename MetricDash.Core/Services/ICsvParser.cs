using MetricDash.Core.Models;

namespace MetricDash.Core.Services;

public interface ICsvParser
{
    Task<CsvMetadata?> GetMetadataAsync(string filePath);
    Task<Dictionary<string, MetricSeries>> ParseAsync(string filePath, IEnumerable<string> selectedColumns);
}