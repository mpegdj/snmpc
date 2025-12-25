namespace SnmpNms.UI.Models;

public enum EventSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public class EventLogEntry
{
    public DateTime Timestamp { get; }
    public EventSeverity Severity { get; }
    public string? Device { get; } // e.g. "10.0.0.1:161"
    public string Message { get; }

    public EventLogEntry(DateTime timestamp, EventSeverity severity, string? device, string message)
    {
        Timestamp = timestamp;
        Severity = severity;
        Device = device;
        Message = message;
    }
}


