namespace NetConfigTray.Helpers;

public static class ClipboardHelper
{
    public static void CopyText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard can be locked by another app.
        }
    }
}
