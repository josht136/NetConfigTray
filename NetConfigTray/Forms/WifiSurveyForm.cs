using NetConfigTray.Helpers;
using NetConfigTray.Services;

namespace NetConfigTray.Forms;

// Implemented in Milestone 6 (Wi-Fi survey).
public sealed class WifiSurveyForm : Form
{
    private readonly AppServices _services;

    public WifiSurveyForm(AppServices services)
    {
        _services = services;
        Text = $"{AppBranding.ShortName} — Wi-Fi survey";
        ClientSize = new Size(900, 580);
        AppTheme.ApplyFormChrome(this);
        HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(this);
    }
}
