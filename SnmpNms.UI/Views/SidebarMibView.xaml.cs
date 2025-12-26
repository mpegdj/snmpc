using System.Windows;
using System.Windows.Controls;
using SnmpNms.Core.Models;

namespace SnmpNms.UI.Views;

public partial class SidebarMibView : UserControl
{
    public event RoutedPropertyChangedEventHandler<object>? SelectedItemChanged;
    public event RoutedEventHandler? MibTreeGetClick;
    public event RoutedEventHandler? MibTreeGetNextClick;
    public event RoutedEventHandler? MibTreeWalkClick;
    public event RoutedEventHandler? MibTreeViewTableClick;
    public event RoutedEventHandler? MibTreeCopyOidClick;
    public event RoutedEventHandler? MibTreeCopyNameClick;

    public SidebarMibView()
    {
        InitializeComponent();
        treeMib.SelectedItemChanged += (s, e) => SelectedItemChanged?.Invoke(s, e);
    }
    
    // MainWindow에서 직접 접근할 수 있도록
    public TreeView TreeView => treeMib;
    
    private void MibTreeGet_Click(object sender, RoutedEventArgs e)
    {
        MibTreeGetClick?.Invoke(sender, e);
    }
    
    private void MibTreeGetNext_Click(object sender, RoutedEventArgs e)
    {
        MibTreeGetNextClick?.Invoke(sender, e);
    }
    
    private void MibTreeWalk_Click(object sender, RoutedEventArgs e)
    {
        MibTreeWalkClick?.Invoke(sender, e);
    }
    
    private void MibTreeViewTable_Click(object sender, RoutedEventArgs e)
    {
        MibTreeViewTableClick?.Invoke(sender, e);
    }
    
    private void MibTreeCopyOid_Click(object sender, RoutedEventArgs e)
    {
        MibTreeCopyOidClick?.Invoke(sender, e);
    }
    
    private void MibTreeCopyName_Click(object sender, RoutedEventArgs e)
    {
        MibTreeCopyNameClick?.Invoke(sender, e);
    }
}

