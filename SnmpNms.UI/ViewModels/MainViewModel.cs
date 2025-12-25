using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<UiSnmpTarget> Devices { get; } = new();

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

    public MainViewModel()
    {
        // 각 탭마다 독립 필터(스코프/Severity/검색)를 갖는다.
        CurrentLog = new EventLogFilterViewModel("Current", Events, () => SelectedDevice, this);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


