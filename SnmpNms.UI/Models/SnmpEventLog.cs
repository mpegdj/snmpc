using System;

namespace SnmpNms.UI.Models;

public enum EventSeverity
{
    Info = 0,
    Notice = 1,
    Warning = 2,
    Error = 3
}

public class SnmpEventLog
{
    public DateTime Timestamp { get; }
    public EventSeverity Severity { get; }
    public string? Device { get; } // e.g. "10.0.0.1:161"
    public string Message { get; }

    public string TimestampString => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string Summary => Message;

    public SnmpEventLog(DateTime timestamp, EventSeverity severity, string? device, string message)
    {
        Timestamp = timestamp;
        Severity = severity;
        Device = device;
        Message = message;
    }

    /// <summary>
    /// 트래픽 관련 로그(Trap, Polling)인지 확인
    /// </summary>
    public static bool IsTrafficLog(string message)
    {
        if (string.IsNullOrEmpty(message)) return false;
        
        string trimmed = message.TrimStart();

        // [T: (Trap) 또는 [P: (Polling) 으로 시작하는지 확인
        if (trimmed.StartsWith("[T:", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.StartsWith("[P:", StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}
