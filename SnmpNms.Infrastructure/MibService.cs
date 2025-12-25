using System.IO;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Mib;
using SnmpNms.Core.Interfaces;

namespace SnmpNms.Infrastructure;

public class MibService : IMibService
{
    private readonly ObjectRegistry _registry;

    public MibService()
    {
        _registry = new ObjectRegistry();
    }

    public void LoadMibModules(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        var files = Directory.GetFiles(directoryPath, "*.mib", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories))
            .ToList();

        if (files.Count == 0) return;

        try
        {
            // SharpSnmpLib의 ObjectRegistry는 Import(IEnumerable<IModule>) 형태를 지원하지 않고
            // 생성자 또는 내부 메서드를 사용해야 하는데 버전마다 다름.
            // 12.x 버전에서는 File.ReadAllText 등을 통해 파싱해야 함.
            
            // 1. 파서 생성 및 파일 로드
            // SharpSnmpLib의 Mib 파싱은 다소 까다로워 예외처리가 중요.
            // 여러 파일을 한꺼번에 파싱해야 의존성(Import)이 해결됨.
            
            var modules = new List<IModule>();
            var parser = new Parser();

            foreach (var file in files)
            {
                try
                {
                    // 스트림 리더로 파일을 열어서 파싱
                    using var reader = new StreamReader(file);
                    var module = parser.ParseToModule(reader);
                    if (module != null)
                    {
                        modules.Add(module);
                    }
                }
                catch
                {
                    // 개별 파일 파싱 에러는 무시 (표준이 아닌 MIB 형식이 섞여있을 수 있음)
                }
            }

            // 2. Registry에 등록 (Tree 갱신)
            // Import 대신 Refresh나 Load를 사용하는 패턴이 있을 수 있음.
            // 12.5.7 버전 기준: ObjectRegistry는 생성자 없이 생성 불가하거나, Import 메서드 사용
            // public void Import(IEnumerable<IModule> modules)
            
            _registry.Import(modules);
            _registry.Refresh(); // 트리 갱신
        }
        catch
        {
            // 전체 로드 실패 (의존성 문제 등)
        }
    }

    public string GetOidName(string oid)
    {
        try
        {
             var id = new ObjectIdentifier(oid);
             // Registry에서 OID 검색
             // Tree.Search(id) -> IDefinition 반환
             var entry = _registry.Tree.Search(id);
             if (entry != null)
             {
                 return entry.Name;
             }
             
             // 만약 정확히 일치하지 않아도, 트리에서 가장 가까운 부모를 찾아서 보여줄 수도 있음.
             // 일단은 정확한 매칭만 이름으로 반환.
             return oid;
        }
        catch
        {
            return oid;
        }
    }

    public string GetOid(string name)
    {
        try
        {
            // 이름으로 OID 찾기 (역방향)
            // ObjectRegistry에는 Translate 기능이 있음
            var oid = _registry.Translate(name);
            return oid.ToString();
        }
        catch
        {
            return name;
        }
    }
}
