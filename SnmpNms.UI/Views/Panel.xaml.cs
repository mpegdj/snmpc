using System.Windows;
using System.Windows.Controls;
using SnmpNms.UI.Views.EventLog;

namespace SnmpNms.UI.Views;

public partial class BottomPanel : UserControl
{
    private EventLogTabControl? _eventLogControl;

    public BottomPanel()
    {
        InitializeComponent();
        InitializeEventLog();
    }

    private void InitializeEventLog()
    {
        // Event Log content will be set from MainWindow
        // For now, create a placeholder
    }

    public void SetEventLogContent(EventLogTabControl eventLogControl)
    {
        _eventLogControl = eventLogControl;
        tabEventLog.Content = eventLogControl;
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

