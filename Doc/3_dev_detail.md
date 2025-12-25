# 프로젝트 상세 개요 (클래스 트리/주요 함수/실행 흐름)

이 문서는 `snmpc` 프로젝트의 **코드 기준 상세 개요**입니다.  
계획/MVP/로그는 `Doc/0_index.md`에서 안내하는 문서를 참고하세요.

---

## 1) 프로젝트 목표

- C#/.NET 9 + WPF로 **SNMPc 스타일의 경량 NMS(Manager)** 를 구현
- 구조 원칙: **UI → Infrastructure → Core** 의존성 방향 유지

---

## 2) 솔루션/프로젝트 구조

- `SnmpNms.Core`
  - **도메인 모델 + 인터페이스(계약)** 만 포함
  - 외부 라이브러리 의존 없음
- `SnmpNms.Infrastructure`
  - Core 인터페이스의 **구현체**
  - SNMP 통신 라이브러리(SharpSnmpLib) 의존
- `SnmpNms.UI`
  - WPF 실행 프로젝트
  - 화면/바인딩/사용자 흐름 + ViewModel

---

## 3) Core 클래스/인터페이스 트리

### 3.1 Interfaces (`SnmpNms.Core/Interfaces`)

- `ISnmpTarget`
  - `IpAddress`, `Port`, `Community`, `Version`, `Timeout`, `Retries`
- `ISnmpClient`
  - `GetAsync(ISnmpTarget target, string oid)`
  - `GetAsync(ISnmpTarget target, IEnumerable<string> oids)`
  - `GetNextAsync(ISnmpTarget target, string oid)`
  - `WalkAsync(ISnmpTarget target, string rootOid)`
- `IMibService`
  - `LoadMibModules(string directoryPath)`
  - `GetOidName(string oid)`
  - `GetOid(string name)`
- `IPollingService`
  - `Start()`, `Stop()`
  - `AddTarget(ISnmpTarget target)`, `RemoveTarget(ISnmpTarget target)`
  - `SetInterval(int intervalMs)`
  - `event OnPollingResult`

### 3.2 Models (`SnmpNms.Core/Models`)

- `SnmpResult`
  - `IsSuccess`, `ErrorMessage`, `Variables`, `ResponseTime`
  - `Success(...)`, `Fail(...)`
- `SnmpVariable`
  - `Oid`, `Value`, `TypeCode`
- `PollingResult`
  - `Target`, `Status`, `ResponseTime`, `Timestamp`, `Message`
- `DeviceStatus` (enum)
  - `Unknown`, `Up`, `Down`
- `SnmpVersion` (enum)
  - `V1`, `V2c`, `V3`

---

## 4) Infrastructure 구현 트리

### 4.1 `SnmpClient : ISnmpClient` (`SnmpNms.Infrastructure/SnmpClient.cs`)

- SharpSnmpLib를 사용해 SNMP 요청을 수행하고 `SnmpResult`로 변환
- 주요 메서드
  - `GetAsync(target, oid|oids)`
    - `Messenger.Get(...)` 사용
  - `GetNextAsync(target, oid)`
    - `GetNextRequestMessage` 기반
  - `WalkAsync(target, rootOid)`
    - `Messenger.Walk(..., WalkMode.WithinSubtree)`

### 4.2 `PollingService : IPollingService` (`SnmpNms.Infrastructure/PollingService.cs`)

- `System.Timers.Timer` 기반(기본 3000ms)
- 타겟 저장: `ConcurrentDictionary<string, ISnmpTarget>` (key = `ip:port`)
- 매 tick마다 모든 타겟을 병렬 폴링: `Task.WhenAll`
- Alive 체크 OID: `sysUpTime` = `1.3.6.1.2.1.1.3.0`

### 4.3 `MibService : IMibService` (`SnmpNms.Infrastructure/MibService.cs`)

- 목적: **OID ↔ Name** 최소 변환
- 기본 표준 일부는 하드코딩 등록(`sysDescr/sysObjectID/sysUpTime/sysName` 등)
- 파일 로딩: `.mib`, `.txt`를 재귀 스캔 후 단순 정규식으로 `OBJECT-TYPE ::= { parent n }`만 파싱
- `GetOidName(oid)`는 `.0` 처리 + Longest prefix match 지원

---

## 5) UI 레이어(화면/상호작용)

### 5.1 App (`SnmpNms.UI/App.xaml(.cs)`)

- `DispatcherUnhandledException` / `UnhandledException`을 받아 `crash.log`로 남길 수 있음
  - 기본 위치: `SnmpNms.UI/bin/Debug/net9.0-windows/crash.log`

### 5.2 MainWindow (`SnmpNms.UI/MainWindow.xaml(.cs)`)

- 수동 DI로 구현체 생성
  - `_snmpClient = new SnmpClient()`
  - `_mibService = new MibService()`
  - `_pollingService = new PollingService(_snmpClient)`
  - `_vm = new MainViewModel()`
- Polling 이벤트 연결: `_pollingService.OnPollingResult += PollingService_OnPollingResult`
- MIB 로드: `LoadMibs()`
- 주요 핸들러(핵심)
  - `BtnGet_Click`: SNMP GET 테스트
  - `ChkAutoPoll_Checked/Unchecked`: Auto Poll 시작/중지
  - `PollingService_OnPollingResult`: 상태 라벨/Map Tree 상태 업데이트
  - Map Tree 조작(다중 선택/드래그&드롭/삭제/더블클릭)

### 5.3 Map Tree 모델 (`SnmpNms.UI/Models`)

- `UiSnmpTarget : ISnmpTarget, INotifyPropertyChanged`
  - `DisplayName` = `ip:port`
  - `Status` 변경 시 UI 갱신
- `MapNode : INotifyPropertyChanged`
  - `NodeType` = `RootSubnet/Subnet/Device/Goto`
  - `Children`, `Parent`, `IsExpanded`, `IsSelected`
  - `EffectiveStatus` (하위 상태 집계: Down > Unknown > Up)
  - `RecomputeEffectiveStatus()` 로 상태 재계산

### 5.4 Event Log (`SnmpNms.UI/Models`, `SnmpNms.UI/ViewModels`, `SnmpNms.UI/Views`)

- `EventLogEntry`, `EventSeverity`
- `EventLogFilterViewModel`
  - 탭별 독립 필터: Scope(All/SelectedDevice), Severity, Search
- `EventLogTabControl`
  - DataGrid 기반 표시/필터

---

## 6) Map Object 등록(팝업)

### 6.1 다이얼로그 (`SnmpNms.UI/Views/Dialogs/MapObjectPropertiesDialog.xaml(.cs)`)

- 탭: `General / Access / Attributes / Dependencies(placeholder)`
- 지원 타입: `Device/Subnet/Goto`
- Add Device Object UX:
  - Address 입력 후 Enter 또는 Lookup 버튼으로
    - Ping 확인
    - SNMP GET(`sysDescr/sysObjectID/sysName`)로 `ObjectName/Description` 자동 채움

### 6.2 연결 위치

- 우측 Edit Button Bar의
  - Add Device / Add Subnet / Add Goto 버튼에서 팝업을 띄우고
  - OK 시 `MainViewModel`을 통해 Map Tree에 추가

---

## 7) “현재 설계상 리스크/다음 확장 포인트”

- **Polling 안정성**: Timer 재진입/중첩 폴링 방지, Retry 정책 반영
- **MIB 파서 고도화**: IMPORTS/ENUM/TYPE/Table 등
- **Trap Listener**: UDP 162 수신 + Event/Alarm 모델 연결
- **Map Database 저장소**: 객체 속성(Icon/Groups/Access/Attributes)을 노드에 구조적으로 저장/로드(DB/파일)

