using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

public sealed class InterfacePopupForm : Form
{
    private readonly AppServices _services;
    private readonly FlowLayoutPanel _interfacePanel;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _fastRefreshTimer;
    private readonly System.Windows.Forms.Timer _slowRefreshTimer;
    private readonly Dictionary<string, InterfaceCardPanel> _cards = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _expandedInterfaceIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _refreshPending;

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
        refreshButton.Click += (_, _) => ForceRefresh(includeSlowDetails: true);

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
            Text = "Click an interface for details"
        };

        Controls.Add(_interfacePanel);
        Controls.Add(_statusLabel);
        Controls.Add(headerPanel);

        _fastRefreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _fastRefreshTimer.Tick += (_, _) => UpdateThroughputOnly();

        _slowRefreshTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        _slowRefreshTimer.Tick += (_, _) => _services.Snapshot.EnsureFresh(TimeSpan.FromSeconds(9));

        _services.Snapshot.SnapshotUpdated += OnSnapshotUpdated;

        Deactivate += (_, _) => Hide();
        Shown += (_, _) =>
        {
            PositionNearTray();
            _services.PublicIp.RefreshAsync();
            ForceRefresh(includeSlowDetails: false);
            _fastRefreshTimer.Start();
            _slowRefreshTimer.Start();
        };

        VisibleChanged += (_, _) =>
        {
            if (Visible)
            {
                _fastRefreshTimer.Start();
                _slowRefreshTimer.Start();
            }
            else
            {
                _fastRefreshTimer.Stop();
                _slowRefreshTimer.Stop();
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
            ForceRefresh(includeSlowDetails: false);
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _services.Snapshot.SnapshotUpdated -= OnSnapshotUpdated;
            _fastRefreshTimer.Dispose();
            _slowRefreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ForceRefresh(bool includeSlowDetails)
    {
        _services.Snapshot.RequestRefresh(includeSlowDetails);
    }

    private void OnSnapshotUpdated()
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(ApplySnapshotToUi);
            return;
        }

        ApplySnapshotToUi();
    }

    private void ApplySnapshotToUi()
    {
        if (IsDisposed || _refreshPending)
        {
            return;
        }

        _refreshPending = true;
        try
        {
            var interfaces = _services.Snapshot.GetSnapshot()
                .Select(EnrichInterface)
                .ToList();

            _statusLabel.Text = interfaces.Count == 0
                ? "Refreshing…"
                : $"Public IP: {_services.PublicIp.GetDisplayText()}{Environment.NewLine}Updated {DateTime.Now:t} · click interface for details";

            _interfacePanel.SuspendLayout();

            if (interfaces.Count == 0)
            {
                _interfacePanel.Controls.Clear();
                _cards.Clear();
                _interfacePanel.Controls.Add(new Label
                {
                    Text = "Refreshing network interfaces…",
                    ForeColor = Color.Gray,
                    Width = _interfacePanel.ClientSize.Width - 24,
                    Padding = new Padding(4)
                });
            }
            else
            {
                RemovePlaceholderLabels();

                var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var info in interfaces)
                {
                    seenIds.Add(info.Id);
                    var (downloadBps, uploadBps) = _services.Throughput.GetThroughput(
                        info.Id,
                        info.BytesReceived,
                        info.BytesSent);

                    _services.ThroughputHistory.AddSample(info.Id, downloadBps, uploadBps);
                    var history = _services.ThroughputHistory.GetDownloadHistory(info.Id);

                    if (!_cards.TryGetValue(info.Id, out var card))
                    {
                        card = CreateCard(info.Id);
                        _cards[info.Id] = card;
                        _interfacePanel.Controls.Add(card);
                    }

                    card.Bind(info, downloadBps, uploadBps, history, _expandedInterfaceIds.Contains(info.Id));
                }

                foreach (var id in _cards.Keys.ToList())
                {
                    if (seenIds.Contains(id))
                    {
                        continue;
                    }

                    if (_cards.Remove(id, out var removed))
                    {
                        _interfacePanel.Controls.Remove(removed);
                        removed.Dispose();
                        _expandedInterfaceIds.Remove(id);
                    }
                }
            }

            _interfacePanel.ResumeLayout(performLayout: true);
        }
        finally
        {
            _refreshPending = false;
        }
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

    private InterfaceCardPanel CreateCard(string interfaceId)
    {
        var card = new InterfaceCardPanel(_interfacePanel.ClientSize.Width - 28)
        {
            Tag = interfaceId
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
                _services.Snapshot.RequestConnectedDevice(id);
            }
            else
            {
                _expandedInterfaceIds.Remove(id);
            }
        };

        return card;
    }

    private void UpdateThroughputOnly()
    {
        if (IsDisposed || _cards.Count == 0)
        {
            return;
        }

        var byteCounts = _services.Snapshot.GetLiveByteCounts();
        foreach (var (id, card) in _cards)
        {
            if (!byteCounts.TryGetValue(id, out var counts))
            {
                continue;
            }

            var (downloadBps, uploadBps) = _services.Throughput.GetThroughput(
                id,
                counts.BytesReceived,
                counts.BytesSent);

            _services.ThroughputHistory.AddSample(id, downloadBps, uploadBps);
            card.UpdateThroughput(downloadBps, uploadBps, _services.ThroughputHistory.GetDownloadHistory(id));
        }
    }

    private void RemovePlaceholderLabels()
    {
        foreach (var label in _interfacePanel.Controls.OfType<Label>().ToList())
        {
            _interfacePanel.Controls.Remove(label);
            label.Dispose();
        }
    }

    private void PositionNearTray()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        Location = new Point(area.Right - Width - 8, area.Bottom - Height - 8);
    }
}
