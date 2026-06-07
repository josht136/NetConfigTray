using Microsoft.Win32;

namespace NetConfigTray.Helpers;

public static class AutostartHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "TillerNetworkTool";
    private const string LegacyAppName = "NetConfigTray";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(AppName) as string;
        if (!string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(key?.GetValue(LegacyAppName) as string);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            var exePath = $"\"{Application.ExecutablePath}\"";
            key.SetValue(AppName, exePath);
            key.DeleteValue(LegacyAppName, throwOnMissingValue: false);
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
            key.DeleteValue(LegacyAppName, throwOnMissingValue: false);
        }
    }
}
