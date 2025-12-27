using System.Globalization;
using System.Windows.Data;

namespace SnmpNms.UI.Converters;

public class TrapStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isListening)
        {
            return isListening ? "Trap Listening" : "Trap Stopped";
        }
        return "Trap Stopped";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

