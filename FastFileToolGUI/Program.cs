using FastFileLib.Logging;

namespace FastFileToolGUI;

static class Program
{
    [STAThread]
    static void Main()
    {
        TraceCapture.Install();
        LogService.Info("App", "FastFile Tool starting");

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
