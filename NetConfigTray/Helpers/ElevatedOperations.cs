namespace NetConfigTray.Helpers;

/// <summary>
/// Dispatcher for operations that run in an elevated child process (launched via
/// <see cref="ElevationHelper.RunElevated"/>). Cases are added as features that need
/// administrator rights are implemented. Returns a process exit code (0 = success).
/// </summary>
public static class ElevatedOperations
{
    public static int Run(string operation, string[] arguments)
    {
        return operation switch
        {
            // Privileged operation handlers are registered here as features land.
            _ => 2 // Unknown operation.
        };
    }
}
