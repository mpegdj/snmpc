using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SnmpNms.UI.Views;

public partial class SidebarMapView : UserControl
{
    public event MouseButtonEventHandler? MapNodeTextMouseLeftButtonDown;

    public SidebarMapView()
    {
        InitializeComponent();
    }

    private void MapNodeText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        MapNodeTextMouseLeftButtonDown?.Invoke(sender, e);
    }
    
    // MainWindow에서 직접 접근할 수 있도록
    public TreeView TreeView => tvDevices;
}
