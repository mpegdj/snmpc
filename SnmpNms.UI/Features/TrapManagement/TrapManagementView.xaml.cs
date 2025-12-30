using System.Windows;
using System.Windows.Controls;

namespace SnmpNms.UI.Features.TrapManagement;

public partial class TrapManagementView : UserControl
{
    public TrapManagementView()
    {
        InitializeComponent();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TrapManagementViewModel vm)
        {
            await vm.RefreshTrapTableAsync();
        }
    }

    private async void BtnRegister_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TrapManagementViewModel vm)
        {
            await vm.RegisterNmsAsync();
        }
    }
}
