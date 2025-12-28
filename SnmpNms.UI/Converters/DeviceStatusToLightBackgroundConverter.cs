using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SnmpNms.Core.Models;

namespace SnmpNms.UI.Converters;

/// <summary>
/// Map Graph View용 배경색 Converter (하얀색/밝은 테마)
/// 폴링 시작 전: 투명 (하얀색 배경)
/// 폴링 시작 후: 상태에 따라 밝은 색상 표시
/// </summary>
public class DeviceStatusToLightBackgroundConverter : IValueConverter
{
    // 밝은 테마에 어울리는 색상
    private static readonly SolidColorBrush UpBrush = new(Color.FromRgb(0xD4, 0xED, 0xDA));      // 밝은 녹색
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xFF, 0xF3, 0xCD)); // 밝은 노랑
    private static readonly SolidColorBrush DownBrush = new(Color.FromRgb(0xF8, 0xD7, 0xDA));    // 밝은 빨강
    private static readonly SolidColorBrush UnknownBrush = Brushes.Transparent;                   // 투명 (폴링 전)

    static DeviceStatusToLightBackgroundConverter()
    {
        UpBrush.Freeze();
        WarningBrush.Freeze();
        DownBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DeviceStatus status) return UnknownBrush;

        return status switch
        {
            DeviceStatus.Up => UpBrush,
            DeviceStatus.Down => DownBrush,
            DeviceStatus.Unknown => UnknownBrush,  // 폴링 전에는 투명
            _ => UnknownBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

