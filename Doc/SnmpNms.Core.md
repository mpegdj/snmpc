# SnmpNms.Core 개요

`SnmpNms.Core`는 **도메인 모델 + 인터페이스(계약)**만 포함하는 프로젝트입니다.  
UI/WPF, SharpSnmpLib 같은 외부 구현 디테일은 여기에 들어오지 않도록 유지하는 게 핵심입니다.

---

## 역할

- **계약(Interface) 정의**: UI/Infrastructure가 "무엇을 할 수 있는지"만 표현
- **도메인 모델 정의**: SNMP 결과, 폴링 결과, 상태 등 공통 데이터 구조
- **의존성 없음**: 순수 .NET 프로젝트로 외부 라이브러리 의존성 없음

---

## 의존성

- **외부 NuGet 없음** (순수 .NET)
- Target: `net9.0`
- 다른 프로젝트에 의존하지 않음 (최상위 레이어)

---

## 폴더 구조

```
SnmpNms.Core/
├── Interfaces/
│   ├── ISnmpClient.cs          # SNMP 통신 인터페이스
│   ├── ISnmpTarget.cs          # SNMP 타겟 정보 인터페이스
│   ├── IPollingService.cs      # Polling 서비스 인터페이스
│   └── IMibService.cs          # MIB 서비스 인터페이스
└── Models/
    ├── SnmpResult.cs           # SNMP 요청 결과
    ├── SnmpVariable.cs         # SNMP 변수 (OID + 값)
    ├── PollingResult.cs        # Polling 결과
    ├── DeviceStatus.cs         # 디바이스 상태 enum
    ├── SnmpVersion.cs          # SNMP 버전 enum
    ├── PollingProtocol.cs      # Polling 프로토콜 enum
    └── MibTreeNode.cs          # MIB 트리 노드 모델
```

---

## 인터페이스 상세

### ISnmpClient

SNMP 통신을 수행하는 클라이언트 인터페이스입니다.

```csharp
public interface ISnmpClient
{
    Task<SnmpResult> GetAsync(ISnmpTarget target, string oid);
    Task<SnmpResult> GetAsync(ISnmpTarget target, IEnumerable<string> oids);
    Task<SnmpResult> GetNextAsync(ISnmpTarget target, string oid);
    Task<SnmpResult> WalkAsync(ISnmpTarget target, string rootOid);
}
```

**메서드 설명:**
- `GetAsync`: 단일 또는 다중 OID에 대한 SNMP GET 요청
- `GetNextAsync`: SNMP GETNEXT 요청 (다음 OID 조회)
- `WalkAsync`: SNMP WALK 요청 (서브트리 순회)

### ISnmpTarget

SNMP 타겟(디바이스) 정보를 표현하는 인터페이스입니다.

```csharp
public interface ISnmpTarget
{
    string IpAddress { get; }           // IP 주소
    int Port { get; }                   // 포트 (기본 161)
    string Community { get; }           // Community String
    SnmpVersion Version { get; }        // SNMP 버전 (V1, V2c, V3)
    int Timeout { get; }                // 타임아웃 (밀리초)
    int Retries { get; }                // 재시도 횟수
    PollingProtocol PollingProtocol { get; }  // Polling 프로토콜
}
```

**주요 속성:**
- `PollingProtocol`: 디바이스 상태 확인에 사용할 프로토콜 (SNMP, Ping, ARP, None)

### IPollingService

백그라운드에서 주기적으로 디바이스 상태를 확인하는 서비스 인터페이스입니다.

```csharp
public interface IPollingService
{
    void Start();
    void Stop();
    void AddTarget(ISnmpTarget target);
    void RemoveTarget(ISnmpTarget target);
    void SetInterval(int intervalMs);
    event EventHandler<PollingResult>? OnPollingResult;
}
```

**이벤트:**
- `OnPollingResult`: Polling 결과가 발생할 때마다 호출되는 이벤트

### IMibService

MIB (Management Information Base) 파일을 로드하고 OID ↔ 이름 변환을 제공하는 서비스 인터페이스입니다.

```csharp
public interface IMibService
{
    void LoadMibModules(string directoryPath);
    string? GetOidName(string oid);
    string? GetOid(string name);
    MibTreeNode? GetMibTree();
}
```

**주요 기능:**
- MIB 파일 로드 및 파싱
- OID를 이름으로 변환
- 이름을 OID로 변환
- MIB 트리 구조 제공

---

## 모델 상세

### SnmpResult

SNMP 요청의 결과를 표현하는 모델입니다.

```csharp
public class SnmpResult
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public List<SnmpVariable> Variables { get; init; }
    public long ResponseTime { get; init; }  // 밀리초
    
    public static SnmpResult Success(List<SnmpVariable> variables, long responseTime);
    public static SnmpResult Fail(string errorMessage);
}
```

**사용 예시:**
```csharp
var result = await snmpClient.GetAsync(target, "1.3.6.1.2.1.1.1.0");
if (result.IsSuccess)
{
    var value = result.Variables[0].Value;
}
```

### SnmpVariable

SNMP 변수 (OID + 값)를 표현하는 모델입니다.

```csharp
public class SnmpVariable
{
    public string Oid { get; init; }      // OID (예: "1.3.6.1.2.1.1.1.0")
    public string Value { get; init; }     // 값 (문자열 표현)
    public string TypeCode { get; init; }  // 타입 코드 (예: "OctetString")
}
```

### PollingResult

Polling 서비스의 결과를 표현하는 모델입니다.

```csharp
public class PollingResult
{
    public ISnmpTarget Target { get; init; }
    public DeviceStatus Status { get; init; }      // Up, Down, Unknown
    public long ResponseTime { get; init; }        // 밀리초
    public DateTime Timestamp { get; init; }
    public string Message { get; init; }            // 상태 메시지
}
```

### DeviceStatus

디바이스 상태를 표현하는 열거형입니다.

```csharp
public enum DeviceStatus
{
    Unknown = 0,  // 상태 알 수 없음
    Up = 1,       // 정상 작동 중
    Down = 2      // 장애 또는 응답 없음
}
```

### SnmpVersion

SNMP 버전을 표현하는 열거형입니다.

```csharp
public enum SnmpVersion
{
    V1 = 0,   // SNMP v1
    V2c = 1,  // SNMP v2c (Community-based)
    V3 = 2    // SNMP v3 (USM)
}
```

### PollingProtocol

디바이스 상태 확인에 사용할 프로토콜을 표현하는 열거형입니다.

```csharp
public enum PollingProtocol
{
    SNMP = 0,  // SNMP로 상태 확인 (sysUpTime OID 사용)
    Ping = 1,  // ICMP Ping으로 상태 확인
    ARP = 2,   // ARP로 상태 확인 (미구현)
    None = 3   // Polling 비활성화
}
```

**사용 시나리오:**
- **SNMP**: SNMP 에이전트가 실행 중인 디바이스
- **Ping**: SNMP를 지원하지 않지만 네트워크 연결은 확인하고 싶은 디바이스
- **ARP**: 로컬 네트워크에서 ARP 테이블로 확인 (향후 구현)
- **None**: Polling을 하지 않을 디바이스

### MibTreeNode

MIB 트리 구조를 표현하는 모델입니다.

```csharp
public class MibTreeNode
{
    public string Name { get; set; }           // 노드 이름 (예: "sysDescr")
    public string Oid { get; set; }           // OID (예: "1.3.6.1.2.1.1.1")
    public string? Description { get; set; }   // 설명
    public string NodeType { get; set; }       // 노드 타입
    public ObservableCollection<MibTreeNode> Children { get; }  // 자식 노드
    public bool IsExpanded { get; set; }       // UI 확장 상태
    public bool IsSelected { get; set; }       // UI 선택 상태
}
```

---

## 현재 설계 의도(핵심 원칙)

### 의존성 역전 원칙 (DIP)

- UI는 Core의 **인터페이스만 참조**한다.
- Infrastructure는 Core의 **인터페이스를 구현**한다.
- 이렇게 하면:
  - UI 변경/교체가 쉬워진다
  - SNMP 라이브러리 교체가 쉬워진다 (예: SharpSnmpLib → 다른 라이브러리)
  - 테스트가 쉬워진다 (Mock 객체 사용 가능)

### 단일 책임 원칙 (SRP)

- 각 인터페이스는 하나의 책임만 가진다
  - `ISnmpClient`: SNMP 통신만 담당
  - `IPollingService`: Polling만 담당
  - `IMibService`: MIB 관리만 담당

### 개방-폐쇄 원칙 (OCP)

- 인터페이스를 통해 확장에 열려있고 수정에 닫혀있다
- 새로운 구현체를 추가해도 Core는 변경되지 않는다

---

## 현재 빈 구멍(추가될 가능성이 큰 Core 영역)

### Trap 관련 계약

- `ITrapListener`: SNMP Trap 수신 인터페이스
- `TrapEvent`: Trap 이벤트 모델
- `IEventService`: 이벤트 관리 서비스

### Alarm/Event 도메인 모델

- `EventSeverity`: 이벤트 심각도 (Info, Warning, Error, Critical)
- `Alarm`: 알람 모델 (Ack/Clear, Dedup 등)
- `EventLogEntry`: 이벤트 로그 엔트리

### Polling 정책 모델

- `PollingPolicy`: 재시도, 스케줄링 정책
- `PollingGroup`: 그룹별 OID 세트
- `PollingSchedule`: 스케줄 정보

### Discovery 관련 모델

- `DiscoveryConfig`: Discovery 설정 모델
- `DiscoveredDevice`: 발견된 디바이스 모델
- `FilterEntry`: 필터 엔트리 모델

---

## 사용 예시

### SNMP GET 요청

```csharp
ISnmpClient client = new SnmpClient();
ISnmpTarget target = new UiSnmpTarget
{
    IpAddress = "192.168.1.1",
    Port = 161,
    Community = "public",
    Version = SnmpVersion.V2c
};

var result = await client.GetAsync(target, "1.3.6.1.2.1.1.1.0");
if (result.IsSuccess)
{
    Console.WriteLine($"Value: {result.Variables[0].Value}");
    Console.WriteLine($"Response Time: {result.ResponseTime}ms");
}
```

### Polling 서비스 사용

```csharp
IPollingService pollingService = new PollingService(snmpClient);
pollingService.OnPollingResult += (sender, result) =>
{
    Console.WriteLine($"{result.Target.IpAddress}: {result.Status}");
};

pollingService.AddTarget(target);
pollingService.SetInterval(3000);  // 3초마다
pollingService.Start();
```

### MIB 서비스 사용

```csharp
IMibService mibService = new MibService();
mibService.LoadMibModules(@"C:\Mib");

string? oid = mibService.GetOid("sysDescr");
// 결과: "1.3.6.1.2.1.1.1"

string? name = mibService.GetOidName("1.3.6.1.2.1.1.1.0");
// 결과: "sysDescr.0"
```

---

## 버전 이력

### v1.0 (초기 구현)
- 기본 인터페이스 및 모델 정의
- SNMP 통신, Polling, MIB 서비스 인터페이스

### v1.1 (PollingProtocol 추가)
- `PollingProtocol` enum 추가
- `ISnmpTarget`에 `PollingProtocol` 속성 추가
- 다양한 프로토콜로 상태 확인 지원

---

## 참고 사항

- Core 프로젝트는 **순수 .NET**이므로 플랫폼 독립적입니다
- UI 프로젝트에서 직접 참조하여 사용합니다
- Infrastructure 프로젝트에서 인터페이스를 구현합니다
- 테스트 프로젝트에서 Mock 객체로 사용할 수 있습니다
