using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
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

    public string Name { get; }
    public ICollectionView View { get; }

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
        INotifyPropertyChanged selectedDeviceNotifier)
    {
        Name = name;
        _events = events;
        _getSelectedDevice = getSelectedDevice;

        View = new ListCollectionView(_events);
        View.Filter = FilterPredicate;

        selectedDeviceNotifier.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == "SelectedDevice") Refresh();
        };
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not EventLogEntry e) return false;

        if (Scope == EventLogScope.SelectedDevice)
        {
            var sel = _getSelectedDevice();
            var selKey = sel is null ? null : $"{sel.IpAddress}:{sel.Port}";
            if (!string.Equals(e.Device, selKey, StringComparison.OrdinalIgnoreCase))
                return false;
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

    public void Refresh() => View.Refresh();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}


