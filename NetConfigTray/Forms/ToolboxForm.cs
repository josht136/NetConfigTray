using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

/// <summary>
/// The Toolbox launcher: a themed window listing the field-engineering tools. Opened from the
/// tray "Tools" submenu and the main window header. Each tool opens its own non-modal window.
/// </summary>
public sealed class ToolboxForm : Form
{
    private readonly AppServices _services;
    private readonly FlowLayoutPanel _flow;

    public ToolboxForm(AppServices services)
    {
        _services = services;

        Text = $"{AppBranding.ShortName} — Toolbox";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 560);
        MinimumSize = new Size(400, 420);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        var header = new Panel { Dock = DockStyle.Top, Height = 56 };
        AppTheme.StyleHeaderPanel(header);
        var title = new Label();
        AppTheme.StyleTitleLabel(title, "Field Toolkit");
        title.Location = new Point(4, 8);
        var subtitle = new Label();
        AppTheme.StyleSubtitleLabel(subtitle, "Console, discovery, and test tools");
        subtitle.Location = new Point(4, 32);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(16, 16, 16, 16),
            BackColor = AppTheme.AppBackground
        };

        Controls.Add(_flow);
        Controls.Add(header);

        AddTool("Console (Serial / SSH / Telnet)",
            "Connect to switch and router consoles over a COM cable, SSH, or Telnet.",
            OpenConsole);
        AddTool("LLDP / CDP discovery",
            "Find which switch and port this interface is plugged into.",
            OpenNeighborDiscovery);
        AddTool("Latency monitor",
            "Continuous ping with min/avg/max/jitter/loss and MTR-style hops.",
            OpenLatencyMonitor);
        AddTool("Port scan",
            "TCP connect scan of common or custom ports with service names.",
            OpenPortScan);
        AddTool("Throughput test (iperf3)",
            "Measure bandwidth to an iperf3 server or run as a server.",
            OpenThroughputTest);
        AddTool("Wi-Fi survey",
            "Scan nearby networks, channel usage, and least-congested channel.",
            OpenWifiSurvey);
    }

    private void AddTool(string name, string description, Action onClick)
    {
        var card = new Panel
        {
            Width = _flow.ClientSize.Width - 40,
            Height = 64,
            Margin = new Padding(0, 0, 0, 10),
            BackColor = AppTheme.Surface,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Left | AnchorStyles.Right
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(AppTheme.Border);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        var nameLabel = new Label
        {
            Text = name,
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.TextPrimary,
            AutoSize = false,
            Location = new Point(14, 10),
            Size = new Size(card.Width - 28, 20),
            BackColor = Color.Transparent
        };
        var descLabel = new Label
        {
            Text = description,
            Font = AppTheme.FontSmall,
            ForeColor = AppTheme.TextMuted,
            AutoSize = false,
            Location = new Point(14, 32),
            Size = new Size(card.Width - 28, 24),
            BackColor = Color.Transparent
        };

        void Hover(bool on) => card.BackColor = on ? AppTheme.SurfaceHover : AppTheme.Surface;

        card.Click += (_, _) => onClick();
        nameLabel.Click += (_, _) => onClick();
        descLabel.Click += (_, _) => onClick();
        foreach (Control c in new Control[] { card, nameLabel, descLabel })
        {
            c.MouseEnter += (_, _) => Hover(true);
            c.MouseLeave += (_, _) => Hover(false);
        }

        card.Controls.Add(nameLabel);
        card.Controls.Add(descLabel);
        _flow.Controls.Add(card);
    }

    private void OpenConsole() => new ConsoleTerminalForm(_services).Show(this);

    private void OpenNeighborDiscovery() => new NeighborDiscoveryForm(_services).Show(this);

    private void OpenLatencyMonitor() => new LatencyMonitorForm(_services).Show(this);

    private void OpenPortScan() => new PortScanForm(_services).Show(this);

    private void OpenThroughputTest() => new ThroughputTestForm(_services).Show(this);

    private void OpenWifiSurvey() => new WifiSurveyForm(_services).Show(this);
}
