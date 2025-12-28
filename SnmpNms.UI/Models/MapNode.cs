using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SnmpNms.Core.Models;

namespace SnmpNms.UI.Models;

public enum MapNodeType
{
    RootSubnet = 0,
    Subnet = 1,
    Device = 2,
    Goto = 3
}

public class MapNode : INotifyPropertyChanged
{
    public MapNodeType NodeType { get; }

    private string _name;
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public UiSnmpTarget? Target { get; }

    public MapNode? Parent { get; private set; }

    public ObservableCollection<MapNode> Children { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    // Map Graph View 위치 (Canvas.Left, Canvas.Top)
    private double _x;
    public double X
    {
        get => _x;
        set
        {
            if (Math.Abs(_x - value) < 0.001) return;
            _x = value;
            OnPropertyChanged();
        }
    }

    private double _y;
    public double Y
    {
        get => _y;
        set
        {
            if (Math.Abs(_y - value) < 0.001) return;
            _y = value;
            OnPropertyChanged();
        }
    }

    // SNMPc 스타일: 색상은 "가장 높은 우선순위" 상태를 표시(Down > Unknown > Up)
    private DeviceStatus _effectiveStatus = DeviceStatus.Unknown;
    public DeviceStatus EffectiveStatus
    {
        get => _effectiveStatus;
        private set
        {
            if (_effectiveStatus == value) return;
            _effectiveStatus = value;
            OnPropertyChanged();
        }
    }

    public string DisplayName =>
        NodeType switch
        {
            MapNodeType.Device => Target?.DisplayName ?? Name,
            _ => Name
        };

    public MapNode(MapNodeType nodeType, string name, UiSnmpTarget? target = null)
    {
        NodeType = nodeType;
        _name = name;
        Target = target;

        Children.CollectionChanged += ChildrenOnCollectionChanged;

        if (Target is not null)
        {
            Target.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(UiSnmpTarget.Status))
                {
                    RecomputeEffectiveStatus();
                    Parent?.RecomputeEffectiveStatus();
                }
                else if (args.PropertyName is nameof(UiSnmpTarget.Alias) or nameof(UiSnmpTarget.IpAddress) or nameof(UiSnmpTarget.Port))
                {
                    // Device 표시명(DisplayName)이 바뀔 수 있으므로 UI 갱신
                    OnPropertyChanged(nameof(DisplayName));
                }
            };
        }

        RecomputeEffectiveStatus();
    }

    public void AddChild(MapNode child)
    {
        child.Parent = this;
        Children.Add(child);
        RecomputeEffectiveStatus();
    }

    public void RemoveChild(MapNode child)
    {
        if (Children.Remove(child))
        {
            child.Parent = null;
            RecomputeEffectiveStatus();
        }
    }

    private void ChildrenOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (MapNode n in e.NewItems)
            {
                n.Parent = this;
                n.PropertyChanged += ChildOnPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (MapNode n in e.OldItems)
            {
                n.PropertyChanged -= ChildOnPropertyChanged;
            }
        }

        RecomputeEffectiveStatus();
    }

    private void ChildOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EffectiveStatus))
        {
            RecomputeEffectiveStatus();
        }
    }

    public void RecomputeEffectiveStatus()
    {
        if (NodeType == MapNodeType.Device)
        {
            EffectiveStatus = Target?.Status ?? DeviceStatus.Unknown;
            return;
        }

        if (Children.Count == 0)
        {
            EffectiveStatus = DeviceStatus.Unknown;
            return;
        }

        // highest priority among descendants (Down > Warning > Notice > Unknown > Up)
        var statuses = Children.Select(c => c.EffectiveStatus).ToList();
        if (statuses.Any(s => s == DeviceStatus.Down)) EffectiveStatus = DeviceStatus.Down;
        else if (statuses.Any(s => s == DeviceStatus.Warning)) EffectiveStatus = DeviceStatus.Warning;
        else if (statuses.Any(s => s == DeviceStatus.Notice)) EffectiveStatus = DeviceStatus.Notice;
        else if (statuses.Any(s => s == DeviceStatus.Unknown)) EffectiveStatus = DeviceStatus.Unknown;
        else EffectiveStatus = DeviceStatus.Up;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


