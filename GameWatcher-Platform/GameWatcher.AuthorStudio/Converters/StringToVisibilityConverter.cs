using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameWatcher.AuthorStudio.Converters;

/// <summary>
/// Converts string values to Visibility. Returns Visible if string is not null/empty, otherwise Collapsed.
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return string.IsNullOrEmpty(str) ? Visibility.Collapsed : Visibility.Visible;
        }
        
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException("StringToVisibilityConverter is one-way only");
    }
}
