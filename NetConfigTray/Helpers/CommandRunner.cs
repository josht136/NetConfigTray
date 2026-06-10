using System.Diagnostics;

namespace NetConfigTray.Helpers;

/// <summary>
/// Runs short-lived console commands (ipconfig, etc.) and captures their output.
/// All methods are blocking and intended to be called from a background thread.
/// </summary>
public static class CommandRunner
{
    public sealed record CommandResult(int ExitCode, string Output);

    public static CommandResult Run(string fileName, string arguments, int timeoutMs = 15000)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new CommandResult(-1, $"Failed to start {fileName}.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(timeoutMs))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort.
                }

                return new CommandResult(-1, $"{fileName} timed out.");
            }

            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
            return new CommandResult(process.ExitCode, output.Trim());
        }
        catch (Exception ex)
        {
            return new CommandResult(-1, $"Error running {fileName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs a command and invokes <paramref name="onLine"/> for each stdout line as it arrives.
    /// Returns the process exit code (or -1 on failure). Honors the cancellation token by killing
    /// the process.
    /// </summary>
    public static int RunStreaming(
        string fileName,
        string arguments,
        Action<string> onLine,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                onLine($"Failed to start {fileName}.");
                return -1;
            }

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Best effort.
                }
            });

            while (process.StandardOutput.ReadLine() is { } line)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                onLine(line);
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            onLine($"Error running {fileName}: {ex.Message}");
            return -1;
        }
    }
}
