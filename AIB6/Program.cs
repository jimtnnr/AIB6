using Avalonia;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using AIB6.Helpers;

namespace AIB6
{
    public static class Program
    {
        public static IConfiguration Configuration { get; private set; }
        public static AppSettings AppSettings { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            // Load appsettings.json from the project root
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Bind to AppSettings model
            AppSettings = new AppSettings();
            Configuration.Bind(AppSettings);
            PromptTemplateRegistry.Load(AppSettings.Paths.PromptTemplatesFolder);

            // Continue to Avalonia
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
         
        }

        // Avalonia configuration, used by visual designer as well
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}