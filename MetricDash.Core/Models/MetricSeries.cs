using System;
using System.Collections.Generic;
using System.Linq;

namespace MetricDash.Core.Models;

public record MetricPoint(DateTime Timestamp, double Value);

public class MetricSeries
{
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public List<MetricPoint> Points { get; } = new();

    public double Min => Points.Min(p => p.Value);
    public double Max => Points.Max(p => p.Value);
    public double Avg => Points.Average(p => p.Value);
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