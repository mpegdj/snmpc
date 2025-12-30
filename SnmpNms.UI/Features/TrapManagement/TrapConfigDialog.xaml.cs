using System.Windows;

namespace SnmpNms.UI.Features.TrapManagement;

public partial class TrapConfigDialog : Window
{
    public TrapConfigDialog(TrapManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
