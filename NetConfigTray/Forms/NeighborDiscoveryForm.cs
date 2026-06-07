using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

// Implemented in Milestone 3 (LLDP/CDP discovery).
public sealed class NeighborDiscoveryForm : Form
{
    private readonly AppServices _services;

    public NeighborDiscoveryForm(AppServices services)
    {
        _services = services;
        Text = $"{AppBranding.ShortName} — LLDP / CDP discovery";
        ClientSize = new Size(720, 480);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);
    }
}
