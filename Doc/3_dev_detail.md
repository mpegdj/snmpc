# 프로젝트 상세 개요 (클래스 트리/주요 함수/실행 흐름)

이 문서는 `snmpc` 프로젝트의 **코드 기준 상세 개요**입니다.  
계획/MVP/로그는 `Doc/0_index.md`에서 안내하는 문서를 참고하세요.

---

## 1) 프로젝트 목표

- C#/.NET 9 + WPF로 **SNMPc 스타일의 경량 NMS(Manager)** 를 구현
- 구조 원칙: **UI → Infrastructure → Core** 의존성 방향 유지
- VS Code 스타일의 현대적 UI 제공

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
  - `WalkAsync(ISnmpTarget target, string rootOid, CancellationToken token)`
  - `SetAsync(ISnmpTarget target, string oid, string value, string type)`
- `IMibService`
  - `LoadMibModules(string directoryPath)`
  - `GetOidName(string oid)`
  - `GetOid(string name)`
  - `GetMibTree()` → `MibTreeNode` 반환
- `IPollingService`
  - `Start()`, `Stop()`
  - `AddTarget(ISnmpTarget target)`, `RemoveTarget(ISnmpTarget target)`
  - `SetInterval(int intervalMs)`
  - `event OnPollingResult`
- `ITrapListener` (신규)
  - `IsListening` (속성)
  - `Start(int port = 162)`, `Stop()`
  - `event OnTrapReceived`
  - `GetListenerInfo()` → `(string ipAddress, int port)`
  - `GetLocalNetworkIp()` → 실제 네트워크 IP 주소 반환

### 3.2 Models (`SnmpNms.Core/Models`)

- `SnmpResult`
  - `IsSuccess`, `ErrorMessage`, `Variables`, `ResponseTime`
  - `Success(...)`, `Fail(...)`
- `SnmpVariable`
  - `Oid`, `Value`, `TypeCode`
- `PollingResult`
  - `Target`, `Status`, `ResponseTime`, `Timestamp`, `Message`
- `TrapEvent` (신규)
  - `SourceIp`, `SourcePort`, `Community`, `Version`, `Timestamp`, `Variables`
- `MibTreeNode` (신규)
  - `Name`, `Oid`, `NodeType` (Folder/Table/Scalar/CustomTable)
  - `Children`, `IsExpanded`, `IsSelected`, `IsVisible` (필터링용)
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
  - `WalkAsync(target, rootOid, token)`
    - `Messenger.Walk(..., WalkMode.WithinSubtree)` 사용
    - `CancellationToken` 지원으로 취소 가능
    - 하위 OID가 없으면 스칼라 OID 직접 조회
  - `SetAsync(target, oid, value, type)`
    - `Messenger.Set(...)` 사용

### 4.2 `PollingService : IPollingService` (`SnmpNms.Infrastructure/PollingService.cs`)

- `System.Timers.Timer` 기반(기본 3000ms)
- 타겟 저장: `ConcurrentDictionary<string, ISnmpTarget>` (key = `ip:port`)
- 매 tick마다 모든 타겟을 병렬 폴링: `Task.WhenAll`
- Alive 체크 OID: `sysUpTime` = `1.3.6.1.2.1.1.3.0`

### 4.3 `MibService : IMibService` (`SnmpNms.Infrastructure/MibService.cs`)

- 목적: **OID ↔ Name** 변환 및 MIB 트리 구조 생성
- 기본 표준 일부는 하드코딩 등록(`sysDescr/sysObjectID/sysUpTime/sysName` 등)
- 파일 로딩: `.mib`, `.txt`를 재귀 스캔 후 정규식으로 파싱
  - `OBJECT IDENTIFIER`, `MODULE-IDENTITY`, `OBJECT-TYPE` 파싱
  - 소문자로 시작하는 이름만 매칭 (키워드 제외)
  - 키워드 필터링: "IMPORTS", "EXPORTS", "FROM", "BEGIN", "END" 등
- `GetOidName(oid)`는 `.0` 처리 + Longest prefix match 지원
- `GetMibTree()`: 계층적 트리 구조 생성
  - 루트: "Snmp Mibs"
    - `mgmt` (1.3.6.1.2.1) - 표준 MIB 서브트리
    - `Private` (1.3.6.1.4.1) - vendor-specific MIB 서브트리
    - `Custom-Tables` - 사용자 정의 테이블
  - OID 숫자 순으로 정렬

### 4.4 `TrapListener : ITrapListener` (`SnmpNms.Infrastructure/TrapListener.cs`) (신규)

- UDP 소켓 기반 Trap 수신 서버
- 포트: 기본 162 (UDP)
- 주요 기능:
  - `Start(port)`: UDP 소켓 바인딩 및 수신 시작
  - `Stop()`: 소켓 닫기 및 수신 중지
  - `OnTrapReceived` 이벤트: Trap 수신 시 발생
  - `GetLocalNetworkIp()`: 실제 네트워크 IP 주소 자동 감지
    - 우선순위: Ethernet > Wireless > 기타
    - Loopback(127.0.0.1) 제외

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
  - `_trapListener = new TrapListener()` (신규)
  - `_vm = new MainViewModel()`
- Polling 이벤트 연결: `_pollingService.OnPollingResult += PollingService_OnPollingResult`
- Trap 이벤트 연결: `_trapListener.OnTrapReceived += TrapListener_OnTrapReceived` (신규)
- MIB 로드: `LoadMibs()`
- 주요 핸들러(핵심)
  - `BtnGet_Click`: SNMP GET 테스트
  - `BtnGetNext_Click`: SNMP GET-NEXT 테스트 (신규)
  - `BtnWalk_Click`: SNMP WALK 테스트 (신규)
  - `BtnStopWalk_Click`: WALK 취소 (신규)
  - `ChkAutoPoll_Checked/Unchecked`: Auto Poll 시작/중지
  - `PollingService_OnPollingResult`: 상태 라벨/Map Tree 상태 업데이트
  - `TrapListener_OnTrapReceived`: Trap 수신 시 Event Log에 기록 (신규)
  - Map Tree 조작(다중 선택/드래그&드롭/삭제/더블클릭)
  - MIB View 필터링: `FilterMibTreeByDevice()` (신규)
  - MIB View 선택 시 SNMP Test 탭 자동 업데이트 (신규)

### 5.3 Map Tree 모델 (`SnmpNms.UI/Models`)

- `UiSnmpTarget : ISnmpTarget, INotifyPropertyChanged`
  - `DisplayName` = `ip:port`
  - `Status` 변경 시 UI 갱신
  - `Maker`, `SysObjectId` 속성 추가 (신규)
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

### 5.5 MIB View (`SnmpNms.UI/Views/SidebarMibView.xaml(.cs)`) (신규)

- 계층적 MIB 트리 표시
- 노드 선택 시 SNMP Test 탭의 OID 필드 자동 업데이트
- 컨텍스트 메뉴:
  - Get, Get Next, Walk
  - View Table, Copy OID, Copy Name
- 디바이스 선택에 따른 필터링:
  - 선택된 디바이스의 Enterprise OID와 일치하는 노드만 표시
  - `IsVisible` 속성으로 필터링 제어

### 5.6 SNMP Test 탭 (`SnmpNms.UI/MainWindow.xaml`) (개선)

- 입력 필드: IP Address, Community, OID
- 버튼: Get, Get Next, Walk, Stop (Walk 취소용)
- 결과 표시 영역: `[Index/Total] | OID Name | OID | Type | Value` 형식
- Status 영역: 작업 상태 및 소요 시간 표시
- Auto Poll 체크박스: 자동 Polling 시작/중지

---

## 6) Map Object 등록(팝업)

### 6.1 다이얼로그 (`SnmpNms.UI/Views/Dialogs/MapObjectPropertiesDialog.xaml(.cs)`)

- 탭 구조:
  - **General**: 기본 정보 (Address, Alias, Device, Icon, Description)
  - **Access**: SNMP 접근 설정 (Read/Write Community, Version)
  - **Attributes**: Polling 설정 (Interval, Timeout, Retries, Polling Protocol)
  - **Trap**: Trap 설정 (신규)
    - Trap Destination IP/Port 설정
    - "Get Trap Info" 버튼: 현재 Trap 설정 조회
    - "Configure Trap" 버튼: Trap 설정 적용
    - Trap 정보 표시 영역
- 지원 타입: `Device/Subnet/Goto`
- Add Device Object UX:
  - Address 입력 후 Enter 또는 Lookup 버튼으로
    - Ping 확인
    - SNMP GET(`sysDescr/sysObjectID/sysName`)로 `ObjectName/Description` 자동 채움
  - Maker 정보 자동 추출 (신규)
  - SysObjectId 저장 (신규)

### 6.2 Trap 설정 기능 (신규)

- **Trap Destination IP**: 기본값은 NMS의 실제 네트워크 IP (127.0.0.1 아님)
- **Trap Destination Port**: 기본값 162
- **Get Trap Info**: 표준 SNMP Trap OID (`1.3.6.1.6.3.18.1.3.0`)로 현재 설정 조회
- **Configure Trap**: 
  - 표준 OID로 Trap 설정
  - MVE5000/MVD5000의 경우 전용 Trap Destination Table 사용 (미구현)

### 6.3 연결 위치

- 우측 Edit Button Bar의
  - Add Device / Add Subnet / Add Goto 버튼에서 팝업을 띄우고
  - OK 시 `MainViewModel`을 통해 Map Tree에 추가

---

## 7) Discovery 기능

### 7.1 DiscoveryPollingAgentsDialog (`SnmpNms.UI/Views/Dialogs/DiscoveryPollingAgentsDialog.xaml(.cs)`)

- Discovery 및 Polling 설정 구성
- 탭 구조:
  - **General**: Discovery 및 Polling 기본 설정
  - **Proto**: SNMP 버전 및 Find Options
  - **Seeds**: 네트워크 범위 설정 (CIDR 표기법 지원)
  - **Comm**: Community String 관리
  - **Filters**: 검색 필터 설정 (Address, Maker, Device Pattern)
- 설정 저장/로드: `discovery_config.json`

### 7.2 DiscoveryProgressDialog (`SnmpNms.UI/Views/Dialogs/DiscoveryProgressDialog.xaml(.cs)`)

- Discovery 진행 상황 표시 및 결과 확인
- 주요 기능:
  - 실시간 검색 진행 상황 표시 (타임스탬프 포함)
  - 발견된 디바이스 목록 표시 (DataGrid)
  - 디바이스 선택/해제 (체크박스)
  - 필터링 옵션:
    - Maker/Device 드롭다운: 선택된 항목의 체크박스 토글
    - All, SNMP, Ping, V1, V2 버튼: 조건별 일괄 선택/해제
  - 옵션:
    - "Add devices without subnet": 서브넷 없이 Default 아래에 직접 추가
    - "Configure Trap Destination": 선택된 디바이스에 Trap 설정
    - "Standard NTT": MVE5000/MVD5000 전용 Trap 설정
- 병렬 IP 스캔 처리
- Maker 정보 자동 추출 (sysObjectID 기반)

---

## 8) 주요 기능 상세

### 8.1 SNMP 통신

- **GET**: 단일 OID 조회
- **GET-NEXT**: 다음 OID 조회 (연속 조회 지원)
- **WALK**: 하위 트리 순회 (취소 가능)
- **SET**: OID 값 설정 (Trap 설정 등)

### 8.2 Polling

- 주기적 SNMP GET 요청으로 디바이스 상태 모니터링
- `sysUpTime` OID로 Alive 체크
- 상태 변경 시 Event Log에 기록
- Map Tree의 상태 아이콘 자동 업데이트

### 8.3 Trap 수신 (신규)

- UDP 포트 162에서 Trap 메시지 수신
- Trap 수신 시 Event Log에 자동 기록
- Status Bar에 Trap Listener 상태 표시
- Trap 수신 이벤트: `OnTrapReceived`

### 8.4 MIB 관리

- MIB 파일 파싱 및 OID ↔ Name 변환
- 계층적 MIB 트리 구조 생성
- 디바이스 선택에 따른 필터링
- MIB View에서 OID 선택 시 SNMP Test 탭 자동 업데이트

### 8.5 디바이스 Discovery

- 네트워크 범위 기반 자동 검색
- Ping 및 SNMP GET으로 디바이스 발견
- Maker 정보 자동 추출
- 발견된 디바이스를 Map에 일괄 추가

---

## 9) 실행 흐름

### 9.1 앱 시작

1. `App.xaml` → `App.xaml.cs` 실행
2. `MainWindow` 생성
3. 서비스 초기화 (수동 DI)
   ```csharp
   _snmpClient = new SnmpClient();
   _mibService = new MibService();
   _pollingService = new PollingService(_snmpClient);
   _trapListener = new TrapListener();
   _vm = new MainViewModel();
   ```
4. Polling 이벤트 연결
5. Trap 이벤트 연결 (신규)
6. MIB 로드: `LoadMibs()`

### 9.2 디바이스 추가

1. Map Object Properties Dialog 열기
2. Address 입력 및 Lookup 실행
3. Ping 및 SNMP GET으로 정보 조회
4. Maker 정보 추출 (신규)
5. OK 클릭 시 Map Tree에 추가

### 9.3 Discovery 실행

1. DiscoveryPollingAgentsDialog에서 설정 구성
2. Discovery 시작
3. DiscoveryProgressDialog에서 진행 상황 확인
4. 발견된 디바이스 선택
5. OK 클릭 시 Map에 추가
6. 선택 시 Trap 설정 적용 (옵션)

### 9.4 MIB View 필터링 (신규)

1. Map Object View에서 디바이스 선택
2. `FilterMibTreeByDevice()` 호출
3. 디바이스의 `SysObjectId`에서 Enterprise OID 추출
4. Private 노드의 자식 중 일치하는 노드만 표시
5. 디바이스 선택 해제 시 필터 리셋

---

## 10) 현재 설계상 리스크/다음 확장 포인트

### 10.1 완료된 항목

- ✅ **Trap Listener**: UDP 162 수신 + Event Log 연결
- ✅ **MIB 파서 개선**: 키워드 필터링, 이름 검증 강화
- ✅ **MIB View 필터링**: 디바이스 선택에 따른 OID 필터링
- ✅ **SNMP Test 개선**: Get Next, Walk 기능 추가
- ✅ **Discovery 개선**: Maker 정보 추출, Trap 설정 옵션

### 10.2 향후 개선 사항

- **Polling 안정성**: Timer 재진입/중첩 폴링 방지, Retry 정책 반영
- **MIB 파서 고도화**: IMPORTS/ENUM/TYPE/Table 등 완전한 SMIv2 문법 지원
- **Map Database 저장소**: 객체 속성(Icon/Groups/Access/Attributes)을 노드에 구조적으로 저장/로드(DB/파일)
- **Trap 설정 고도화**: MVE5000/MVD5000 전용 Trap Destination Table 완전 구현
- **MIB View 필터링 개선**: nel 하위 노드 사라지는 문제 해결

---

## 11) 참고 문서

- `Doc/0_index.md`: 프로젝트 전체 개요 및 문서 인덱스
- `Doc/7.mib_database.md`: MIB Database 상세 문서
- `Doc/8_discovery_object.md`: Discovery 기능 상세 문서
- `Doc/12.trap_engine.md`: Trap Listener 구현 문서
- `Doc/13.Snmp_Test_rev.md`: SNMP Test 탭 개선 문서

---

## 12) 변경 이력

- **2024-12-XX**: 문서 초기 작성
- **2024-12-XX**: Trap Listener 기능 추가 반영
- **2024-12-XX**: MIB View 필터링 기능 추가 반영
- **2024-12-XX**: SNMP Test 탭 개선 사항 반영
- **2024-12-XX**: Discovery 기능 개선 사항 반영
- **2024-12-XX**: 전체 문서 갱신 및 구조 개선
