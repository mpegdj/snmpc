using System.Windows.Input;

namespace SnmpNms.UI;

public static class MainWindowCommands
{
    public static readonly RoutedUICommand MapProperties = new("Map Properties", nameof(MapProperties), typeof(MainWindowCommands));
    public static readonly RoutedUICommand MapOpen = new("Open Map", nameof(MapOpen), typeof(MainWindowCommands));
    public static readonly RoutedUICommand MapQuickPoll = new("Quick Poll", nameof(MapQuickPoll), typeof(MainWindowCommands));
    public static readonly RoutedUICommand MapMibTable = new("MIB Table", nameof(MapMibTable), typeof(MainWindowCommands));
    public static readonly RoutedUICommand MapDelete = new("Delete", nameof(MapDelete), typeof(MainWindowCommands));
}


