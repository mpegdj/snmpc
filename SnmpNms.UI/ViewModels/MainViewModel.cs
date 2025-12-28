using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SnmpNms.UI.Models;
using SnmpNms.UI.Services;

namespace SnmpNms.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    // Map Selection Tree roots (SNMPc: Root Subnet)
    public ObservableCollection<MapNode> MapRoots { get; } = new();
    public MapNode RootSubnet { get; }
    public MapNode DefaultSubnet { get; }

    // Map Selection multi-select (SNMPc: ctrl/shift)
    public ObservableCollection<MapNode> SelectedMapNodes { get; } = new();

    // Debug/UX: Device tab list (all device nodes)
    public ObservableCollection<MapNode> DeviceNodes { get; } = new();

    private MapNode? _selectedDeviceNode;
    public MapNode? SelectedDeviceNode
    {
        get => _selectedDeviceNode;
        set
        {
            if (ReferenceEquals(_selectedDeviceNode, value)) return;
            _selectedDeviceNode = value;
            OnPropertyChanged();

            // keep existing bindings working (Device tab details / SNMP Test)
            SelectedDevice = value?.Target;
        }
    }

    public ObservableCollection<SnmpEventLog> Events { get; } = new();

    private UiSnmpTarget? _selectedDevice;
    public UiSnmpTarget? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (ReferenceEquals(_selectedDevice, value)) return;
            _selectedDevice = value;
            OnPropertyChanged();
        }
    }

    public EventLogFilterViewModel CurrentLog { get; }
    public EventLogFilterViewModel HistoryLog { get; }
    public EventLogFilterViewModel Custom1Log { get; }
    public EventLogFilterViewModel Custom2Log { get; }
    public EventLogFilterViewModel Custom3Log { get; }
    public EventLogFilterViewModel Custom4Log { get; }
    public EventLogFilterViewModel Custom5Log { get; }
    public EventLogFilterViewModel Custom6Log { get; }
    public EventLogFilterViewModel Custom7Log { get; }
    public EventLogFilterViewModel Custom8Log { get; }
    
    // Output (Traffic Log) - Debug용으로 사용
    public OutputViewModel Output { get; } = new();
    
    // Debug (앱 동작 및 명령어 실행)
    public DebugViewModel Debug { get; } = new();
    
    // Com (통신 hex와 text)
    public ComViewModel Com { get; } = new();
    
    // Log (trap과 polling만)
    public LogViewModel Log { get; }
    
    // Log Save Services
    public LogSaveService LogSaveService { get; } = new();
    public OutputSaveService OutputSaveService { get; } = new();

    private bool _isPollingRunning;
    public bool IsPollingRunning
    {
        get => _isPollingRunning;
        set
        {
            if (_isPollingRunning != value)
            {
                _isPollingRunning = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _isTrapListening;
    public bool IsTrapListening
    {
        get => _isTrapListening;
        set
        {
            if (_isTrapListening != value)
            {
                _isTrapListening = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _port161TxPulse;
    public bool Port161TxPulse { get => _port161TxPulse; set { _port161TxPulse = value; OnPropertyChanged(); } }

    private bool _port161RxPulse;
    public bool Port161RxPulse { get => _port161RxPulse; set { _port161RxPulse = value; OnPropertyChanged(); } }

    private bool _port162RxPulse;
    public bool Port162RxPulse { get => _port162RxPulse; set { _port162RxPulse = value; OnPropertyChanged(); } }

    private bool _port162TxPulse;
    public bool Port162TxPulse { get => _port162TxPulse; set { _port162TxPulse = value; OnPropertyChanged(); } }

    private bool _trapPulse;
    public bool TrapPulse { get => _trapPulse; set { _trapPulse = value; OnPropertyChanged(); } }

    private bool _errorPulse;
    public bool ErrorPulse { get => _errorPulse; set { _errorPulse = value; OnPropertyChanged(); } }

    private bool _noticePulse;
    public bool NoticePulse { get => _noticePulse; set { _noticePulse = value; OnPropertyChanged(); } }

    private bool _warningPulse;
    public bool WarningPulse { get => _warningPulse; set { _warningPulse = value; OnPropertyChanged(); } }

    private bool _infoPulse;
    public bool InfoPulse { get => _infoPulse; set { _infoPulse = value; OnPropertyChanged(); } }

    public MainViewModel()
    {
        RootSubnet = new MapNode(MapNodeType.RootSubnet, "Root Subnet");
        DefaultSubnet = new MapNode(MapNodeType.Subnet, "Default");
        RootSubnet.AddChild(DefaultSubnet);
        MapRoots.Add(RootSubnet);

        // UX: 첫 실행 시 트리가 접혀있으면 "Device가 안 보인다"로 느껴져서 기본 확장
        RootSubnet.IsExpanded = true;
        DefaultSubnet.IsExpanded = true;

        // 각 탭마다 독립 필터(스코프/Severity/검색)를 갖는다.
        // CurrentLog는 MapNode 선택에 따라 필터링됨
        CurrentLog = new EventLogFilterViewModel("Current", Events, () => SelectedDevice, this, () => SelectedMapNodes.FirstOrDefault(), SelectedMapNodes);
        HistoryLog = new EventLogFilterViewModel("History", Events, () => SelectedDevice, this);
        Custom1Log = new EventLogFilterViewModel("Custom 1", Events, () => SelectedDevice, this);
        Custom2Log = new EventLogFilterViewModel("Custom 2", Events, () => SelectedDevice, this);
        Custom3Log = new EventLogFilterViewModel("Custom 3", Events, () => SelectedDevice, this);
        Custom4Log = new EventLogFilterViewModel("Custom 4", Events, () => SelectedDevice, this);
        Custom5Log = new EventLogFilterViewModel("Custom 5", Events, () => SelectedDevice, this);
        Custom6Log = new EventLogFilterViewModel("Custom 6", Events, () => SelectedDevice, this);
        Custom7Log = new EventLogFilterViewModel("Custom 7", Events, () => SelectedDevice, this);
        Custom8Log = new EventLogFilterViewModel("Custom 8", Events, () => SelectedDevice, this);
        
        // Log (trap과 polling만)
        Log = new LogViewModel(Events);
    }

    public void AddEvent(EventSeverity severity, string? device, string message)
    {
        // System 메시지는 Debug에만 기록하고 트래픽 로그(Events)에는 넣지 않음
        if (message.StartsWith("[System]", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogSystem(message);
            return;
        }

        // Pulse Triggers
        if (severity == EventSeverity.Error) TriggerErrorPulse();
        else if (severity == EventSeverity.Notice) TriggerNoticePulse();
        else if (severity == EventSeverity.Warning) TriggerWarningPulse();
        else if (severity == EventSeverity.Info) TriggerInfoPulse();

        var entry = new SnmpEventLog(DateTime.Now, severity, device, message);
        
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Events.Add(entry);
            
            // 메모리 제한 적용
            if (Events.Count > LogSaveService.MaxLinesInMemory)
            {
                Events.RemoveAt(0);
            }
            
            // 필터링된 뷰 갱신
            CurrentLog.Refresh();
        });
        
        // 파일 저장
        if (LogSaveService.IsEnabled)
        {
            LogSaveService.SaveLogEntry(entry);
        }
    }

    public void TriggerPort161TxPulse() { Port161TxPulse = true; System.Threading.Tasks.Task.Delay(300).ContinueWith(_ => Port161TxPulse = false); }
    public void TriggerPort161RxPulse(bool success) { Port161RxPulse = true; System.Threading.Tasks.Task.Delay(300).ContinueWith(_ => Port161RxPulse = false); }
    public void TriggerPort162TxPulse() { Port162TxPulse = true; System.Threading.Tasks.Task.Delay(300).ContinueWith(_ => Port162TxPulse = false); }
    public void TriggerPort162RxPulse() { Port162RxPulse = true; System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => Port162RxPulse = false); }
    private void TriggerTrapPulse() { TrapPulse = true; System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => TrapPulse = false); }
    private void TriggerErrorPulse() { ErrorPulse = true; System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ => ErrorPulse = false); }
    private void TriggerNoticePulse() { NoticePulse = true; System.Threading.Tasks.Task.Delay(800).ContinueWith(_ => NoticePulse = false); }
    private void TriggerWarningPulse() { WarningPulse = true; System.Threading.Tasks.Task.Delay(1000).ContinueWith(_ => WarningPulse = false); }
    private void TriggerInfoPulse() { InfoPulse = true; System.Threading.Tasks.Task.Delay(500).ContinueWith(_ => InfoPulse = false); }

    public void AddSystemInfo(string message)
    {
        AddEvent(EventSeverity.Info, null, message);
        // Debug에도 기록
        Debug.LogSystem(message);
    }

    public void ClearEvents()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Events.Clear();
            CurrentLog.Refresh();
        });
    }

    public void SaveEvents()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv",
            FileName = $"EventLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var ev in Events)
                {
                    sb.AppendLine($"[{ev.TimestampString}] [{ev.Severity}] [{ev.Device ?? "System"}] {ev.Message}");
                }
                System.IO.File.WriteAllText(sfd.FileName, sb.ToString());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save log: {ex.Message}");
            }
        }
    }

    public MapNode AddDeviceToSubnet(UiSnmpTarget target, MapNode? subnet = null)
    {
        subnet ??= DefaultSubnet;
        var node = new MapNode(MapNodeType.Device, target.DisplayName, target);
        subnet.AddChild(node);
        if (!DeviceNodes.Contains(node)) DeviceNodes.Add(node);
        return node;
    }

    public void RemoveDeviceNode(MapNode node)
    {
        if (node.NodeType != MapNodeType.Device) return;
        DeviceNodes.Remove(node);
        if (ReferenceEquals(SelectedDeviceNode, node)) SelectedDeviceNode = null;
    }

    public MapNode AddSubnet(string name, MapNode? parentSubnet = null)
    {
        parentSubnet ??= DefaultSubnet;
        var node = new MapNode(MapNodeType.Subnet, name);
        parentSubnet.AddChild(node);
        return node;
    }

    public MapNode AddGoto(string name, string gotoSubnetName, MapNode? parentSubnet = null)
    {
        parentSubnet ??= DefaultSubnet;
        // Address를 별도 보관 구조가 없어서, 우선 표시용 Name에 함께 담는다(후속에서 속성 모델로 확장)
        var display = string.IsNullOrWhiteSpace(gotoSubnetName) ? name : $"{name} -> {gotoSubnetName}";
        var node = new MapNode(MapNodeType.Goto, display);
        parentSubnet.AddChild(node);
        return node;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


