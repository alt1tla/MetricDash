using System.ComponentModel;
using System.Drawing.Drawing2D;
using MetricDash.Core.Models;

namespace MetricDash.WinForms;

public class ChartPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<MetricSeries> Series { get; set; } = new();

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ChartType { get; set; } = "Line";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Theme { get; set; } = "Light";

    private static readonly Color[] Palette = {
        Color.DodgerBlue, Color.OrangeRed, Color.ForestGreen,
        Color.Purple, Color.Gold, Color.Cyan, Color.Magenta
    };

    public ChartPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.WhiteSmoke;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        bool dark = Theme == "Dark";
        e.Graphics.Clear(dark ? Color.FromArgb(25, 25, 25) : Color.WhiteSmoke);

        if (Series.Count == 0 || Series.All(s => s.Points.Count == 0))
        {
            DrawText(e.Graphics, "Выберите метрики для отображения", ForeColor, 14, true);
            return;
        }

        var valid = Series.Where(s => s.Points.Any()).ToList();
        if (valid.Count == 0) return;

        int pad = 60;
        int w = Width - pad * 2;
        int h = Height - pad * 2;
        int ox = pad, oy = Height - pad;

        double min = valid.Min(s => s.Points.Min(p => p.Value));
        double max = valid.Max(s => s.Points.Max(p => p.Value));
        double range = max - min > 0 ? max - min : 1;

        DrawGrid(e.Graphics, dark, pad, w, oy, min, range, h);

        for (int i = 0; i < valid.Count; i++)
            DrawSeries(e.Graphics, valid[i], i, ox, oy, w, h, min, range, dark);

        DrawLegend(e.Graphics, valid, pad, 15, dark);
    }

    private void DrawGrid(Graphics g, bool dark, int pad, int w, int oy, double min, double range, int h)
    {
        using var grid = new Pen(dark ? Color.FromArgb(50, 50, 50) : Color.LightGray);
        using var axis = new Pen(dark ? Color.Gray : Color.Black, 2);
        using var font = new Font("Segoe UI", 8);
        var brush = dark ? Brushes.LightGray : Brushes.Gray;

        for (int i = 0; i <= 5; i++)
        {
            int y = oy - (int)(i * h / 5);
            g.DrawLine(grid, pad, y, pad + w, y);
            g.DrawString((min + i * range / 5).ToString("F1"), font, brush, 5, y - 6);
        }
        g.DrawLine(axis, pad, oy, pad + w, oy);
        g.DrawLine(axis, pad, oy - h, pad, oy);
    }

    private void DrawSeries(Graphics g, MetricSeries series, int idx, int ox, int oy, int cw, int ch, double min, double range, bool dark)
    {
        var pts = series.Points;
        if (pts.Count < 2) return;

        Color col = Palette[idx % Palette.Length];
        using var pen = new Pen(col, 2);
        using var fill = new SolidBrush(Color.FromArgb(dark ? 40 : 60, col));
        var path = new GraphicsPath();

        for (int i = 0; i < pts.Count; i++)
        {
            int x = ox + (int)(i * (double)cw / Math.Max(1, pts.Count - 1));
            int y = oy - (int)((pts[i].Value - min) / range * ch);
            if (i == 0) path.StartFigure();
            path.AddLine(x, y, x, y);
        }

        if (ChartType == "Line" || ChartType == "Area")
        {
            g.DrawPath(pen, path);
            if (ChartType == "Area")
            {
                int lastX = ox + (int)((pts.Count - 1) * (double)cw / Math.Max(1, pts.Count - 1));
                path.AddLine(lastX, oy, ox, oy);
                path.CloseFigure();
                g.FillPath(fill, path);
            }
        }
        else if (ChartType == "Bar")
        {
            int bw = Math.Max(1, cw / pts.Count - 1);
            for (int i = 0; i < pts.Count; i++)
            {
                int x = ox + (int)(i * (double)cw / pts.Count);
                int bh = (int)((pts[i].Value - min) / range * ch);
                g.FillRectangle(fill, x, oy - bh, bw, bh);
            }
        }
    }

    private void DrawLegend(Graphics g, List<MetricSeries> valid, int x, int y, bool dark)
    {
        using var font = new Font("Segoe UI", 9);
        int cy = y;
        for (int i = 0; i < valid.Count; i++)
        {
            Color col = Palette[i % Palette.Length];
            using var b = new SolidBrush(col);
            g.FillRectangle(b, x, cy, 12, 12);
            g.DrawString(valid[i].Name, font, dark ? Brushes.White : Brushes.Black, x + 18, cy);
            cy += 18;
        }
    }

    private void DrawText(Graphics g, string text, Color color, float size, bool center)
    {
        using var font = new Font("Segoe UI", size, FontStyle.Italic);
        using var brush = new SolidBrush(color);
        var sf = new StringFormat { Alignment = center ? StringAlignment.Center : StringAlignment.Near };
        var rect = center ? ClientRectangle : new Rectangle(20, 20, Width - 40, Height - 40);
        g.DrawString(text, font, brush, rect, sf);
    }
}