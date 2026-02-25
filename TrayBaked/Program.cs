namespace TrayBaked;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Prevent multiple instances
        using var mutex = new System.Threading.Mutex(true, "TrayBaked_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("TrayBaked is already running.", "TrayBaked",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Install the WinForms sync context now so TrayAppContext can capture it
        // before Application.Run() starts the message loop.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        Application.Run(new TrayAppContext());
    }
}
