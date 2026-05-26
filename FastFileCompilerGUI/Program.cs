using FastFileLib.Logging;

namespace FastFileCompilerGUI;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Route Debug/Trace.WriteLine calls into LogService so the View > Logs window
        // captures them. Safe to call once at startup.
        TraceCapture.Install();
        LogService.Info("App", "FastFile Compiler starting");

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
