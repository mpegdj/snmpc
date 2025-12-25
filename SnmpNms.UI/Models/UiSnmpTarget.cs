using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;

namespace SnmpNms.UI.Models;

public class UiSnmpTarget : ISnmpTarget
{
    public string IpAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 161;
    public string Community { get; set; } = "public";
    public SnmpVersion Version { get; set; } = SnmpVersion.V2c;
    public int Timeout { get; set; } = 3000;
    public int Retries { get; set; } = 1;

    public string DisplayName => $"{IpAddress}:{Port}";
}

