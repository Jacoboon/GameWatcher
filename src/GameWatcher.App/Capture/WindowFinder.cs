using System.Text;

namespace GameWatcher.App.Capture;

internal static class WindowFinder
{
    public static IntPtr FindByTitleSubstring(string substring)
    {
        substring = substring.Trim();
        IntPtr found = IntPtr.Zero;
        Win32.EnumWindows((h, l) =>
        {
            if (!Win32.IsWindowVisible(h)) return true;
            int len = Win32.GetWindowTextLength(h);
            if (len <= 0) return true;
            var sb = new StringBuilder(len + 1);
            Win32.GetWindowText(h, sb, sb.Capacity);
            var title = sb.ToString();
            if (title.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                found = h;
                return false; // stop
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}

