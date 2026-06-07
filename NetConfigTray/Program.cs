using NetConfigTray.Helpers;

namespace NetConfigTray;

internal static class Program
{
    private const string MutexName = "NetConfigTray_SingleInstance_Mutex";

    [STAThread]
    private static int Main(string[] args)
    {
        // Elevated child-process mode: perform a single privileged operation and exit.
        if (args.Length >= 2 && args[0] == ElevationHelper.ElevatedOpSwitch)
        {
            return ElevatedOperations.Run(args[1], args[2..]);
        }

        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
        {
            System.Diagnostics.Debug.WriteLine(eventArgs.Exception);
        };

        Application.Run(new TrayApplicationContext());
        return 0;
    }
}
