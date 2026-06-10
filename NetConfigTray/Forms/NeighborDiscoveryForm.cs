using NetConfigTray.Helpers;
using NetConfigTray.Models;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

/// <summary>
/// Listens for LLDP/CDP advertisements on a chosen adapter and shows which switch and port this
/// machine is plugged into. Requires the Npcap driver (prompts to install if missing).
/// </summary>
public sealed class NeighborDiscoveryForm : Form
{
    private const int DefaultListenSeconds = 60;

    private readonly AppServices _services;
    private readonly NeighborDiscoveryService _discovery = new();
    private readonly ComboBox _deviceCombo = new();
    private readonly Button _listenButton;
    private readonly Label _statusLabel;
    private readonly ListView _results;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private readonly Dictionary<string, ListViewItem> _itemsByKey = new();

    private int _secondsLeft;
    private bool _listening;

    public NeighborDiscoveryForm(AppServices services)
    {
        _services = services;

        Text = $"{AppBranding.ShortName} — LLDP / CDP discovery";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 480);
        MinimumSize = new Size(560, 320);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = AppTheme.Surface };

        _deviceCombo.SetBounds(12, 11, 380, 24);
        _deviceCombo.DropDownStyle = ComboBoxStyle.DropDownList;

        _listenButton = new Button { Text = "Listen", Size = new Size(120, 30), Location = new Point(404, 9) };
        AppTheme.StyleAccentButton(_listenButton);
        _listenButton.Click += (_, _) => ToggleListen();

        var refresh = new Button { Text = "↻ Adapters", Size = new Size(100, 30), Location = new Point(532, 9) };
        AppTheme.StyleGhostButton(refresh);
        refresh.Click += (_, _) => LoadDevices();

        toolbar.Controls.Add(_deviceCombo);
        toolbar.Controls.Add(_listenButton);
        toolbar.Controls.Add(refresh);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 28, BackColor = AppTheme.Surface };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Padding = new Padding(12, 0, 0, 0),
            Text = "Select an adapter and click Listen."
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
        _results.Columns.Add("PROTO", 60);
        _results.Columns.Add("SWITCH / SYSTEM", 200);
        _results.Columns.Add("PORT", 150);
        _results.Columns.Add("VLAN", 60);
        _results.Columns.Add("MGMT IP", 130);
        _results.Columns.Add("PLATFORM", 200);

        Controls.Add(_results);
        Controls.Add(statusPanel);
        Controls.Add(toolbar);

        _discovery.NeighborFound += OnNeighborFound;
        _discovery.Error += OnDiscoveryError;

        _countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _countdownTimer.Tick += OnCountdownTick;

        FormClosing += (_, _) =>
        {
            _countdownTimer.Stop();
            _discovery.Dispose();
        };

        Shown += (_, _) => LoadDevices();
    }

    private void LoadDevices()
    {
        if (!NpcapHelper.IsInstalled())
        {
            _statusLabel.Text = "Npcap not detected — capture is unavailable.";
            PromptInstallNpcap();
            return;
        }

        _deviceCombo.Items.Clear();
        var devices = NeighborDiscoveryService.ListDevices();
        foreach (var device in devices)
        {
            _deviceCombo.Items.Add(device);
        }

        _deviceCombo.DisplayMember = nameof(CaptureDeviceDescriptor.Description);

        if (_deviceCombo.Items.Count > 0)
        {
            _deviceCombo.SelectedIndex = 0;
            _statusLabel.Text = $"{_deviceCombo.Items.Count} adapter(s) available.";
        }
        else
        {
            _statusLabel.Text = "No capture adapters found.";
        }
    }

    private void PromptInstallNpcap()
    {
        var result = MessageBox.Show(
            this,
            "LLDP/CDP discovery needs the Npcap capture driver, which was not detected.\n\n" +
            "Open the Npcap download page now?",
            $"{AppBranding.ShortName} — Npcap required",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Information);

        if (result == DialogResult.Yes)
        {
            NpcapHelper.OpenDownloadPage();
        }
    }

    private void ToggleListen()
    {
        if (_listening)
        {
            StopListening("Stopped.");
            return;
        }

        if (_deviceCombo.SelectedItem is not CaptureDeviceDescriptor descriptor)
        {
            _statusLabel.Text = "Select an adapter first.";
            return;
        }

        _results.Items.Clear();
        _itemsByKey.Clear();
        _discovery.Start(descriptor.Name);
        _listening = true;
        _secondsLeft = DefaultListenSeconds;
        _listenButton.Text = "Stop";
        _statusLabel.Text = $"Listening on {descriptor.Description} … {_secondsLeft}s (advertisements arrive every 30–60s)";
        _countdownTimer.Start();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        _secondsLeft--;
        if (_secondsLeft <= 0)
        {
            StopListening($"Done listening. {_itemsByKey.Count} neighbor(s) found.");
            return;
        }

        if (_itemsByKey.Count == 0)
        {
            _statusLabel.Text = $"Listening … {_secondsLeft}s remaining (no neighbors yet)";
        }
        else
        {
            _statusLabel.Text = $"Listening … {_secondsLeft}s · {_itemsByKey.Count} neighbor(s) found";
        }
    }

    private void StopListening(string status)
    {
        _countdownTimer.Stop();
        _discovery.Stop();
        _listening = false;
        _listenButton.Text = "Listen";
        _statusLabel.Text = status;
    }

    private void OnNeighborFound(NeighborInfo neighbor)
    {
        RunOnUi(() =>
        {
            var values = new[]
            {
                neighbor.Protocol == NeighborProtocol.Lldp ? "LLDP" : "CDP",
                neighbor.SystemName ?? neighbor.ChassisId ?? "—",
                neighbor.PortDescription ?? neighbor.PortId ?? "—",
                neighbor.Vlan?.ToString() ?? "—",
                neighbor.ManagementAddress ?? "—",
                neighbor.Platform ?? neighbor.SystemDescription ?? "—"
            };

            if (_itemsByKey.TryGetValue(neighbor.Key, out var existing))
            {
                for (var i = 0; i < values.Length; i++)
                {
                    existing.SubItems[i].Text = values[i];
                }
            }
            else
            {
                var item = new ListViewItem(values[0]);
                for (var i = 1; i < values.Length; i++)
                {
                    item.SubItems.Add(values[i]);
                }

                _results.Items.Add(item);
                _itemsByKey[neighbor.Key] = item;
            }
        });
    }

    private void OnDiscoveryError(string message)
    {
        RunOnUi(() => StopListening(message));
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
