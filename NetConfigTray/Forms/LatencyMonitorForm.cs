using NetConfigTray.Helpers;
using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

/// <summary>
/// PingPlotter/MTR-style latency monitor: continuous ping to a host (optionally per-hop) showing
/// min/avg/max/jitter/loss, a live chart of the destination latency, and CSV export.
/// </summary>
public sealed class LatencyMonitorForm : Form
{
    private readonly AppServices _services;
    private readonly LatencyMonitorService _monitor = new();
    private readonly TextBox _hostBox = new();
    private readonly CheckBox _mtrCheck = new();
    private readonly Button _startButton;
    private readonly Button _exportButton;
    private readonly Label _statusLabel;
    private readonly ListView _hopList;
    private readonly TimeSeriesChartControl _chart;

    private bool _running;

    public LatencyMonitorForm(AppServices services)
    {
        _services = services;

        Text = $"{AppBranding.ShortName} — Latency monitor";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(860, 580);
        MinimumSize = new Size(620, 420);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = AppTheme.Surface };

        var hostLabel = new Label
        {
            Text = "Target",
            AutoSize = true,
            Location = new Point(12, 16),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent
        };
        _hostBox.SetBounds(64, 12, 220, 24);
        _hostBox.BackColor = AppTheme.SurfaceRaised;
        _hostBox.ForeColor = AppTheme.TextPrimary;
        _hostBox.BorderStyle = BorderStyle.FixedSingle;
        _hostBox.Text = PrefillTarget();

        _mtrCheck.Text = "Trace hops (MTR)";
        _mtrCheck.SetBounds(296, 14, 140, 22);
        _mtrCheck.ForeColor = AppTheme.TextSecondary;
        _mtrCheck.BackColor = Color.Transparent;

        _startButton = new Button { Text = "Start", Size = new Size(90, 30), Location = new Point(442, 9) };
        AppTheme.StyleAccentButton(_startButton);
        _startButton.Click += (_, _) => ToggleRun();

        _exportButton = new Button { Text = "Export CSV", Size = new Size(110, 30), Location = new Point(540, 9) };
        AppTheme.StyleGhostButton(_exportButton);
        _exportButton.Click += (_, _) => ExportCsv();

        toolbar.Controls.Add(hostLabel);
        toolbar.Controls.Add(_hostBox);
        toolbar.Controls.Add(_mtrCheck);
        toolbar.Controls.Add(_startButton);
        toolbar.Controls.Add(_exportButton);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = AppTheme.Surface };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Padding = new Padding(12, 0, 0, 0),
            Text = "Enter a target and click Start."
        };
        statusPanel.Controls.Add(_statusLabel);

        _chart = new TimeSeriesChartControl
        {
            Dock = DockStyle.Bottom,
            Height = 170,
            ValueFormatter = v => $"{v:0} ms"
        };

        _hopList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false
        };
        AppTheme.StyleListView(_hopList);
        _hopList.Columns.Add("HOP", 44);
        _hopList.Columns.Add("ADDRESS", 150);
        _hopList.Columns.Add("HOSTNAME", 200);
        _hopList.Columns.Add("LOSS%", 60);
        _hopList.Columns.Add("LAST", 60);
        _hopList.Columns.Add("AVG", 60);
        _hopList.Columns.Add("MIN", 60);
        _hopList.Columns.Add("MAX", 60);
        _hopList.Columns.Add("JITTER", 60);

        Controls.Add(_hopList);
        Controls.Add(_chart);
        Controls.Add(statusPanel);
        Controls.Add(toolbar);

        _monitor.Updated += OnMonitorUpdated;
        _monitor.Failed += OnMonitorFailed;

        FormClosing += (_, _) => _monitor.Dispose();
    }

    private string PrefillTarget()
    {
        var gateway = _services.Snapshot.GetPrimaryInterface()?.Gateway;
        return string.IsNullOrWhiteSpace(gateway) ? "8.8.8.8" : gateway;
    }

    private void ToggleRun()
    {
        if (_running)
        {
            _monitor.Stop();
            _running = false;
            _startButton.Text = "Start";
            _statusLabel.Text = "Stopped.";
            return;
        }

        var target = _hostBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            _statusLabel.Text = "Enter a target host or IP.";
            return;
        }

        _hopList.Items.Clear();
        _monitor.Start(target, _mtrCheck.Checked);
        _running = true;
        _startButton.Text = "Stop";
        _statusLabel.Text = _mtrCheck.Checked ? $"Tracing route to {target}…" : $"Pinging {target}…";
    }

    private void OnMonitorUpdated()
    {
        RunOnUi(RefreshHops);
    }

    private void OnMonitorFailed(string message)
    {
        RunOnUi(() =>
        {
            _running = false;
            _startButton.Text = "Start";
            _statusLabel.Text = message;
        });
    }

    private void RefreshHops()
    {
        var hops = _monitor.Snapshot();

        _hopList.BeginUpdate();
        try
        {
            while (_hopList.Items.Count > hops.Count)
            {
                _hopList.Items.RemoveAt(_hopList.Items.Count - 1);
            }

            for (var i = 0; i < hops.Count; i++)
            {
                var hop = hops[i];
                var values = new[]
                {
                    hop.HopNumber?.ToString() ?? "·",
                    hop.Address,
                    hop.Hostname ?? "—",
                    $"{hop.LossPercent:0}",
                    hop.Received == 0 ? "—" : $"{hop.Last:0}",
                    hop.Received == 0 ? "—" : $"{hop.Average:0}",
                    hop.Received == 0 ? "—" : $"{hop.DisplayMin:0}",
                    hop.Received == 0 ? "—" : $"{hop.Max:0}",
                    hop.Received == 0 ? "—" : $"{hop.Jitter:0}"
                };

                if (i < _hopList.Items.Count)
                {
                    var item = _hopList.Items[i];
                    for (var c = 0; c < values.Length; c++)
                    {
                        if (item.SubItems[c].Text != values[c])
                        {
                            item.SubItems[c].Text = values[c];
                        }
                    }
                }
                else
                {
                    var item = new ListViewItem(values[0]);
                    for (var c = 1; c < values.Length; c++)
                    {
                        item.SubItems.Add(values[c]);
                    }

                    _hopList.Items.Add(item);
                }
            }
        }
        finally
        {
            _hopList.EndUpdate();
        }

        UpdateChart(hops);

        if (_running)
        {
            var destination = hops.LastOrDefault();
            if (destination is not null)
            {
                _statusLabel.Text = $"{_monitor.Target} · {destination.Received}/{destination.Sent} replies · " +
                                    $"avg {destination.Average:0} ms · loss {destination.LossPercent:0}%";
            }
        }
    }

    private void UpdateChart(IReadOnlyList<LatencyHop> hops)
    {
        var destination = hops.LastOrDefault(h => h.Samples.Count > 0) ?? hops.LastOrDefault();
        if (destination is null)
        {
            return;
        }

        var series = new TimeSeriesChartControl.Series(
            "latency",
            AppTheme.Cyan,
            destination.Samples,
            destination.Last,
            Fill: true);

        var title = destination.HopNumber is { } n
            ? $"Hop {n} · {destination.Address}"
            : $"{_monitor.Target} latency";
        _chart.Update(title, new[] { series });
    }

    private void ExportCsv()
    {
        using var save = new SaveFileDialog
        {
            Title = "Export latency data",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"latency-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
        };

        if (save.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            File.WriteAllText(save.FileName, _monitor.ToCsv());
            _statusLabel.Text = $"Exported to {Path.GetFileName(save.FileName)}.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, AppBranding.ShortName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
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
