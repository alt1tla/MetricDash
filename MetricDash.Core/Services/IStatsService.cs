using MetricDash.Core.Models;

namespace MetricDash.Core.Services;

public interface IStatsService
{
    Dictionary<string, string> GetSummary(Dictionary<string, MetricSeries> series);
    string ExportToJson(Dictionary<string, MetricSeries> series);
    string ExportToCsv(Dictionary<string, MetricSeries> series);
}