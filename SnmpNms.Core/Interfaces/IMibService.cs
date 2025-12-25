using SnmpNms.Core.Models;

namespace SnmpNms.Core.Interfaces;

public interface IMibService
{
    void LoadMibModules(string directoryPath);
    string GetOidName(string oid);
    string GetOid(string name);
    MibTreeNode GetMibTree();
}

