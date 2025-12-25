namespace SnmpNms.Core.Interfaces;

public interface IMibService
{
    void LoadMibModules(string directoryPath);
    string GetOidName(string oid);
    string GetOid(string name);
}

