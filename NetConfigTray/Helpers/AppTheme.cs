using NetConfigTray.Models;

namespace NetConfigTray.Helpers;

public static class AppTheme
{
    // Solarized Dark
    public static readonly Color Base03 = ColorFromHex(0x002B36);
    public static readonly Color Base02 = ColorFromHex(0x073642);
    public static readonly Color Base01 = ColorFromHex(0x586E75);
    public static readonly Color Base00 = ColorFromHex(0x657B83);
    public static readonly Color Base0 = ColorFromHex(0x839496);
    public static readonly Color Base1 = ColorFromHex(0x93A1A1);
    public static readonly Color Yellow = ColorFromHex(0xB58900);
    public static readonly Color Orange = ColorFromHex(0xCB4B16);
    public static readonly Color Red = ColorFromHex(0xDC322F);
    public static readonly Color Magenta = ColorFromHex(0xD33682);
    public static readonly Color Violet = ColorFromHex(0x6C71C4);
    public static readonly Color Blue = ColorFromHex(0x268BD2);
    public static readonly Color Cyan = ColorFromHex(0x2AA198);
    public static readonly Color Green = ColorFromHex(0x859900);

    // 2026 surface system
    public static readonly Color AppBackground = Base03;
    public static readonly Color Surface = Base02;
    public static readonly Color SurfaceRaised = ColorFromHex(0x0E4452);
    public static readonly Color SurfaceHover = ColorFromHex(0x104A59);
    public static readonly Color Border = ColorFromHex(0x1A5563);
    public static readonly Color BorderSubtle = ColorFromHex(0x124652);
    public static readonly Color Accent = Cyan;
    public static readonly Color AccentSoft = Color.FromArgb(36, 42, 161, 152);
    public static readonly Color TextPrimary = Base1;
    public static readonly Color TextSecondary = Base0;
    public static readonly Color TextMuted = Base01;
    public static readonly Color SelectionBar = Cyan;

    public static readonly Font FontBody = new("Segoe UI", 9.5F);
    public static readonly Font FontSmall = new("Segoe UI", 8.25F, FontStyle.Regular);
    public static readonly Font FontCaption = new("Segoe UI", 8.25F, FontStyle.Bold);
    public static readonly Font FontTitle = new("Segoe UI Semibold", 13F, FontStyle.Bold);
    public static readonly Font FontWindowTitle = new("Segoe UI Semibold", 14F, FontStyle.Bold);
    public static readonly Font FontHeader = new("Segoe UI Semibold", 16F, FontStyle.Bold);
    public static readonly Font FontSection = new("Segoe UI Semibold", 10F, FontStyle.Bold);
    public static readonly Font FontValue = new("Cascadia Mono", 9.75F);
    public static readonly Font FontValueFallback = new("Consolas", 9.75F);

    public static Font ValueFont =>
        FontFamily.Families.Any(f => f.Name.Equals("Cascadia Mono", StringComparison.OrdinalIgnoreCase))
            ? FontValue
            : FontValueFallback;

    public static void ApplyFormChrome(Form form)
    {
        form.BackColor = AppBackground;
        form.ForeColor = TextPrimary;
        form.Font = FontBody;
    }

    public static void StyleHeaderPanel(Panel panel)
    {
        panel.BackColor = Surface;
        panel.ForeColor = TextPrimary;
        panel.Padding = new Padding(20, 14, 20, 14);
    }

    public static void StyleStatusLabel(Label label)
    {
        label.BackColor = Surface;
        label.ForeColor = TextMuted;
        label.Font = FontSmall;
        label.Padding = new Padding(20, 0, 20, 0);
    }

    public static void StyleTitleLabel(Label label, string text)
    {
        label.Text = text;
        label.Font = FontWindowTitle;
        label.ForeColor = TextPrimary;
        label.AutoSize = true;
        label.BackColor = Color.Transparent;
    }

    public static void StyleSubtitleLabel(Label label, string text)
    {
        label.Text = text;
        label.Font = FontSmall;
        label.ForeColor = TextMuted;
        label.AutoSize = true;
        label.BackColor = Color.Transparent;
    }

    public static void StyleAccentButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.Font = FontCaption;
        button.Cursor = Cursors.Hand;
        button.BackColor = AccentSoft;
        button.ForeColor = Accent;
        button.FlatAppearance.BorderColor = Accent;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = ColorFromHex(0x164E5C);
        button.FlatAppearance.MouseDownBackColor = SurfaceRaised;
    }

    public static void StyleGhostButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.Font = FontCaption;
        button.Cursor = Cursors.Hand;
        button.BackColor = Surface;
        button.ForeColor = TextSecondary;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = SurfaceHover;
        button.FlatAppearance.MouseDownBackColor = SurfaceRaised;
    }

    public static void StyleSplitContainer(SplitContainer split)
    {
        split.BackColor = BorderSubtle;
        split.Panel1.BackColor = Surface;
        split.Panel2.BackColor = AppBackground;
    }

    public static void StyleListView(ListView list)
    {
        list.BackColor = Surface;
        list.ForeColor = TextPrimary;
        list.Font = FontBody;
        list.BorderStyle = BorderStyle.None;
    }

    public static Color ConfigColor(IpConfigurationType type) =>
        type == IpConfigurationType.Dhcp ? Green : Orange;

    public static Color ConfigBackground(IpConfigurationType type) =>
        type == IpConfigurationType.Dhcp
            ? ColorFromHex(0x1A3020)
            : ColorFromHex(0x3A2218);

    public static Color ListSelectionBackground(bool focused) =>
        focused ? ColorFromHex(0x164049) : SurfaceHover;

    private static Color ColorFromHex(int rgb)
    {
        return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }
}
