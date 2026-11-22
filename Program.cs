namespace BtBattery;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Tray-only app: no main window. An ApplicationContext keeps the message
        // loop alive while owning just the tray icon, until the user exits.
        Application.Run(new TrayContext());
    }
}
