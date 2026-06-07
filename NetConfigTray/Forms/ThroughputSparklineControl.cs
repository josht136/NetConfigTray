namespace NetConfigTray.Forms;

public sealed class ThroughputSparklineControl : Control
{
    private IReadOnlyList<long> _samples = Array.Empty<long>();

    public ThroughputSparklineControl()
    {
        Height = 36;
        Width = 280;
        BackColor = Color.White;
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
        graphics.Clear(Color.White);

        if (_samples.Count < 2)
        {
            using var emptyBrush = new SolidBrush(Color.Gray);
            graphics.DrawString("Collecting throughput samples…", Font, emptyBrush, 2, 10);
            return;
        }

        var max = Math.Max(_samples.Max(), 1);
        var left = 2;
        var top = 4;
        var width = ClientSize.Width - 4;
        var height = ClientSize.Height - 8;
        var step = width / (float)(_samples.Count - 1);

        using var gridPen = new Pen(Color.FromArgb(230, 230, 230));
        graphics.DrawLine(gridPen, left, top + height, left + width, top + height);

        var points = new PointF[_samples.Count];
        for (var i = 0; i < _samples.Count; i++)
        {
            var x = left + step * i;
            var y = top + height - (_samples[i] / (float)max * height);
            points[i] = new PointF(x, y);
        }

        using var linePen = new Pen(Color.FromArgb(0, 120, 215), 2f);
        graphics.DrawLines(linePen, points);
    }
}
