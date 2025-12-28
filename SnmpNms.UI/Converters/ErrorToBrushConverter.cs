using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SnmpNms.UI.Converters;

/// <summary>
/// bool(IsError)를 Brush로 변환 - 에러면 빨간색, 아니면 기본색
/// </summary>
public class ErrorToBrushConverter : IValueConverter
{
    private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(255, 100, 100));
    private static readonly Brush NormalBrush = new SolidColorBrush(Color.FromRgb(0, 0, 0));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isError && isError)
        {
            return ErrorBrush;
        }
        return NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

