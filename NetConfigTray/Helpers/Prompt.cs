namespace NetConfigTray.Helpers;

/// <summary>Small themed single-line input dialog (returns null on cancel).</summary>
public static class Prompt
{
    public static string? Show(IWin32Window? owner, string title, string message, string defaultValue = "")
    {
        using var form = new Form
        {
            Text = $"{AppBranding.ShortName} — {title}",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(360, 140)
        };
        AppTheme.ApplyFormChrome(form);
        form.HandleCreated += (_, _) => DarkModeHelper.TryEnableDarkTitleBar(form);

        var label = new Label
        {
            Text = message,
            AutoSize = false,
            Location = new Point(16, 14),
            Size = new Size(328, 20),
            ForeColor = AppTheme.TextSecondary,
            BackColor = Color.Transparent
        };

        var input = new TextBox
        {
            Text = defaultValue,
            Location = new Point(16, 40),
            Size = new Size(328, 24),
            BackColor = AppTheme.SurfaceRaised,
            ForeColor = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(90, 30), Location = new Point(254, 90) };
        AppTheme.StyleAccentButton(ok);
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(90, 30), Location = new Point(154, 90) };
        AppTheme.StyleGhostButton(cancel);

        form.Controls.Add(label);
        form.Controls.Add(input);
        form.Controls.Add(ok);
        form.Controls.Add(cancel);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog(owner) == DialogResult.OK && !string.IsNullOrWhiteSpace(input.Text)
            ? input.Text
            : null;
    }
}
