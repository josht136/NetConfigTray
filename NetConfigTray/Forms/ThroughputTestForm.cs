using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

// Implemented in Milestone 5 (iperf3 throughput test).
public sealed class ThroughputTestForm : Form
{
    private readonly AppServices _services;

    public ThroughputTestForm(AppServices services)
    {
        _services = services;
        Text = $"{AppBranding.ShortName} — Throughput test";
        ClientSize = new Size(820, 560);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);
    }
}
