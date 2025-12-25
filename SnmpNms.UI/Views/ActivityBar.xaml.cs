using System.Windows;
using System.Windows.Controls;

namespace SnmpNms.UI.Views;

public enum ActivityBarView
{
    Map,
    Search,
    EventLog,
    Settings
}

public partial class ActivityBar : UserControl
{
    public event EventHandler<ActivityBarView>? ViewChanged;
    
    private ActivityBarView _currentView = ActivityBarView.Map;

    public ActivityBar()
    {
        InitializeComponent();
        UpdateButtonStyles();
    }

    public ActivityBarView CurrentView
    {
        get => _currentView;
        set
        {
            if (_currentView != value)
            {
                _currentView = value;
                UpdateButtonStyles();
                ViewChanged?.Invoke(this, value);
            }
        }
    }

    private void UpdateButtonStyles()
    {
        btnMap.Style = _currentView == ActivityBarView.Map 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
        
        btnSearch.Style = _currentView == ActivityBarView.Search 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
        
        btnEventLog.Style = _currentView == ActivityBarView.EventLog 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
        
        btnSettings.Style = _currentView == ActivityBarView.Settings 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
    }

    private void BtnMap_Click(object sender, RoutedEventArgs e)
    {
        CurrentView = ActivityBarView.Map;
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        CurrentView = ActivityBarView.Search;
    }

    private void BtnEventLog_Click(object sender, RoutedEventArgs e)
    {
        CurrentView = ActivityBarView.EventLog;
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        CurrentView = ActivityBarView.Settings;
    }
}

