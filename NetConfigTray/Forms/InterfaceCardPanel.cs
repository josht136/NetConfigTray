using NetConfigTray.Helpers;
using NetConfigTray.Models;

namespace NetConfigTray.Forms;

public sealed class InterfaceCardPanel : Panel
{
    private static readonly Color DhcpColor = Color.FromArgb(16, 124, 16);
    private static readonly Color StaticColor = Color.FromArgb(202, 80, 16);
    private static readonly Color DhcpBackground = Color.FromArgb(223, 246, 221);
    private static readonly Color StaticBackground = Color.FromArgb(255, 236, 224);

    private readonly Panel _summaryPanel;
    private readonly Panel _detailsPanel;
    private readonly Label _nameLabel;
    private readonly Label _ipLabel;
    private readonly Label _badge;
    private readonly Label _hintLabel;
    private NetworkInterfaceInfo? _info;
    private long _downloadBps;
    private long _uploadBps;

    public event EventHandler? ExpandedChanged;

    public bool IsExpanded { get; private set; }

    public InterfaceCardPanel(int width)
    {
        Width = width;
        BackColor = Color.FromArgb(248, 248, 248);
        Margin = new Padding(0, 0, 0, 8);
        Padding = new Padding(12, 10, 12, 10);
        Cursor = Cursors.Hand;

        _summaryPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.Transparent
        };

        _nameLabel = new Label
        {
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(0, 0)
        };

        _ipLabel = new Label
        {
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(50, 50, 50),
            AutoSize = true,
            Location = new Point(0, 24)
        };

        _badge = new Label
        {
            Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
            AutoSize = false,
            Size = new Size(56, 22),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        _hintLabel = new Label
        {
            Text = "Click for details",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(0, 40)
        };

        _summaryPanel.Controls.Add(_nameLabel);
        _summaryPanel.Controls.Add(_ipLabel);
        _summaryPanel.Controls.Add(_badge);
        _summaryPanel.Controls.Add(_hintLabel);

        _detailsPanel = new Panel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Visible = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 8, 0, 0)
        };

        Controls.Add(_detailsPanel);
        Controls.Add(_summaryPanel);

        Click += (_, _) => ToggleExpanded();
        _summaryPanel.Click += (_, _) => ToggleExpanded();
        _nameLabel.Click += (_, _) => ToggleExpanded();
        _ipLabel.Click += (_, _) => ToggleExpanded();
        _hintLabel.Click += (_, _) => ToggleExpanded();

        Resize += (_, _) => _badge.Location = new Point(Width - 80, 0);
    }

    public void Bind(
        NetworkInterfaceInfo info,
        long downloadBps,
        long uploadBps,
        IReadOnlyList<long> downloadHistory,
        bool expanded)
    {
        var detailsChanged = _info is null ||
            _info.Id != info.Id ||
            _info.IPv4Address != info.IPv4Address ||
            _info.ConfigurationType != info.ConfigurationType ||
            _info.Gateway != info.Gateway ||
            _info.ConnectedDevice?.MacAddress != info.ConnectedDevice?.MacAddress ||
            _info.ConnectedDevice?.Hostname != info.ConnectedDevice?.Hostname;

        _info = info;
        _downloadBps = downloadBps;
        _uploadBps = uploadBps;

        var isDhcp = info.ConfigurationType == IpConfigurationType.Dhcp;
        _nameLabel.Text = info.IsPrimary ? $"{info.Name} (Primary)" : info.Name;
        _ipLabel.Text = info.IPv4Address;
        _badge.Text = info.ConfigurationLabel;
        _badge.ForeColor = isDhcp ? DhcpColor : StaticColor;
        _badge.BackColor = isDhcp ? DhcpBackground : StaticBackground;
        _badge.Location = new Point(Width - 80, 0);

        if (expanded != IsExpanded)
        {
            IsExpanded = expanded;
            _detailsPanel.Visible = expanded;
            _hintLabel.Text = expanded ? "Click to collapse" : "Click for details";
            detailsChanged = true;
        }

        if (detailsChanged)
        {
            RebuildDetails(downloadHistory);
        }
        else if (IsExpanded)
        {
            UpdateThroughput(downloadBps, uploadBps, downloadHistory);
        }

        UpdateHeight();
    }

    public void UpdateThroughput(long downloadBps, long uploadBps, IReadOnlyList<long> downloadHistory)
    {
        _downloadBps = downloadBps;
        _uploadBps = uploadBps;

        if (!IsExpanded || _info is null)
        {
            return;
        }

        UpdateDetailValue("Download", FormatHelper.FormatThroughput(_downloadBps));
        UpdateDetailValue("Upload", FormatHelper.FormatThroughput(_uploadBps));

        foreach (Control control in _detailsPanel.Controls)
        {
            if (control is ThroughputSparklineControl sparkline)
            {
                sparkline.SetSamples(downloadHistory);
                break;
            }
        }
    }

    private void UpdateDetailValue(string labelText, string value)
    {
        foreach (Control control in _detailsPanel.Controls)
        {
            if (control is Label label &&
                (label.Font.Style & FontStyle.Bold) != 0 &&
                string.Equals(label.Text, labelText, StringComparison.Ordinal))
            {
                var valueLabel = _detailsPanel.Controls
                    .OfType<Label>()
                    .FirstOrDefault(l => l.Location.Y == label.Location.Y + 14);

                if (valueLabel is not null)
                {
                    valueLabel.Text = value;
                }

                break;
            }
        }
    }

    public void SetExpanded(bool expanded)
    {
        if (IsExpanded == expanded)
        {
            return;
        }

        IsExpanded = expanded;
        _detailsPanel.Visible = expanded;
        _hintLabel.Text = expanded ? "Click to collapse" : "Click for details";
        UpdateHeight();
        ExpandedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ToggleExpanded()
    {
        SetExpanded(!IsExpanded);
    }

    private void RebuildDetails(IReadOnlyList<long> downloadHistory)
    {
        if (_info is null)
        {
            return;
        }

        _detailsPanel.Controls.Clear();
        var y = 0;

        AddDetailRow("CIDR", _info.Cidr, ref y);
        AddDetailRow("MAC address", _info.MacAddress, ref y);
        AddDetailRow("Link speed", FormatHelper.FormatLinkSpeed(_info.LinkSpeedBps), ref y);
        AddDetailRow("Download", FormatHelper.FormatThroughput(_downloadBps), ref y);
        AddDetailRow("Upload", FormatHelper.FormatThroughput(_uploadBps), ref y);
        AddDetailRow("Connection uptime", _info.ConnectionUptime ?? "Unknown", ref y);
        AddDetailRow("Gateway", string.IsNullOrWhiteSpace(_info.Gateway) ? "None" : _info.Gateway, ref y);
        AddDetailRow("Gateway ping", _info.GatewayPing ?? "—", ref y);
        AddDetailRow("Route metric", _info.RouteMetric?.ToString() ?? "Unknown", ref y);
        AddDetailRow("DNS servers", _info.DnsServers, ref y);

        if (_info.Subnet is not null)
        {
            AddSectionHeader("Subnet", ref y);
            AddDetailRow("Network", _info.Subnet.NetworkAddress, ref y);
            AddDetailRow("Broadcast", _info.Subnet.BroadcastAddress, ref y);
            AddDetailRow("Host range", $"{_info.Subnet.FirstHost} – {_info.Subnet.LastHost}", ref y);
            AddDetailRow("Usable hosts", _info.Subnet.UsableHosts.ToString(), ref y);
        }

        if (_info.ConfigurationType == IpConfigurationType.Dhcp)
        {
            AddSectionHeader("DHCP lease", ref y);
            AddDetailRow("DHCP server", _info.DhcpServer ?? "Unknown", ref y);
            AddDetailRow("Lease obtained", _info.DhcpLeaseObtained ?? "Unknown", ref y);
            AddDetailRow("Lease expires", _info.DhcpLeaseExpires ?? "Unknown", ref y);
        }

        if (!string.IsNullOrWhiteSpace(_info.WifiChannel))
        {
            AddSectionHeader("Wi-Fi", ref y);
            AddDetailRow("Channel", _info.WifiChannel!, ref y);
            AddDetailRow("Band", _info.WifiBand ?? "Unknown", ref y);
            AddDetailRow("Radio type", _info.WifiRadioType ?? "Unknown", ref y);
        }

        AddSectionHeader("Throughput history", ref y);
        var sparkline = new ThroughputSparklineControl
        {
            Width = Width - 24,
            Location = new Point(0, y)
        };
        sparkline.SetSamples(downloadHistory);
        _detailsPanel.Controls.Add(sparkline);
        y += sparkline.Height + 8;

        if (_info.ConnectedDevice is not null)
        {
            AddSectionHeader("Connected device", ref y);
            AddDetailRow("Role", _info.ConnectedDevice.Role, ref y);

            if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.IpAddress))
            {
                AddDetailRow("IP", _info.ConnectedDevice.IpAddress, ref y);
            }

            if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.Hostname))
            {
                AddDetailRow("Hostname / SSID", _info.ConnectedDevice.Hostname, ref y);
            }

            if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.MacAddress))
            {
                AddDetailRow("MAC", _info.ConnectedDevice.MacAddress, ref y);
            }

            if (!string.IsNullOrWhiteSpace(_info.ConnectedDevice.ExtraInfo))
            {
                AddDetailRow("Details", _info.ConnectedDevice.ExtraInfo, ref y);
            }
        }

        var copyAllButton = new Button
        {
            Text = "Copy all details",
            AutoSize = true,
            FlatStyle = FlatStyle.System,
            Location = new Point(0, y + 4)
        };
        copyAllButton.Click += (_, _) => ClipboardHelper.CopyText(BuildCopyText());
        _detailsPanel.Controls.Add(copyAllButton);
        y += copyAllButton.Height + 8;

        _detailsPanel.Height = y;
    }

    private void AddSectionHeader(string text, ref int y)
    {
        var label = new Label
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 102, 204),
            AutoSize = true,
            Location = new Point(0, y)
        };
        _detailsPanel.Controls.Add(label);
        y += label.Height + 4;
    }

    private void AddDetailRow(string labelText, string value, ref int y)
    {
        var label = new Label
        {
            Text = labelText,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(0, y)
        };

        var valueLabel = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 9F),
            AutoSize = true,
            MaximumSize = new Size(Width - 90, 0),
            Location = new Point(0, y + 14)
        };

        var copyButton = new Button
        {
            Text = "Copy",
            Size = new Size(52, 22),
            FlatStyle = FlatStyle.System,
            Location = new Point(Width - 74, y + 10)
        };
        copyButton.Click += (_, _) => ClipboardHelper.CopyText(value);

        _detailsPanel.Controls.Add(label);
        _detailsPanel.Controls.Add(valueLabel);
        _detailsPanel.Controls.Add(copyButton);

        y += Math.Max(valueLabel.Height + 18, 34);
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

    private void UpdateHeight()
    {
        Height = _summaryPanel.Height + (IsExpanded ? _detailsPanel.Height : 0) + Padding.Vertical;
    }
}
