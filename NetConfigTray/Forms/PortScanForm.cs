using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

// Implemented in Milestone 4 (port scan).
public sealed class PortScanForm : Form
{
    private readonly AppServices _services;

    public PortScanForm(AppServices services)
    {
        _services = services;
        Text = $"{AppBranding.ShortName} — Port scan";
        ClientSize = new Size(760, 520);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);
    }

    public PortScanForm(AppServices services, string targetHost)
        : this(services)
    {
        _ = targetHost;
    }
}
