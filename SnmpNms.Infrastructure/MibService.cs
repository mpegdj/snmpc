using System.Text.RegularExpressions;
using SnmpNms.Core.Interfaces;

namespace SnmpNms.Infrastructure;

public class MibService : IMibService
{
    // OID -> Name (e.g., "1.3.6.1.2.1.1.3" -> "sysUpTime")
    private readonly Dictionary<string, string> _oidToName = new();
    
    // Name -> OID (e.g., "sysUpTime" -> "1.3.6.1.2.1.1.3")
    private readonly Dictionary<string, string> _nameToOid = new();

    public MibService()
    {
        // 기본 표준 MIB 중 가장 흔한 것들만 하드코딩으로 미리 등록 (필수)
        Register("1.3.6.1.2.1.1.1", "sysDescr");
        Register("1.3.6.1.2.1.1.2", "sysObjectID");
        Register("1.3.6.1.2.1.1.3", "sysUpTime");
        Register("1.3.6.1.2.1.1.4", "sysContact");
        Register("1.3.6.1.2.1.1.5", "sysName");
        Register("1.3.6.1.2.1.1.6", "sysLocation");
        Register("1.3.6.1.2.1.1.7", "sysServices");
    }

    private void Register(string oid, string name)
    {
        // .0 (스칼라 인스턴스) 처리를 위해 유연하게 저장하거나, 검색 시 처리
        // 여기서는 기본 OID 저장
        if (!_oidToName.ContainsKey(oid)) _oidToName[oid] = name;
        if (!_nameToOid.ContainsKey(name)) _nameToOid[name] = oid;
    }

    public void LoadMibModules(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        var files = Directory.GetFiles(directoryPath, "*.mib", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories))
            .ToList();

        foreach (var file in files)
        {
            ParseMibFile(file);
        }
    }

    private void ParseMibFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            
            // 아주 단순화된 파서: "이름 OBJECT-TYPE ... ::= { 부모 숫자 }" 패턴 찾기
            // 예: videoResolution OBJECT-TYPE ... ::= { videoEntry 1 }
            
            var regex = new Regex(@"([a-zA-Z0-9_-]+)\s+OBJECT-TYPE.*?::=\s*\{\s*([a-zA-Z0-9_-]+)\s+(\d+)\s*\}", RegexOptions.Singleline);
            var matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                if (match.Success)
                {
                    var name = match.Groups[1].Value;
                    var parentName = match.Groups[2].Value;
                    var lastId = match.Groups[3].Value;

                    // 부모 OID를 알고 있다면 현재 OID 완성 가능
                    if (_nameToOid.TryGetValue(parentName, out var parentOid))
                    {
                        var currentOid = $"{parentOid}.{lastId}";
                        Register(currentOid, name);
                    }
                }
            }
        }
        catch
        {
            // 무시
        }
    }

    public string GetOidName(string oid)
    {
        // 1. 정확히 일치하는 경우
        if (_oidToName.TryGetValue(oid, out var name)) return name;

        // 2. ".0" (인스턴스) 제거하고 검색
        if (oid.EndsWith(".0"))
        {
            var baseOid = oid.Substring(0, oid.Length - 2);
            if (_oidToName.TryGetValue(baseOid, out var baseName))
            {
                return $"{baseName}.0";
            }
        }
        
        // 3. 인덱스가 붙은 경우 (e.g., .1.3)
        // 가장 길게 매칭되는 OID 찾기 (Longest Match)
        var match = _oidToName.Keys
            .Where(k => oid.StartsWith(k + "."))
            .OrderByDescending(k => k.Length)
            .FirstOrDefault();

        if (match != null)
        {
            var suffix = oid.Substring(match.Length);
            return $"{_oidToName[match]}{suffix}";
        }

        return oid;
    }

    public string GetOid(string name)
    {
        return _nameToOid.TryGetValue(name, out var oid) ? oid : name;
    }
}

