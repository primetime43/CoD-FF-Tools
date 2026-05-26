using FastFileLib.Logging;

namespace FastFileExtractor;

static class Program
{
    [STAThread]
    static void Main()
    {
        TraceCapture.Install();
        LogService.Info("App", "FastFile Extractor starting");

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
