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
using GameWatcher.Engine.Detection;
using GameWatcher.Runtime.Services;
using GameWatcher.Runtime.Services.Capture;
using GameWatcher.Studio.ViewModels;
using GameWatcher.Studio.Views;
using FF1.PixelRemaster.Detection;
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
                // Create logs in both project source and runtime directories for development
                var runtimeLogsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                var projectLogsDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "logs");
                
                Directory.CreateDirectory(runtimeLogsDir);
                Directory.CreateDirectory(projectLogsDir);

                Console.WriteLine($"[SERILOG] Runtime logs: {runtimeLogsDir}");
                Console.WriteLine($"[SERILOG] Project logs: {projectLogsDir}");

                // Use timestamp for session-based logs (new file per session)
                var sessionTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                
                config.ReadFrom.Configuration(context.Configuration)
                      .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                      .WriteTo.File(
                          Path.Combine(runtimeLogsDir, $"gamewatcher-studio_{sessionTimestamp}.log"),
                          rollingInterval: RollingInterval.Infinite, // No rolling, one file per session
                          retainedFileCountLimit: 30, // Keep last 30 sessions
                          shared: false,
                          flushToDiskInterval: TimeSpan.FromSeconds(1))
                      .WriteTo.File(
                          Path.Combine(projectLogsDir, $"gamewatcher-studio_{sessionTimestamp}.log"),
                          rollingInterval: RollingInterval.Infinite,
                          retainedFileCountLimit: 30,
                          shared: false,
                          flushToDiskInterval: TimeSpan.FromSeconds(1));
            })
            .ConfigureServices((context, services) =>
            {
                // Core Engine Services for capture & detection
                services.AddLogging();
                
                // Capture services (Studio needs these to detect dialogue and play voiceovers)
                services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
                services.AddSingleton<ITextboxDetector>(sp =>
                {
                    var logger = sp.GetService<ILogger<DynamicTextboxDetector>>();
                    return new DynamicTextboxDetector(FF1DetectionConfig.GetConfig(), logger);
                });
                services.AddSingleton<GameCaptureService>();
                
                // UI
                services.AddSingleton<MainWindow>();
            });
    }

    public static IServiceProvider? Services => ((App)Current)._host?.Services;
}