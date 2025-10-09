using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GameWatcher.AuthorStudio.Converters;

/// <summary>
/// Converts activity log lines to colors based on their prefix symbols.
/// </summary>
public class LogLineColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string line)
            return Brushes.LimeGreen; // Default

        // Extract message part (after timestamp if present)
        var messagePart = line.Contains("]") ? line.Substring(line.IndexOf(']') + 1).TrimStart() : line;

        // Color-code based on message prefix
        if (messagePart.StartsWith("✓"))
            return Brushes.LimeGreen;      // Success/learned
        
        if (messagePart.StartsWith("⚠️") || messagePart.StartsWith("WARNING"))
            return Brushes.Orange;          // Warning
        
        if (messagePart.StartsWith("❌") || messagePart.StartsWith("ERROR"))
            return Brushes.Red;             // Error
        
        if (messagePart.StartsWith("ℹ️") || messagePart.StartsWith("INFO"))
            return Brushes.DodgerBlue;      // Info
        
        if (messagePart.StartsWith("[Activity]"))
            return Brushes.Cyan;            // Activity tracking
        
        // Default for regular log messages
        return Brushes.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
