using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SnmpNms.Core.Models;

namespace SnmpNms.UI.Converters;

public class DeviceStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DeviceStatus status) return Brushes.Gray;

        return status switch
        {
            DeviceStatus.Up => Brushes.LimeGreen,
            DeviceStatus.Down => Brushes.Red,
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}


