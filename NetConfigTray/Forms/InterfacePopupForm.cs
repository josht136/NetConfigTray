using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

public sealed class InterfacePopupForm : Form
{
    private static readonly Color DhcpColor = Color.FromArgb(16, 124, 16);
    private static readonly Color StaticColor = Color.FromArgb(202, 80, 16);
    private static readonly Color DhcpBackground = Color.FromArgb(223, 246, 221);
    private static readonly Color StaticBackground = Color.FromArgb(255, 236, 224);

    private readonly NetworkInfoService _networkInfoService;
    private readonly FlowLayoutPanel _interfacePanel;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _refreshTimer;

    public InterfacePopupForm(NetworkInfoService networkInfoService)
    {
        _networkInfoService = networkInfoService;

        Text = "NetConfigTray";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ControlBox = true;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(360, 280);
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
            Height = 22,
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Auto-refreshes every 2 seconds"
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
            interfaces = _networkInfoService.GetActiveInterfaces();
            _statusLabel.Text = $"Updated {DateTime.Now:t} · auto-refresh every 2s";
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
                _interfacePanel.Controls.Add(CreateInterfaceCard(info));
            }
        }

        _interfacePanel.ResumeLayout(performLayout: true);
    }

    private Control CreateInterfaceCard(NetworkInterfaceInfo info)
    {
        var isDhcp = info.ConfigurationType == IpConfigurationType.Dhcp;
        var badgeColor = isDhcp ? DhcpColor : StaticColor;
        var badgeBackground = isDhcp ? DhcpBackground : StaticBackground;

        var card = new Panel
        {
            Width = _interfacePanel.ClientSize.Width - 28,
            Height = 72,
            BackColor = Color.FromArgb(248, 248, 248),
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(12, 10, 12, 10)
        };

        var nameLabel = new Label
        {
            Text = info.Name,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        var ipLabel = new Label
        {
            Text = info.IPv4Address,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(50, 50, 50),
            AutoSize = true,
            Location = new Point(0, 24)
        };

        var badge = new Label
        {
            Text = info.ConfigurationLabel,
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            ForeColor = badgeColor,
            BackColor = badgeBackground,
            AutoSize = false,
            Size = new Size(56, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(card.Width - 68, 0)
        };

        card.Controls.Add(nameLabel);
        card.Controls.Add(ipLabel);
        card.Controls.Add(badge);

        card.Resize += (_, _) => badge.Location = new Point(card.Width - 68, 0);

        return card;
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
