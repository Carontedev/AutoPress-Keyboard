using System.Threading;

namespace AutoPress.App;

public partial class App : System.Windows.Application
{
    private Mutex? instance;
    private bool ownsMutex;
    protected override void OnStartup(StartupEventArgs e)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        // A small, mostly static tool: avoid a dedicated D3D context while games use the GPU.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        base.OnStartup(e);
        instance = new Mutex(true, "Local\\AutoPressKeyboard.Desktop", out ownsMutex);
        if (!ownsMutex)
        {
            MessageBox.Show("AutoPress is already running. Open it from the system tray.", "AutoPress");
            Shutdown();
            return;
        }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsMutex) instance?.ReleaseMutex();
        instance?.Dispose();
        base.OnExit(e);
    }
}
