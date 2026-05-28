using MetricDash.Core.Models;
using MetricDash.Core.Services;
using MetricDash.Core.Settings;

namespace MetricDash.WinForms;

// 🔹 БЕЗ partial, если Form1.Designer.cs удалён
public class Form1 : Form
{
    private readonly CsvParser _parser = new();
    private readonly StatsService _stats = new();
    private AppSettings _settings;
    private Dictionary<string, MetricSeries> _allData = new();
    private string _currentFilePath = string.Empty;

    private readonly ChartPanel _chart;
    private readonly FlowLayoutPanel _checkPanel;
    private readonly ComboBox _typeCombo;
    private readonly Label _status;
    private readonly Button _btnLoad, _btnExport, _btnSettings;

    public Form1()
    {
        _settings = AppSettings.Load("winforms_settings.json");

        _chart = new() { Dock = DockStyle.Fill, ChartType = _settings.DefaultChartType, Theme = _settings.Theme };
        _checkPanel = new() { Dock = DockStyle.Top, Height = 80, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, Padding = new Padding(10) };
        _status = new() { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
        _typeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        _typeCombo.Items.AddRange(new[] { "Line", "Bar", "Area" });
        _typeCombo.SelectedItem = _settings.DefaultChartType;

        _btnLoad = new() { Text = "Загрузить CSV", Width = 140, Height = 35 };
        _btnExport = new() { Text = "Экспорт", Width = 100, Height = 35 };
        _btnSettings = new() { Text = "Тема", Width = 120, Height = 35 };

        var topPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(10) };
        topPanel.Controls.AddRange(new Control[] { _btnLoad, _btnExport, _btnSettings, new Label { Text = "Тип графика:", Margin = new Padding(15, 8, 5, 0), AutoSize = true }, _typeCombo });

        Controls.Add(_chart);
        Controls.Add(_checkPanel);
        Controls.Add(_status);
        Controls.Add(topPanel);

        _btnLoad.Click += async (_, _) => await LoadFile();
        _btnExport.Click += (_, _) => Export();
        _btnSettings.Click += (_, _) => ToggleTheme();
        _typeCombo.SelectedIndexChanged += (_, _) => { _chart.ChartType = _typeCombo.SelectedItem?.ToString() ?? "Line"; _chart.Invalidate(); };

        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F1) { ShowHelp(); e.Handled = true; }
            if (e.KeyCode == Keys.F5) { UpdateChart(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.E) { Export(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.S) { ToggleTheme(); e.Handled = true; }
        };

        ApplyTheme();
        Text = "MetricDash — Анализ метрик";
        Size = new(950, 650);
        MinimumSize = new(600, 500);
        StartPosition = FormStartPosition.CenterScreen;
        _status.Text = "Готово к работе. Загрузите CSV файл.";
    }

    private async Task LoadFile()
    {
        using var dlg = new OpenFileDialog { Filter = "CSV Files|*.csv", Title = "Выберите файл метрик" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _currentFilePath = dlg.FileName;
        _status.Text = "Чтение метаданных...";
        var meta = await _parser.GetMetadataAsync(_currentFilePath);
        if (meta == null) { _status.Text = "Ошибка чтения или пустой файл."; return; }

        _checkPanel.Controls.Clear();
        var columns = meta.Columns.Where(c => c != meta.DateColumn).ToList();
        foreach (var col in columns)
        {
            var cb = new CheckBox { Text = col, AutoSize = true, Margin = new Padding(10, 5, 0, 0), Checked = true };
            cb.CheckedChanged += (_, _) => UpdateChart();
            _checkPanel.Controls.Add(cb);
        }

        _status.Text = $"Парсинг данных из {meta.RowCount} строк...";
        _allData = await _parser.ParseAsync(_currentFilePath, columns);
        _status.Text = $"Загружено: {meta.FileName} | Метрик: {_allData.Count}";
        UpdateChart();
    }

    private void UpdateChart()
    {
        var selected = _checkPanel.Controls.OfType<CheckBox>().Where(cb => cb.Checked).Select(cb => cb.Text).ToList();
        _chart.Series = selected.Where(s => _allData.ContainsKey(s)).Select(s => _allData[s]).ToList();
        _chart.Invalidate();
        _status.Text = $"Отображение: {selected.Count} метрик | {_chart.Series.Sum(s => s.Points.Count)} точек";
    }

    private void Export()
    {
        if (_chart.Series.Count == 0) { _status.Text = "⚠️ Нет данных для экспорта."; return; }
        using var dlg = new SaveFileDialog { Filter = "JSON|*.json|CSV|*.csv", Title = "Экспорт выбранных метрик" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var data = _chart.Series.ToDictionary(s => s.Name, s => s);
        string content = dlg.FilterIndex == 1 ? _stats.ExportToJson(data) : _stats.ExportToCsv(data);
        File.WriteAllText(dlg.FileName, content);
        _status.Text = $"Экспортировано: {dlg.FileName}";
    }

    private void ToggleTheme()
    {
        _settings.Theme = _settings.Theme == "Light" ? "Dark" : "Light";
        _settings.Save("winforms_settings.json");
        ApplyTheme();
        _chart.Theme = _settings.Theme;
        _chart.Invalidate();
        _status.Text = $"Тема: {_settings.Theme}";
    }

    private void ApplyTheme()
    {
        bool dark = _settings.Theme == "Dark";
        BackColor = dark ? Color.FromArgb(30, 30, 30) : Color.WhiteSmoke;
        ForeColor = dark ? Color.White : Color.Black;
        _status.BackColor = dark ? Color.FromArgb(40, 40, 40) : SystemColors.Control;
        _status.ForeColor = dark ? Color.LightGray : Color.Black;
        foreach (Control c in _checkPanel.Controls)
        {
            if (c is CheckBox cb) { cb.ForeColor = ForeColor; cb.BackColor = BackColor; }
        }
    }

    private void ShowHelp() => MessageBox.Show(
        "📖 Справка:\n• F1 — эта справка\n• F5 — обновить график\n• Ctrl+E — экспорт данных\n• Ctrl+S — сменить тему\n\nВыберите CSV, отметьте метрики и наблюдайте графики!",
        "Справка", MessageBoxButtons.OK, MessageBoxIcon.Information);
}