using System.ComponentModel;
using NetConfigTray.Helpers;

namespace NetConfigTray.Forms;

/// <summary>
/// A reusable multi-series time-series chart (latency, throughput, etc.). Series share a common
/// scale; the header shows each series' current value, and the graph shows a peak label and a zero
/// baseline. Values are formatted via <see cref="ValueFormatter"/>.
/// </summary>
public sealed class TimeSeriesChartControl : Control
{
    public sealed record Series(string Name, Color Color, IReadOnlyList<double> Samples, double Current, bool Fill = false);

    private string _title = string.Empty;
    private IReadOnlyList<Series> _series = Array.Empty<Series>();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<double, string> ValueFormatter { get; set; } = v => v.ToString("0.#");

    public TimeSeriesChartControl()
    {
        Height = 200;
        Width = 360;
        BackColor = AppTheme.Surface;
        DoubleBuffered = true;
    }

    public void Update(string title, IReadOnlyList<Series> series)
    {
        _title = title;
        _series = series;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(AppTheme.Surface);

        var width = ClientSize.Width;
        var height = ClientSize.Height;

        const int pad = 12;
        const int headerHeight = 22;

        TextRenderer.DrawText(
            g,
            _title,
            AppTheme.FontSection,
            new Rectangle(pad, 6, width - (pad * 2), headerHeight),
            AppTheme.TextPrimary,
            TextFormatFlags.Left | TextFormatFlags.WordEllipsis);

        // Legend (current values), right-aligned, one segment per series.
        var legendRight = width - pad;
        for (var i = _series.Count - 1; i >= 0; i--)
        {
            var s = _series[i];
            var text = $"{s.Name}: {ValueFormatter(s.Current)}";
            var size = TextRenderer.MeasureText(text, AppTheme.FontCaption);
            var rect = new Rectangle(legendRight - size.Width, 8, size.Width, 16);
            TextRenderer.DrawText(g, text, AppTheme.FontCaption, rect, s.Color, TextFormatFlags.Right);
            legendRight -= size.Width + 16;
        }

        var graphTop = headerHeight + 14;
        var graphBottom = height - 22;
        var graphLeft = pad;
        var graphWidth = width - (pad * 2);
        var graphHeight = graphBottom - graphTop;

        if (graphHeight < 30 || graphWidth < 30)
        {
            return;
        }

        var graphRect = new Rectangle(graphLeft, graphTop, graphWidth, graphHeight);
        using (var border = new Pen(AppTheme.Border))
        {
            g.DrawRectangle(border, graphRect);
        }

        var peak = 0.0;
        foreach (var s in _series)
        {
            if (s.Samples.Count > 0)
            {
                peak = Math.Max(peak, s.Samples.Max());
            }
        }

        TextRenderer.DrawText(
            g,
            $"peak {ValueFormatter(peak)}",
            AppTheme.FontSmall,
            new Rectangle(graphLeft + 4, graphTop + 2, graphWidth - 8, 14),
            AppTheme.TextSecondary,
            TextFormatFlags.Left);

        TextRenderer.DrawText(
            g,
            ValueFormatter(0),
            AppTheme.FontSmall,
            new Rectangle(graphLeft, graphBottom + 4, graphWidth, 14),
            AppTheme.TextMuted,
            TextFormatFlags.Left);

        var hasData = _series.Any(s => s.Samples.Count >= 2);
        if (!hasData)
        {
            TextRenderer.DrawText(
                g,
                "Collecting samples…",
                AppTheme.FontSmall,
                graphRect,
                AppTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var max = Math.Max(peak, double.Epsilon);
        var plotTop = graphTop + 18;
        var plotHeight = graphBottom - plotTop - 2;

        foreach (var s in _series)
        {
            DrawSeries(g, s, max, graphLeft, graphWidth, plotTop, plotHeight, graphBottom);
        }
    }

    private static void DrawSeries(
        Graphics g,
        Series s,
        double max,
        int graphLeft,
        int graphWidth,
        int plotTop,
        int plotHeight,
        int graphBottom)
    {
        if (s.Samples.Count < 2)
        {
            return;
        }

        var step = graphWidth / (float)(s.Samples.Count - 1);
        var points = new PointF[s.Samples.Count];
        for (var i = 0; i < s.Samples.Count; i++)
        {
            var x = graphLeft + step * i;
            var clamped = Math.Max(0, s.Samples[i]);
            var y = plotTop + plotHeight - (float)(clamped / max * plotHeight);
            points[i] = new PointF(x, y);
        }

        if (s.Fill)
        {
            using var fillBrush = new SolidBrush(Color.FromArgb(36, s.Color.R, s.Color.G, s.Color.B));
            var fillPoints = new PointF[points.Length + 2];
            Array.Copy(points, fillPoints, points.Length);
            fillPoints[^2] = new PointF(points[^1].X, graphBottom - 1);
            fillPoints[^1] = new PointF(points[0].X, graphBottom - 1);
            g.FillPolygon(fillBrush, fillPoints);
        }

        using var linePen = new Pen(s.Color, 2f);
        g.DrawLines(linePen, points);
    }
}
