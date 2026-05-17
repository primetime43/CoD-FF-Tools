using FastFileLib.Logging;

namespace FastFileConverterGUI;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        TraceCapture.Install();
        LogService.Info("App", "FastFile Converter starting");

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}
