using NetConfigTray.Helpers;

namespace NetConfigTray.Forms;

public sealed class ThroughputSparklineControl : Control
{
    private IReadOnlyList<long> _samples = Array.Empty<long>();

    public ThroughputSparklineControl()
    {
        Height = 44;
        Width = 280;
        BackColor = AppTheme.Surface;
        DoubleBuffered = true;
    }

    public void SetSamples(IReadOnlyList<long> samples)
    {
        _samples = samples;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        var graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(AppTheme.Surface);

        var inner = new Rectangle(1, 1, ClientSize.Width - 2, ClientSize.Height - 2);
        using (var border = new Pen(AppTheme.Border))
        {
            graphics.DrawRectangle(border, inner);
        }

        if (_samples.Count < 2)
        {
            using var emptyBrush = new SolidBrush(AppTheme.TextMuted);
            TextRenderer.DrawText(
                graphics,
                "Collecting throughput samples…",
                AppTheme.FontSmall,
                new Rectangle(8, 0, ClientSize.Width - 16, ClientSize.Height),
                AppTheme.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            return;
        }

        var max = Math.Max(_samples.Max(), 1);
        var left = 8;
        var top = 6;
        var width = ClientSize.Width - 16;
        var height = ClientSize.Height - 14;
        var step = width / (float)(_samples.Count - 1);

        using var gridPen = new Pen(AppTheme.BorderSubtle);
        graphics.DrawLine(gridPen, left, top + height, left + width, top + height);

        var points = new PointF[_samples.Count];
        for (var i = 0; i < _samples.Count; i++)
        {
            var x = left + step * i;
            var y = top + height - (_samples[i] / (float)max * height);
            points[i] = new PointF(x, y);
        }

        using var fillBrush = new SolidBrush(Color.FromArgb(28, AppTheme.Cyan.R, AppTheme.Cyan.G, AppTheme.Cyan.B));
        var fillPoints = new PointF[points.Length + 2];
        Array.Copy(points, fillPoints, points.Length);
        fillPoints[^2] = new PointF(points[^1].X, top + height);
        fillPoints[^1] = new PointF(points[0].X, top + height);
        graphics.FillPolygon(fillBrush, fillPoints);

        using var linePen = new Pen(AppTheme.Cyan, 2f);
        graphics.DrawLines(linePen, points);
    }
}
