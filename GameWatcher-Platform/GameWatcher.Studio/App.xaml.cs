using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using System.IO;
using GameWatcher.Engine.Services;
using GameWatcher.Engine.Ocr;
using GameWatcher.Engine.Packs;
using GameWatcher.Runtime.Services;
using GameWatcher.Studio.ViewModels;
using GameWatcher.Studio.Views;
using ModernWpf;

namespace GameWatcher.Studio;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        // Simplified - no DI host for now
        _host = null!; // We'll fix this later
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Skip complex DI for now - just show the window directly
            var mainWindow = new Views.MainWindow();
            mainWindow.Show();

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup failed: {ex.Message}\n\nFull error:\n{ex}", "GameWatcher Studio Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync(TimeSpan.FromSeconds(5));
        _host.Dispose();
        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                config.SetBasePath(basePath)
                      .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
            })
            .UseSerilog((context, config) =>
            {
                config.ReadFrom.Configuration(context.Configuration)
                      .WriteTo.File(
                          Path.Combine("logs", "gamewatcher-studio_.log"),
                          rollingInterval: RollingInterval.Day,
                          retainedFileCountLimit: 7,
                          shared: true,
                          flushToDiskInterval: TimeSpan.FromSeconds(1));
            })
            .ConfigureServices((context, services) =>
            {
                // Minimal working services - add complexity later
                services.AddLogging();
                services.AddSingleton<MainWindow>();
            });
    }

    public static IServiceProvider Services => ((App)Current)._host.Services;
}