using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace SnmpNms.UI.ViewModels;

/// <summary>
/// Debug 로그 항목 (앱 동작 및 명령어 실행)
/// </summary>
public class DebugLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Category { get; set; } = ""; // "System", "Command", "Action" 등
    public string Message { get; set; } = "";
    public bool IsError { get; set; }
    
    public string TimestampString => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
    
    public string FormattedLine => $"[{TimestampString}] [{Category}] {Message}";
}

/// <summary>
/// Debug 탭의 ViewModel - 앱 동작 및 명령어 실행 로그 관리
/// </summary>
public class DebugViewModel : INotifyPropertyChanged
{
    private const int MaxLogEntries = 1000;
    
    public ObservableCollection<DebugLogEntry> DebugLogs { get; } = new();
    
    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set
        {
            if (_autoScroll == value) return;
            _autoScroll = value;
            OnPropertyChanged();
        }
    }
    
    private bool _showTimestamp = true;
    public bool ShowTimestamp
    {
        get => _showTimestamp;
        set
        {
            if (_showTimestamp == value) return;
            _showTimestamp = value;
            OnPropertyChanged();
        }
    }
    
    /// <summary>
    /// 시스템 로그 추가
    /// </summary>
    public void LogSystem(string message)
    {
        AddLog("System", message, false);
    }
    
    /// <summary>
    /// 명령어 실행 로그 추가
    /// </summary>
    public void LogCommand(string command, string? result = null)
    {
        var message = result != null ? $"{command} -> {result}" : command;
        AddLog("Command", message, false);
    }
    
    /// <summary>
    /// 액션 로그 추가
    /// </summary>
    public void LogAction(string action, string? details = null)
    {
        var message = details != null ? $"{action}: {details}" : action;
        AddLog("Action", message, false);
    }
    
    /// <summary>
    /// 에러 로그 추가
    /// </summary>
    public void LogError(string category, string message)
    {
        AddLog(category, message, true);
    }
    
    private void AddLog(string category, string message, bool isError)
    {
        var entry = new DebugLogEntry
        {
            Timestamp = DateTime.Now,
            Category = category,
            Message = message,
            IsError = isError
        };
        
        // UI 스레드에서 실행
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            DebugLogs.Add(entry);
            
            // 최대 개수 초과 시 오래된 항목 제거
            while (DebugLogs.Count > MaxLogEntries)
            {
                DebugLogs.RemoveAt(0);
            }
        });
    }
    
    /// <summary>
    /// 로그 전체 삭제
    /// </summary>
    public void Clear()
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            DebugLogs.Clear();
        });
    }
    
    /// <summary>
    /// 로그를 텍스트로 내보내기
    /// </summary>
    public string ExportToText()
    {
        var sb = new StringBuilder();
        foreach (var log in DebugLogs)
        {
            sb.AppendLine(log.FormattedLine);
        }
        return sb.ToString();
    }

    public void SaveToFile()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt",
            FileName = $"DebugLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                System.IO.File.WriteAllText(sfd.FileName, ExportToText());
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

