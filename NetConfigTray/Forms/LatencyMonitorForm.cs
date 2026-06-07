using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

// Implemented in Milestone 4 (latency monitor).
public sealed class LatencyMonitorForm : Form
{
    private readonly AppServices _services;

    public LatencyMonitorForm(AppServices services)
    {
        _services = services;
        Text = $"{AppBranding.ShortName} — Latency monitor";
        ClientSize = new Size(820, 560);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);
    }
}
