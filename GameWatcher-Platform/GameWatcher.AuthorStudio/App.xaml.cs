using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Windows;
using System.IO;
using System.Collections.Generic;
using GameWatcher.AuthorStudio.Services;
using GameWatcher.AuthorStudio.ViewModels;
using GameWatcher.Engine.Ocr;
using GameWatcher.Engine.Packs;
using GameWatcher.Engine.Services;
using ModernWpf;

namespace GameWatcher.AuthorStudio;

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
            // Set dark theme to match Studio
            ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;

            // Build and start the host for proper logging and DI
            _host = CreateHostBuilder().Build();
            await _host.StartAsync();

            // Create main window through DI
            var mainWindow = _host.Services.GetRequiredService<Views.MainWindow>();
            
            // Get logger from DI
            var logger = _host.Services.GetService<ILogger<App>>();
            logger?.LogInformation("GameWatcher Author Studio starting up");
            
            mainWindow.Show();
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup failed: {ex.Message}\n\nFull error:\n{ex}", 
                "GameWatcher Author Studio Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            try
            {
                var logger = _host.Services.GetService<ILogger<App>>();
                logger?.LogInformation("GameWatcher Author Studio shutting down");
                
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
                      .AddInMemoryCollection(new Dictionary<string, string?>
                      {
                          ["Serilog:MinimumLevel:Default"] = "Information",
                          ["AuthorStudio:AutoSaveIntervalMinutes"] = "5"
                      });
            })
            .UseSerilog((context, config) =>
            {
                var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(logsDir);

                // Use timestamp for session-based logs (new file per session)
                var sessionTimestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var logPath = Path.Combine(logsDir, $"author-studio_{sessionTimestamp}.log");

                config.ReadFrom.Configuration(context.Configuration)
                      .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                      .WriteTo.File(
                          logPath,
                          rollingInterval: RollingInterval.Infinite, // No rolling, one file per session
                          retainedFileCountLimit: 30, // Keep last 30 sessions
                          shared: false, // Not shared since it's session-specific
                          flushToDiskInterval: TimeSpan.FromSeconds(1));
            })
            .ConfigureServices((context, services) =>
            {
                // Core services
                services.AddLogging();

                // Author Studio Services (existing, now via DI)
                services.AddSingleton<DiscoveryService>();
                services.AddSingleton<SpeakerStore>();
                services.AddSingleton<SessionStore>();
                services.AddSingleton<PackExporter>();
                services.AddSingleton<PackLoader>();
                services.AddSingleton<OpenAiTtsService>();
                services.AddSingleton<AudioPlaybackService>();
                services.AddSingleton<AuthorSettingsService>();
                services.AddSingleton<OcrFixesStore>();
                services.AddSingleton<UserSettingsStore>();
                services.AddSingleton<AudioStore>();

                // ViewModels (to be created)
                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<DiscoveryViewModel>();
                services.AddTransient<SpeakersViewModel>();
                services.AddTransient<VoiceLabViewModel>();
                services.AddTransient<PackBuilderViewModel>();
                services.AddTransient<SettingsViewModel>();

                // Main Window
                services.AddSingleton<Views.MainWindow>();
            });
    }

    public static IServiceProvider? Services => ((App)Current)._host?.Services;
}
