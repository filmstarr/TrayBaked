namespace TrayBaked;

static class Program
{
    [System.STAThread]
    static void Main()
    {
        // Prevent multiple instances
        using var mutex = new System.Threading.Mutex(true, "TrayBaked_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "TrayBaked is already running.",
                "TrayBaked",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
