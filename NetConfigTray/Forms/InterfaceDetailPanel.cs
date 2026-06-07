using NetConfigTray.Helpers;
using NetConfigTray.Models;

namespace NetConfigTray.Forms;

public sealed class InterfaceDetailPanel : Panel
{
    private readonly Panel _contentPanel;
    private NetworkInterfaceInfo? _info;
    private long _downloadBps;
    private long _uploadBps;
    private ThroughputSparklineControl? _sparkline;
    private string? _boundInterfaceId;
    private string? _layoutSignature;

    public InterfaceDetailPanel()
    {
        Dock = DockStyle.Fill;
        BackColor = AppTheme.AppBackground;
        ForeColor = AppTheme.TextPrimary;
        AutoScroll = true;
        Padding = new Padding(32, 28, 32, 28);
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);

        _contentPanel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = AppTheme.AppBackground,
            Location = new Point(0, 0),
            MinimumSize = new Size(280, 0)
        };

        Controls.Add(_contentPanel);
        Resize += (_, _) => _contentPanel.Width = Math.Max(280, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 8);
    }

    public void ShowPlaceholder(string message)
    {
        _info = null;
        _sparkline = null;
        _boundInterfaceId = null;
        _layoutSignature = null;
        _contentPanel.SuspendLayout();
        try
        {
            _contentPanel.Controls.Clear();
            _contentPanel.Controls.Add(new Label
            {
                Text = message,
                ForeColor = AppTheme.TextMuted,
                Font = AppTheme.FontBody,
                AutoSize = true,
                MaximumSize = new Size(Math.Max(240, ClientSize.Width - Padding.Horizontal - 8), 0),
                Location = new Point(0, 0),
                BackColor = Color.Transparent
            });
            _contentPanel.Width = Math.Max(240, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        }
        finally
        {
            _contentPanel.ResumeLayout(true);
        }
    }

    public void Bind(
        NetworkInterfaceInfo info,
        long downloadBps,
        long uploadBps,
        IReadOnlyList<long> downloadHistory)
    {
        var layoutSignature = BuildLayoutSignature(info);
        var canUpdateInPlace = _boundInterfaceId == info.Id
            && string.Equals(_layoutSignature, layoutSignature, StringComparison.Ordinal)
            && _contentPanel.Controls.Count > 0
            && _info is not null;

        _info = info;
        _downloadBps = downloadBps;
        _uploadBps = uploadBps;
        _boundInterfaceId = info.Id;
        _layoutSignature = layoutSignature;

        if (canUpdateInPlace)
        {
            ApplyLiveValues(downloadHistory);
            return;
        }

        Rebuild(downloadHistory);
    }

    public void UpdateThroughput(long downloadBps, long uploadBps, IReadOnlyList<long> downloadHistory)
    {
        _downloadBps = downloadBps;
        _uploadBps = uploadBps;
        ApplyLiveValues(downloadHistory);
    }

    public void UpdateLiveFields(NetworkInterfaceInfo info)
    {
        _info = info;
        UpdateValue("Connection uptime", info.ConnectionUptime ?? "Unknown");
        UpdateValue("Gateway ping", info.GatewayPing ?? "—");
    }

    private void ApplyLiveValues(IReadOnlyList<long> downloadHistory)
    {
        if (_info is null)
        {
            return;
        }

        UpdateValue("Download", FormatHelper.FormatThroughput(_downloadBps));
        UpdateValue("Upload", FormatHelper.FormatThroughput(_uploadBps));
        UpdateValue("Connection uptime", _info.ConnectionUptime ?? "Unknown");
        UpdateValue("Gateway ping", _info.GatewayPing ?? "—");
        _sparkline?.SetSamples(downloadHistory);
    }

    private static string BuildLayoutSignature(NetworkInterfaceInfo info)
    {
        var deviceSignature = info.ConnectedDevice is null
            ? string.Empty
            : string.Join("|",
                info.ConnectedDevice.Role,
                info.ConnectedDevice.IpAddress,
                info.ConnectedDevice.Hostname,
                info.ConnectedDevice.MacAddress,
                info.ConnectedDevice.ExtraInfo);

        var subnetSignature = info.Subnet is null
            ? string.Empty
            : string.Join("|",
                info.Subnet.NetworkAddress,
                info.Subnet.BroadcastAddress,
                info.Subnet.FirstHost,
                info.Subnet.LastHost,
                info.Subnet.UsableHosts);

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
            subnetSignature,
            deviceSignature);
    }

    private void Rebuild(IReadOnlyList<long> downloadHistory)
    {
        if (_info is null)
        {
            return;
        }

        _contentPanel.SuspendLayout();
        try
        {
            _contentPanel.Controls.Clear();
            _sparkline = null;

            var y = 0;
            var contentWidth = Math.Max(280, _contentPanel.Width);

            AddHeader(_info.Name, _info.IsPrimary, ref y, contentWidth);
            AddBadge(_info.ConfigurationLabel, _info.ConfigurationType, ref y, contentWidth);
            y += 8;
            AddDetailRow("IP address", _info.IPv4Address, ref y, contentWidth);
            AddDetailRow("CIDR", _info.Cidr, ref y, contentWidth);
            AddDetailRow("MAC address", _info.MacAddress, ref y, contentWidth);
            AddDetailRow("Link speed", FormatHelper.FormatLinkSpeed(_info.LinkSpeedBps), ref y, contentWidth);
            AddDetailRow("Download", FormatHelper.FormatThroughput(_downloadBps), ref y, contentWidth, "Download");
            AddDetailRow("Upload", FormatHelper.FormatThroughput(_uploadBps), ref y, contentWidth, "Upload");
            AddDetailRow("Connection uptime", _info.ConnectionUptime ?? "Unknown", ref y, contentWidth);
            AddDetailRow("Gateway", string.IsNullOrWhiteSpace(_info.Gateway) ? "None" : _info.Gateway, ref y, contentWidth);
            AddDetailRow("Gateway ping", _info.GatewayPing ?? "—", ref y, contentWidth);
            AddDetailRow("Route metric", _info.RouteMetric?.ToString() ?? "Unknown", ref y, contentWidth);
            AddDetailRow("DNS servers", _info.DnsServers, ref y, contentWidth);

            if (_info.Subnet is not null)
            {
                AddSectionHeader("Subnet", ref y, contentWidth);
                AddDetailRow("Network", _info.Subnet.NetworkAddress, ref y, contentWidth);
                AddDetailRow("Broadcast", _info.Subnet.BroadcastAddress, ref y, contentWidth);
                AddDetailRow("Host range", $"{_info.Subnet.FirstHost} – {_info.Subnet.LastHost}", ref y, contentWidth);
                AddDetailRow("Usable hosts", _info.Subnet.UsableHosts.ToString(), ref y, contentWidth);
            }

            if (_info.ConfigurationType == IpConfigurationType.Dhcp)
            {
                AddSectionHeader("DHCP lease", ref y, contentWidth);
                AddDetailRow("DHCP server", _info.DhcpServer ?? "Unknown", ref y, contentWidth);
                AddDetailRow("Lease obtained", _info.DhcpLeaseObtained ?? "Unknown", ref y, contentWidth);
                AddDetailRow("Lease expires", _info.DhcpLeaseExpires ?? "Unknown", ref y, contentWidth);
            }

            if (!string.IsNullOrWhiteSpace(_info.WifiChannel))
            {
                AddSectionHeader("Wi-Fi", ref y, contentWidth);
                AddDetailRow("Channel", _info.WifiChannel!, ref y, contentWidth);
                AddDetailRow("Band", _info.WifiBand ?? "Unknown", ref y, contentWidth);
                AddDetailRow("Radio type", _info.WifiRadioType ?? "Unknown", ref y, contentWidth);
            }

            AddSectionHeader("Throughput history", ref y, contentWidth);
            _sparkline = new ThroughputSparklineControl
            {
                Width = contentWidth - 8,
                Location = new Point(0, y)
            };
            _sparkline.SetSamples(downloadHistory);
            _contentPanel.Controls.Add(_sparkline);
            y += _sparkline.Height + 12;

            if (_info.ConnectedDevice is not null)
            {
                AddSectionHeader("Connected device", ref y, contentWidth);
                AddDetailRow("Role", _info.ConnectedDevice.Role, ref y, contentWidth);

                if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.IpAddress))
                {
                    AddDetailRow("IP", _info.ConnectedDevice.IpAddress, ref y, contentWidth);
                }

                if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.Hostname))
                {
                    AddDetailRow("Hostname / SSID", _info.ConnectedDevice.Hostname, ref y, contentWidth);
                }

                if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.MacAddress))
                {
                    AddDetailRow("MAC", _info.ConnectedDevice.MacAddress, ref y, contentWidth);
                }

                if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.ExtraInfo))
                {
                    AddDetailRow("Details", _info.ConnectedDevice.ExtraInfo, ref y, contentWidth);
                }
            }

            var copyAllButton = new Button
            {
                Text = "Copy all details",
                AutoSize = true,
                Location = new Point(0, y + 4)
            };
            AppTheme.StyleAccentButton(copyAllButton);
            copyAllButton.Click += (_, _) => ClipboardHelper.CopyText(BuildCopyText());
            _contentPanel.Controls.Add(copyAllButton);

            _contentPanel.Height = y + copyAllButton.Height + 16;
            _contentPanel.Width = contentWidth;
        }
        finally
        {
            _contentPanel.ResumeLayout(true);
        }
    }

    private void AddHeader(string name, bool isPrimary, ref int y, int width)
    {
        var title = new Label
        {
            Text = isPrimary ? $"{name}  ·  Primary" : name,
            Font = AppTheme.FontHeader,
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            Location = new Point(0, y),
            MaximumSize = new Size(width - 8, 0),
            BackColor = Color.Transparent
        };
        _contentPanel.Controls.Add(title);
        y += title.Height + 8;
    }

    private void AddBadge(string text, IpConfigurationType type, ref int y, int width)
    {
        var badge = new Label
        {
            Name = "ConfigBadge",
            Text = text.ToUpperInvariant(),
            Font = AppTheme.FontCaption,
            ForeColor = AppTheme.ConfigColor(type),
            BackColor = AppTheme.ConfigBackground(type),
            AutoSize = true,
            Padding = new Padding(10, 5, 10, 5),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(0, y)
        };
        _contentPanel.Controls.Add(badge);
        y += badge.Height + 14;
    }

    private void AddSectionHeader(string text, ref int y, int width)
    {
        y += 6;
        var rule = new Panel
        {
            BackColor = AppTheme.BorderSubtle,
            Location = new Point(0, y),
            Size = new Size(width, 1)
        };
        _contentPanel.Controls.Add(rule);
        y += 10;

        var label = new Label
        {
            Text = text.ToUpperInvariant(),
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.Accent,
            AutoSize = true,
            Location = new Point(0, y),
            MaximumSize = new Size(width - 8, 0),
            BackColor = Color.Transparent
        };
        _contentPanel.Controls.Add(label);
        y += label.Height + 8;
    }

    private void AddDetailRow(string labelText, string value, ref int y, int width, string? valueKey = null)
    {
        var rowPanel = new Panel
        {
            BackColor = AppTheme.Surface,
            Location = new Point(0, y),
            Size = new Size(width, 58),
            Padding = new Padding(14, 10, 14, 10)
        };

        var label = new Label
        {
            Text = labelText.ToUpperInvariant(),
            Font = AppTheme.FontCaption,
            ForeColor = AppTheme.TextMuted,
            AutoSize = true,
            Location = new Point(14, 10),
            BackColor = Color.Transparent
        };

        var valueLabel = new Label
        {
            Name = valueKey is null ? null : $"Value_{valueKey}",
            Tag = labelText,
            Text = value,
            Font = AppTheme.ValueFont,
            ForeColor = AppTheme.TextPrimary,
            AutoSize = true,
            MaximumSize = new Size(width - 120, 0),
            Location = new Point(14, 30),
            BackColor = Color.Transparent
        };

        var copyButton = new Button
        {
            Text = "Copy",
            Size = new Size(64, 28),
            Location = new Point(width - 78, 15)
        };
        AppTheme.StyleGhostButton(copyButton);
        copyButton.Click += (_, _) => ClipboardHelper.CopyText(value);

        rowPanel.Controls.Add(label);
        rowPanel.Controls.Add(valueLabel);
        rowPanel.Controls.Add(copyButton);
        _contentPanel.Controls.Add(rowPanel);

        y += rowPanel.Height + 8;
    }

    private void UpdateValue(string labelText, string value)
    {
        foreach (Control control in _contentPanel.Controls)
        {
            if (control is not Panel rowPanel)
            {
                continue;
            }

            foreach (Control child in rowPanel.Controls)
            {
                if (child is Label { Tag: string tag } label && tag == labelText)
                {
                    label.Text = value;
                    return;
                }
            }
        }
    }

    private string BuildCopyText()
    {
        if (_info is null)
        {
            return string.Empty;
        }

        var lines = new List<string>
        {
            $"Interface: {_info.Name}",
            $"IP: {_info.IPv4Address}",
            $"Config: {_info.ConfigurationLabel}",
            $"CIDR: {_info.Cidr}",
            $"MAC: {_info.MacAddress}",
            $"Link speed: {FormatHelper.FormatLinkSpeed(_info.LinkSpeedBps)}",
            $"Download: {FormatHelper.FormatThroughput(_downloadBps)}",
            $"Upload: {FormatHelper.FormatThroughput(_uploadBps)}",
            $"Connection uptime: {_info.ConnectionUptime}",
            $"Gateway: {_info.Gateway}",
            $"Gateway ping: {_info.GatewayPing}",
            $"Route metric: {_info.RouteMetric}",
            $"DNS: {_info.DnsServers}"
        };

        if (_info.Subnet is not null)
        {
            lines.Add($"Network: {_info.Subnet.NetworkAddress}");
            lines.Add($"Broadcast: {_info.Subnet.BroadcastAddress}");
            lines.Add($"Host range: {_info.Subnet.FirstHost} – {_info.Subnet.LastHost}");
            lines.Add($"Usable hosts: {_info.Subnet.UsableHosts}");
        }

        if (_info.ConfigurationType == IpConfigurationType.Dhcp)
        {
            lines.Add($"DHCP server: {_info.DhcpServer}");
            lines.Add($"Lease obtained: {_info.DhcpLeaseObtained}");
            lines.Add($"Lease expires: {_info.DhcpLeaseExpires}");
        }

        if (!string.IsNullOrWhiteSpace(_info.WifiChannel))
        {
            lines.Add($"Wi-Fi channel: {_info.WifiChannel}");
            lines.Add($"Wi-Fi band: {_info.WifiBand}");
            lines.Add($"Wi-Fi radio: {_info.WifiRadioType}");
        }

        if (_info.ConnectedDevice is not null)
        {
            lines.Add($"Connected device: {_info.ConnectedDevice.Role}");
            if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.IpAddress))
            {
                lines.Add($"Device IP: {_info.ConnectedDevice.IpAddress}");
            }
            if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.Hostname))
            {
                lines.Add($"Device hostname/SSID: {_info.ConnectedDevice.Hostname}");
            }
            if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.MacAddress))
            {
                lines.Add($"Device MAC: {_info.ConnectedDevice.MacAddress}");
            }
            if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.ExtraInfo))
            {
                lines.Add($"Device details: {_info.ConnectedDevice.ExtraInfo}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
