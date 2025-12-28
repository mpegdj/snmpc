using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SnmpNms.UI.Views.EventLog;

public partial class EventLogTabControl : UserControl
{
    private int _lastItemCount = 0;

    public EventLogTabControl()
    {
        InitializeComponent();
        Loaded += EventLogTabControl_Loaded;
        DataContextChanged += EventLogTabControl_DataContextChanged;
        Loaded += EventLogTabControl_Loaded_SetTag;
    }

    private void EventLogTabControl_Loaded_SetTag(object sender, RoutedEventArgs e)
    {
        // 상위 컨트롤에서 MainViewModel 찾기
        DependencyObject? element = this;
        while (element != null)
        {
            // Visual Tree를 따라 올라가면서 DataContext 확인
            var parent = VisualTreeHelper.GetParent(element);
            if (parent is FrameworkElement fe && fe.DataContext is ViewModels.MainViewModel mainVm)
            {
                Tag = mainVm;
                
                // PropertyChanged 이벤트 구독하여 IsPollingRunning 변경 감지
                mainVm.PropertyChanged += MainViewModel_PropertyChanged;
                // 초기 상태 설정
                UpdateSpinnerAnimation(mainVm.IsPollingRunning);
                break;
            }
            element = parent;
        }
    }

    private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "IsPollingRunning")
        {
            // IsPollingRunning이 변경되면 애니메이션 시작/중지
            var mainVm = sender as ViewModels.MainViewModel;
            if (mainVm != null)
            {
                Tag = mainVm;
                UpdateSpinnerAnimation(mainVm.IsPollingRunning);
            }
        }
        // IsTrapListening 변경은 UI 바인딩으로 자동 업데이트됨
    }

    private void UpdateSpinnerAnimation(bool isRunning)
    {
        var storyboard = (Storyboard)Resources["SpinnerAnimation"];
        if (isRunning)
        {
            storyboard.Begin(spinnerIcon, true);
        }
        else
        {
            storyboard.Stop(spinnerIcon);
        }
    }

    private void EventLogTabControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        // DataGrid의 LoadingRow 이벤트 구독
        dataGridLog.LoadingRow += DataGridLog_LoadingRow;
    }

    private void EventLogTabControl_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        // DataContext가 변경되면 View의 변경 사항 감지
        if (e.OldValue is ViewModels.EventLogFilterViewModel oldViewModel)
        {
            // 이전 ViewModel의 Events 컬렉션 변경 구독 해제
            oldViewModel.Events.CollectionChanged -= Events_CollectionChanged;
        }

        if (e.NewValue is ViewModels.EventLogFilterViewModel newViewModel)
        {
            // 새 ViewModel의 Events 컬렉션 변경 구독
            newViewModel.Events.CollectionChanged += Events_CollectionChanged;
            // DataGrid에 바인딩된 후 항목 수를 확인하도록 지연
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                _lastItemCount = dataGridLog.Items.Count;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void Events_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 새 항목이 추가되면 자동으로 스크롤
        if (e.Action == NotifyCollectionChangedAction.Add && 
            dataGridLog.Items.Count > 0)
        {
            // UI 스레드에서 실행되도록 Dispatcher 사용
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                // 마지막 항목으로 스크롤
                if (dataGridLog.Items.Count > 0)
                {
                    dataGridLog.ScrollIntoView(dataGridLog.Items[dataGridLog.Items.Count - 1]);
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void DataGridLog_LoadingRow(object? sender, DataGridRowEventArgs e)
    {
        // 마지막 행이 로드될 때 자동으로 스크롤
        if (dataGridLog.Items.Count > 0 && 
            e.Row.GetIndex() == dataGridLog.Items.Count - 1 &&
            e.Row.GetIndex() >= _lastItemCount)
        {
            _lastItemCount = dataGridLog.Items.Count;
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                dataGridLog.ScrollIntoView(e.Row.Item);
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    private void BtnEventClear_Click(object sender, RoutedEventArgs e)
    {
        if (Tag is ViewModels.MainViewModel mainVm)
        {
            mainVm.ClearEvents();
        }
    }

    private void BtnEventSave_Click(object sender, RoutedEventArgs e)
    {
        if (Tag is ViewModels.MainViewModel mainVm)
        {
            mainVm.SaveEvents();
        }
    }
}


