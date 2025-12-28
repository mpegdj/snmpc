namespace SnmpNms.Core.Models;

public class TrapEvent
{
    public DateTime Timestamp { get; }
    public string SourceIpAddress { get; }
    public int SourcePort { get; }
    public SnmpVersion Version { get; }
    public string? Community { get; }
    public string? EnterpriseOid { get; }
    public string? GenericTrapType { get; }
    public string? SpecificTrapType { get; }
    public List<SnmpVariable> Variables { get; }
    public string? ErrorMessage { get; }
    public byte[]? RawData { get; } // Raw 바이트 데이터

    public TrapEvent(
        string sourceIpAddress,
        int sourcePort,
        SnmpVersion version,
        string? community = null,
        string? enterpriseOid = null,
        string? genericTrapType = null,
        string? specificTrapType = null,
        List<SnmpVariable>? variables = null,
        string? errorMessage = null,
        byte[]? rawData = null)
    {
        Timestamp = DateTime.Now;
        SourceIpAddress = sourceIpAddress;
        SourcePort = sourcePort;
        Version = version;
        Community = community;
        EnterpriseOid = enterpriseOid;
        GenericTrapType = genericTrapType;
        SpecificTrapType = specificTrapType;
        Variables = variables ?? new List<SnmpVariable>();
        ErrorMessage = errorMessage;
        RawData = rawData;
    }
}

