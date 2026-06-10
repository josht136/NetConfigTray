using System.Net;
using System.Net.NetworkInformation;
using NetConfigTray.Helpers;
using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

/// <summary>
/// AngryIP-style LAN sweep over an interface's subnet: pings each host in range and lists
/// the responsive ones with latency, reverse-DNS hostname, and ARP-resolved MAC.
/// </summary>
public sealed class LanScanForm : Form
{
    private const int MaxScanHosts = 1024;
    private const int Concurrency = 64;
    private const int PingTimeoutMs = 600;

    private readonly AppServices? _services;
    private readonly NetworkInterfaceInfo _interface;
    private readonly ListView _results;
    private readonly ProgressBar _progress;
    private readonly Label _statusLabel;
    private readonly Button _scanButton;
    private readonly Button _copyButton;
    private readonly ContextMenuStrip _hostMenu = new();

    private CancellationTokenSource? _cts;
    private bool _scanning;
    private int _scanned;
    private int _alive;
    private int _total;

    public LanScanForm(NetworkInterfaceInfo interfaceInfo)
        : this(interfaceInfo, null)
    {
    }

    public LanScanForm(NetworkInterfaceInfo interfaceInfo, AppServices? services)
    {
        _interface = interfaceInfo;
        _services = services;

        Text = $"{AppBranding.ShortName} — LAN scan · {interfaceInfo.Name}";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 520);
        MinimumSize = new Size(520, 320);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        var header = new Panel { Dock = DockStyle.Top, Height = 52 };
        AppTheme.StyleHeaderPanel(header);
        var headerLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = interfaceInfo.Subnet is null
                ? "No subnet information available"
                : $"Scanning {interfaceInfo.Subnet.NetworkAddress} ({interfaceInfo.Subnet.UsableHosts} usable hosts)",
            Font = AppTheme.FontSection,
            ForeColor = AppTheme.TextPrimary,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(4, 0, 0, 0)
        };
        header.Controls.Add(headerLabel);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = AppTheme.Surface };

        _scanButton = new Button { Text = "Start scan", Size = new Size(110, 30), Location = new Point(12, 7) };
        AppTheme.StyleAccentButton(_scanButton);
        _scanButton.Click += (_, _) => OnScanButtonClicked();

        _copyButton = new Button { Text = "Copy results", Size = new Size(110, 30), Location = new Point(130, 7) };
        AppTheme.StyleGhostButton(_copyButton);
        _copyButton.Click += (_, _) => CopyResults();

        _progress = new ProgressBar
        {
            Location = new Point(252, 12),
            Size = new Size(200, 20),
            Style = ProgressBarStyle.Continuous
        };

        _statusLabel = new Label
        {
            Location = new Point(466, 7),
            Size = new Size(280, 30),
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Ready"
        };

        toolbar.Controls.Add(_scanButton);
        toolbar.Controls.Add(_copyButton);
        toolbar.Controls.Add(_progress);
        toolbar.Controls.Add(_statusLabel);

        _results = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HideSelection = false
        };
        AppTheme.StyleListView(_results);
        _results.Columns.Add("IP ADDRESS", 160);
        _results.Columns.Add("PING", 90);
        _results.Columns.Add("HOSTNAME", 300);
        _results.Columns.Add("MAC", 180);
        _results.MouseClick += OnResultsMouseClick;

        Controls.Add(_results);
        Controls.Add(toolbar);
        Controls.Add(header);

        FormClosing += (_, _) => _cts?.Cancel();
        Shown += (_, _) => BeginScan();
    }

    private void OnResultsMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right)
        {
            return;
        }

        var hit = _results.HitTest(e.Location);
        if (hit.Item is null)
        {
            return;
        }

        hit.Item.Selected = true;
        var ip = hit.Item.Text;

        _hostMenu.Items.Clear();
        _hostMenu.Items.Add(new ToolStripMenuItem($"Scan ports on {ip}…", null, (_, _) =>
        {
            if (_services is not null)
            {
                new PortScanForm(_services, ip).Show(this);
            }
        }) { Enabled = _services is not null });
        _hostMenu.Items.Add(new ToolStripMenuItem("Copy IP address", null, (_, _) => ClipboardHelper.CopyText(ip)));
        _hostMenu.Show(_results, e.Location);
    }

    private void OnScanButtonClicked()
    {
        if (_scanning)
        {
            _cts?.Cancel();
            return;
        }

        BeginScan();
    }

    private void BeginScan()
    {
        if (_scanning)
        {
            return;
        }

        if (_interface.Subnet is null)
        {
            _statusLabel.Text = "No subnet to scan.";
            return;
        }

        var hosts = SubnetCalculatorHelper.EnumerateHostRange(
            _interface.Subnet.FirstHost,
            _interface.Subnet.LastHost,
            MaxScanHosts);

        if (hosts.Count == 0)
        {
            _statusLabel.Text = "No hosts in range.";
            return;
        }

        if (_interface.Subnet.UsableHosts > MaxScanHosts)
        {
            var proceed = MessageBox.Show(
                this,
                $"This network has {_interface.Subnet.UsableHosts:N0} usable hosts. " +
                $"Scanning will be limited to the first {MaxScanHosts:N0} addresses " +
                $"(starting at {_interface.Subnet.FirstHost}).\n\nContinue?",
                "Large network",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (proceed != DialogResult.Yes)
            {
                return;
            }
        }

        _results.Items.Clear();
        _scanned = 0;
        _alive = 0;
        _total = hosts.Count;
        _progress.Value = 0;
        _progress.Maximum = _total;
        _scanning = true;
        _scanButton.Text = "Stop";
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(() => RunScan(hosts, token));
    }

    private async Task RunScan(IReadOnlyList<string> hosts, CancellationToken token)
    {
        try
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Concurrency,
                CancellationToken = token
            };

            await Parallel.ForEachAsync(hosts, options, async (host, ct) =>
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(host, PingTimeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        var latency = reply.RoundtripTime <= 0 ? "<1 ms" : $"{reply.RoundtripTime} ms";
                        var hostname = IPAddress.TryParse(host, out var ip)
                            ? ArpHelper.ResolveHostname(ip, TimeSpan.FromMilliseconds(500))
                            : null;
                        var mac = IPAddress.TryParse(host, out var ipForMac)
                            ? ArpHelper.ResolveMacAddress(ipForMac)
                            : null;

                        AddResult(host, latency, hostname, mac);
                    }
                }
                catch
                {
                    // Host unreachable or ping failed.
                }
                finally
                {
                    MarkScanned();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Stopped by user.
        }
        finally
        {
            RunOnUi(FinishScan);
        }
    }

    private void AddResult(string ip, string latency, string? hostname, string? mac)
    {
        Interlocked.Increment(ref _alive);
        RunOnUi(() =>
        {
            var item = new ListViewItem(ip);
            item.SubItems.Add(latency);
            item.SubItems.Add(string.IsNullOrWhiteSpace(hostname) ? "—" : hostname);
            item.SubItems.Add(string.IsNullOrWhiteSpace(mac) ? "—" : mac);
            item.Tag = IpSortKey(ip);
            InsertSorted(item);
        });
    }

    private void InsertSorted(ListViewItem item)
    {
        var key = (uint)(item.Tag ?? 0u);
        var index = 0;
        while (index < _results.Items.Count && (uint)(_results.Items[index].Tag ?? 0u) < key)
        {
            index++;
        }

        _results.Items.Insert(index, item);
    }

    private void MarkScanned()
    {
        var done = Interlocked.Increment(ref _scanned);
        if (done % 16 == 0 || done == _total)
        {
            RunOnUi(UpdateProgress);
        }
    }

    private void UpdateProgress()
    {
        _progress.Value = Math.Min(_scanned, _progress.Maximum);
        _statusLabel.Text = $"{_scanned}/{_total} scanned · {_alive} alive";
    }

    private void FinishScan()
    {
        _scanning = false;
        _scanButton.Text = "Rescan";
        _progress.Value = _progress.Maximum;
        _statusLabel.Text = $"Done · {_scanned}/{_total} scanned · {_alive} alive";
    }

    private void CopyResults()
    {
        if (_results.Items.Count == 0)
        {
            return;
        }

        var lines = new List<string> { "IP\tPing\tHostname\tMAC" };
        foreach (ListViewItem item in _results.Items)
        {
            lines.Add(string.Join("\t", item.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text)));
        }

        ClipboardHelper.CopyText(string.Join(Environment.NewLine, lines));
    }

    private static uint IpSortKey(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address))
        {
            return 0;
        }

        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private void RunOnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // Window closing.
            }

            return;
        }

        action();
    }
}
