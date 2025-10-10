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
/// Returns 0.0 for non-numeric types (strings, arrays, nulls)
/// </summary>
public class NumericToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return 0.0;

        // Don't try to convert non-numeric types
        if (value is string || value is bool || value is Array)
            return 0.0;

        try
        {
            return System.Convert.ToDouble(value);
        }
        catch (Exception)
        {
            // Return safe default for any conversion errors
            return 0.0;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return 0;

        try
        {
            // Convert back to original type based on targetType
            if (targetType == typeof(int) || targetType == typeof(int?))
                return System.Convert.ToInt32(value);
            
            return System.Convert.ToDouble(value);
        }
        catch (Exception)
        {
            return 0;
        }
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

/// <summary>
/// Converts string array to newline-separated string for display
/// </summary>
public class StringArrayToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string[] stringArray)
            return string.Join(Environment.NewLine, stringArray);
        
        if (value is string stringValue)
            return stringValue;
        
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue)
            return stringValue.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
        
        return Array.Empty<string>();
    }
}