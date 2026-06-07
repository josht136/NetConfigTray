using System.Runtime.InteropServices;

namespace NetConfigTray.Helpers;

internal static class DarkModeHelper
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static void TryEnableDarkTitleBar(IWin32Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        try
        {
            var useDark = 1;
            _ = DwmSetWindowAttribute(window.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
        }
        catch
        {
            // Best effort only.
        }
    }
}
