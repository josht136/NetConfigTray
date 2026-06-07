using NetConfigTray.Helpers;
using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

public sealed class InterfacePopupForm : Form
{
    private readonly AppServices _services;
    private readonly ListView _interfaceList;
    private readonly InterfaceDetailPanel _detailPanel;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _fastRefreshTimer;
    private readonly System.Windows.Forms.Timer _slowRefreshTimer;
    private readonly Dictionary<string, NetworkInterfaceInfo> _interfacesById = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedInterfaceId;
    private bool _refreshPending;
    private bool _forceClose;

    public InterfacePopupForm(AppServices services)
    {
        _services = services;

        Text = AppBranding.FullName;
        FormBorderStyle = FormBorderStyle.Sizable;
        ControlBox = true;
        MinimizeBox = true;
        MaximizeBox = true;
        ShowInTaskbar = true;
        TopMost = false;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(640, 420);
        ClientSize = new Size(760, 480);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding = new Padding(12, 8, 12, 8)
        };

        var titleLabel = new Label
        {
            Text = AppBranding.FullName,
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(12, 10)
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
            refreshButton.Location = new Point(headerPanel.Width - refreshButton.Width - 12, 8);
        };

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 220,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(230, 230, 230),
            Panel1MinSize = 160,
            Panel2MinSize = 280
        };

        _interfaceList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9F)
        };
        _interfaceList.Columns.Add("Interface", 110);
        _interfaceList.Columns.Add("Address", 100);
        _interfaceList.SelectedIndexChanged += (_, _) => OnInterfaceSelected();

        splitContainer.Panel1.Controls.Add(_interfaceList);
        splitContainer.Panel1.BackColor = Color.White;
        splitContainer.Panel1.Padding = new Padding(8);

        _detailPanel = new InterfaceDetailPanel();
        splitContainer.Panel2.Controls.Add(_detailPanel);
        splitContainer.Panel2.BackColor = Color.White;

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 12, 0),
            Text = $"{AppBranding.ShortName} — loading…"
        };

        Controls.Add(splitContainer);
        Controls.Add(_statusLabel);
        Controls.Add(headerPanel);

        _fastRefreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _fastRefreshTimer.Tick += (_, _) => UpdateThroughputOnly();

        _slowRefreshTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        _slowRefreshTimer.Tick += (_, _) => _services.Snapshot.EnsureFresh(TimeSpan.FromSeconds(9));

        _services.Snapshot.SnapshotUpdated += OnSnapshotUpdated;

        Shown += (_, _) =>
        {
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

    public void ShowMainWindow()
    {
        if (IsDisposed)
        {
            return;
        }

        if (!Visible)
        {
            Show();
        }

        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
        ForceRefresh(includeSlowDetails: false);
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_forceClose && e.CloseReason != CloseReason.ApplicationExitCall)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
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

    private void HideToTray()
    {
        Hide();
        WindowState = FormWindowState.Normal;
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

            _interfacesById.Clear();
            foreach (var info in interfaces)
            {
                _interfacesById[info.Id] = info;
            }

            _statusLabel.Text = interfaces.Count == 0
                ? $"{AppBranding.ShortName} — refreshing…"
                : $"{AppBranding.ShortName} · Public IP {_services.PublicIp.GetDisplayText()} · Updated {DateTime.Now:t}";

            var previousSelection = _selectedInterfaceId;
            _interfaceList.BeginUpdate();
            _interfaceList.Items.Clear();

            foreach (var info in interfaces)
            {
                var item = new ListViewItem(info.Name)
                {
                    Tag = info.Id
                };
                item.SubItems.Add(info.IPv4Address);
                item.SubItems.Add(info.ConfigurationLabel);
                if (info.IsPrimary)
                {
                    item.Font = new Font(_interfaceList.Font, FontStyle.Bold);
                }

                _interfaceList.Items.Add(item);
            }

            _interfaceList.EndUpdate();

            SelectInterface(previousSelection ?? interfaces.FirstOrDefault(i => i.IsPrimary)?.Id ?? interfaces.FirstOrDefault()?.Id);
            OnInterfaceSelected();
        }
        finally
        {
            _refreshPending = false;
        }
    }

    private void SelectInterface(string? interfaceId)
    {
        if (_interfaceList.Items.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(interfaceId))
        {
            foreach (ListViewItem item in _interfaceList.Items)
            {
                if (string.Equals(item.Tag as string, interfaceId, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    return;
                }
            }
        }

        foreach (ListViewItem item in _interfaceList.Items)
        {
            if (string.Equals(item.Tag as string, _interfacesById.Values.FirstOrDefault(i => i.IsPrimary)?.Id, StringComparison.OrdinalIgnoreCase))
            {
                item.Selected = true;
                item.Focused = true;
                item.EnsureVisible();
                return;
            }
        }

        _interfaceList.Items[0].Selected = true;
    }

    private void OnInterfaceSelected()
    {
        if (_interfaceList.SelectedItems.Count == 0)
        {
            return;
        }

        var selectedId = _interfaceList.SelectedItems[0].Tag as string;
        if (string.IsNullOrWhiteSpace(selectedId) ||
            !_interfacesById.TryGetValue(selectedId, out var info))
        {
            return;
        }

        _selectedInterfaceId = selectedId;
        _services.Snapshot.RequestConnectedDevice(selectedId);

        var (downloadBps, uploadBps) = _services.Throughput.GetThroughput(
            info.Id,
            info.BytesReceived,
            info.BytesSent);

        _services.ThroughputHistory.AddSample(info.Id, downloadBps, uploadBps);
        var history = _services.ThroughputHistory.GetDownloadHistory(info.Id);
        _detailPanel.Bind(info, downloadBps, uploadBps, history);
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

    private void UpdateThroughputOnly()
    {
        if (IsDisposed || string.IsNullOrWhiteSpace(_selectedInterfaceId))
        {
            return;
        }

        if (!_interfacesById.TryGetValue(_selectedInterfaceId, out var info))
        {
            return;
        }

        var byteCounts = _services.Snapshot.GetLiveByteCounts();
        if (!byteCounts.TryGetValue(_selectedInterfaceId, out var counts))
        {
            return;
        }

        info = info with
        {
            BytesReceived = counts.BytesReceived,
            BytesSent = counts.BytesSent
        };
        _interfacesById[_selectedInterfaceId] = info;

        var (downloadBps, uploadBps) = _services.Throughput.GetThroughput(
            _selectedInterfaceId,
            counts.BytesReceived,
            counts.BytesSent);

        _services.ThroughputHistory.AddSample(_selectedInterfaceId, downloadBps, uploadBps);
        _detailPanel.UpdateThroughput(downloadBps, uploadBps, _services.ThroughputHistory.GetDownloadHistory(_selectedInterfaceId));
    }
}
