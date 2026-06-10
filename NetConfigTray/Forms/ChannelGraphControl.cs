using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

/// <summary>
/// inSSIDer-style channel-overlap view: draws each network as a bell curve centered on its channel,
/// with height proportional to signal, for the selected band.
/// </summary>
public sealed class ChannelGraphControl : Control
{
    private static readonly Color[] Palette =
    {
        AppTheme.Cyan, AppTheme.Blue, AppTheme.Green, AppTheme.Yellow,
        AppTheme.Orange, AppTheme.Magenta, AppTheme.Violet, AppTheme.Red
    };

    private IReadOnlyList<WifiBss> _networks = Array.Empty<WifiBss>();
    private double _band = 2.4;

    public ChannelGraphControl()
    {
        DoubleBuffered = true;
        BackColor = AppTheme.Surface;
        Height = 240;
    }

    public void SetData(IReadOnlyList<WifiBss> networks, double bandGhz)
    {
        _networks = networks;
        _band = bandGhz;
        Invalidate();
    }

    private (int min, int max) ChannelRange => _band switch
    {
        2.4 => (1, 14),
        5.0 => (32, 165),
        _ => (1, 233)
    };

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(AppTheme.Surface);

        const int padLeft = 36;
        const int padBottom = 24;
        const int padTop = 12;
        const int padRight = 12;

        var plotWidth = Width - padLeft - padRight;
        var plotHeight = Height - padTop - padBottom;
        if (plotWidth < 40 || plotHeight < 40)
        {
            return;
        }

        var (minCh, maxCh) = ChannelRange;
        var span = Math.Max(1, maxCh - minCh);

        var baseY = padTop + plotHeight;

        using (var axisPen = new Pen(AppTheme.Border))
        {
            g.DrawLine(axisPen, padLeft, baseY, padLeft + plotWidth, baseY);
            g.DrawLine(axisPen, padLeft, padTop, padLeft, baseY);
        }

        // Channel ticks.
        var tickStep = _band == 2.4 ? 1 : 4;
        for (var ch = minCh; ch <= maxCh; ch += tickStep)
        {
            var x = padLeft + (int)((ch - minCh) / (double)span * plotWidth);
            TextRenderer.DrawText(g, ch.ToString(), AppTheme.FontSmall,
                new Point(x - 8, baseY + 4), AppTheme.TextMuted);
        }

        var bandNetworks = _networks.Where(n => Math.Abs(n.BandGhz - _band) < 0.6).ToList();
        if (bandNetworks.Count == 0)
        {
            TextRenderer.DrawText(g, "No networks on this band", AppTheme.FontBody,
                new Rectangle(padLeft, padTop, plotWidth, plotHeight), AppTheme.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var colorIndex = 0;
        foreach (var net in bandNetworks)
        {
            var color = Palette[colorIndex++ % Palette.Length];
            var centerX = padLeft + (int)((net.Channel - minCh) / (double)span * plotWidth);
            var halfWidth = (int)(plotWidth / (double)span * (_band == 2.4 ? 2.0 : 1.2));
            halfWidth = Math.Max(halfWidth, 10);
            var peakY = baseY - (int)(net.SignalPercent / 100.0 * plotHeight);

            DrawBell(g, color, centerX, halfWidth, baseY, peakY);

            var label = $"{net.Ssid} (ch {net.Channel})";
            TextRenderer.DrawText(g, label, AppTheme.FontSmall,
                new Point(centerX - 30, Math.Max(padTop, peakY - 16)), color);
        }
    }

    private static void DrawBell(Graphics g, Color color, int centerX, int halfWidth, int baseY, int peakY)
    {
        var left = centerX - halfWidth;
        var right = centerX + halfWidth;

        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddBezier(
            new Point(left, baseY),
            new Point(centerX - halfWidth / 2, baseY),
            new Point(centerX - halfWidth / 2, peakY),
            new Point(centerX, peakY));
        path.AddBezier(
            new Point(centerX, peakY),
            new Point(centerX + halfWidth / 2, peakY),
            new Point(centerX + halfWidth / 2, baseY),
            new Point(right, baseY));

        using var fill = new SolidBrush(Color.FromArgb(48, color.R, color.G, color.B));
        g.FillPath(fill, path);
        using var pen = new Pen(color, 1.5f);
        g.DrawPath(pen, path);
    }
}
