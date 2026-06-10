using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

// Implemented in Milestone 2 (console access).
public sealed class ConsoleTerminalForm : Form
{
    private readonly AppServices _services;

    public ConsoleTerminalForm(AppServices services)
    {
        _services = services;
        Text = $"{AppBranding.ShortName} — Console";
        ClientSize = new Size(820, 520);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);
    }
}
