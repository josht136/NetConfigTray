using NetConfigTray.Helpers;

namespace NetConfigTray.Forms;

public sealed class ThroughputSparklineControl : Control
{
    private IReadOnlyList<long> _samples = Array.Empty<long>();
    private long _currentBps;
    private string _title = "DOWNLOAD THROUGHPUT";

    public ThroughputSparklineControl()
    {
        Height = 104;
        Width = 280;
        BackColor = AppTheme.Surface;
        DoubleBuffered = true;
    }

    public void SetTitle(string title)
    {
        _title = title;
        Invalidate();
    }

    public void SetSamples(IReadOnlyList<long> samples)
    {
        _samples = samples;
        _currentBps = samples.Count > 0 ? samples[^1] : 0;
        Invalidate();
    }

    public void Update(IReadOnlyList<long> samples, long currentBps)
    {
        _samples = samples;
        _currentBps = currentBps;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(AppTheme.Surface);

        var width = ClientSize.Width;
        var height = ClientSize.Height;

        using (var separator = new Pen(AppTheme.Border))
        {
            graphics.DrawLine(separator, 0, 0, width, 0);
        }

        // Header: title (left) + current value (right).
        TextRenderer.DrawText(
            graphics,
            _title,
            AppTheme.FontCaption,
            new Rectangle(10, 8, width - 20, 16),
            AppTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.WordEllipsis);

        TextRenderer.DrawText(
            graphics,
            FormatHelper.FormatThroughput(_currentBps),
            AppTheme.FontCaption,
            new Rectangle(10, 8, width - 20, 16),
            AppTheme.Accent,
            TextFormatFlags.Right);

        var graphTop = 30;
        var graphBottom = height - 22;
        var graphLeft = 10;
        var graphRight = width - 10;
        var graphHeight = graphBottom - graphTop;
        var graphWidth = graphRight - graphLeft;

        var graphRect = new Rectangle(graphLeft, graphTop, graphWidth, graphHeight);
        using (var border = new Pen(AppTheme.Border))
        {
            graphics.DrawRectangle(border, graphRect);
        }

        var peak = _samples.Count > 0 ? Math.Max(_samples.Max(), 0) : 0;

        // Peak label (top-left inside graph) and baseline 0 (bottom-left).
        TextRenderer.DrawText(
            graphics,
            $"peak {FormatHelper.FormatThroughput(peak)}",
            AppTheme.FontSmall,
            new Rectangle(graphLeft + 4, graphTop + 2, graphWidth - 8, 14),
            AppTheme.TextSecondary,
            TextFormatFlags.Left);

        TextRenderer.DrawText(
            graphics,
            "0 B/s",
            AppTheme.FontSmall,
            new Rectangle(graphLeft, graphBottom + 4, graphWidth, 14),
            AppTheme.TextMuted,
            TextFormatFlags.Left);

        if (_samples.Count < 2)
        {
            TextRenderer.DrawText(
                graphics,
                "Collecting samples…",
                AppTheme.FontSmall,
                graphRect,
                AppTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var max = Math.Max(peak, 1);
        var plotTop = graphTop + 16;
        var plotHeight = graphBottom - plotTop - 2;
        var step = graphWidth / (float)(_samples.Count - 1);

        var points = new PointF[_samples.Count];
        for (var i = 0; i < _samples.Count; i++)
        {
            var x = graphLeft + step * i;
            var y = plotTop + plotHeight - (_samples[i] / (float)max * plotHeight);
            points[i] = new PointF(x, y);
        }

        using var fillBrush = new SolidBrush(Color.FromArgb(36, AppTheme.Cyan.R, AppTheme.Cyan.G, AppTheme.Cyan.B));
        var fillPoints = new PointF[points.Length + 2];
        Array.Copy(points, fillPoints, points.Length);
        fillPoints[^2] = new PointF(points[^1].X, graphBottom - 1);
        fillPoints[^1] = new PointF(points[0].X, graphBottom - 1);
        graphics.FillPolygon(fillBrush, fillPoints);

        using var linePen = new Pen(AppTheme.Cyan, 2f);
        graphics.DrawLines(linePen, points);
    }
}
