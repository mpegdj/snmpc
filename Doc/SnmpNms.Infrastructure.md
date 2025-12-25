# SnmpNms.Infrastructure 개요

`SnmpNms.Infrastructure`는 **Core 인터페이스의 실제 구현**을 두는 프로젝트입니다.  
SharpSnmpLib 같은 외부 라이브러리 의존은 여기서만 가져가고, UI/Core에 새지 않게 유지합니다.

---

## 역할

- `ISnmpClient`, `IPollingService`, `IMibService`의 **구현 제공**
- 외부 SNMP 라이브러리(SharpSnmpLib) 어댑터 역할
- 네트워크 통신 및 파일 I/O 처리
- 외부 라이브러리와의 의존성을 UI에서 격리

---

## 의존성

- **NuGet**: `Lextm.SharpSnmpLib` (`12.5.7`)
- **Target**: `net9.0`
- **ProjectReference**: `SnmpNms.Core`
- **외부 의존성**: SharpSnmpLib만 사용 (UI는 SharpSnmpLib을 직접 참조하지 않음)

---

## 폴더/파일 구조

```
SnmpNms.Infrastructure/
├── SnmpClient.cs          # ISnmpClient 구현
├── PollingService.cs      # IPollingService 구현
└── MibService.cs          # IMibService 구현
```

---

## 클래스 상세

### SnmpClient

`ISnmpClient` 인터페이스의 구현체입니다. SharpSnmpLib을 사용하여 SNMP 통신을 수행합니다.

#### 주요 메서드

**GetAsync (단일 OID)**
```csharp
public async Task<SnmpResult> GetAsync(ISnmpTarget target, string oid)
```

**GetAsync (다중 OID)**
```csharp
public async Task<SnmpResult> GetAsync(ISnmpTarget target, IEnumerable<string> oids)
```

**GetNextAsync**
```csharp
public async Task<SnmpResult> GetNextAsync(ISnmpTarget target, string oid)
```

**WalkAsync**
```csharp
public async Task<SnmpResult> WalkAsync(ISnmpTarget target, string rootOid)
```

#### 구현 세부사항

- **비동기 처리**: SharpSnmpLib의 동기 호출을 `Task.Run(...)`으로 감싸 UI 스레드 블로킹 회피
- **에러 처리**: 예외 발생 시 `SnmpResult.Fail(ex.Message)`로 변환
- **응답 시간 측정**: `Stopwatch`를 사용하여 응답 시간 측정
- **버전 매핑**: `SnmpVersion` enum을 SharpSnmpLib의 `VersionCode`로 변환

#### 사용 예시

```csharp
var client = new SnmpClient();
var target = new UiSnmpTarget { IpAddress = "192.168.1.1", Community = "public" };
var result = await client.GetAsync(target, "1.3.6.1.2.1.1.1.0");
```

---

### PollingService

`IPollingService` 인터페이스의 구현체입니다. 백그라운드에서 주기적으로 디바이스 상태를 확인합니다.

#### 주요 속성 및 메서드

```csharp
public class PollingService : IPollingService
{
    private readonly Timer _timer;
    private readonly ConcurrentDictionary<string, ISnmpTarget> _targets;
    
    public event EventHandler<PollingResult>? OnPollingResult;
    
    public void Start();
    public void Stop();
    public void AddTarget(ISnmpTarget target);
    public void RemoveTarget(ISnmpTarget target);
    public void SetInterval(int intervalMs);
}
```

#### 구현 세부사항

**타이머 기반 Polling**
- `System.Timers.Timer` 사용 (기본 3000ms 주기)
- `AutoReset = true`로 주기적 실행
- `Elapsed` 이벤트에서 모든 타겟을 병렬로 Polling

**타겟 관리**
- `ConcurrentDictionary<string, ISnmpTarget>` 사용 (key = `"ip:port"`)
- 스레드 안전한 추가/제거 지원

**병렬 처리**
- `Task.WhenAll`을 사용하여 모든 타겟을 동시에 Polling
- 각 타겟의 Polling은 독립적으로 실행

**PollingProtocol별 처리**

`PollTargetAsync` 메서드에서 `target.PollingProtocol`에 따라 다른 방식으로 상태를 확인합니다:

1. **SNMP** (기본)
   - sysUpTime OID (`1.3.6.1.2.1.1.3.0`) 조회
   - 응답 성공 시 `DeviceStatus.Up`
   - 실패 시 `DeviceStatus.Down`

2. **Ping**
   - `System.Net.NetworkInformation.Ping` 사용
   - 타임아웃: 1000ms
   - 응답 성공 시 `DeviceStatus.Up`, 응답 시간 표시
   - 실패 시 `DeviceStatus.Down`

3. **ARP**
   - 현재 미구현
   - `DeviceStatus.Unknown` 반환

4. **None**
   - Polling 비활성화
   - `DeviceStatus.Unknown` 반환

#### 사용 예시

```csharp
var pollingService = new PollingService(snmpClient);
pollingService.OnPollingResult += (sender, result) =>
{
    Console.WriteLine($"{result.Target.IpAddress}: {result.Status}");
};

pollingService.AddTarget(target);
pollingService.SetInterval(3000);
pollingService.Start();
```

#### 주의사항

- **Timer 재진입 가능성**: 이전 tick이 끝나기 전에 다음 tick이 올 수 있음 (중첩 Polling)
  - 현재는 `Task.WhenAll`로 병렬 처리하므로 문제 없음
  - 향후 `SemaphoreSlim`으로 동시성 제어 고려 가능

- **ISnmpTarget.Retries**: 현재 실제 로직에 반영되지 않음
  - 향후 재시도 로직 추가 필요

---

### MibService

`IMibService` 인터페이스의 구현체입니다. MIB 파일을 로드하고 OID ↔ 이름 변환을 제공합니다.

#### 주요 메서드

```csharp
public class MibService : IMibService
{
    public void LoadMibModules(string directoryPath);
    public string? GetOidName(string oid);
    public string? GetOid(string name);
    public MibTreeNode? GetMibTree();
}
```

#### 구현 세부사항

**MIB 파일 로딩**
- `.mib`, `.txt` 파일을 모두 읽어서 파싱
- 정규식을 사용하여 `OBJECT-TYPE ::= { parent n }` 패턴 파싱
- 기본 표준 OID는 하드코딩 등록 (`sysDescr`, `sysUpTime` 등)

**OID → 이름 변환**
- 정확히 일치하는 OID 검색
- `.0` 제거 후 base OID 검색
- Longest match (접두사)로 인덱스가 붙은 OID도 일부 표시

**이름 → OID 변환**
- 이름으로 OID 검색
- 대소문자 무시

**MIB 트리 구조**
- 파싱된 MIB를 트리 구조로 구성
- `MibTreeNode` 컬렉션으로 표현

#### 사용 예시

```csharp
var mibService = new MibService();
mibService.LoadMibModules(@"C:\Mib");

string? oid = mibService.GetOid("sysDescr");
// 결과: "1.3.6.1.2.1.1.1"

string? name = mibService.GetOidName("1.3.6.1.2.1.1.1.0");
// 결과: "sysDescr.0"
```

#### 제한사항

- **IMPORTS/모듈 의존성**: 현재 미구현
- **ENUM/TYPE 해석**: 현재 미구현
- **테이블 해석**: 현재 미구현
- 현재는 "이름 변환용 최소" 수준의 구현

---

## 성능 고려사항

### SnmpClient

- **Task.Run 기반**: 단순하지만 대규모 장비에서 스레드풀 부하 가능
- **타임아웃 처리**: 각 요청에 타임아웃 적용
- **병렬 요청**: 다중 OID 요청 시 병렬 처리 고려 필요

### PollingService

- **병렬 처리**: `Task.WhenAll`로 모든 타겟을 동시에 Polling
- **타이머 주기**: 기본 3초, `SetInterval`로 조정 가능
- **메모리 관리**: `ConcurrentDictionary`로 타겟 관리

### MibService

- **파일 I/O**: MIB 파일 로딩 시 파일 I/O 발생
- **메모리 사용**: 모든 MIB를 메모리에 로드
- **파싱 성능**: 정규식 기반 파싱으로 대용량 MIB 파일에서 성능 저하 가능

---

## 에러 처리

### SnmpClient

- 네트워크 오류: `SnmpResult.Fail(ex.Message)`로 변환
- 타임아웃: `SnmpResult.Fail("Timeout")` 반환
- 잘못된 OID: SharpSnmpLib 예외를 `SnmpResult.Fail`로 변환

### PollingService

- Polling 실패: `OnPollingResult` 이벤트로 `DeviceStatus.Down` 전달
- 예외 발생: 예외 메시지를 포함한 `PollingResult` 전달

### MibService

- 파일 읽기 오류: 예외 발생 (호출자가 처리)
- 파싱 오류: 무시하고 다음 파일로 진행
- OID/이름 없음: `null` 반환

---

## 현재 리스크/개선 포인트

### PollingService

1. **Timer 재진입 가능성**
   - 현재는 `Task.WhenAll`로 병렬 처리하므로 문제 없음
   - 향후 `SemaphoreSlim`으로 동시성 제어 고려

2. **ISnmpTarget.Retries 미사용**
   - 현재 재시도 로직 없음
   - 향후 재시도 로직 추가 필요

3. **타겟 추가/제거 시점**
   - Polling 중 타겟 추가/제거 시 동기화 필요

### SnmpClient

1. **Task.Run 기반의 한계**
   - 대규모 장비에서 스레드풀 부하 가능
   - 향후 `CancellationToken` 지원 고려

2. **타임아웃 처리**
   - 각 요청에 타임아웃 적용되지만, 전체 요청 시간 제한 없음

3. **에러 메시지**
   - SharpSnmpLib 예외 메시지를 그대로 전달
   - 사용자 친화적인 메시지 변환 고려

### MibService

1. **파싱 제한**
   - IMPORTS/모듈 의존성 미구현
   - ENUM/TYPE 해석 미구현
   - 테이블 해석 미구현

2. **성능**
   - 대용량 MIB 파일에서 파싱 성능 저하 가능
   - 캐싱 전략 고려 필요

---

## 버전 이력

### v1.0 (초기 구현)
- SnmpClient, PollingService, MibService 기본 구현
- SharpSnmpLib 어댑터 구현

### v1.1 (PollingProtocol 지원)
- PollingService에 PollingProtocol별 처리 로직 추가
- SNMP, Ping, ARP, None 프로토콜 지원

---

## 참고 사항

- Infrastructure 프로젝트는 **유일하게 SharpSnmpLib을 참조**하는 프로젝트입니다
- UI 프로젝트는 Infrastructure를 통해 간접적으로 SharpSnmpLib을 사용합니다
- Core 인터페이스를 구현하므로, 다른 SNMP 라이브러리로 교체 가능합니다
