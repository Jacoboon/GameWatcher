using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using System.IO;
using System.Collections.Generic;
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
    private IHost? _host;

    public App()
    {
        // Initialize host builder for proper logging and DI
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Build and start the host for proper logging
            _host = CreateHostBuilder().Build();
            await _host.StartAsync();

            // Create main window with logging support
            var mainWindow = new Views.MainWindow();
            
            // Get logger from DI if available
            try
            {
                var logger = _host.Services.GetService<ILogger<App>>();
                logger?.LogInformation("GameWatcher Studio starting up");
            }
            catch
            {
                // Fallback if logging fails
                Console.WriteLine("[APP] GameWatcher Studio starting up");
            }
            
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
        if (_host != null)
        {
            try
            {
                await _host.StopAsync(TimeSpan.FromSeconds(5));
                _host.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[APP] Error during shutdown: {ex.Message}");
            }
        }
        base.OnExit(e);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                var basePath = AppDomain.CurrentDomain.BaseDirectory;
                config.SetBasePath(basePath)
                      .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                      .AddInMemoryCollection(new Dictionary<string, string?>
                      {
                          // Fallback defaults if appsettings.json is missing
                          ["Serilog:MinimumLevel:Default"] = "Information",
                          ["GameWatcher:AutoStart"] = "true",
                          ["GameWatcher:DetectionIntervalMs"] = "2000"
                      });
            })
            .UseSerilog((context, config) =>
            {
                // Ensure logs directory exists
                var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logsDir);

                config.ReadFrom.Configuration(context.Configuration)
                      .WriteTo.Console()
                      .WriteTo.File(
                          Path.Combine(logsDir, "gamewatcher-studio_.log"),
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

    public static IServiceProvider? Services => ((App)Current)._host?.Services;
}