using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace NetConfigTray.Helpers;

/// <summary>
/// On-demand elevation. The app runs as the invoker (no UAC at startup); privileged operations
/// relaunch the executable with <c>--elevated-op &lt;name&gt; [args...]</c> via the ShellExecute
/// "runas" verb, which triggers a single UAC prompt for that operation only.
/// </summary>
public static class ElevationHelper
{
    public const string ElevatedOpSwitch = "--elevated-op";

    public static bool IsCurrentProcessElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Relaunches this executable elevated to perform a single operation, waiting for it to finish.
    /// Returns the operation's exit code, or null if the user declined the UAC prompt / it failed
    /// to start.
    /// </summary>
    public static int? RunElevated(string operation, params string[] arguments)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return null;
        }

        var args = new List<string> { ElevatedOpSwitch, operation };
        args.AddRange(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = BuildArguments(args)
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User declined the UAC prompt (or it could not be shown).
            return null;
        }
        catch
        {
            return null;
        }
    }

    public static string BuildArguments(IEnumerable<string> arguments)
    {
        return string.Join(' ', arguments.Select(QuoteIfNeeded));
    }

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }
}
