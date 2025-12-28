using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SnmpNms.UI.Converters;

/// <summary>
/// int 값이 0보다 크면 Visible, 그렇지 않으면 Collapsed 반환
/// </summary>
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && intValue > 0)
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

