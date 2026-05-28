using System.Text;
using System.Text.Json;
using MetricDash.Core.Models;

namespace MetricDash.Core.Services;

public class StatsService : IStatsService
{
    public Dictionary<string, string> GetSummary(Dictionary<string, MetricSeries> series)
    {
        var summary = new Dictionary<string, string>();
        foreach (var (name, data) in series)
        {
            // Пропускаем метрики без числовых данных
            if (data.Points.Count == 0) continue;

            summary[$"{name} (avg)"] = $"{data.Avg:F2} {data.Unit}";
            summary[$"{name} (min/max)"] = $"{data.Min:F2} / {data.Max:F2} {data.Unit}";
            summary[$"{name} (trend)"] = data.Trend >= 0 ? $"▲ +{data.Trend:F2}" : $"▼ {data.Trend:F2}";
        }
        return summary;
    }

    public string ExportToJson(Dictionary<string, MetricSeries> series)
    {
        var export = series
            .Where(kv => kv.Value.Points.Count > 0) // Фильтруем пустые серии
            .ToDictionary(
                kv => kv.Key,
                kv => new {
                    unit = kv.Value.Unit,
                    count = kv.Value.Points.Count,
                    min = kv.Value.Min,
                    max = kv.Value.Max,
                    avg = kv.Value.Avg,
                    trend = kv.Value.Trend,
                    points = kv.Value.Points.Select(p => new { ts = p.Timestamp.ToString("o"), p.Value })
                });
        return JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
    }

    public string ExportToCsv(Dictionary<string, MetricSeries> series)
    {
        var validSeries = series.Where(kv => kv.Value.Points.Count > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        if (validSeries.Count == 0) return "Нет числовых данных для экспорта.";

        var sb = new StringBuilder();
        sb.AppendLine("Timestamp," + string.Join(",", validSeries.Keys));

        var maxPoints = validSeries.Values.Max(v => v.Points.Count);
        for (int i = 0; i < maxPoints; i++)
        {
            var ts = validSeries.Values.FirstOrDefault()?.Points.ElementAtOrDefault(i)?.Timestamp.ToString("o") ?? "";
            var values = validSeries.Values.Select(v => i < v.Points.Count ? v.Points[i].Value.ToString("F4") : "");
            sb.AppendLine($"{ts},{string.Join(",", values)}");
        }
        return sb.ToString();
    }
}