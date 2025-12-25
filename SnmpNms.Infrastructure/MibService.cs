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
            // 실제 MIB 구조는 훨씬 복잡(IMPORTS, SEQUENCE 등)하지만, 
            // 1차적으로 '이름'과 '마지막 숫자'만 파악해서 매핑 시도.
            // * 한계: 완전한 OID 트리(부모 OID 추적)를 만들려면 2-Pass 파싱이 필요함.
            
            // 여기서는 정규식보다는, "Line-by-Line"으로 읽으면서
            // "::= {" 패턴이 있는 줄을 분석하는 방식이 더 안전함.
            
            // MIB 파일이 복잡하므로, 지금 당장은
            // "sysDescr ::= { system 1 }" 같은 패턴을 찾아내는 정규식 사용
            // 패턴: (단어) ... ::= { (부모) (숫자) }
            
            // 예: videoResolution OBJECT-TYPE ... ::= { videoEntry 1 }
            
            var regex = new Regex(@"([a-zA-Z0-9_-]+)\s+OBJECT-TYPE.*?::=\s*\{\s*([a-zA-Z0-9_-]+)\s+(\d+)\s*\}", RegexOptions.Singleline);
            var matches = regex.Matches(content);

            // 주의: 부모의 OID를 모르면 전체 OID를 완성할 수 없음.
            // 따라서 이 방식(Regex Only)은 '부모 OID'를 이미 알고 있거나, 
            // 파일 내에서 순차적으로 파악해야 함.
            
            // => 일단 1차 구현에서는 "파일 로드 시도는 하되, 실제 OID 트리 구성은 복잡하므로 스킵"
            // 대신, 자주 쓰는 OID들을 수동으로 추가하거나
            // 추후 'SharpSnmpLib.Mib' 패키지(유료/구버전)를 구하거나 다른 라이브러리 검토 필요.
            
            // 하지만! 사용자가 제공한 MVD5000 MIB를 보면
            // enterprises.nel.mve5000.systemInfo 이런 식일 것임.
            // root부터 파싱하기 어려우니 로그만 남기고 넘어감.
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
