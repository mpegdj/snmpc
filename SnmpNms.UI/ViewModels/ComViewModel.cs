using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace SnmpNms.UI.ViewModels;

/// <summary>
/// 통신 로그 항목 (hex와 text 표시)
/// </summary>
public class ComLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Direction { get; set; } = ""; // "[out]" or "[in]"
    public byte[] RawData { get; set; } = Array.Empty<byte>();
    public string Target { get; set; } = ""; // IP:Port
    
    public string TimestampString => Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
    
    public string HexString
    {
        get
        {
            if (RawData == null || RawData.Length == 0)
                return "";
            
            var hex = BitConverter.ToString(RawData).Replace("-", " ");
            // 16바이트씩 줄바꿈
            var sb = new StringBuilder();
            for (int i = 0; i < hex.Length; i += 48) // 16바이트 * 3 (2 hex + 1 space) = 48
            {
                if (i > 0) sb.AppendLine();
                var length = Math.Min(48, hex.Length - i);
                sb.Append(hex.Substring(i, length));
            }
            return sb.ToString();
        }
    }
    
    public string TextString
    {
        get
        {
            if (RawData == null || RawData.Length == 0)
                return "";
            
            var sb = new StringBuilder();
            foreach (var b in RawData)
            {
                if (b >= 32 && b < 127)
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append('.');
                }
            }
            return sb.ToString();
        }
    }
}

/// <summary>
/// Com 탭의 ViewModel - 통신 hex와 text 로그 관리
/// </summary>
public class ComViewModel : INotifyPropertyChanged
{
    private const int MaxLogEntries = 1000;
    
    public ObservableCollection<ComLogEntry> ComLogs { get; } = new();
    
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
    public void LogSend(byte[] rawData, string target)
    {
        AddLog("[out]", rawData, target);
    }
    
    /// <summary>
    /// 수신 로그 추가
    /// </summary>
    public void LogReceive(byte[] rawData, string target)
    {
        AddLog("[in]", rawData, target);
    }
    
    private void AddLog(string direction, byte[] rawData, string target)
    {
        var entry = new ComLogEntry
        {
            Timestamp = DateTime.Now,
            Direction = direction,
            RawData = rawData ?? Array.Empty<byte>(),
            Target = target
        };
        
        // UI 스레드에서 실행
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ComLogs.Add(entry);
            
            // 최대 개수 초과 시 오래된 항목 제거
            while (ComLogs.Count > MaxLogEntries)
            {
                ComLogs.RemoveAt(0);
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
            ComLogs.Clear();
        });
    }
    
    /// <summary>
    /// 로그를 텍스트로 내보내기
    /// </summary>
    public string ExportToText()
    {
        var sb = new StringBuilder();
        foreach (var log in ComLogs)
        {
            sb.AppendLine($"[{log.TimestampString}] {log.Direction} {log.Target}");
            sb.AppendLine($"HEX: {log.HexString}");
            sb.AppendLine($"TXT: {log.TextString}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

