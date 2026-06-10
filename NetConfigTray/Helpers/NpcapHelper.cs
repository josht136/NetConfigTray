namespace NetConfigTray.Helpers;

/// <summary>
/// Detects whether the Npcap capture driver is present. LLDP/CDP capture (via SharpPcap) needs it.
/// </summary>
public static class NpcapHelper
{
    public const string DownloadUrl = "https://npcap.com/#download";

    public static bool IsInstalled()
    {
        try
        {
            var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var candidates = new[]
            {
                Path.Combine(system32, "Npcap", "wpcap.dll"),
                Path.Combine(system32, "wpcap.dll"),
                Path.Combine(system32, "Npcap", "Packet.dll")
            };

            return candidates.Any(File.Exists);
        }
        catch
        {
            return false;
        }
    }

    public static void OpenDownloadPage()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DownloadUrl)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Browser unavailable.
        }
    }
}
