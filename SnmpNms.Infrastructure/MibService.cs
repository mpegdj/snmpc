using System.Text.RegularExpressions;
using SnmpNms.Core.Interfaces;
using SnmpNms.Core.Models;

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
        
        // 표준 루트 OID 정의
        Register("1.3.6.1", "iso.org.dod.internet");
        Register("1.3.6.1.4.1", "enterprises");
        Register("1.3.6.1.2.1", "mgmt");
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
            var previousCount = _oidToName.Count;
            
            // 여러 번 반복 파싱하여 의존성 해결 (최대 10회)
            for (int iteration = 0; iteration < 10; iteration++)
            {
                var beforeCount = _oidToName.Count;
                
                // 1단계: OBJECT IDENTIFIER 정의 파싱 (부모-자식 관계 구축)
                // 예: nel                OBJECT IDENTIFIER ::= { enterprises 3930 }
                // 예: mvd5000General OBJECT IDENTIFIER ::= { mvd5000 1 }
                // 공백이 많을 수 있으므로 \s+ 사용, 여러 줄에 걸칠 수 있으므로 Singleline 사용
                var oidDefRegex = new Regex(@"^([a-zA-Z0-9_-]+)\s+OBJECT\s+IDENTIFIER\s*::=\s*\{\s*([a-zA-Z0-9_-]+)\s+(\d+)\s*\}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var oidDefMatches = oidDefRegex.Matches(content);
                
                foreach (Match match in oidDefMatches)
                {
                    if (match.Success)
                    {
                        var name = match.Groups[1].Value.Trim();
                        var parentName = match.Groups[2].Value.Trim();
                        var lastId = match.Groups[3].Value.Trim();

                        // 이미 등록되어 있으면 스킵
                        if (_nameToOid.ContainsKey(name)) continue;

                        // 부모 OID를 알고 있다면 현재 OID 완성 가능
                        if (_nameToOid.TryGetValue(parentName, out var parentOid))
                        {
                            var currentOid = $"{parentOid}.{lastId}";
                            Register(currentOid, name);
                            System.Diagnostics.Debug.WriteLine($"  [OID] {name} = {currentOid} (parent: {parentName})");
                        }
                        // enterprises는 특별 처리 (1.3.6.1.4.1)
                        else if (parentName.Equals("enterprises", StringComparison.OrdinalIgnoreCase))
                        {
                            var currentOid = $"1.3.6.1.4.1.{lastId}";
                            Register(currentOid, name);
                            System.Diagnostics.Debug.WriteLine($"  [OID] {name} = {currentOid} (parent: enterprises)");
                        }
                    }
                }
                
                // 2단계: MODULE-IDENTITY 정의 파싱
                // 예: mvd5000 MODULE-IDENTITY ... ::= { nel 37 }
                // MODULE-IDENTITY 다음에 여러 줄이 올 수 있으므로 .*? 사용
                var moduleIdentityRegex = new Regex(@"^([a-zA-Z0-9_-]+)\s+MODULE-IDENTITY[\s\S]*?::=\s*\{\s*([a-zA-Z0-9_-]+)\s+(\d+)\s*\}", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var moduleMatches = moduleIdentityRegex.Matches(content);
                
                foreach (Match match in moduleMatches)
                {
                    if (match.Success)
                    {
                        var name = match.Groups[1].Value.Trim();
                        var parentName = match.Groups[2].Value.Trim();
                        var lastId = match.Groups[3].Value.Trim();

                        if (_nameToOid.ContainsKey(name)) continue;

                        if (_nameToOid.TryGetValue(parentName, out var parentOid))
                        {
                            var currentOid = $"{parentOid}.{lastId}";
                            Register(currentOid, name);
                            System.Diagnostics.Debug.WriteLine($"  [MODULE] {name} = {currentOid} (parent: {parentName})");
                        }
                        else if (parentName.Equals("enterprises", StringComparison.OrdinalIgnoreCase))
                        {
                            var currentOid = $"1.3.6.1.4.1.{lastId}";
                            Register(currentOid, name);
                            System.Diagnostics.Debug.WriteLine($"  [MODULE] {name} = {currentOid} (parent: enterprises)");
                        }
                    }
                }
                
                // 3단계: OBJECT-TYPE 정의 파싱
                // 예: mvd5000SysDescr OBJECT-TYPE ... ::= { mvd5000System 1 }
                // OBJECT-TYPE 다음에 여러 줄이 올 수 있으므로 .*? 사용
                var objectTypeRegex = new Regex(@"^([a-zA-Z0-9_-]+)\s+OBJECT-TYPE[\s\S]*?::=\s*\{\s*([a-zA-Z0-9_-]+)\s+(\d+)\s*\}", RegexOptions.Multiline);
                var objectTypeMatches = objectTypeRegex.Matches(content);

                foreach (Match match in objectTypeMatches)
                {
                    if (match.Success)
                    {
                        var name = match.Groups[1].Value.Trim();
                        var parentName = match.Groups[2].Value.Trim();
                        var lastId = match.Groups[3].Value.Trim();

                        if (_nameToOid.ContainsKey(name)) continue;

                        if (_nameToOid.TryGetValue(parentName, out var parentOid))
                        {
                            var currentOid = $"{parentOid}.{lastId}";
                            Register(currentOid, name);
                            System.Diagnostics.Debug.WriteLine($"  [OBJ] {name} = {currentOid} (parent: {parentName})");
                        }
                    }
                }
                
                // 더 이상 새로운 항목이 추가되지 않으면 종료
                if (_oidToName.Count == beforeCount)
                    break;
            }
            
            var addedCount = _oidToName.Count - previousCount;
            if (addedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"MIB File parsed: {Path.GetFileName(filePath)}, Added {addedCount} OIDs (Total: {_oidToName.Count})");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"MIB File parsed: {Path.GetFileName(filePath)}, No OIDs added (Total: {_oidToName.Count})");
            }
        }
        catch (Exception ex)
        {
            // 파싱 오류는 무시하되, 디버깅을 위해 로그 남길 수 있음
            System.Diagnostics.Debug.WriteLine($"MIB Parse Error: {filePath} - {ex.Message}");
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

    public MibTreeNode GetMibTree()
    {
        var root = new MibTreeNode { Name = "Snmp Mibs", NodeType = MibNodeType.Folder, IsExpanded = true };

        // mgmt 서브트리 (1.3.6.1.2.1) - 표준 MIB
        var mgmtNode = new MibTreeNode 
        { 
            Name = "mgmt", 
            Oid = "1.3.6.1.2.1",
            NodeType = MibNodeType.Folder,
            IsExpanded = false
        };
        root.Children.Add(mgmtNode);

        // Private 서브트리 (1.3.6.1.4.1) - vendor-specific MIB
        var privateNode = new MibTreeNode 
        { 
            Name = "Private", 
            Oid = "1.3.6.1.4.1",
            NodeType = MibNodeType.Folder,
            IsExpanded = false
        };
        root.Children.Add(privateNode);

        // Custom-Tables
        var customTablesNode = new MibTreeNode 
        { 
            Name = "Custom-Tables", 
            NodeType = MibNodeType.Folder,
            IsExpanded = false
        };
        root.Children.Add(customTablesNode);

        // OID를 파싱하여 트리 구조 생성
        BuildTreeFromOids(mgmtNode, privateNode, customTablesNode);

        // 디버깅: 로드된 OID 개수 확인
        System.Diagnostics.Debug.WriteLine($"GetMibTree: Total OIDs loaded: {_oidToName.Count}");
        System.Diagnostics.Debug.WriteLine($"GetMibTree: mgmt children: {mgmtNode.Children.Count}, Private children: {privateNode.Children.Count}");
        
        // 처음 10개 OID 출력
        var sampleOids = _oidToName.Take(10).ToList();
        foreach (var kvp in sampleOids)
        {
            System.Diagnostics.Debug.WriteLine($"  Sample OID: {kvp.Key} -> {kvp.Value}");
        }

        return root;
    }

    private void BuildTreeFromOids(MibTreeNode mgmtNode, MibTreeNode privateNode, MibTreeNode customTablesNode)
    {
        // OID를 세그먼트별로 분리하여 트리 구조 생성
        var oidSegments = new Dictionary<string, MibTreeNode>();

        foreach (var kvp in _oidToName)
        {
            var oid = kvp.Key;
            var name = kvp.Value;

            // OID가 mgmt (1.3.6.1.2.1) 또는 Private (1.3.6.1.4.1)로 시작하는지 확인
            MibTreeNode? rootNode = null;
            string relativeOid = "";

            if (oid.StartsWith("1.3.6.1.2.1"))
            {
                rootNode = mgmtNode;
                relativeOid = oid.StartsWith("1.3.6.1.2.1.") ? oid.Substring("1.3.6.1.2.1.".Length) : "";
            }
            else if (oid.StartsWith("1.3.6.1.4.1"))
            {
                rootNode = privateNode;
                relativeOid = oid.StartsWith("1.3.6.1.4.1.") ? oid.Substring("1.3.6.1.4.1.".Length) : "";
            }

            if (rootNode is null || string.IsNullOrEmpty(relativeOid)) continue;

            // OID 세그먼트를 따라 트리 구조 생성
            var segments = relativeOid.Split('.');
            var currentParent = rootNode;

            for (int i = 0; i < segments.Length; i++)
            {
                var segmentOid = i == 0 ? segments[0] : string.Join(".", segments.Take(i + 1));
                var fullOid = rootNode == mgmtNode 
                    ? $"1.3.6.1.2.1.{segmentOid}"
                    : $"1.3.6.1.4.1.{segmentOid}";
                var segmentKey = $"{rootNode.Name}.{segmentOid}";

                if (!oidSegments.TryGetValue(segmentKey, out var segmentNode))
                {
                    // 마지막 세그먼트인 경우 실제 이름 사용, 아니면 세그먼트 번호 사용
                    var nodeName = (i == segments.Length - 1) ? name : segments[i];
                    segmentNode = new MibTreeNode
                    {
                        Name = nodeName,
                        Oid = fullOid,
                        NodeType = (i == segments.Length - 1) ? MibNodeType.Scalar : MibNodeType.Folder
                    };
                    oidSegments[segmentKey] = segmentNode;
                    currentParent.Children.Add(segmentNode);
                }

                currentParent = segmentNode;
            }
        }

        // 트리 정렬 (이름 순)
        SortTree(mgmtNode);
        SortTree(privateNode);
    }

    private void SortTree(MibTreeNode node)
    {
        // 자식 노드를 이름 순으로 정렬
        var sortedChildren = node.Children.OrderBy(c => c.Name).ToList();
        node.Children.Clear();
        foreach (var child in sortedChildren)
        {
            node.Children.Add(child);
            SortTree(child); // 재귀적으로 정렬
        }
    }
}

