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

/// <summary>
/// Converts numeric values to Double for Slider bindings (Minimum, Maximum, Value)
/// </summary>
public class NumericToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return 0.0;

        return System.Convert.ToDouble(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return 0;

        // Convert back to original type based on targetType
        if (targetType == typeof(int) || targetType == typeof(int?))
            return System.Convert.ToInt32(value);
        
        return System.Convert.ToDouble(value);
    }
}

/// <summary>
/// Safely converts values to Boolean for CheckBox bindings
/// </summary>
public class SafeBooleanConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue;
        
        // Return null for non-boolean values (unchecked state)
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue;
        
        return false;
    }
}