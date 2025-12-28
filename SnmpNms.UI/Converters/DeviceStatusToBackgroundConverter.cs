using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SnmpNms.Core.Models;

namespace SnmpNms.UI.Converters;

/// <summary>
/// Map Graph View용 배경색 Converter (어두운 톤)
/// </summary>
public class DeviceStatusToBackgroundConverter : IValueConverter
{
    // VSCode 다크 테마에 어울리는 어두운 톤
    private static readonly SolidColorBrush UpBrush = new(Color.FromRgb(0x2D, 0x4A, 0x3E));      // 어두운 녹색
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0x4A, 0x4A, 0x2D)); // 어두운 노랑
    private static readonly SolidColorBrush DownBrush = new(Color.FromRgb(0x4A, 0x2D, 0x2D));    // 어두운 빨강
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromRgb(0x3C, 0x3C, 0x3C)); // 회색

    static DeviceStatusToBackgroundConverter()
    {
        UpBrush.Freeze();
        WarningBrush.Freeze();
        DownBrush.Freeze();
        UnknownBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DeviceStatus status) return UnknownBrush;

        return status switch
        {
            DeviceStatus.Up => UpBrush,
            DeviceStatus.Down => DownBrush,
            _ => UnknownBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

