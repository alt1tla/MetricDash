using MetricDash.Core.Models;
using MetricDash.Core.Services;
using MetricDash.Core.Settings;

namespace MetricDash.Cli;

class Program
{
    static async Task Main(string[] args)
    {
        var settings = AppSettings.Load("cli_settings.json");
        var parser = new CsvParser();
        var stats = new StatsService();

        string? filePath = null;
        string? metricsArg = null;
        string? exportFormat = null;
        bool showHelp = false;

        // Парсинг аргументов
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file" when i + 1 < args.Length: filePath = args[++i]; break;
                case "--metrics" when i + 1 < args.Length: metricsArg = args[++i]; break;
                case "--export" when i + 1 < args.Length: exportFormat = args[++i]; break;
                case "--help": showHelp = true; break;
            }
        }

        if (showHelp || string.IsNullOrEmpty(filePath))
        {
            PrintHelp();
            return;
        }

        // Метаданные файла 
        var meta = await parser.GetMetadataAsync(filePath);
        if (meta == null)
        {
            Console.WriteLine("Файл не найден, пуст или не является CSV.");
            return;
        }

        Console.WriteLine($"Файл: {meta.FileName} | Строки данных: {meta.RowCount}");
        Console.WriteLine($"Доступные колонки: {string.Join(", ", meta.Columns)}\n");

        // Определяем метрики для анализа
        var selectedMetrics = new List<string>();
        if (!string.IsNullOrEmpty(metricsArg))
        {
            selectedMetrics = metricsArg.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim()).ToList();
        }
        else
        {
            // Интерактивный выбор, если метрики не переданы в аргументах
            Console.Write("Введите названия колонок через запятую (или Enter для всех): ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(input))
                selectedMetrics = meta.Columns.Where(c => c != meta.DateColumn).ToList();
            else
                selectedMetrics = input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim()).ToList();
        }

        // Валидация: оставляем только те, что реально есть в файле
        var validMetrics = selectedMetrics.Intersect(meta.Columns).ToList();
        if (validMetrics.Count == 0)
        {
            Console.WriteLine("Ни одна из выбранных метрик не найдена в файле.");
            return;
        }

        Console.WriteLine($"\nАнализ: {string.Join(", ", validMetrics)}\n");

        // Парсим и анализируем
        var data = await parser.ParseAsync(filePath, validMetrics);
        if (data.Count == 0)
        {
            Console.WriteLine("Не удалось извлечь числовые данные для выбранных колонок.");
            return;
        }

        // Вывод или экспорт
        if (!string.IsNullOrEmpty(exportFormat))
            Export(stats, data, exportFormat);
        else
        {
            Console.WriteLine("Статистика:");
            foreach (var kvp in stats.GetSummary(data))
                Console.WriteLine($"  • {kvp.Key}: {kvp.Value}");
        }
    }

    static void Export(IStatsService stats, Dictionary<string, MetricSeries> data, string format)
    {
        string output = format.ToLower() switch
        {
            "json" => stats.ExportToJson(data),
            "csv" => stats.ExportToCsv(data),
            _ => stats.ExportToJson(data)
        };
        Console.WriteLine(output);
    }

    static void PrintHelp() => Console.WriteLine(
        "MetricDash CLI — анализ CSV с метриками\n\n" +
        "Использование:\n" +
        "  dotnet run --project MetricDash.Cli -- --file data.csv [опции]\n\n" +
        "Опции:\n" +
        "  --file <path>       Путь к CSV (обязательно)\n" +
        "  --metrics <list>    Колонки для анализа (через запятую). Если нет → интерактивный выбор\n" +
        "  --export <json|csv> Вывод статистики в консоль в выбранном формате\n" +
        "  --help              Эта справка\n\n" +
        "Пример:\n" +
        "  dotnet run --project MetricDash.Cli -- --file server.log --metrics cpu_percent,memory_mb --export json");
}