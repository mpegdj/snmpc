using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using SnmpNms.UI.ViewModels;
using SnmpNms.UI.Views.EventLog;

namespace SnmpNms.UI.Views;

public partial class BottomPanel : UserControl
{
    private OutputViewModel? _outputViewModel;
    
    public BottomPanel()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// OutputViewModel 설정
    /// </summary>
    public void SetOutputViewModel(OutputViewModel outputViewModel)
    {
        _outputViewModel = outputViewModel;
        
        // Output ListView에 바인딩
        lvOutput.ItemsSource = outputViewModel.TrafficLogs;
        
        // 자동 스크롤 설정
        outputViewModel.TrafficLogs.CollectionChanged += TrafficLogs_CollectionChanged;
    }
    
    private void TrafficLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (chkAutoScroll?.IsChecked == true && e.Action == NotifyCollectionChangedAction.Add)
        {
            // 자동 스크롤
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (lvOutput != null && lvOutput.Items.Count > 0)
                {
                    lvOutput.ScrollIntoView(lvOutput.Items[lvOutput.Items.Count - 1]);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void TabRadio_Checked(object sender, RoutedEventArgs e)
    {
        UpdateTabVisibility();
    }
    
    private void UpdateTabVisibility()
    {
        if (eventLogContent == null || outputContent == null || terminalContent == null)
            return;
            
        // 모든 탭 숨기기
        eventLogContent.Visibility = Visibility.Collapsed;
        outputContent.Visibility = Visibility.Collapsed;
        terminalContent.Visibility = Visibility.Collapsed;
        
        // 선택된 탭만 표시
        if (rbEventLog?.IsChecked == true)
        {
            eventLogContent.Visibility = Visibility.Visible;
        }
        else if (rbOutput?.IsChecked == true)
        {
            outputContent.Visibility = Visibility.Visible;
        }
        else if (rbTerminal?.IsChecked == true)
        {
            terminalContent.Visibility = Visibility.Visible;
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
    
    private void BtnOutputClear_Click(object sender, RoutedEventArgs e)
    {
        _outputViewModel?.Clear();
    }
    
    private void BtnOutputCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_outputViewModel != null)
        {
            var text = _outputViewModel.ExportToText();
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
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

