using System.Globalization;
using System.Windows.Data;

namespace SnmpNms.UI.Converters;

public class PollingStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPolling)
        {
            return isPolling ? "Polling 중..." : "대기 중";
        }
        return "대기 중";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

