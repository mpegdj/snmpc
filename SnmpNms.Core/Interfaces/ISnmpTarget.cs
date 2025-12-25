using SnmpNms.Core.Models;

namespace SnmpNms.Core.Interfaces;

public interface ISnmpTarget
{
    string IpAddress { get; }
    int Port { get; }
    string Community { get; }
    SnmpVersion Version { get; }
    int Timeout { get; }
    int Retries { get; }
    PollingProtocol PollingProtocol { get; }
}

