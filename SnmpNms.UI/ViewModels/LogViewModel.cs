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
    private readonly ObservableCollection<SnmpEventLog> _sourceEvents;
    
    public ObservableCollection<SnmpEventLog> LogEntries { get; } = new();
    
    public LogViewModel(ObservableCollection<SnmpEventLog> sourceEvents)
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
    private bool IsTrapOrPollingLog(SnmpEventLog entry)
    {
        return SnmpEventLog.IsTrafficLog(entry.Message);
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

    public void SaveToFile()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt",
            FileName = $"TrafficLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var entry in LogEntries)
                {
                    sb.AppendLine($"{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{entry.Severity}] {entry.Device} {entry.Message}");
                }
                System.IO.File.WriteAllText(sfd.FileName, sb.ToString());
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save log: {ex.Message}");
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

