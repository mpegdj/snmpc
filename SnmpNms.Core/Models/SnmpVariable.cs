namespace SnmpNms.Core.Models;

public class SnmpVariable
{
    public string Oid { get; }
    public string Value { get; }
    public string TypeCode { get; } // e.g., "Integer32", "OctetString"

    public SnmpVariable(string oid, string value, string typeCode)
    {
        Oid = oid;
        Value = value;
        TypeCode = typeCode;
    }

    public override string ToString()
    {
        return $"{Oid} = {TypeCode}: {Value}";
    }
}

