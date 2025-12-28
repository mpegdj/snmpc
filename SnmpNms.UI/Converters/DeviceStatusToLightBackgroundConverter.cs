using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SnmpNms.Core.Models;

namespace SnmpNms.UI.Converters;

/// <summary>
/// Map Graph View용 배경색 Converter (하얀색/밝은 테마)
/// 폴링 시작 전: 투명 또는 밝은 회색 (parameter에 따라)
/// 폴링 시작 후: 상태에 따라 밝은 색상 표시
/// </summary>
public class DeviceStatusToLightBackgroundConverter : IValueConverter
{
    // 밝은 테마에 어울리는 색상
    private static readonly SolidColorBrush UpBrush = new(Colors.LightGreen);    // 밝은 초록
    private static readonly SolidColorBrush DownBrush = new(Colors.Red);         // 완전 빨강
    private static readonly SolidColorBrush WarningBrush = new(Colors.Orange);   // 주황 (Warning)
    private static readonly SolidColorBrush NoticeBrush = new(Colors.Yellow);    // 노랑 (Notice)
    private static readonly SolidColorBrush UnknownBrush = Brushes.Transparent;                   
    private static readonly SolidColorBrush HeaderUnknownBrush = new(Color.FromRgb(0xF5, 0xF5, 0xF5)); 

    static DeviceStatusToLightBackgroundConverter()
    {
        UpBrush.Freeze();
        DownBrush.Freeze();
        WarningBrush.Freeze();
        NoticeBrush.Freeze();
        HeaderUnknownBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DeviceStatus status) return UnknownBrush;
        
        var isHeader = parameter is string str && str.Equals("Header", StringComparison.OrdinalIgnoreCase);

        return status switch
        {
            DeviceStatus.Up => UpBrush,
            DeviceStatus.Down => DownBrush,
            DeviceStatus.Warning => WarningBrush,
            DeviceStatus.Notice => NoticeBrush,
            DeviceStatus.Unknown => isHeader ? HeaderUnknownBrush : UnknownBrush,
            _ => isHeader ? HeaderUnknownBrush : UnknownBrush
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

