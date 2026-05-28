using System.Globalization;
using MetricDash.Core.Models;

namespace MetricDash.Core.Services;

public class CsvParser : ICsvParser
{
    public async Task<CsvMetadata?> GetMetadataAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length < 2) return null;

        // Trim + удаление невидимых символов
        var columns = lines[0].Split(',')
            .Select(c => c.Trim().Replace("\uFEFF", ""))
            .ToList();

        return new CsvMetadata(
            Path.GetFileName(filePath),
            columns,
            lines.Length - 1,
            DateColumn: columns.FirstOrDefault(c =>
                c.Contains("date", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("time", StringComparison.OrdinalIgnoreCase) ||
                c.Equals("timestamp", StringComparison.OrdinalIgnoreCase)),
            Separator: ',');
    }

    public async Task<Dictionary<string, MetricSeries>> ParseAsync(string filePath, IEnumerable<string> selectedColumns)
    {
        var result = new Dictionary<string, MetricSeries>();
        var lines = await File.ReadAllLinesAsync(filePath);
        if (lines.Length < 2) return result;

        // Заголовки: чистим и нормализуем
        var headers = lines[0].Split(',')
            .Select(h => h.Trim().Replace("\uFEFF", ""))
            .ToList();

        // Поиск временной колонки (регистронезависимо)
        var timeColumn = headers.FirstOrDefault(h =>
            h.Contains("date", StringComparison.OrdinalIgnoreCase) ||
            h.Contains("time", StringComparison.OrdinalIgnoreCase) ||
            h.Equals("timestamp", StringComparison.OrdinalIgnoreCase));

        // Сопоставление колонок: регистронезависимое + с защитой от повторов
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in selectedColumns)
        {
            var trimmed = col.Trim().Replace("\uFEFF", "");
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Пропускаем временную колонку
            if (string.Equals(trimmed, timeColumn, StringComparison.OrdinalIgnoreCase))
                continue;

            // Ищем колонку в заголовках (регистронезависимо)
            var index = headers.FindIndex(h => string.Equals(h, trimmed, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && !columnIndex.ContainsKey(trimmed))
                columnIndex[trimmed] = index;
        }

        if (columnIndex.Count == 0) return result;

        int timeIndex = timeColumn != null
            ? headers.FindIndex(h => string.Equals(h, timeColumn, StringComparison.OrdinalIgnoreCase))
            : -1;
        bool hasTime = timeIndex >= 0;

        // Инициализация серий
        foreach (var colName in columnIndex.Keys)
            result[colName] = new MetricSeries { Name = colName, Unit = GetUnit(colName) };

        // Парсинг строк данных
        for (int i = 1; i < lines.Length; i++)
        {
            var values = lines[i].Split(',');

            // Парсинг временной метки
            DateTime ts = DateTime.MinValue;
            if (hasTime && timeIndex >= 0 && timeIndex < values.Length)
            {
                var timeVal = values[timeIndex].Trim();
                ts = ParseTimestamp(timeVal);
                if (ts == DateTime.MinValue) ts = DateTime.MinValue.AddSeconds(i - 1);
            }
            else
            {
                ts = DateTime.MinValue.AddSeconds(i - 1);
            }

            // Парсинг числовых значений
            foreach (var (colName, idx) in columnIndex)
            {
                if (idx >= 0 && idx < values.Length)
                {
                    var rawVal = values[idx].Trim().Replace("\uFEFF", "");
                    if (double.TryParse(rawVal, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                        result[colName].AddPoint(ts, val);
                }
            }
        }
        Console.Error.WriteLine($"[DEBUG] Загружено серий: {result.Count}");
        foreach (var (name, series) in result)
            Console.Error.WriteLine($"[DEBUG] {name}: {series.Points.Count} точек");
        return result;
    }

    private string GetUnit(string colName) => colName.ToLower() switch
    {
        var s when s.Contains("mem") || s.Contains("bytes") => "B",
        var s when s.Contains("cpu") || s.Contains("percent") || s.Contains("%") => "%",
        var s when s.Contains("time") || s.Contains("ms") || s.Contains("latency") => "ms",
        var s when s.Contains("count") || s.Contains("num") || s.Contains("threads") => "шт",
        var s when s.Contains("io") || s.Contains("mbps") || s.Contains("kbps") => "MB/s",
        _ => ""
    };

    private DateTime ParseTimestamp(string s)
    {
        var cleaned = s.Trim().Replace("\uFEFF", "");
        if (DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt;
        if (long.TryParse(cleaned, out var unix))
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(unix).DateTime; }
            catch { return DateTime.MinValue; }
        }
        return DateTime.MinValue;
    }
}