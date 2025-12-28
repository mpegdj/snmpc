using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using SnmpNms.UI.Models;

namespace SnmpNms.UI.ViewModels;

/// <summary>
/// Log 탭의 ViewModel - trap과 polling 로그만 필터링
/// </summary>
public class LogViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<EventLogEntry> _sourceEvents;
    
    public ObservableCollection<EventLogEntry> LogEntries { get; } = new();
    
    public LogViewModel(ObservableCollection<EventLogEntry> sourceEvents)
    {
        _sourceEvents = sourceEvents;
        _sourceEvents.CollectionChanged += SourceEvents_CollectionChanged;
        Refresh();
    }
    
    private void SourceEvents_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Refresh();
    }
    
    /// <summary>
    /// trap과 polling 로그만 필터링
    /// </summary>
    public void Refresh()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var filtered = _sourceEvents
                .Where(e => IsTrapOrPollingLog(e))
                .ToList();
            
            LogEntries.Clear();
            foreach (var entry in filtered)
            {
                LogEntries.Add(entry);
            }
        });
    }
    
    /// <summary>
    /// trap 또는 polling 로그인지 확인
    /// </summary>
    private bool IsTrapOrPollingLog(EventLogEntry entry)
    {
        if (entry == null || string.IsNullOrEmpty(entry.Message))
            return false;
        
        var message = entry.Message;
        
        // Trap 로그: [Trap] 또는 [Trap Test]로 시작
        if (message.StartsWith("[Trap", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Polling 로그: [Polling] 또는 Polling 관련 메시지
        if (message.Contains("Polling", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Poll", StringComparison.OrdinalIgnoreCase))
            return true;
        
        return false;
    }
    
    /// <summary>
    /// 로그 전체 삭제
    /// </summary>
    public void Clear()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            LogEntries.Clear();
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

