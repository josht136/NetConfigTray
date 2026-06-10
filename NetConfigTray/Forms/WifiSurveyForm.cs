using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

/// <summary>
/// Wi-Fi survey/analyzer: scans visible BSSIDs, lists them with signal/channel/security, shows a
/// channel-overlap graph per band, and recommends the least-congested channel.
/// </summary>
public sealed class WifiSurveyForm : Form
{
    private readonly AppServices _services;
    private readonly WifiScanService _scanner = new();
    private readonly ComboBox _bandCombo = new();
    private readonly Button _scanButton;
    private readonly Label _statusLabel;
    private readonly Label _recommendLabel;
    private readonly ListView _list;
    private readonly ChannelGraphControl _graph;

    private IReadOnlyList<WifiBss> _networks = Array.Empty<WifiBss>();
    private bool _scanning;

    public WifiSurveyForm(AppServices services)
    {
        _services = services;

        Text = $"{AppBranding.ShortName} — Wi-Fi survey";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(920, 600);
        MinimumSize = new Size(640, 460);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = AppTheme.Surface };

        _scanButton = new Button { Text = "Scan", Size = new Size(90, 30), Location = new Point(12, 9) };
        AppTheme.StyleAccentButton(_scanButton);
        _scanButton.Click += (_, _) => StartScan();

        var bandLabel = new Label
        {
            Text = "Band",
            AutoSize = true,
            Location = new Point(116, 16),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent
        };
        _bandCombo.SetBounds(156, 11, 120, 24);
        _bandCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _bandCombo.Items.AddRange(new object[] { "2.4 GHz", "5 GHz", "6 GHz" });
        _bandCombo.SelectedIndex = 0;
        _bandCombo.SelectedIndexChanged += (_, _) => RefreshBandViews();

        _recommendLabel = new Label
        {
            AutoSize = false,
            Location = new Point(292, 9),
            Size = new Size(600, 30),
            ForeColor = AppTheme.Accent,
            Font = AppTheme.FontCaption,
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };

        toolbar.Controls.Add(_scanButton);
        toolbar.Controls.Add(bandLabel);
        toolbar.Controls.Add(_bandCombo);
        toolbar.Controls.Add(_recommendLabel);

        var statusPanel = new Panel { Dock = DockStyle.Bottom, Height = 26, BackColor = AppTheme.Surface };
        _statusLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = AppTheme.TextMuted,
            Font = AppTheme.FontSmall,
            Padding = new Padding(12, 0, 0, 0),
            Text = "Click Scan to survey nearby Wi-Fi networks."
        };
        statusPanel.Controls.Add(_statusLabel);

        _graph = new ChannelGraphControl { Dock = DockStyle.Top, Height = 240 };

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            HideSelection = false
        };
        AppTheme.StyleListView(_list);
        _list.Columns.Add("SSID", 200);
        _list.Columns.Add("BSSID", 150);
        _list.Columns.Add("SIGNAL", 80);
        _list.Columns.Add("RSSI", 70);
        _list.Columns.Add("CH", 50);
        _list.Columns.Add("BAND", 70);
        _list.Columns.Add("PHY", 90);
        _list.Columns.Add("SECURITY", 90);

        Controls.Add(_list);
        Controls.Add(_graph);
        Controls.Add(statusPanel);
        Controls.Add(toolbar);

        Shown += (_, _) => StartScan();
    }

    private double SelectedBand => _bandCombo.SelectedIndex switch
    {
        1 => 5.0,
        2 => 6.0,
        _ => 2.4
    };

    private async void StartScan()
    {
        if (_scanning)
        {
            return;
        }

        _scanning = true;
        _scanButton.Enabled = false;
        _statusLabel.Text = "Scanning… (this takes a few seconds)";

        try
        {
            _networks = await _scanner.ScanAsync();
            _statusLabel.Text = _networks.Count == 0
                ? "No networks found (is Wi-Fi enabled?)."
                : $"Found {_networks.Count} BSSID(s) across all bands.";
            PopulateList();
            RefreshBandViews();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _scanning = false;
            _scanButton.Enabled = true;
        }
    }

    private void PopulateList()
    {
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (var net in _networks)
            {
                var item = new ListViewItem(net.Ssid);
                item.SubItems.Add(net.Bssid);
                item.SubItems.Add($"{net.SignalPercent}%");
                item.SubItems.Add($"{net.Rssi} dBm");
                item.SubItems.Add(net.Channel.ToString());
                item.SubItems.Add(net.BandGhz > 0 ? $"{net.BandGhz:0.#} GHz" : "—");
                item.SubItems.Add(net.Phy);
                item.SubItems.Add(net.Secured ? "secured" : "open");
                _list.Items.Add(item);
            }
        }
        finally
        {
            _list.EndUpdate();
        }
    }

    private void RefreshBandViews()
    {
        var band = SelectedBand;
        _graph.SetData(_networks, band);

        if (_networks.Count == 0)
        {
            _recommendLabel.Text = string.Empty;
            return;
        }

        var count = _networks.Count(n => Math.Abs(n.BandGhz - band) < 0.6);
        var recommended = WifiScanService.RecommendChannel(_networks, band);
        _recommendLabel.Text = count == 0
            ? $"No networks on {band:0.#} GHz — channel {recommended} is clear."
            : $"{count} network(s) on {band:0.#} GHz · least-congested channel: {recommended}";
    }
}
