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
    private bool _splitterInitialized;

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
        MinimumSize = new Size(680, 460);
        ClientSize = new Size(820, 520);
        AppTheme.ApplyFormChrome(this);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56
        };
        AppTheme.StyleHeaderPanel(headerPanel);

        var accentMark = new Panel
        {
            Size = new Size(4, 22),
            BackColor = AppTheme.Accent,
            Location = new Point(20, 17)
        };

        var titleStack = new Panel
        {
            BackColor = Color.Transparent,
            Location = new Point(32, 10),
            Size = new Size(360, 36)
        };

        var titleLabel = new Label();
        AppTheme.StyleTitleLabel(titleLabel, AppBranding.FullName);

        var subtitleLabel = new Label();
        AppTheme.StyleSubtitleLabel(subtitleLabel, "Network interfaces · live status");

        titleLabel.Location = new Point(0, 0);
        subtitleLabel.Location = new Point(0, 22);
        titleStack.Controls.Add(titleLabel);
        titleStack.Controls.Add(subtitleLabel);

        var refreshButton = new Button
        {
            Text = "Refresh",
            Size = new Size(88, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        AppTheme.StyleAccentButton(refreshButton);
        refreshButton.Click += (_, _) => ForceRefresh(includeSlowDetails: true);

        headerPanel.Controls.Add(accentMark);
        headerPanel.Controls.Add(titleStack);
        headerPanel.Controls.Add(refreshButton);
        headerPanel.Resize += (_, _) =>
        {
            refreshButton.Location = new Point(headerPanel.Width - refreshButton.Width - 20, 12);
        };

        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterWidth = 1,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };
        AppTheme.StyleSplitContainer(_splitContainer);

        _interfaceList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            OwnerDraw = true
        };
        AppTheme.StyleListView(_interfaceList);
        // Force a taller row height so the larger primary-interface font and the
        // DHCP/Static type text are not vertically clipped in owner-draw Details view.
        _interfaceList.SmallImageList = new ImageList { ImageSize = new Size(1, 34) };
        _interfaceList.Columns.Add("INTERFACE", 220);
        _interfaceList.Columns.Add("TYPE", 64);
        _interfaceList.DrawColumnHeader += OnInterfaceListDrawColumnHeader;
        _interfaceList.DrawSubItem += OnInterfaceListDrawSubItem;
        _interfaceList.SelectedIndexChanged += OnInterfaceSelected;

        _splitContainer.Panel1.Controls.Add(_interfaceList);
        _splitContainer.Panel1.Padding = new Padding(8, 10, 4, 10);
        _splitContainer.Panel1.Resize += (_, _) => ResizeInterfaceListColumns();

        _detailPanel = new InterfaceDetailPanel();
        _splitContainer.Panel2.Controls.Add(_detailPanel);

        var statusPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = AppTheme.Surface,
            Padding = new Padding(0)
        };
        statusPanel.Paint += (_, e) =>
        {
            using var pen = new Pen(AppTheme.Border);
            e.Graphics.DrawLine(pen, 0, 0, statusPanel.Width, 0);
        };

        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"{AppBranding.ShortName} — loading…"
        };
        AppTheme.StyleStatusLabel(_statusLabel);
        statusPanel.Controls.Add(_statusLabel);

        SuspendLayout();
        Controls.Add(headerPanel);
        Controls.Add(statusPanel);
        Controls.Add(_splitContainer);
        ResumeLayout(false);

        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        Load += (_, _) => ApplySnapshotToUi();

        _fastRefreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _fastRefreshTimer.Tick += (_, _) => UpdateThroughputOnly();

        _slowRefreshTimer = new System.Windows.Forms.Timer { Interval = 10000 };
        _slowRefreshTimer.Tick += (_, _) => _services.Snapshot.EnsureFresh(TimeSpan.FromSeconds(9));

        _services.Snapshot.SnapshotUpdated += OnSnapshotUpdated;
        _services.GatewayPing.GatewayPingUpdated += OnGatewayPingUpdated;

        Shown += (_, _) =>
        {
            BeginInvoke(() =>
            {
                ConfigureSplitterLayout();
                ResizeInterfaceListColumns();
            });
            _services.PublicIp.RefreshAsync();
            ForceRefresh(includeSlowDetails: false);
            _fastRefreshTimer.Start();
            _slowRefreshTimer.Start();
        };

        Layout += (_, _) =>
        {
            if (!_splitterInitialized && _splitContainer.Width >= 480)
            {
                ConfigureSplitterLayout();
                ResizeInterfaceListColumns();
            }
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
        if (_splitContainer.Panel1.Width < 300)
        {
            _splitterInitialized = false;
        }

        ConfigureSplitterLayout();
        ResizeInterfaceListColumns();
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
        if (_splitContainer.IsDisposed || _splitContainer.Width < 480)
        {
            return;
        }

        const int panel1Min = 320;
        const int panel2Min = 260;

        var available = _splitContainer.Width - _splitContainer.SplitterWidth;
        if (available <= panel1Min + panel2Min)
        {
            return;
        }

        _splitContainer.Panel1MinSize = 0;
        _splitContainer.Panel2MinSize = 0;

        var maxLeft = available - panel2Min;
        if (!_splitterInitialized)
        {
            var preferredLeft = Math.Max(available / 2, panel1Min);
            _splitContainer.SplitterDistance = Math.Clamp(preferredLeft, panel1Min, maxLeft);
            _splitterInitialized = true;
        }
        else if (_splitContainer.SplitterDistance < panel1Min)
        {
            _splitContainer.SplitterDistance = panel1Min;
        }
        else if (_splitContainer.SplitterDistance > maxLeft)
        {
            _splitContainer.SplitterDistance = maxLeft;
        }

        _splitContainer.Panel1MinSize = panel1Min;
        _splitContainer.Panel2MinSize = panel2Min;
    }

    private void ResizeInterfaceListColumns()
    {
        if (_interfaceList.Columns.Count < 2 || _interfaceList.ClientSize.Width <= 0)
        {
            return;
        }

        const int typeWidth = 64;
        var available = _interfaceList.ClientSize.Width - 4;
        _interfaceList.Columns[1].Width = typeWidth;
        _interfaceList.Columns[0].Width = Math.Max(160, available - typeWidth);
        _interfaceList.Invalidate();
    }

    private void OnInterfaceListDrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
    {
        e.DrawDefault = false;
        using var bg = new SolidBrush(AppTheme.SurfaceRaised);
        e.Graphics.FillRectangle(bg, e.Bounds);

        using var pen = new Pen(AppTheme.Border);
        e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

        var text = e.Header?.Text ?? string.Empty;
        var rect = Rectangle.Inflate(e.Bounds, -10, 0);
        TextRenderer.DrawText(
            e.Graphics,
            text,
            AppTheme.FontCaption,
            rect,
            AppTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
    }

    private void OnInterfaceListDrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
    {
        e.DrawDefault = false;
        var selected = e.Item?.Selected == true;
        var focused = _interfaceList.Focused;
        var bgColor = selected ? AppTheme.ListSelectionBackground(focused) : AppTheme.Surface;
        var textColor = selected ? AppTheme.TextPrimary : AppTheme.TextSecondary;

        using (var bg = new SolidBrush(bgColor))
        {
            e.Graphics.FillRectangle(bg, e.Bounds);
        }

        if (selected && e.ColumnIndex == 0)
        {
            using var accent = new SolidBrush(AppTheme.SelectionBar);
            e.Graphics.FillRectangle(accent, e.Bounds.Left, e.Bounds.Top + 4, 3, e.Bounds.Height - 8);
        }

        if (e.SubItem is null)
        {
            return;
        }

        var text = e.SubItem.Text;
        var font = AppTheme.FontBody;
        var foreground = textColor;
        var isPrimary = e.Item?.Tag is string id
            && _interfacesById.TryGetValue(id, out var info)
            && info.IsPrimary;

        if (e.ColumnIndex == 0)
        {
            if (isPrimary)
            {
                font = AppTheme.FontTitle;
                foreground = AppTheme.TextPrimary;
            }
        }
        else if (e.ColumnIndex == 1)
        {
            foreground = string.Equals(text, "DHCP", StringComparison.OrdinalIgnoreCase)
                ? AppTheme.Green
                : AppTheme.Orange;
            font = AppTheme.FontCaption;
        }

        var textBounds = Rectangle.Inflate(e.Bounds, -10, 0);
        TextRenderer.DrawText(
            e.Graphics,
            text,
            font,
            textBounds,
            foreground,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
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
            _services.GatewayPing.GatewayPingUpdated -= OnGatewayPingUpdated;
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

    private void OnGatewayPingUpdated(string gateway)
    {
        RunOnUiThread(() =>
        {
            if (string.IsNullOrWhiteSpace(_selectedInterfaceId) ||
                !_interfacesById.TryGetValue(_selectedInterfaceId, out var info) ||
                !string.Equals(info.Gateway, gateway, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var updated = info with
            {
                GatewayPing = _services.GatewayPing.GetLatencyText(gateway)
            };
            _interfacesById[_selectedInterfaceId] = updated;
            _detailPanel.UpdateLiveFields(updated);
        });
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
                        var item = new ListViewItem(info.Name)
                        {
                            Tag = info.Id
                        };
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
