using MetricDash.Core.Models;
using MetricDash.Core.Services;
using MetricDash.Core.Settings;

namespace MetricDash.WinForms;

public class Form1 : Form
{
    private readonly CsvParser _parser = new();
    private readonly StatsService _stats = new();
    private AppSettings _settings;
    private Dictionary<string, MetricSeries> _allData = new();
    private string _currentFilePath = string.Empty;

    private readonly TabControl _tabs;
    private readonly ChartPanel _chart;
    private readonly FlowLayoutPanel _checkPanel;
    private readonly ComboBox _typeCombo;
    private readonly Panel _analyticsPanel;
    private readonly FlowLayoutPanel _statsCards;
    private readonly DataGridView _dataGrid;
    private readonly Label _status;
    private readonly Button _btnLoad, _btnExport, _btnSettings;

    public Form1()
    {
        _settings = AppSettings.Load("winforms_settings.json");

        _tabs = new() { Dock = DockStyle.Fill };

        // График
        var chartTab = new TabPage("📈График");

        // Холст графика
        _chart = new() { Dock = DockStyle.Fill, ChartType = _settings.DefaultChartType, Theme = _settings.Theme };

        // Панель чекбоксов
        _checkPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 100, 
            AutoSize = true, 
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(10)
        };

        // Панель с селектором типа графика
        _typeCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Margin = new Padding(10, 5, 0, 0) };
        _typeCombo.Items.AddRange(new[] { "Line", "Bar", "Area" });
        _typeCombo.SelectedItem = _settings.DefaultChartType;

        var chartControls = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 45,  
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10)
        };
        chartControls.Controls.Add(new Label { Text = "Тип графика:", AutoSize = true, Margin = new Padding(0, 12, 5, 0) });
        chartControls.Controls.Add(_typeCombo);

        // Порядок добавления
        chartTab.Controls.Add(_chart);           
        chartTab.Controls.Add(_checkPanel);    
        chartTab.Controls.Add(chartControls);   

        // Аналитика
        var analyticsTab = new TabPage("📊 Аналитика");
        _analyticsPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(10) };
        _statsCards = new()
        {
            Dock = DockStyle.Top,
            Height = 130,  
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(5)
        };
        _dataGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells };

        var analyticsSplit = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 130, Orientation = Orientation.Horizontal };
        analyticsSplit.Panel1.Controls.Add(_statsCards);
        analyticsSplit.Panel2.Controls.Add(_dataGrid);
        analyticsTab.Controls.Add(analyticsSplit);

        _tabs.TabPages.Add(chartTab);
        _tabs.TabPages.Add(analyticsTab);

        // Верхняя панель кнопок
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 55,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(10)
        };
        _btnLoad = new() { Text = "Загрузить CSV", Width = 140, Height = 35 };
        _btnExport = new() { Text = "Экспорт", Width = 100, Height = 35 };
        _btnSettings = new() { Text = "Сменить тему", Width = 120, Height = 35 };
        topPanel.Controls.AddRange(new Control[] { _btnLoad, _btnExport, _btnSettings });

        // Статус-бар
        _status = new() { Dock = DockStyle.Bottom, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };

        // Сборка формы
        Controls.Add(_tabs);
        Controls.Add(_status);
        Controls.Add(topPanel);

        // События
        _btnLoad.Click += async (_, _) => await LoadFile();
        _btnExport.Click += (_, _) => Export();
        _btnSettings.Click += (_, _) => ToggleTheme();
        _typeCombo.SelectedIndexChanged += (_, _) => { _chart.ChartType = _typeCombo.SelectedItem?.ToString() ?? "Line"; _chart.Invalidate(); };
        _tabs.SelectedIndexChanged += (_, _) => { if (_tabs.SelectedTab?.Text?.Contains("Аналитика") == true) UpdateAnalytics(); };

        KeyPreview = true;
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.F1) { ShowHelp(); e.Handled = true; }
            if (e.KeyCode == Keys.F5) { if (_tabs.SelectedTab?.Text?.Contains("График") == true) UpdateChart(); else UpdateAnalytics(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.E) { Export(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.S) { ToggleTheme(); e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.D1) { _tabs.SelectedIndex = 0; e.Handled = true; }
            if (e.Control && e.KeyCode == Keys.D2) { _tabs.SelectedIndex = 1; e.Handled = true; }
        };

        ApplyTheme();
        Text = "MetricDash — Анализ метрик";
        Size = new(1000, 700);
        MinimumSize = new(700, 550); 
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
            cb.CheckedChanged += (_, _) => { UpdateChart(); if (_tabs.SelectedTab?.Text?.Contains("Аналитика") == true) UpdateAnalytics(); };
            _checkPanel.Controls.Add(cb);
        }

        _status.Text = $"Парсинг данных из {meta.RowCount} строк...";
        _allData = await _parser.ParseAsync(_currentFilePath, columns);
        _status.Text = $"Загружено: {meta.FileName} | Метрик: {_allData.Count}";

        UpdateChart();
        if (_tabs.SelectedTab?.Text?.Contains("Аналитика") == true) UpdateAnalytics();
    }

    private void UpdateChart()
    {
        var selected = _checkPanel.Controls.OfType<CheckBox>()
            .Where(cb => cb.Checked && _allData.ContainsKey(cb.Text))
            .Select(cb => _allData[cb.Text])
            .ToList();
        _chart.Series = selected;
        _chart.Invalidate();
        _status.Text = $"График: {selected.Count} метрик | {_chart.Series.Sum(s => s?.Points?.Count ?? 0)} точек";
    }

    private void UpdateAnalytics()
    {
        var selected = _checkPanel.Controls.OfType<CheckBox>()
            .Where(cb => cb.Checked && _allData.ContainsKey(cb.Text) && _allData[cb.Text].Points.Any())
            .Select(cb => _allData[cb.Text]).ToList();

        _statsCards.Controls.Clear();

        foreach (var series in selected)
        {
            var card = CreateStatCard(series);
            _statsCards.Controls.Add(card);
        }

        SetupDataGrid(selected);

        _status.Text = $"Аналитика: {selected.Count} метрик | {_stats.GetSummary(selected.ToDictionary(s => s.Name, s => s)).Count} показателей";
    }

    private Panel CreateStatCard(MetricSeries series)
    {
        bool dark = _settings.Theme == "Dark";
        var summary = _stats.GetSummary(new() { [series.Name] = series });

        var card = new Panel
        {
            Width = 220,
            Height = 100,
            Margin = new Padding(5),
            BackColor = dark ? Color.FromArgb(40, 40, 40) : Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var trend = summary.GetValueOrDefault($"{series.Name} (trend)", "—");
        bool isPositive = trend.Contains("▲");
        Color accent = isPositive ? (dark ? Color.FromArgb(0, 80, 0) : Color.LightGreen)
                                  : (dark ? Color.FromArgb(80, 0, 0) : Color.LightCoral);
        Color trendFore = isPositive ? (dark ? Color.LightGreen : Color.Green)
                                     : (dark ? Color.LightCoral : Color.Red);

        var lblName = new Label
        {
            Text = series.Name,
            Font = new("Segoe UI", 10, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 8),
            ForeColor = dark ? Color.White : Color.Black
        };

        var lblAvg = new Label
        {
            Text = summary.GetValueOrDefault($"{series.Name} (avg)", "—"),
            Font = new("Segoe UI", 9),
            AutoSize = true,
            Location = new Point(10, 30),
            ForeColor = dark ? Color.LightGray : Color.Black
        };

        var lblRange = new Label
        {
            Text = summary.GetValueOrDefault($"{series.Name} (min/max)", "—"),
            Font = new("Segoe UI", 8),
            AutoSize = true,
            Location = new Point(10, 50),
            ForeColor = Color.Gray
        };

        var lblTrend = new Label
        {
            Text = trend,
            Font = new("Segoe UI", 9, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 70),
            ForeColor = trendFore,
            BackColor = accent,
            Padding = new Padding(5, 2, 5, 2)
        };

        card.Controls.AddRange(new Control[] { lblName, lblAvg, lblRange, lblTrend });
        return card;
    }

    private void SetupDataGrid(List<MetricSeries> series)
    {
        _dataGrid.Columns.Clear();
        _dataGrid.Rows.Clear();

        if (series.Count == 0 || !series.Any(s => s.Points.Any())) return;

        _dataGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Timestamp", HeaderText = "Время", Width = 150 });
        foreach (var s in series)
            _dataGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = s.Name, HeaderText = $"{s.Name} [{s.Unit}]", Width = 100 });

        int maxPoints = series.Where(s => s.Points.Any()).Min(s => s.Points.Count);
        int skip = Math.Max(0, maxPoints - 100);

        for (int i = skip; i < maxPoints; i++)
        {
            var row = new DataGridViewRow();
            // 🔹 Проверка: series[0] существует и имеет точки
            var first = series.FirstOrDefault(s => s.Points.Any());
            if (first == null || i >= first.Points.Count) continue;

            row.Cells.Add(new DataGridViewTextBoxCell { Value = first.Points[i].Timestamp.ToString("HH:mm:ss") });
            foreach (var s in series)
            {
                var val = i < s.Points.Count ? s.Points[i].Value.ToString("F2") : "";
                row.Cells.Add(new DataGridViewTextBoxCell { Value = val });
            }
            _dataGrid.Rows.Add(row);
        }
    }

    private void Export()
    {
        if (_allData.Count == 0) { _status.Text = "Нет данных для экспорта."; return; }

        var selected = _checkPanel.Controls.OfType<CheckBox>()
            .Where(cb => cb.Checked && _allData.ContainsKey(cb.Text))
            .Select(cb => _allData[cb.Text]).ToList();

        if (selected.Count == 0) { _status.Text = "Выберите метрики для экспорта."; return; }

        using var dlg = new SaveFileDialog { Filter = "JSON|*.json|CSV|*.csv", Title = "Экспорт аналитики" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var data = selected.ToDictionary(s => s.Name, s => s);
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
        UpdateAnalytics();
        _status.Text = $"Тема: {_settings.Theme}";
    }

    private void ApplyTheme()
    {
        bool dark = _settings.Theme == "Dark";

        // Базовые цвета формы
        BackColor = dark ? Color.FromArgb(30, 30, 30) : Color.WhiteSmoke;
        ForeColor = dark ? Color.White : Color.Black;

        // Статус-бар
        _status.BackColor = dark ? Color.FromArgb(40, 40, 40) : SystemColors.Control;
        _status.ForeColor = dark ? Color.LightGray : Color.Black;

        // Вкладки
        _tabs.BackColor = dark ? Color.FromArgb(35, 35, 35) : Color.White;
        _tabs.ForeColor = ForeColor;
        foreach (TabPage tab in _tabs.TabPages)
        {
            tab.BackColor = dark ? Color.FromArgb(30, 30, 30) : Color.WhiteSmoke;
            tab.ForeColor = ForeColor;
        }

        // Верхняя панель кнопок
        var panelColor = dark ? Color.FromArgb(35, 35, 35) : SystemColors.Control;
        var btnBackColor = dark ? Color.FromArgb(60, 60, 60) : Color.White;
        var btnForeColor = dark ? Color.White : Color.Black;
        var btnBorderColor = dark ? Color.Gray : Color.DarkGray;

        foreach (Control c in Controls)
        {
            if (c is FlowLayoutPanel fp && fp.Dock == DockStyle.Top && fp.Controls.Contains(_btnLoad))
            {
                fp.BackColor = panelColor;
                foreach (Control btn in fp.Controls)
                {
                    if (btn is Button b)
                    {
                        b.BackColor = btnBackColor;
                        b.ForeColor = btnForeColor;
                        b.FlatStyle = FlatStyle.Flat;
                        b.FlatAppearance.BorderSize = 1;
                        b.FlatAppearance.BorderColor = btnBorderColor;
                    }
                }
            }
        }

        // Чекбоксы метрик
        foreach (Control c in _checkPanel.Controls)
            if (c is CheckBox cb) { cb.ForeColor = ForeColor; cb.BackColor = BackColor; }

        // Селектор типа графика
        _typeCombo.BackColor = btnBackColor;
        _typeCombo.ForeColor = btnForeColor;
        _typeCombo.FlatStyle = FlatStyle.Flat;

        // Таблица данных
        _dataGrid.BackgroundColor = dark ? Color.FromArgb(25, 25, 25) : Color.White;
        _dataGrid.GridColor = dark ? Color.FromArgb(50, 50, 50) : Color.LightGray;
        _dataGrid.DefaultCellStyle.ForeColor = dark ? Color.White : Color.Black;
        _dataGrid.DefaultCellStyle.BackColor = dark ? Color.FromArgb(35, 35, 35) : Color.White;
        _dataGrid.ColumnHeadersDefaultCellStyle.BackColor = dark ? Color.FromArgb(45, 45, 45) : Color.LightGray;
        _dataGrid.ColumnHeadersDefaultCellStyle.ForeColor = dark ? Color.White : Color.Black;

        // Карточки аналитики
        if (_statsCards.Controls.Count > 0)
        {
            var currentSelected = _checkPanel.Controls.OfType<CheckBox>()
                .Where(cb => cb.Checked && _allData.ContainsKey(cb.Text) && _allData[cb.Text].Points.Any())
                .Select(cb => _allData[cb.Text]).ToList();

            _statsCards.Controls.Clear();
            foreach (var series in currentSelected)
                _statsCards.Controls.Add(CreateStatCard(series));
        }

        // Принудительная перерисовка
        _chart.Invalidate();
        _statsCards.Invalidate();
        _dataGrid.Refresh();
        Refresh();
    }

    private void ShowHelp() => MessageBox.Show(
        "Справка:\n• F1 — эта справка\n• F5 — обновить текущую вкладку\n• Ctrl+E — экспорт данных\n• Ctrl+S — сменить тему\n• Ctrl+1 / Ctrl+2 — переключить вкладку (График / Аналитика)\n\nВыберите CSV, отметьте метрики и анализируйте!",
        "Справка", MessageBoxButtons.OK, MessageBoxIcon.Information);
}