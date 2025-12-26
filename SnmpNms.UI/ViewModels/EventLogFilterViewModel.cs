using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Threading;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.ViewModels;

public enum EventLogScope
{
    All = 0,
    SelectedDevice = 1
}

public enum EventSeverityFilter
{
    Any = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

public class EventLogFilterViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<EventLogEntry> _events;
    private readonly Func<UiSnmpTarget?> _getSelectedDevice;
    private readonly Func<MapNode?> _getSelectedMapNode;

    public string Name { get; }
    public ICollectionView View { get; }
    public ObservableCollection<EventLogEntry> Events => _events;

    private string _filterInfo = "Polling ALL";
    public string FilterInfo
    {
        get => _filterInfo;
        private set
        {
            if (_filterInfo != value)
            {
                _filterInfo = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateFilterInfo()
    {
        try
        {
            var selectedMapNode = _getSelectedMapNode();
            System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] UpdateFilterInfo called: selectedMapNode={selectedMapNode?.Name ?? "null"}, NodeType={selectedMapNode?.NodeType}");
            System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] _getSelectedMapNode delegate: {(_getSelectedMapNode != null ? "not null" : "null")}");
            
            if (selectedMapNode == null)
            {
                System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] selectedMapNode is null, setting FilterInfo to 'Polling ALL'");
                FilterInfo = "Polling ALL";
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] selectedMapNode found: Name={selectedMapNode.Name}, NodeType={selectedMapNode.NodeType}, Target={selectedMapNode.Target?.IpAddress ?? "null"}");
            
            if (selectedMapNode.NodeType == MapNodeType.RootSubnet)
            {
                FilterInfo = "Polling ALL";
                System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] RootSubnet selected, FilterInfo='Polling ALL'");
            }
            else if (selectedMapNode.NodeType == MapNodeType.Device && selectedMapNode.Target != null)
            {
                FilterInfo = $"Device: {selectedMapNode.Name} ({selectedMapNode.Target.IpAddress})";
                System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] Device selected, FilterInfo='{FilterInfo}'");
            }
            else if (selectedMapNode.NodeType == MapNodeType.Subnet)
            {
                FilterInfo = $"Subnet: {selectedMapNode.Name}";
                System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] Subnet selected, FilterInfo='{FilterInfo}'");
            }
            else
            {
                FilterInfo = "Polling ALL";
                System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] Unknown NodeType, FilterInfo='Polling ALL'");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] UpdateFilterInfo error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] StackTrace: {ex.StackTrace}");
            FilterInfo = "Polling ALL";
        }
    }

    public IReadOnlyList<EventLogScope> AvailableScopes { get; } =
        new[] { EventLogScope.All, EventLogScope.SelectedDevice };

    public IReadOnlyList<EventSeverityFilter> AvailableSeverities { get; } =
        new[] { EventSeverityFilter.Any, EventSeverityFilter.Info, EventSeverityFilter.Warning, EventSeverityFilter.Error };

    private EventLogScope _scope = EventLogScope.All;
    public EventLogScope Scope
    {
        get => _scope;
        set
        {
            if (_scope == value) return;
            _scope = value;
            OnPropertyChanged();
            Refresh();
        }
    }

    private EventSeverityFilter _severity = EventSeverityFilter.Any;
    public EventSeverityFilter Severity
    {
        get => _severity;
        set
        {
            if (_severity == value) return;
            _severity = value;
            OnPropertyChanged();
            Refresh();
        }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value ?? "";
            OnPropertyChanged();
            Refresh();
        }
    }

    public EventLogFilterViewModel(
        string name,
        ObservableCollection<EventLogEntry> events,
        Func<UiSnmpTarget?> getSelectedDevice,
        INotifyPropertyChanged selectedDeviceNotifier,
        Func<MapNode?>? getSelectedMapNode = null,
        System.Collections.Specialized.INotifyCollectionChanged? selectedMapNodesNotifier = null)
    {
        Name = name;
        _events = events;
        _getSelectedDevice = getSelectedDevice;
        _getSelectedMapNode = getSelectedMapNode ?? (() => null);

        View = new ListCollectionView(_events);
        View.Filter = FilterPredicate;

        selectedDeviceNotifier.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "SelectedDevice") Refresh();
        };
        
        // SelectedMapNodes 변경 시 필터 업데이트
        if (selectedMapNodesNotifier != null)
        {
            System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] Registering CollectionChanged handler for SelectedMapNodes");
            selectedMapNodesNotifier.CollectionChanged += (_, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] CollectionChanged fired: Action={e.Action}, NewItems={e.NewItems?.Count ?? 0}, OldItems={e.OldItems?.Count ?? 0}");
                if (e.NewItems != null)
                {
                    foreach (var item in e.NewItems)
                    {
                        if (item is MapNode node)
                        {
                            System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] New item: {node.Name}, NodeType={node.NodeType}");
                        }
                    }
                }
                // CollectionChanged는 이미 UI 스레드에서 발생하므로 바로 업데이트
                UpdateFilterInfo();
                Refresh();
            };
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[EventLogFilterViewModel] selectedMapNodesNotifier is null, CollectionChanged handler not registered!");
        }
        
        // 초기 FilterInfo 설정
        UpdateFilterInfo();

        // Events 컬렉션 변경 시 View 자동 Refresh
        _events.CollectionChanged += (_, _) => Refresh();
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not EventLogEntry e) return false;

        // MapNode 선택에 따른 필터링
        var selectedMapNode = _getSelectedMapNode();
        if (selectedMapNode != null)
        {
            if (selectedMapNode.NodeType == MapNodeType.RootSubnet)
            {
                // RootSubnet 선택 시 모든 로그 표시 (필터링 없음)
            }
            else if (selectedMapNode.NodeType == MapNodeType.Device && selectedMapNode.Target != null)
            {
                // Device 선택 시 해당 device만
                var deviceKey = $"{selectedMapNode.Target.IpAddress}:{selectedMapNode.Target.Port}";
                if (!string.Equals(e.Device, deviceKey, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else if (selectedMapNode.NodeType == MapNodeType.Subnet)
            {
                // Subnet 선택 시 그 subnet과 하위 subnet의 모든 device
                var deviceKeys = GetDeviceKeysForMapNode(selectedMapNode);
                if (deviceKeys.Count > 0)
                {
                    // 선택된 Subnet에 속한 device만 표시
                    if (e.Device == null || !deviceKeys.Contains(e.Device, StringComparer.OrdinalIgnoreCase))
                        return false;
                }
                else
                {
                    // Subnet에 device가 없으면 아무것도 표시 안 함
                    return false;
                }
            }
        }
        else
        {
            // MapNode가 선택되지 않았을 때는 모든 로그 표시 (필터링 없음)
            // Scope 필터는 무시하고 모든 로그를 표시
        }

        if (Severity != EventSeverityFilter.Any)
        {
            var expected = Severity switch
            {
                EventSeverityFilter.Info => EventSeverity.Info,
                EventSeverityFilter.Warning => EventSeverity.Warning,
                EventSeverityFilter.Error => EventSeverity.Error,
                _ => EventSeverity.Info
            };

            if (e.Severity != expected) return false;
        }

        var q = (SearchText ?? "").Trim();
        if (!string.IsNullOrEmpty(q))
        {
            var hay = $"{e.Device} {e.Message}".ToLowerInvariant();
            if (!hay.Contains(q.ToLowerInvariant()))
                return false;
        }

        return true;
    }

    private HashSet<string> GetDeviceKeysForMapNode(MapNode node)
    {
        var deviceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Subnet 선택 시 그 subnet과 하위 subnet의 모든 device 수집
        if (node.NodeType == MapNodeType.Subnet)
        {
            CollectDeviceKeys(node, deviceKeys);
        }
        
        return deviceKeys;
    }

    private void CollectDeviceKeys(MapNode node, HashSet<string> deviceKeys)
    {
        foreach (var child in node.Children)
        {
            if (child.NodeType == MapNodeType.Device && child.Target != null)
            {
                deviceKeys.Add($"{child.Target.IpAddress}:{child.Target.Port}");
            }
            else if (child.NodeType == MapNodeType.Subnet)
            {
                CollectDeviceKeys(child, deviceKeys);
            }
        }
    }

    public void Refresh() => View.Refresh();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


