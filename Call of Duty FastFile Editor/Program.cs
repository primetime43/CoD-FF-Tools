using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Call_of_Duty_FastFile_Editor.Services;
using Call_of_Duty_FastFile_Editor.UI;
using FastFileLib.Logging;

namespace Call_of_Duty_FastFile_Editor
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Capture Debug.WriteLine / Trace.WriteLine calls (used throughout the codebase)
            // into LogService so the Logs tab can render them.
            TraceCapture.Install();
            LogService.Info("App", "FastFile Editor starting");

            ApplicationConfiguration.Initialize();

            // Load the persisted light/dark theme and start auto-theming every form.
            ThemeManager.Initialize();

            // Create the service collection
            var services = new ServiceCollection();

            // Register your application services
            services.AddSingleton<IRawFileService, RawFileService>();

            // Register your forms so they get DependencyInjected
            services.AddSingleton<MainWindowForm>();

            // Build the provider
            var provider = services.BuildServiceProvider();

            // 5) Run the app by resolving MainWindowForm from the container
            Application.Run(provider.GetRequiredService<MainWindowForm>());
        }
    }
}