using System.Windows;
using System.Windows.Controls;

namespace SnmpNms.UI.Views;

public partial class Sidebar : UserControl
{
    public event EventHandler<ActivityBarView>? ViewChanged;
    
    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(Sidebar), 
            new PropertyMetadata("EXPLORER"));

    public static readonly DependencyProperty CurrentContentProperty =
        DependencyProperty.Register(nameof(CurrentContent), typeof(object), typeof(Sidebar),
            new PropertyMetadata(null, OnCurrentContentChanged));

    private static void OnCurrentContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Sidebar sidebar && sidebar.contentArea != null)
        {
            sidebar.contentArea.Content = e.NewValue;
        }
    }

    private ActivityBarView _currentView = ActivityBarView.Map;

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public object? CurrentContent
    {
        get => GetValue(CurrentContentProperty);
        set => SetValue(CurrentContentProperty, value);
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

    public Sidebar()
    {
        InitializeComponent();
        UpdateButtonStyles();
    }

    private void UpdateButtonStyles()
    {
        // Map 버튼
        btnMap.Style = _currentView == ActivityBarView.Map 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
        iconMap.Style = _currentView == ActivityBarView.Map
            ? (Style)FindResource("ActivityBarIconActiveStyle")
            : (Style)FindResource("ActivityBarIconStyle");
        
        // MIB 버튼
        btnMib.Style = _currentView == ActivityBarView.Mib 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
        iconMib.Style = _currentView == ActivityBarView.Mib
            ? (Style)FindResource("ActivityBarIconActiveStyle")
            : (Style)FindResource("ActivityBarIconStyle");
        
        // Search 버튼
        btnSearch.Style = _currentView == ActivityBarView.Search 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
        iconSearch.Style = _currentView == ActivityBarView.Search
            ? (Style)FindResource("ActivityBarIconActiveStyle")
            : (Style)FindResource("ActivityBarIconStyle");
        
        // Event Log 버튼
        btnEventLog.Style = _currentView == ActivityBarView.EventLog 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
        iconEventLog.Style = _currentView == ActivityBarView.EventLog
            ? (Style)FindResource("ActivityBarIconActiveStyle")
            : (Style)FindResource("ActivityBarIconStyle");
        
        // Settings 버튼
        btnSettings.Style = _currentView == ActivityBarView.Settings 
            ? (Style)FindResource("ActivityBarButtonActiveStyle")
            : (Style)FindResource("ActivityBarButtonStyle");
        iconSettings.Style = _currentView == ActivityBarView.Settings
            ? (Style)FindResource("ActivityBarIconActiveStyle")
            : (Style)FindResource("ActivityBarIconStyle");
    }

    private void BtnMap_Click(object sender, RoutedEventArgs e)
    {
        CurrentView = ActivityBarView.Map;
    }

    private void BtnMib_Click(object sender, RoutedEventArgs e)
    {
        CurrentView = ActivityBarView.Mib;
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
    
    private void BtnToggleSearch_Click(object sender, RoutedEventArgs e)
    {
        // 현재 Content가 SidebarMapView인 경우 검색 패널 토글
        if (contentArea.Content is SidebarMapView mapView)
        {
            mapView.OpenSearch();
        }
        else if (contentArea.Content is SidebarMibView mibView)
        {
            mibView.OpenSearch();
        }
    }
}

