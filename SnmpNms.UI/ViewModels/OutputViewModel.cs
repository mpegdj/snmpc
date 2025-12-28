using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using SnmpNms.UI.Services;

namespace SnmpNms.UI.ViewModels;

/// <summary>
/// SNMP 통신 트래픽 로그 항목
/// </summary>
public class TrafficLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Direction { get; set; } = ""; // ">>>" (송신) or "<<<" (수신)
    public string Protocol { get; set; } = ""; // "SNMP", "TRAP" 등
    public string Operation { get; set; } = ""; // "GET", "GET-NEXT", "WALK", "SET" 등
    public string Target { get; set; } = ""; // IP:Port
    public string Oid { get; set; } = "";
    public string Details { get; set; } = "";
    public bool IsError { get; set; }
    
    public string TimestampString => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
    
    public string FormattedLine => $"[{TimestampString}] {Direction} {Protocol} {Operation} {Target} {Oid} {Details}";
}

/// <summary>
/// Output 탭의 ViewModel - SNMP 통신 트래픽 로그 관리
/// </summary>
public class OutputViewModel : INotifyPropertyChanged
{
    private const int MaxLogEntries = 1000;
    
    public ObservableCollection<TrafficLogEntry> TrafficLogs { get; } = new();
    
    private OutputSaveService? _saveService;
    
    /// <summary>
    /// 저장 서비스 설정
    /// </summary>
    public void SetSaveService(OutputSaveService saveService)
    {
        _saveService = saveService;
    }
    
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
    /// 송신 로그 추가
    /// </summary>
    public void LogSend(string protocol, string operation, string target, string oid, string details = "")
    {
        AddLog(">>>", protocol, operation, target, oid, details, false);
    }
    
    /// <summary>
    /// 수신 로그 추가
    /// </summary>
    public void LogReceive(string protocol, string operation, string target, string oid, string details = "")
    {
        AddLog("<<<", protocol, operation, target, oid, details, false);
    }
    
    /// <summary>
    /// 에러 로그 추가
    /// </summary>
    public void LogError(string protocol, string operation, string target, string oid, string errorMessage)
    {
        AddLog("!!!", protocol, operation, target, oid, $"ERROR: {errorMessage}", true);
    }
    
    private void AddLog(string direction, string protocol, string operation, string target, string oid, string details, bool isError)
    {
        var entry = new TrafficLogEntry
        {
            Timestamp = DateTime.Now,
            Direction = direction,
            Protocol = protocol,
            Operation = operation,
            Target = target,
            Oid = oid,
            Details = details,
            IsError = isError
        };
        
        // 파일 저장
        if (_saveService?.IsEnabled == true)
        {
            _saveService.SaveTrafficEntry(entry);
        }
        
        // UI 스레드에서 실행
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            TrafficLogs.Add(entry);
            
            // 최대 개수 초과 시 오래된 항목 제거 (저장 서비스의 MaxLinesInMemory 사용)
            var maxLines = _saveService?.MaxLinesInMemory ?? MaxLogEntries;
            while (TrafficLogs.Count > maxLines)
            {
                TrafficLogs.RemoveAt(0);
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
            TrafficLogs.Clear();
        });
    }
    
    /// <summary>
    /// 로그를 텍스트로 내보내기
    /// </summary>
    public string ExportToText()
    {
        var sb = new StringBuilder();
        foreach (var log in TrafficLogs)
        {
            sb.AppendLine(log.FormattedLine);
        }
        return sb.ToString();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

