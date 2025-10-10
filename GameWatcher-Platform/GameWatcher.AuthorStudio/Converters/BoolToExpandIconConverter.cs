using System.Globalization;
using System.Windows.Data;

namespace GameWatcher.AuthorStudio.Converters;

/// <summary>
/// Converts a boolean to expand/collapse icon.
/// True (expanded) = "▼", False (collapsed) = "▶"
/// </summary>
public class BoolToExpandIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "▼" : "▶";
        }
        return "▶";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
