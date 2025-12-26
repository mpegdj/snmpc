using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SnmpNms.UI.Models;

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

    public ObservableCollection<EventLogEntry> Events { get; } = new();

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
    }

    public void AddEvent(EventSeverity severity, string? device, string message)
    {
        Events.Add(new EventLogEntry(DateTime.Now, severity, device, message));
        // 기본 탭은 마지막에 추가된 이벤트가 보이도록 Current만 Refresh
        CurrentLog.Refresh();
    }

    public void AddSystemInfo(string message) => AddEvent(EventSeverity.Info, null, message);

    public void ClearEvents() => Events.Clear();

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


