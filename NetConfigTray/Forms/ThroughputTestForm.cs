using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

/// <summary>
/// iperf3 throughput tester. Runs as a client (to an iperf3 server) or as a local server, showing
/// a live Mbps chart and the parsed summary. Requires a bundled or PATH-resolved iperf3.exe.
/// </summary>
public sealed class ThroughputTestForm : Form
{
    private readonly AppServices _services;
    private readonly RadioButton _clientRadio = new() { Text = "Client", Checked = true };
    private readonly RadioButton _serverRadio = new() { Text = "Server" };
    private readonly TextBox _hostBox = new();
    private readonly NumericUpDown _portBox = new();
    private readonly NumericUpDown _durationBox = new();
    private readonly NumericUpDown _streamsBox = new();
    private readonly CheckBox _reverseCheck = new() { Text = "Reverse (-R)" };
    private readonly CheckBox _udpCheck = new() { Text = "UDP" };
    private Button _startButton = null!;
    private readonly Label _statusLabel;
    private readonly TextBox _output = new();
    private readonly TimeSeriesChartControl _chart;
    private readonly List<double> _samples = new();

    private CancellationTokenSource? _cts;
    private bool _running;

    public ThroughputTestForm(AppServices services)
    {
        _services = services;

        Text = $"{AppBranding.ShortName} — Throughput test (iperf3)";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(840, 600);
        MinimumSize = new Size(620, 460);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = AppTheme.Surface };
        BuildToolbar(toolbar);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = AppTheme.Surface };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Padding = new Padding(12, 0, 0, 0)
        };
        statusPanel.Controls.Add(_statusLabel);

        _chart = new TimeSeriesChartControl
        {
            Dock = DockStyle.Top,
            Height = 200,
            ValueFormatter = v => $"{v:0.#} Mbps"
        };

        _output.Multiline = true;
        _output.ReadOnly = true;
        _output.Dock = DockStyle.Fill;
        _output.ScrollBars = ScrollBars.Both;
        _output.WordWrap = false;
        _output.BackColor = AppTheme.AppBackground;
        _output.ForeColor = AppTheme.TextPrimary;
        _output.Font = AppTheme.ValueFont;
        _output.BorderStyle = BorderStyle.None;

        Controls.Add(_output);
        Controls.Add(_chart);
        Controls.Add(statusPanel);
        Controls.Add(toolbar);

        FormClosing += (_, _) => _cts?.Cancel();

        UpdateModeState();
        CheckAvailability();
    }

    private void BuildToolbar(Panel toolbar)
    {
        _clientRadio.SetBounds(12, 8, 70, 22);
        _serverRadio.SetBounds(86, 8, 70, 22);
        foreach (var radio in new[] { _clientRadio, _serverRadio })
        {
            radio.ForeColor = AppTheme.TextSecondary;
            radio.BackColor = Color.Transparent;
            radio.CheckedChanged += (_, _) => UpdateModeState();
            toolbar.Controls.Add(radio);
        }

        AddLabel(toolbar, "Host", 12, 40);
        _hostBox.SetBounds(54, 36, 180, 24);
        StyleInput(_hostBox);

        AddLabel(toolbar, "Port", 246, 40);
        ConfigureNumeric(_portBox, 1, 65535, 5201);
        _portBox.SetBounds(284, 36, 70, 24);

        AddLabel(toolbar, "Time", 366, 40);
        ConfigureNumeric(_durationBox, 1, 3600, 10);
        _durationBox.SetBounds(404, 36, 60, 24);

        AddLabel(toolbar, "Streams", 476, 40);
        ConfigureNumeric(_streamsBox, 1, 128, 1);
        _streamsBox.SetBounds(534, 36, 60, 24);

        _reverseCheck.SetBounds(606, 38, 110, 22);
        _udpCheck.SetBounds(606, 8, 80, 22);
        foreach (var check in new[] { _reverseCheck, _udpCheck })
        {
            check.ForeColor = AppTheme.TextSecondary;
            check.BackColor = Color.Transparent;
            toolbar.Controls.Add(check);
        }

        _startButton = new Button { Text = "Start", Size = new Size(90, 30), Location = new Point(700, 8) };
        AppTheme.StyleAccentButton(_startButton);
        _startButton.Click += (_, _) => ToggleRun();
        toolbar.Controls.Add(_startButton);

        var prefillHost = _services.Snapshot.GetPrimaryInterface()?.Gateway;
        _hostBox.Text = string.IsNullOrWhiteSpace(prefillHost) ? string.Empty : prefillHost;
    }

    private void CheckAvailability()
    {
        if (IperfService.IsAvailable)
        {
            _statusLabel.Text = "Ready. Start an iperf3 server on the far end, then run as client.";
            return;
        }

        _startButton.Enabled = false;
        _statusLabel.Text = "iperf3.exe not found in tools\\iperf3\\ or PATH.";
        var result = MessageBox.Show(
            this,
            "iperf3.exe was not found.\n\nPlace it in the app's tools\\iperf3\\ folder (or on PATH). " +
            "Open the iperf3 download page now?",
            $"{AppBranding.ShortName} — iperf3 missing",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(IperfService.DownloadUrl)
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                // Browser unavailable.
            }
        }
    }

    private void UpdateModeState()
    {
        var client = _clientRadio.Checked;
        _hostBox.Enabled = client;
        _durationBox.Enabled = client;
        _streamsBox.Enabled = client;
        _reverseCheck.Enabled = client;
        _udpCheck.Enabled = client;
    }

    private void ToggleRun()
    {
        if (_running)
        {
            _cts?.Cancel();
            return;
        }

        var exe = IperfService.ResolveExecutable();
        if (exe is null)
        {
            CheckAvailability();
            return;
        }

        string args;
        if (_clientRadio.Checked)
        {
            if (string.IsNullOrWhiteSpace(_hostBox.Text))
            {
                _statusLabel.Text = "Enter the iperf3 server host.";
                return;
            }

            args = IperfService.BuildClientArguments(
                _hostBox.Text.Trim(),
                (int)_portBox.Value,
                (int)_durationBox.Value,
                (int)_streamsBox.Value,
                _reverseCheck.Checked,
                _udpCheck.Checked,
                udpBandwidthMbps: 100);
        }
        else
        {
            args = IperfService.BuildServerArguments((int)_portBox.Value);
        }

        _output.Clear();
        _samples.Clear();
        _chart.Update("Throughput", Array.Empty<TimeSeriesChartControl.Series>());
        _running = true;
        _startButton.Text = "Stop";
        _statusLabel.Text = $"Running: iperf3 {args}";
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(() =>
        {
            var exit = CommandRunner.RunStreaming(exe, args, OnLine, token);
            RunOnUi(() =>
            {
                _running = false;
                _startButton.Text = "Start";
                _statusLabel.Text = token.IsCancellationRequested
                    ? "Stopped."
                    : $"Finished (exit code {exit}).";
            });
        });
    }

    private void OnLine(string line)
    {
        RunOnUi(() =>
        {
            _output.AppendText(line + Environment.NewLine);

            if (IperfService.IsSummaryLine(line))
            {
                _statusLabel.Text = "Summary: " + line.Trim();
            }
            else if (IperfService.TryParseIntervalMbps(line, out var mbps))
            {
                _samples.Add(mbps);
                if (_samples.Count > 240)
                {
                    _samples.RemoveAt(0);
                }

                _chart.Update("Throughput", new[]
                {
                    new TimeSeriesChartControl.Series("Mbps", AppTheme.Cyan, _samples.ToArray(), mbps, Fill: true)
                });
            }
        });
    }

    private static void ConfigureNumeric(NumericUpDown box, int min, int max, int value)
    {
        box.Minimum = min;
        box.Maximum = max;
        box.Value = value;
        box.BackColor = AppTheme.SurfaceRaised;
        box.ForeColor = AppTheme.TextPrimary;
        box.BorderStyle = BorderStyle.FixedSingle;
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
