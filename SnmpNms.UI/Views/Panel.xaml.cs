using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using SnmpNms.UI.ViewModels;
using SnmpNms.UI.Views.EventLog;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.Views;

public partial class BottomPanel : UserControl
{
    private DebugViewModel? _debugViewModel;
    private ComViewModel? _comViewModel;
    private LogViewModel? _logViewModel;
    
    public BottomPanel()
    {
        InitializeComponent();
    }
    
    /// <summary>
    /// MainViewModel 설정
    /// </summary>
    public void SetMainViewModel(MainViewModel viewModel)
    {
        // Log ViewModel 설정
        _logViewModel = viewModel.Log;
        if (logContent != null)
        {
            logContent.DataContext = viewModel.Log;
            viewModel.Log.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }
        
        // Debug ViewModel 설정
        _debugViewModel = viewModel.Debug;
        if (debugContent != null)
        {
            debugContent.DataContext = viewModel.Debug;
            viewModel.Debug.DebugLogs.CollectionChanged += DebugLogs_CollectionChanged;
        }
        
        // Com ViewModel 설정
        _comViewModel = viewModel.Com;
        if (comContent != null)
        {
            comContent.DataContext = viewModel.Com;
            viewModel.Com.ComLogs.CollectionChanged += ComLogs_CollectionChanged;
        }
    }
    
    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (chkLogAutoScroll?.IsChecked == true && e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (dgLog != null && dgLog.Items.Count > 0)
                {
                    dgLog.ScrollIntoView(dgLog.Items[dgLog.Items.Count - 1], null);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
    
    private void DebugLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (chkDebugAutoScroll?.IsChecked == true && e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (lvDebug != null && lvDebug.Items.Count > 0)
                {
                    lvDebug.ScrollIntoView(lvDebug.Items[lvDebug.Items.Count - 1]);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
    
    private void ComLogs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (chkComAutoScroll?.IsChecked == true && e.Action == NotifyCollectionChangedAction.Add)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (lvCom != null && lvCom.Items.Count > 0)
                {
                    lvCom.ScrollIntoView(lvCom.Items[lvCom.Items.Count - 1]);
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
        if (logContent == null || debugContent == null || comContent == null)
            return;
            
        // 모든 탭 숨기기
        logContent.Visibility = Visibility.Collapsed;
        debugContent.Visibility = Visibility.Collapsed;
        comContent.Visibility = Visibility.Collapsed;
        
        // 선택된 탭만 표시
        if (rbLog?.IsChecked == true)
        {
            logContent.Visibility = Visibility.Visible;
        }
        else if (rbDebug?.IsChecked == true)
        {
            debugContent.Visibility = Visibility.Visible;
        }
        else if (rbCom?.IsChecked == true)
        {
            comContent.Visibility = Visibility.Visible;
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
    
    private void BtnLogClear_Click(object sender, RoutedEventArgs e)
    {
        _logViewModel?.Clear();
    }
    
    private void BtnLogCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_logViewModel != null && dgLog != null)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in dgLog.Items)
            {
                if (item is SnmpNms.UI.Models.SnmpEventLog evLogEntry)
                {
                    sb.AppendLine($"{evLogEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{evLogEntry.Severity}] {evLogEntry.Device} {evLogEntry.Message}");
                }
            }
            var text = sb.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }
    }
    
    private void BtnDebugClear_Click(object sender, RoutedEventArgs e)
    {
        _debugViewModel?.Clear();
    }
    
    private void BtnDebugCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_debugViewModel != null)
        {
            var text = _debugViewModel.ExportToText();
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }
    }
    
    private void BtnLogSave_Click(object sender, RoutedEventArgs e)
    {
        // LogViewModel은 Events의 래퍼이므로 MainViewModel에서 처리하도록 위임하거나
        // 직접 다이얼로그 처리를 할 수 있습니다. 
        // 여기서는 MainViewModel을 찾아 호출하는 방식을 유도하거나 ViewModel에 메서드 노출 필요.
        // 현재 구조상 _logViewModel만 있으므로, _logViewModel에 SaveToFile을 추가하는 것이 정석입니다.
        _logViewModel?.SaveToFile();
    }
    
    private void BtnComClear_Click(object sender, RoutedEventArgs e)
    {
        _comViewModel?.Clear();
    }
    
    private void BtnComCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_comViewModel != null)
        {
            var text = _comViewModel.ExportToText();
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }
    }

    private void BtnComSave_Click(object sender, RoutedEventArgs e)
    {
        _comViewModel?.SaveToFile();
    }

    private void BtnDebugSave_Click(object sender, RoutedEventArgs e)
    {
        _debugViewModel?.SaveToFile();
    }

    public void ToggleVisibility()
    {
        Visibility = Visibility == Visibility.Visible 
            ? Visibility.Collapsed 
            : Visibility.Visible;
    }
}

