using NetConfigTray.Helpers;
using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

public sealed class InterfacePopupForm : Form
{
    private readonly AppServices _services;
    private readonly SplitContainer _splitContainer;
    private readonly ListView _interfaceList;
    private readonly InterfaceDetailPanel _detailPanel;
    private readonly Label _statusLabel;
    private readonly System.Windows.Forms.Timer _fastRefreshTimer;
    private readonly System.Windows.Forms.Timer _slowRefreshTimer;
    private readonly Dictionary<string, NetworkInterfaceInfo> _interfacesById = new(StringComparer.OrdinalIgnoreCase);
    private string? _selectedInterfaceId;
    private bool _forceClose;
    private string _lastListSignature = string.Empty;
    private string _lastDetailLayoutSignature = string.Empty;

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

        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(230, 230, 230),
            Panel1MinSize = 0,
            Panel2MinSize = 0
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
        _interfaceList.Columns.Add("Interface", 120);
        _interfaceList.Columns.Add("Address", 110);
        _interfaceList.Columns.Add("Config", 60);
        _interfaceList.SelectedIndexChanged += OnInterfaceSelected;

        _splitContainer.Panel1.Controls.Add(_interfaceList);
        _splitContainer.Panel1.BackColor = Color.White;
        _splitContainer.Panel1.Padding = new Padding(8);

        _detailPanel = new InterfaceDetailPanel();
        _splitContainer.Panel2.Controls.Add(_detailPanel);
        _splitContainer.Panel2.BackColor = Color.White;

        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 12, 0),
            Text = $"{AppBranding.ShortName} — loading…"
        };

        SuspendLayout();
        Controls.Add(headerPanel);
        Controls.Add(_statusLabel);
        Controls.Add(_splitContainer);
        ResumeLayout(false);

        Load += (_, _) =>
        {
            ConfigureSplitterLayout();
            ApplySnapshotToUi();
        };

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
        ApplySnapshotToUi();
        ForceRefresh(includeSlowDetails: false);
    }

    private void RunOnUiThread(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (!IsHandleCreated)
        {
            EventHandler? handler = null;
            handler = (_, _) =>
            {
                Load -= handler;
                if (!IsDisposed)
                {
                    action();
                }
            };
            Load += handler;
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
            return;
        }

        action();
    }

    private void ConfigureSplitterLayout()
    {
        if (_splitContainer.IsDisposed || _splitContainer.Width <= 0)
        {
            return;
        }

        const int panel1Min = 140;
        const int panel2Min = 240;
        const int preferredDistance = 220;

        _splitContainer.Panel1MinSize = 0;
        _splitContainer.Panel2MinSize = 0;

        var maxDistance = _splitContainer.Width - _splitContainer.SplitterWidth;
        _splitContainer.SplitterDistance = Math.Clamp(
            preferredDistance,
            0,
            Math.Max(0, maxDistance));

        if (maxDistance < panel1Min + panel2Min)
        {
            return;
        }

        _splitContainer.Panel1MinSize = panel1Min;
        _splitContainer.Panel2MinSize = panel2Min;
        maxDistance = _splitContainer.Width - panel2Min - _splitContainer.SplitterWidth;
        _splitContainer.SplitterDistance = Math.Clamp(preferredDistance, panel1Min, maxDistance);
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
        RunOnUiThread(ApplySnapshotToUi);
    }

    private void ApplySnapshotToUi()
    {
        if (IsDisposed)
        {
            return;
        }

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

            var listSignature = BuildListSignature(interfaces);
            var listChanged = !string.Equals(listSignature, _lastListSignature, StringComparison.Ordinal);

            if (listChanged)
            {
                _lastListSignature = listSignature;
                var previousSelection = _selectedInterfaceId;

                _interfaceList.BeginUpdate();
                try
                {
                    _interfaceList.Items.Clear();

                    foreach (var info in interfaces)
                    {
                        var item = new ListViewItem(info.IsPrimary ? $"{info.Name} *" : info.Name)
                        {
                            Tag = info.Id
                        };
                        item.SubItems.Add(info.IPv4Address);
                        item.SubItems.Add(info.ConfigurationLabel);
                        _interfaceList.Items.Add(item);
                    }
                }
                finally
                {
                    _interfaceList.EndUpdate();
                }

                if (interfaces.Count == 0)
                {
                    _selectedInterfaceId = null;
                    _detailPanel.ShowPlaceholder("No active interfaces found. Click Refresh to try again.");
                    return;
                }

                var targetId = previousSelection
                    ?? interfaces.FirstOrDefault(i => i.IsPrimary)?.Id
                    ?? interfaces[0].Id;

                _interfaceList.SelectedIndexChanged -= OnInterfaceSelected;
                try
                {
                    SelectInterface(targetId);
                }
                finally
                {
                    _interfaceList.SelectedIndexChanged += OnInterfaceSelected;
                }
            }

            if (interfaces.Count > 0)
            {
                var selectedId = _interfaceList.SelectedItems.Count > 0
                    ? _interfaceList.SelectedItems[0].Tag as string
                    : _selectedInterfaceId;

                if (!string.IsNullOrWhiteSpace(selectedId) &&
                    _interfacesById.TryGetValue(selectedId, out var selectedInfo))
                {
                    var detailLayoutSignature = BuildDetailLayoutSignature(selectedInfo);
                    if (listChanged ||
                        !string.Equals(detailLayoutSignature, _lastDetailLayoutSignature, StringComparison.Ordinal))
                    {
                        _lastDetailLayoutSignature = detailLayoutSignature;
                        UpdateDetailPanelForSelection();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"{AppBranding.ShortName} — update failed: {ex.Message}";
            _detailPanel.ShowPlaceholder("Unable to display network details. Try Refresh again.");
        }
    }

    private static string BuildListSignature(IReadOnlyList<NetworkInterfaceInfo> interfaces)
    {
        return string.Join("|", interfaces.Select(i => i.ChangeSignature));
    }

    private static string BuildDetailLayoutSignature(NetworkInterfaceInfo info)
    {
        var deviceSignature = info.ConnectedDevice is null
            ? string.Empty
            : string.Join("|",
                info.ConnectedDevice.Role,
                info.ConnectedDevice.IpAddress,
                info.ConnectedDevice.Hostname,
                info.ConnectedDevice.MacAddress,
                info.ConnectedDevice.ExtraInfo);

        return string.Join("|",
            info.Id,
            info.Name,
            info.IsPrimary,
            info.ConfigurationType,
            info.IPv4Address,
            info.Cidr,
            info.MacAddress,
            info.LinkSpeedBps,
            info.Gateway,
            info.DnsServers,
            info.RouteMetric,
            info.DhcpServer,
            info.DhcpLeaseObtained,
            info.DhcpLeaseExpires,
            info.WifiChannel,
            info.WifiBand,
            info.WifiRadioType,
            deviceSignature);
    }

    private void UpdateDetailPanelForSelection()
    {
        if (_interfaceList.SelectedItems.Count == 0)
        {
            _detailPanel.ShowPlaceholder("Select an interface to view details.");
            return;
        }

        var selectedId = _interfaceList.SelectedItems[0].Tag as string;
        if (string.IsNullOrWhiteSpace(selectedId) ||
            !_interfacesById.TryGetValue(selectedId, out var info))
        {
            return;
        }

        _selectedInterfaceId = selectedId;
        _lastDetailLayoutSignature = BuildDetailLayoutSignature(info);

        if (info.ConnectedDevice is null)
        {
            _services.Snapshot.RequestConnectedDevice(selectedId);
        }

        try
        {
            var (downloadBps, uploadBps) = _services.Throughput.GetThroughput(
                info.Id,
                info.BytesReceived,
                info.BytesSent);

            _services.ThroughputHistory.AddSample(info.Id, downloadBps, uploadBps);
            var history = _services.ThroughputHistory.GetDownloadHistory(info.Id);
            _detailPanel.Bind(info, downloadBps, uploadBps, history);
        }
        catch (Exception ex)
        {
            _detailPanel.ShowPlaceholder($"Unable to show details: {ex.Message}");
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

    private void OnInterfaceSelected(object? sender, EventArgs e)
    {
        UpdateDetailPanelForSelection();
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
        _detailPanel.UpdateThroughput(
            downloadBps,
            uploadBps,
            _services.ThroughputHistory.GetDownloadHistory(_selectedInterfaceId));

        if (_interfacesById.TryGetValue(_selectedInterfaceId, out var refreshed))
        {
            refreshed = refreshed with
            {
                ConnectionUptime = _services.Uptime.GetUptimeText(_selectedInterfaceId, isActive: true),
                GatewayPing = _services.GatewayPing.GetLatencyText(refreshed.Gateway)
            };
            _interfacesById[_selectedInterfaceId] = refreshed;
            _detailPanel.UpdateLiveFields(refreshed);
        }
    }
}
