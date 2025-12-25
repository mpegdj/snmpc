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
        // 기본 표준 MIB 로드 (필요시)
        // _registry.Import(MibModule.Create(...)); 
        // SharpSnmpLib은 기본적으로 일부 MIB를 알 수도 있지만, 
        // 보통은 명시적으로 로드해야 합니다.
    }

    public void LoadMibModules(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        var files = Directory.GetFiles(directoryPath, "*.mib", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(directoryPath, "*.txt", SearchOption.AllDirectories))
            .ToList();

        // SharpSnmpLib의 MibModule을 사용하여 파일 파싱 및 등록
        // 주의: 의존성(IMPORTS) 순서가 중요할 수 있음.
        // 여기서는 단순하게 발견된 파일들을 로드 시도.
        
        foreach (var file in files)
        {
            try 
            {
                // MIB 파일 파싱은 SharpSnmpLib의 Parser 사용
                // 하지만 SharpSnmpLib 10+ 버전에서는 ObjectRegistry.Import가 사라지거나 방식이 다를 수 있음.
                // 일반적인 방식: MibModule 리스트 생성 -> ObjectTree 생성.
                
                // 여기서는 구현 복잡도를 낮추기 위해, SharpSnmpLib의 구버전 방식보다는
                // 최신 패턴이나 혹은 직접 매핑을 관리하는 방식을 쓸 수도 있지만,
                // 라이브러리 기능을 최대한 활용해 봄.

                // * 중요: SharpSnmpLib의 MIB 파싱 기능은 완전하지 않을 수 있고 
                // 상용 제품 수준의 MIB 파서는 매우 복잡함.
                // 우선은 'ObjectRegistry' 대신 'Reload'나 'Compile' 메서드가 있는지 확인 필요.
                
                // (임시 구현) 일단 인터페이스만 맞추고, 실제 파싱 로직은
                // 라이브러리 버전에 맞는 정확한 코드로 채워넣어야 함.
                // 12.5.7 버전 기준으로는 IModule 파싱 후 ObjectTree에 추가하는 방식.
                
                // TODO: 실제 파싱 로직 구현 (복잡하므로 1차는 스텁)
            }
            catch
            {
                // 로드 실패 무시
            }
        }
    }

    public string GetOidName(string oid)
    {
        try
        {
             // _registry.Translate(oid) 같은 기능 사용
             // 만약 못 찾으면 원래 OID 반환
             var id = new ObjectIdentifier(oid);
             // 라이브러리 기능으로 해석 시도
             // var entry = _registry.Tree.Search(id);
             // return entry != null ? entry.Name : oid;
             
             return oid; // 1차 구현: 그대로 반환 (통과용)
        }
        catch
        {
            return oid;
        }
    }

    public string GetOid(string name)
    {
        // 반대 매핑
        return name; 
    }
}

