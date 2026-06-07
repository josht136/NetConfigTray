using NetConfigTray.Helpers;

namespace NetConfigTray.Forms;

public sealed class ThroughputSparklineControl : Control
{
    private IReadOnlyList<long> _downloadSamples = Array.Empty<long>();
    private IReadOnlyList<long> _uploadSamples = Array.Empty<long>();
    private long _downloadBps;
    private long _uploadBps;
    private string _title = "THROUGHPUT";

    private static Color DownloadColor => AppTheme.Cyan;
    private static Color UploadColor => AppTheme.Yellow;

    public ThroughputSparklineControl()
    {
        Height = 120;
        Width = 280;
        BackColor = AppTheme.Surface;
        DoubleBuffered = true;
    }

    public void SetTitle(string title)
    {
        _title = title;
        Invalidate();
    }

    public void Update(
        IReadOnlyList<long> downloadSamples,
        long downloadBps,
        IReadOnlyList<long> uploadSamples,
        long uploadBps)
    {
        _downloadSamples = downloadSamples;
        _uploadSamples = uploadSamples;
        _downloadBps = downloadBps;
        _uploadBps = uploadBps;
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

        const int pad = 10;
        const int headerHeight = 20;
        const int valueWidth = 95;

        // Header: title (left, width-constrained) + current download/upload (right).
        TextRenderer.DrawText(
            graphics,
            _title,
            AppTheme.FontCaption,
            new Rectangle(pad, 6, Math.Max(40, width - (pad * 2) - (valueWidth * 2)), headerHeight),
            AppTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.WordEllipsis);

        TextRenderer.DrawText(
            graphics,
            $"↓ {FormatHelper.FormatThroughput(_downloadBps)}",
            AppTheme.FontCaption,
            new Rectangle(width - pad - (valueWidth * 2), 6, valueWidth, headerHeight),
            DownloadColor,
            TextFormatFlags.Right);

        TextRenderer.DrawText(
            graphics,
            $"↑ {FormatHelper.FormatThroughput(_uploadBps)}",
            AppTheme.FontCaption,
            new Rectangle(width - pad - valueWidth, 6, valueWidth, headerHeight),
            UploadColor,
            TextFormatFlags.Right);

        var graphTop = headerHeight + 12;
        var graphBottom = height - 20;
        var graphLeft = pad;
        var graphRight = width - pad;
        var graphHeight = graphBottom - graphTop;
        var graphWidth = graphRight - graphLeft;

        if (graphHeight < 24 || graphWidth < 24)
        {
            return;
        }

        var graphRect = new Rectangle(graphLeft, graphTop, graphWidth, graphHeight);
        using (var border = new Pen(AppTheme.Border))
        {
            graphics.DrawRectangle(border, graphRect);
        }

        var downloadPeak = _downloadSamples.Count > 0 ? _downloadSamples.Max() : 0;
        var uploadPeak = _uploadSamples.Count > 0 ? _uploadSamples.Max() : 0;
        var peak = Math.Max(downloadPeak, uploadPeak);

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

        if (_downloadSamples.Count < 2 && _uploadSamples.Count < 2)
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

        DrawSeries(graphics, _downloadSamples, max, graphLeft, graphWidth, plotTop, plotHeight, graphBottom, DownloadColor, fill: true);
        DrawSeries(graphics, _uploadSamples, max, graphLeft, graphWidth, plotTop, plotHeight, graphBottom, UploadColor, fill: false);
    }

    private static void DrawSeries(
        Graphics graphics,
        IReadOnlyList<long> samples,
        long max,
        int graphLeft,
        int graphWidth,
        int plotTop,
        int plotHeight,
        int graphBottom,
        Color color,
        bool fill)
    {
        if (samples.Count < 2)
        {
            return;
        }

        var step = graphWidth / (float)(samples.Count - 1);
        var points = new PointF[samples.Count];
        for (var i = 0; i < samples.Count; i++)
        {
            var x = graphLeft + step * i;
            var y = plotTop + plotHeight - (samples[i] / (float)max * plotHeight);
            points[i] = new PointF(x, y);
        }

        if (fill)
        {
            using var fillBrush = new SolidBrush(Color.FromArgb(36, color.R, color.G, color.B));
            var fillPoints = new PointF[points.Length + 2];
            Array.Copy(points, fillPoints, points.Length);
            fillPoints[^2] = new PointF(points[^1].X, graphBottom - 1);
            fillPoints[^1] = new PointF(points[0].X, graphBottom - 1);
            graphics.FillPolygon(fillBrush, fillPoints);
        }

        using var linePen = new Pen(color, 2f);
        graphics.DrawLines(linePen, points);
    }
}
