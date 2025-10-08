using System.Windows;
using ModernWpf;

namespace GameWatcher.AuthorStudio
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Match Studio: prefer dark theme
            try { ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark; } catch { }
            base.OnStartup(e);
        }
    }
}
