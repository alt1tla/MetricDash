namespace MetricDash.Core.Models;

public record MetricPoint(DateTime Timestamp, double Value);

public class MetricSeries
{
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public List<MetricPoint> Points { get; } = new();

    public double Min => Points.Any() ? Points.Min(p => p.Value) : 0;
    public double Max => Points.Any() ? Points.Max(p => p.Value) : 0;
    public double Avg => Points.Any() ? Points.Average(p => p.Value) : 0;
    public double Trend => CalculateTrend();

    private double CalculateTrend()
    {
        if (Points.Count < 2) return 0;
        var first = Points.Take(Points.Count / 4).Average(p => p.Value);
        var last = Points.SkipLast(Points.Count / 4).Average(p => p.Value);
        return last - first;
    }

    public void AddPoint(DateTime ts, double val) => Points.Add(new MetricPoint(ts, val));
}