namespace NetConfigTray;

internal static class Program
{
    private const string MutexName = "NetConfigTray_SingleInstance_Mutex";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
