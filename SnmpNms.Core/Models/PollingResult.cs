using SnmpNms.Core.Interfaces;

namespace SnmpNms.Core.Models;

public class PollingResult
{
    public ISnmpTarget Target { get; }
    public DeviceStatus Status { get; }
    public long ResponseTime { get; }
    public DateTime Timestamp { get; }
    public string Message { get; }

    public PollingResult(ISnmpTarget target, DeviceStatus status, long responseTime, string message)
    {
        Target = target;
        Status = status;
        ResponseTime = responseTime;
        Message = message;
        Timestamp = DateTime.Now;
    }
}

