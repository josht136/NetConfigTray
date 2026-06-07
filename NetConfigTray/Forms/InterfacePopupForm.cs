using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

public sealed class InterfacePopupForm : Form
{
    private readonly AppServices _services;
    private readonly FlowLayoutPanel _interfacePanel;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly HashSet<string> _expandedInterfaceIds = new(StringComparer.OrdinalIgnoreCase);

    public InterfacePopupForm(AppServices services)
    {
        _services = services;

        Text = "NetConfigTray";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ControlBox = true;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(440, 420);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9F);
        Padding = new Padding(12);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.White
        };

        var titleLabel = new Label
        {
            Text = "Network Interfaces",
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 4)
        };

        var refreshButton = new Button
        {
            Text = "Refresh",
            Size = new Size(72, 28),
            FlatStyle = FlatStyle.System,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        refreshButton.Click += (_, _) => RefreshInterfaces();

        headerPanel.Controls.Add(titleLabel);
        headerPanel.Controls.Add(refreshButton);
        headerPanel.Resize += (_, _) =>
        {
            refreshButton.Location = new Point(headerPanel.Width - refreshButton.Width, 4);
        };

        _interfacePanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0)
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.TopLeft,
            Text = "Click an interface for details · auto-refresh every 2s"
        };

        Controls.Add(_interfacePanel);
        Controls.Add(_statusLabel);
        Controls.Add(headerPanel);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _refreshTimer.Tick += (_, _) => RefreshInterfaces();

        Deactivate += (_, _) => Hide();
        Shown += (_, _) =>
        {
            PositionNearTray();
            _services.PublicIp.RefreshAsync();
            RefreshInterfaces();
            _refreshTimer.Start();
        };

        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                _refreshTimer.Start();
            }
            else
            {
                _refreshTimer.Stop();
            }
        };
    }

    public bool ShowNearTray()
    {
        if (IsDisposed)
        {
            return false;
        }

        if (Visible)
        {
            Hide();
            return true;
        }

        try
        {
            PositionNearTray();
            Show();
            Activate();
            RefreshInterfaces();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.ApplicationExitCall)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private void PositionNearTray()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        Location = new Point(area.Right - Width - 8, area.Bottom - Height - 8);
    }

    private void RefreshInterfaces()
    {
        if (IsDisposed)
        {
            return;
        }

        IReadOnlyList<NetworkInterfaceInfo> interfaces;

        try
        {
            interfaces = _services.NetworkInfo.GetActiveInterfaces()
                .Select(EnrichInterface)
                .ToList();
            _statusLabel.Text =
                $"Public IP: {_services.PublicIp.GetDisplayText()}{Environment.NewLine}Updated {DateTime.Now:t} · click interface for details";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
            interfaces = Array.Empty<NetworkInterfaceInfo>();
        }

        _interfacePanel.SuspendLayout();
        _interfacePanel.Controls.Clear();

        if (interfaces.Count == 0)
        {
            _interfacePanel.Controls.Add(new Label
            {
                Text = "No active network interfaces found.",
                ForeColor = Color.Gray,
                Width = _interfacePanel.ClientSize.Width - 24,
                Padding = new Padding(4)
            });
        }
        else
        {
            foreach (var info in interfaces)
            {
                var (downloadBps, uploadBps) = _services.Throughput.GetThroughput(
                    info.Id,
                    info.BytesReceived,
                    info.BytesSent);

                _services.ThroughputHistory.AddSample(info.Id, downloadBps, uploadBps);
                var history = _services.ThroughputHistory.GetDownloadHistory(info.Id);

                var card = new InterfaceCardPanel(_interfacePanel.ClientSize.Width - 28)
                {
                    Tag = info.Id
                };
                card.ExpandedChanged += (_, _) =>
                {
                    if (card.Tag is not string id)
                    {
                        return;
                    }

                    if (card.IsExpanded)
                    {
                        _expandedInterfaceIds.Add(id);
                    }
                    else
                    {
                        _expandedInterfaceIds.Remove(id);
                    }
                };
                card.Bind(info, downloadBps, uploadBps, history, _expandedInterfaceIds.Contains(info.Id));
                _interfacePanel.Controls.Add(card);
            }
        }

        _interfacePanel.ResumeLayout(performLayout: true);
    }

    private NetworkInterfaceInfo EnrichInterface(NetworkInterfaceInfo info)
    {
        _services.GatewayPing.QueuePing(info.Gateway);

        return info with
        {
            ConnectionUptime = _services.Uptime.GetUptimeText(info.Id, isActive: true),
            GatewayPing = _services.GatewayPing.GetLatencyText(info.Gateway)
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}
