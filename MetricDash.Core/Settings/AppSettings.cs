using System.Text.Json;

namespace MetricDash.Core.Settings;

public class AppSettings
{
    public string Theme { get; set; } = "Light";
    public int MaxPoints { get; set; } = 1000; // лимит точек для отрисовки
    public string DefaultChartType { get; set; } = "Line"; // Line, Bar, Area
    public Dictionary<string, double> AlertThresholds { get; set; } = new();

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));

    public static AppSettings Load(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path))! : new AppSettings();
}