using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

/// <summary>
/// TCP connect port scanner with service names and optional banner grab. Can be opened standalone
/// or prefilled with a host (e.g. from a LAN scan result).
/// </summary>
public sealed class PortScanForm : Form
{
    private readonly AppServices _services;
    private readonly PortScanService _scanner = new();
    private readonly TextBox _hostBox = new();
    private readonly TextBox _portsBox = new();
    private readonly CheckBox _bannerCheck = new();
    private readonly Button _scanButton;
    private readonly ProgressBar _progress = new();
    private readonly Label _statusLabel;
    private readonly ListView _results;

    private CancellationTokenSource? _cts;
    private bool _scanning;
    private int _done;
    private int _total;
    private int _open;

    public PortScanForm(AppServices services)
        : this(services, null)
    {
    }

    public PortScanForm(AppServices services, string? targetHost)
    {
        _services = services;

        Text = $"{AppBranding.ShortName} — Port scan";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 520);
        MinimumSize = new Size(560, 340);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = AppTheme.Surface };

        AddLabel(toolbar, "Host", 12, 12);
        _hostBox.SetBounds(60, 8, 220, 24);
        StyleInput(_hostBox);
        _hostBox.Text = targetHost ?? string.Empty;

        AddLabel(toolbar, "Ports", 12, 46);
        _portsBox.SetBounds(60, 42, 360, 24);
        StyleInput(_portsBox);
        _portsBox.Text = string.Join(",", WellKnownPorts.CommonPorts);

        _bannerCheck.Text = "Grab banners";
        _bannerCheck.SetBounds(300, 10, 130, 22);
        _bannerCheck.Checked = true;
        _bannerCheck.ForeColor = AppTheme.TextSecondary;
        _bannerCheck.BackColor = Color.Transparent;

        var commonButton = new Button { Text = "Common", Size = new Size(80, 24), Location = new Point(440, 42) };
        AppTheme.StyleGhostButton(commonButton);
        commonButton.Click += (_, _) => _portsBox.Text = string.Join(",", WellKnownPorts.CommonPorts);

        _scanButton = new Button { Text = "Scan", Size = new Size(90, 30), Location = new Point(440, 7) };
        AppTheme.StyleAccentButton(_scanButton);
        _scanButton.Click += (_, _) => ToggleScan();

        _progress.SetBounds(540, 12, 200, 20);
        _progress.Style = ProgressBarStyle.Continuous;

        toolbar.Controls.Add(_hostBox);
        toolbar.Controls.Add(_portsBox);
        toolbar.Controls.Add(_bannerCheck);
        toolbar.Controls.Add(commonButton);
        toolbar.Controls.Add(_scanButton);
        toolbar.Controls.Add(_progress);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = AppTheme.Surface };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Padding = new Padding(12, 0, 0, 0),
            Text = "Enter a host and click Scan."
        };
        statusPanel.Controls.Add(_statusLabel);

        _results = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false
        };
        AppTheme.StyleListView(_results);
        _results.Columns.Add("PORT", 80);
        _results.Columns.Add("STATE", 80);
        _results.Columns.Add("SERVICE", 140);
        _results.Columns.Add("BANNER", 420);

        Controls.Add(_results);
        Controls.Add(statusPanel);
        Controls.Add(toolbar);

        FormClosing += (_, _) => _cts?.Cancel();
    }

    private void ToggleScan()
    {
        if (_scanning)
        {
            _cts?.Cancel();
            return;
        }

        var host = _hostBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            _statusLabel.Text = "Enter a host or IP.";
            return;
        }

        var ports = PortScanService.ParsePorts(_portsBox.Text);
        if (ports.Count == 0)
        {
            _statusLabel.Text = "No valid ports in the list.";
            return;
        }

        _results.Items.Clear();
        _done = 0;
        _open = 0;
        _total = ports.Count;
        _progress.Value = 0;
        _progress.Maximum = _total;
        _scanning = true;
        _scanButton.Text = "Stop";
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var grabBanner = _bannerCheck.Checked;

        _statusLabel.Text = $"Scanning {ports.Count} ports on {host}…";

        Task.Run(async () =>
        {
            try
            {
                await _scanner.ScanAsync(host, ports, grabBanner, OnOpenPort, OnProgress, token);
            }
            catch (OperationCanceledException)
            {
                // Stopped.
            }
            finally
            {
                RunOnUi(FinishScan);
            }
        });
    }

    private void OnOpenPort(PortScanService.OpenPort open)
    {
        Interlocked.Increment(ref _open);
        RunOnUi(() =>
        {
            var item = new ListViewItem(open.Port.ToString());
            item.SubItems.Add("open");
            item.SubItems.Add(open.Service);
            item.SubItems.Add(open.Banner ?? "—");
            item.UseItemStyleForSubItems = false;
            item.SubItems[1].ForeColor = AppTheme.Green;

            var index = 0;
            while (index < _results.Items.Count &&
                   int.Parse(_results.Items[index].Text) < open.Port)
            {
                index++;
            }

            _results.Items.Insert(index, item);
        });
    }

    private void OnProgress()
    {
        var done = Interlocked.Increment(ref _done);
        if (done % 8 == 0 || done == _total)
        {
            RunOnUi(() =>
            {
                _progress.Value = Math.Min(done, _progress.Maximum);
                _statusLabel.Text = $"{done}/{_total} scanned · {_open} open";
            });
        }
    }

    private void FinishScan()
    {
        _scanning = false;
        _scanButton.Text = "Scan";
        _progress.Value = _progress.Maximum;
        _statusLabel.Text = $"Done · {_done}/{_total} scanned · {_open} open";
    }

    private static void AddLabel(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Location = new Point(x, y),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent
        });
    }

    private static void StyleInput(TextBox box)
    {
        box.BackColor = AppTheme.SurfaceRaised;
        box.ForeColor = AppTheme.TextPrimary;
        box.BorderStyle = BorderStyle.FixedSingle;
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
                // Closing.
            }

            return;
        }

        action();
    }
}
