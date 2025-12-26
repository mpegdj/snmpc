using System.Windows;
using System.Windows.Controls;
using SnmpNms.UI.Views.EventLog;

namespace SnmpNms.UI.Views;

public partial class BottomPanel : UserControl
{
    public BottomPanel()
    {
        InitializeComponent();
        
        // TabControl 선택 변경 시 ContentControl 업데이트
        tabPanel.SelectionChanged += TabPanel_SelectionChanged;
        
        // 초기 선택된 탭의 Content 설정
        UpdateContent();
    }

    private void TabPanel_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateContent();
    }

    private void UpdateContent()
    {
        if (tabPanel.SelectedItem is TabItem selectedTab)
        {
            contentArea.Content = selectedTab.Content;
        }
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        // Toggle panel visibility (will be handled by parent)
        var parent = Parent as FrameworkElement;
        if (parent != null)
        {
            if (parent.Height > MinHeight)
            {
                // Collapse
                parent.Height = MinHeight;
            }
            else
            {
                // Expand
                parent.Height = 220;
            }
        }
    }

    public void ToggleVisibility()
    {
        Visibility = Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
    }
}

