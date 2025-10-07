using System.Globalization;
using System.Windows.Data;

namespace GameWatcher.Studio.Converters;

public class InvertBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}

public class BooleanToLoadedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? "Loaded" : "Available";
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}