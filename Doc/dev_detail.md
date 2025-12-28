# SnmpNms 개발 상세 문서

## 목차

1. [프로젝트 개요](#프로젝트-개요)
2. [아키텍처 개요](#아키텍처-개요)
3. [프로젝트 구조](#프로젝트-구조)
4. [주요 컴포넌트 상세](#주요-컴포넌트-상세)
5. [데이터 모델](#데이터-모델)
6. [UI 구조 및 컴포넌트](#ui-구조-및-컴포넌트)
7. [주요 기능 상세](#주요-기능-상세)
8. [개발 가이드](#개발-가이드)
9. [참고 문서](#참고-문서)

---

## 프로젝트 개요

### 프로젝트명
**SnmpNms** - SNMP Network Management System

### 목적
C#/.NET 9 + WPF 기반의 SNMPc 스타일 경량 NMS(Network Management System) 개발

### 기술 스택
- **프레임워크**: .NET 9.0
- **UI 프레임워크**: WPF (Windows Presentation Foundation)
- **아키텍처 패턴**: MVVM (Model-View-ViewModel)
- **SNMP 라이브러리**: SharpSnmpLib (Lextm.SharpSnmpLib)
- **언어**: C#

### 주요 기능
- SNMP 디바이스 관리 및 모니터링
- 네트워크 디바이스 자동 Discovery
- 실시간 Polling 및 상태 모니터링
- MIB 브라우저 및 트리 뷰
- 이벤트 로그 및 알람 관리
- VS Code 스타일 현대적 UI

---

## 아키텍처 개요

### 계층 구조

```
┌─────────────────────────────────────────┐
│           UI Layer (WPF)                 │
│  (Views, ViewModels, Converters)         │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│      Core Layer (Interfaces/Models)      │
│  (ISnmpClient, IPollingService, Models)   │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼──────────────────────────┐
│   Infrastructure Layer (Implementations)  │
│  (SnmpClient, PollingService, MibService) │
└───────────────────────────────────────────┘
```

### 설계 원칙
1. **의존성 역전 원칙 (DIP)**: UI와 Infrastructure는 Core 인터페이스에 의존
2. **단일 책임 원칙 (SRP)**: 각 클래스는 하나의 책임만 가짐
3. **개방-폐쇄 원칙 (OCP)**: 확장에는 열려있고 수정에는 닫혀있음
4. **인터페이스 분리 원칙 (ISP)**: 클라이언트는 사용하지 않는 인터페이스에 의존하지 않음

---

## 프로젝트 구조

### 솔루션 구조

```
SnmpNms.sln
├── SnmpNms.Core              # 핵심 인터페이스 및 모델
├── SnmpNms.Infrastructure     # 구현체
└── SnmpNms.UI                # WPF UI 애플리케이션
```

### SnmpNms.Core

**목적**: 비즈니스 로직의 핵심 인터페이스 및 데이터 모델 정의

**주요 구성요소**:
- `Interfaces/`: 서비스 인터페이스 정의
  - `ISnmpClient.cs`: SNMP 통신 인터페이스
  - `ISnmpTarget.cs`: SNMP 타겟 디바이스 인터페이스
  - `IPollingService.cs`: Polling 서비스 인터페이스
  - `IMibService.cs`: MIB 서비스 인터페이스
  - `ITrapListener.cs`: Trap 수신 인터페이스
- `Models/`: 데이터 모델 정의
  - `DeviceStatus.cs`: 디바이스 상태 열거형
  - `PollingProtocol.cs`: Polling 프로토콜 열거형
  - `PollingResult.cs`: Polling 결과 모델
  - `SnmpResult.cs`: SNMP 결과 모델
  - `SnmpVariable.cs`: SNMP 변수 모델
  - `SnmpVersion.cs`: SNMP 버전 열거형
  - `MibTreeNode.cs`: MIB 트리 노드 모델
  - `TrapEvent.cs`: Trap 이벤트 모델

**의존성**: 없음 (순수 인터페이스 및 모델)

### SnmpNms.Infrastructure

**목적**: Core 인터페이스의 실제 구현체 제공

**주요 구성요소**:
- `SnmpClient.cs`: SNMP 통신 구현 (SharpSnmpLib 사용)
- `PollingService.cs`: 주기적 Polling 서비스 구현
- `MibService.cs`: MIB 파일 파싱 및 트리 생성 구현
- `TrapListener.cs`: SNMP Trap 수신 서비스 구현

**의존성**:
- `SnmpNms.Core`: 인터페이스 및 모델 참조
- `Lextm.SharpSnmpLib`: SNMP 라이브러리

### SnmpNms.UI

**목적**: WPF 기반 사용자 인터페이스 제공

**주요 구성요소**:
- `MainWindow.xaml/cs`: 메인 윈도우 및 애플리케이션 진입점
- `ViewModels/`: 뷰모델 클래스
  - `MainViewModel.cs`: 메인 뷰모델
  - `EventLogFilterViewModel.cs`: 이벤트 로그 필터 뷰모델
- `Models/`: UI 전용 모델
  - `MapNode.cs`: 맵 트리 노드 모델
  - `UiSnmpTarget.cs`: UI용 SNMP 타겟 모델
  - `EventLogEntry.cs`: 이벤트 로그 엔트리 모델
- `Views/`: 사용자 컨트롤 및 뷰
  - `ActivityBar.xaml/cs`: 왼쪽 Activity Bar
  - `Sidebar.xaml/cs`: 사이드바 패널
  - `SidebarMapView.xaml/cs`: 맵 트리 뷰
  - `SidebarMibView.xaml/cs`: MIB 트리 뷰
  - `BottomPanel.xaml/cs`: 하단 패널
  - `EventLog/`: 이벤트 로그 관련 뷰
  - `MapView/`: 맵 뷰 관련 컨트롤
  - `Dialogs/`: 다이얼로그 창들
- `Converters/`: 값 변환기
  - `DeviceStatusToBrushConverter.cs`: 디바이스 상태 → 색상 변환
  - `StringToVisibilityConverter.cs`: 문자열 → Visibility 변환
- `Resources/`: 리소스 파일
  - `VSCodeTheme.xaml`: VS Code 스타일 테마 정의

**의존성**:
- `SnmpNms.Core`: 인터페이스 및 모델 참조
- `SnmpNms.Infrastructure`: 서비스 구현체 참조
- `System.Windows`: WPF 프레임워크

---

## 주요 컴포넌트 상세

### 1. SNMP Client (`SnmpClient.cs`)

**인터페이스**: `ISnmpClient`

**기능**:
- SNMP GET 요청 (단일/다중 OID)
- SNMP GET-NEXT 요청
- SNMP WALK 요청

**구현 세부사항**:
- SharpSnmpLib 라이브러리 사용
- 비동기 작업 (`Task.Run` 사용)
- 응답 시간 측정 (`Stopwatch` 사용)
- 예외 처리 및 에러 메시지 반환

**주요 메서드**:
```csharp
Task<SnmpResult> GetAsync(ISnmpTarget target, string oid)
Task<SnmpResult> GetAsync(ISnmpTarget target, IEnumerable<string> oids)
Task<SnmpResult> GetNextAsync(ISnmpTarget target, string oid)
Task<SnmpResult> WalkAsync(ISnmpTarget target, string rootOid, CancellationToken cancellationToken = default)
Task<SnmpResult> SetAsync(ISnmpTarget target, string oid, string value, string type)
```

### 2. Polling Service (`PollingService.cs`)

**인터페이스**: `IPollingService`

**기능**:
- 주기적 디바이스 상태 Polling
- 여러 Polling 프로토콜 지원 (SNMP, Ping, ARP, None)
- 타겟 추가/제거 관리
- Polling 주기 설정

**구현 세부사항**:
- `System.Timers.Timer` 사용 (기본 3초 주기)
- `ConcurrentDictionary`로 타겟 관리 (스레드 안전)
- `PollingProtocol`에 따라 다른 방식으로 상태 확인
  - **SNMP**: `sysUpTime` OID로 GET 요청
  - **Ping**: `System.Net.NetworkInformation.Ping` 사용
  - **ARP**: 미구현
  - **None**: Polling 비활성화

**주요 메서드**:
```csharp
void Start()
void Stop()
void AddTarget(ISnmpTarget target)
void RemoveTarget(ISnmpTarget target)
void SetInterval(int intervalMs)
event EventHandler<PollingResult> OnPollingResult
```

### 3. MIB Service (`MibService.cs`)

**인터페이스**: `IMibService`

**기능**:
- MIB 파일 파싱 및 로드
- OID ↔ Name 변환
- MIB 트리 구조 생성

**구현 세부사항**:
- 정규표현식 기반 MIB 파일 파싱
- 의존성 해결을 위한 반복 파싱 (최대 10회)
- 기본 표준 MIB 하드코딩 (sysDescr, sysUpTime 등)
- 트리 구조 생성 및 정렬 (OID 숫자 순)
- 키워드 필터링 (IMPORTS, EXPORTS 등 제외)
- 소문자로 시작하는 이름만 등록

**주요 메서드**:
```csharp
void LoadMibModules(string directoryPath)
string GetOidName(string oid)
string GetOid(string name)
MibTreeNode GetMibTree()
```

### 4. Trap Listener (`TrapListener.cs`)

**인터페이스**: `ITrapListener`

**기능**:
- UDP 포트에서 SNMP Trap 수신
- Trap 파싱 및 이벤트 발생
- 실제 네트워크 IP 주소 자동 감지

**구현 세부사항**:
- UDP 소켓 기반 Trap 수신 (기본 포트 162)
- `CancellationTokenSource`로 비동기 작업 제어
- `Task.Run`으로 백그라운드 리스닝 루프
- SharpSnmpLib의 `MessageFactory.ParseMessages`로 Trap 파싱
- SNMP v1/v2c Trap 지원
- 실제 네트워크 IP 주소 자동 감지 (Ethernet > Wireless 우선순위, Loopback 제외)

**주요 메서드**:
```csharp
void Start(int port = 162)
void Stop()
event EventHandler<TrapEvent> OnTrapReceived
(string ipAddress, int port) GetListenerInfo()
string GetLocalNetworkIp()
```

### 5. Main ViewModel (`MainViewModel.cs`)

**기능**:
- 애플리케이션 상태 관리
- 맵 트리 데이터 관리
- 디바이스 선택 관리
- 이벤트 로그 관리

**주요 속성**:
- `MapRoots`: 맵 트리 루트 노드 컬렉션
- `SelectedMapNodes`: 선택된 맵 노드들 (다중 선택)
- `SelectedDeviceNode`: 현재 선택된 디바이스 노드
- `SelectedDevice`: 현재 선택된 디바이스
- `Events`: 이벤트 로그 엔트리 컬렉션
- `CurrentLog`, `HistoryLog`, `Custom1Log` ~ `Custom8Log`: 필터 뷰모델들

**주요 메서드**:
```csharp
void AddEvent(EventSeverity severity, string? device, string message)
MapNode AddDeviceToSubnet(UiSnmpTarget target, MapNode? subnet = null)
void RemoveDevice(MapNode deviceNode)
```

---

## 데이터 모델

### 1. DeviceStatus (Enum)

```csharp
public enum DeviceStatus
{
    Unknown = 0,
    Up = 1,
    Down = 2
}
```

**용도**: 디바이스 상태 표시

### 2. PollingProtocol (Enum)

```csharp
public enum PollingProtocol
{
    SNMP = 0,
    Ping = 1,
    ARP = 2,
    None = 3
}
```

**용도**: Polling에 사용할 프로토콜 지정

### 3. SnmpVersion (Enum)

```csharp
public enum SnmpVersion
{
    V1 = 0,
    V2c = 1,
    V3 = 2
}
```

**용도**: SNMP 버전 지정

### 4. ISnmpTarget (Interface)

**속성**:
- `string IpAddress`: IP 주소
- `int Port`: 포트 번호 (기본 161)
- `string Community`: Community String
- `SnmpVersion Version`: SNMP 버전
- `int Timeout`: 타임아웃 (밀리초)
- `int Retries`: 재시도 횟수
- `PollingProtocol PollingProtocol`: Polling 프로토콜

**구현체**: `UiSnmpTarget` (UI 모델)

### 5. MapNode (Class)

**속성**:
- `MapNodeType NodeType`: 노드 타입 (RootSubnet, Subnet, Device, Goto)
- `string Name`: 노드 이름
- `UiSnmpTarget? Target`: 디바이스 타겟 (Device 타입일 때)
- `MapNode? Parent`: 부모 노드
- `ObservableCollection<MapNode> Children`: 자식 노드들
- `bool IsExpanded`: 확장 상태
- `bool IsSelected`: 선택 상태
- `DeviceStatus EffectiveStatus`: 유효 상태 (자식 노드들의 최고 우선순위 상태)

**기능**:
- 계층 구조 관리
- 상태 전파 (자식 노드 상태 → 부모 노드 상태)
- INotifyPropertyChanged 구현

### 6. MibTreeNode (Class)

**속성**:
- `string Name`: MIB 노드 이름
- `string Oid`: OID 문자열
- `MibNodeType NodeType`: 노드 타입 (Folder, Table, Scalar, CustomTable)
- `ObservableCollection<MibTreeNode> Children`: 자식 노드들
- `bool IsExpanded`: 확장 상태
- `bool IsSelected`: 선택 상태
- `string? Description`: 설명 (선택적)

**기능**:
- MIB 트리 구조 표현
- INotifyPropertyChanged 구현

### 7. PollingResult (Class)

**속성**:
- `ISnmpTarget Target`: Polling 대상
- `DeviceStatus Status`: 상태
- `long ResponseTime`: 응답 시간 (밀리초)
- `string Message`: 상태 메시지

### 8. SnmpResult (Class)

**속성**:
- `bool IsSuccess`: 성공 여부
- `List<SnmpVariable> Variables`: SNMP 변수 리스트
- `long ResponseTime`: 응답 시간 (밀리초)
- `string? ErrorMessage`: 에러 메시지

**정적 팩토리 메서드**:
```csharp
static SnmpResult Success(List<SnmpVariable> variables, long responseTime)
static SnmpResult Fail(string errorMessage)
```

### 9. EventLogEntry (Class)

**속성**:
- `DateTime Timestamp`: 타임스탬프
- `EventSeverity Severity`: 심각도 (Info, Warning, Error)
- `string? Device`: 디바이스 이름 (선택적)
- `string Message`: 메시지

---

## UI 구조 및 컴포넌트

### VS Code 스타일 레이아웃

```
┌─────────────────────────────────────────────────────┐
│ Menu Bar                                             │
├─────────────────────────────────────────────────────┤
│ ToolBar (2개)                                        │
├─────┬───────────┬─────┬─────────────────────────────┤
│     │           │     │                               │
│ A   │ Sidebar   │ S   │  Editor Area (TabControl)   │
│ c   │           │ p   │                               │
│ t   │ (Map/MIB) │ l   │  - Dashboard                 │
│ i   │           │ i   │  - Map View                   │
│ v   │           │ t   │  - Device                     │
│ i   │           │ t   │  - Alarms                     │
│ t   │           │ e   │  - Performance                │
│ y   │           │ r   │  - MIB Table                  │
│     │           │     │  - MIB Graph                  │
│ B   │           │     │  - SNMP Test                  │
│ a   │           │     │                               │
│ r   │           │     │                               │
├─────┴───────────┴─────┴─────────────────────────────┤
│ Bottom Panel (Event Log)                             │
├─────────────────────────────────────────────────────┤
│ Status Bar                                           │
└─────────────────────────────────────────────────────┘
```

### 주요 UI 컴포넌트

#### 1. ActivityBar (`ActivityBar.xaml/cs`)

**위치**: 왼쪽 가장자리 (세로, 48px 너비)

**기능**:
- 뷰 전환 버튼 제공
- 활성 뷰 표시

**버튼**:
- Map (탐색기)
- Search (검색) - 미구현
- Event Log (이벤트 로그) - 미구현
- Settings (설정) - 미구현
- MIB (MIB 브라우저)

**스타일**: VS Code Activity Bar 스타일 적용

#### 2. Sidebar (`Sidebar.xaml/cs`)

**위치**: Activity Bar 오른쪽 (280px 너비, 조절 가능)

**기능**:
- Activity Bar 선택에 따라 내용 변경
- 헤더 텍스트 표시
- 접기/펼치기 가능

**주요 속성**:
- `HeaderText`: 헤더 텍스트 (Dependency Property)
- `CurrentContent`: 현재 표시할 컨텐츠 (Dependency Property)

#### 3. SidebarMapView (`SidebarMapView.xaml/cs`)

**기능**:
- 맵 트리 뷰 표시
- 디바이스 선택 및 다중 선택 지원
- 드래그 앤 드롭 지원
- 컨텍스트 메뉴 제공

**트리 구조**:
- Root Subnet
  - Default (Subnet)
    - Device 1
    - Device 2
    - ...

**이벤트**:
- `PreviewMouseLeftButtonDown`: 마우스 클릭 처리
- `PreviewMouseMove`: 드래그 시작 감지
- `Drop`: 드롭 처리
- `PreviewKeyDown`: 키보드 입력 처리
- `MapNodeTextMouseLeftButtonDown`: 노드 텍스트 클릭 처리

#### 4. SidebarMibView (`SidebarMibView.xaml/cs`)

**기능**:
- MIB 트리 뷰 표시
- MIB 노드 선택
- 컨텍스트 메뉴 제공 (Get, Get Next, Walk, View Table, Copy OID, Copy Name)

**트리 구조**:
- Snmp Mibs
  - mgmt (1.3.6.1.2.1)
  - Private (1.3.6.1.4.1)
  - Custom-Tables

**이벤트**:
- `MibTreeGetClick`: Get 요청
- `MibTreeGetNextClick`: Get Next 요청
- `MibTreeWalkClick`: Walk 요청
- `MibTreeViewTableClick`: 테이블 뷰
- `MibTreeCopyOidClick`: OID 복사
- `MibTreeCopyNameClick`: 이름 복사

#### 5. BottomPanel (`BottomPanel.xaml/cs`)

**위치**: 하단 (220px 높이, 조절 가능)

**기능**:
- 이벤트 로그 표시
- 탭 인터페이스 (Event Log, Output, Terminal - 현재는 Event Log만 구현)

**주요 메서드**:
```csharp
void SetEventLogContent(EventLogTabControl control)
```

#### 6. MainWindow (`MainWindow.xaml/cs`)

**기능**:
- 애플리케이션 메인 윈도우
- 모든 UI 컴포넌트 통합
- 이벤트 처리 및 명령 바인딩

**주요 탭 (Editor Area)**:
- **Dashboard**: 대시보드 (미구현)
- **Map View**: 맵 뷰 (MapViewControl)
- **Device**: 디바이스 목록 (DataGrid)
- **Alarms**: 알람 콘솔 (미구현)
- **Performance**: 성능 모니터링 (미구현)
- **MIB Table**: MIB 테이블 뷰
- **MIB Graph**: MIB 그래프 (미구현)
- **SNMP Test**: SNMP 테스트 도구

**주요 이벤트 핸들러**:
- `TvDevices_MouseLeftButtonDown`: 맵 트리 클릭 처리
- `PollingService_OnPollingResult`: Polling 결과 처리
- `TrapListener_OnTrapReceived`: Trap 수신 처리
- `ActivityBar_ViewChanged`: Activity Bar 뷰 변경 처리
- `FilterMibTreeByDevice()`: 디바이스 선택에 따른 MIB 트리 필터링
- `ResetMibTreeFilter()`: MIB 트리 필터 리셋

### 다이얼로그

#### 1. DiscoveryPollingAgentsDialog

**기능**: Discovery 및 Polling 설정 구성

**탭**:
- **General**: Discovery 및 Polling 기본 설정
- **Proto**: SNMP 버전 및 Find Options
- **Seeds**: 네트워크 범위 설정
- **Comm**: Community String 관리
- **Filters**: 검색 필터 설정

#### 2. DiscoveryProgressDialog

**기능**: Discovery 진행 상황 표시 및 결과 확인

**기능**:
- 진행 상황 표시
- 발견된 디바이스 목록 표시
- 선택된 디바이스 Map에 추가

#### 3. MapObjectPropertiesDialog

**기능**: Map Object 속성 편집

**탭**:
- **General**: 기본 정보 (Address, Name, Icon, Description)
- **Access**: SNMP 접근 설정 (Read/Write Community, Version)
- **Attributes**: Polling 설정 (Interval, Timeout, Retries, Polling Protocol)
- **Dependencies**: 의존성 설정 (미구현)
- **Trap**: Trap 설정 (Trap Destination IP/Port, Get Trap Info, Configure Trap)

#### 4. CompileMibsDialog

**기능**: MIB 파일 컴파일 및 로드

#### 5. LookupPreviewDialog

**기능**: 디바이스 Lookup 결과 미리보기

#### 6. PingLogWindow

**기능**: Ping 테스트 로그 표시

---

## 주요 기능 상세

### 1. Discovery 기능

**목적**: 네트워크에서 SNMP 디바이스를 자동으로 검색

**프로세스**:
1. Discovery 설정 구성 (DiscoveryPollingAgentsDialog)
2. Seed IP 범위 설정
3. Community String 설정
4. 필터 설정 (Address, Maker, DeviceName)
5. Discovery 실행
6. 진행 상황 표시 (DiscoveryProgressDialog)
7. 발견된 디바이스 목록 확인 및 선택
8. 선택된 디바이스 Map에 추가

**Discovery 옵션**:
- **Enable Discovery**: Discovery 활성화
- **Use Subnet Broadcasts**: 서브넷 브로드캐스트 사용
- **Ping Scan Subnets**: 서브넷 Ping 스캔
- **Find SNMP Versions**: SNMP v1/v2c/v3 검색
- **Find Non-SNMP Nodes**: SNMP 미지원 노드 검색
- **Find RMON Devices**: RMON 디바이스 검색
- **Find TCP Ports**: TCP 포트 검색 (WEB, SMTP, Telnet, FTP)

**필터**:
- **Address Filter**: IP 주소 범위 필터
- **Maker Filter**: 제조사 필터
- **DeviceName Filter**: 디바이스 이름 필터

**SNMP Check 로직**:
- Address 필터만 있는 경우: SNMP Check 생략 (빠른 스캔)
- Maker 또는 DeviceName 필터가 있는 경우: SNMP Check 수행

### 2. Polling 기능

**목적**: 주기적으로 디바이스 상태 확인

**프로세스**:
1. Polling Service 시작 (`Start()`)
2. 타겟 추가 (`AddTarget()`)
3. 주기적으로 Polling 실행 (기본 3초)
4. 결과 이벤트 발생 (`OnPollingResult`)
5. UI 업데이트 (상태 표시)

**Polling 프로토콜**:
- **SNMP**: `sysUpTime` OID로 GET 요청
- **Ping**: ICMP Ping 요청
- **ARP**: 미구현
- **None**: Polling 비활성화

**상태 판단**:
- **Up**: 응답 성공
- **Down**: 응답 실패 또는 타임아웃
- **Unknown**: Polling 비활성화 또는 ARP (미구현)

### 3. MIB 브라우저 기능

**목적**: MIB 트리를 탐색하고 SNMP 요청 실행

**프로세스**:
1. MIB 파일 로드 (`LoadMibModules()`)
2. MIB 트리 생성 (`GetMibTree()`)
3. 트리 뷰에 표시
4. 노드 선택
5. 컨텍스트 메뉴로 SNMP 요청 실행

**지원 SNMP 작업**:
- **Get**: 단일 OID 값 조회
- **Get Next**: 다음 OID 값 조회 (연속 조회 지원)
- **Walk**: OID 서브트리 순회 (취소 가능)
- **View Table**: 테이블 뷰 (미구현)

**MIB View 필터링**:
- 디바이스 선택에 따른 Enterprise OID 필터링
- `IsVisible` 속성으로 필터링 제어
- 디바이스 선택 해제 시 필터 리셋

### 4. 이벤트 로그 기능

**목적**: 시스템 이벤트 및 알람 기록 및 표시

**기능**:
- 이벤트 추가 (`AddEvent()`)
- 필터링 (심각도, 디바이스, 검색어)
- 여러 탭 지원 (Current, History, Custom 1-8)
- 실시간 업데이트
- 자동 스크롤 (새 로그 추가 시 마지막 항목으로 자동 스크롤)

**이벤트 심각도**:
- **Info**: 정보성 메시지
- **Warning**: 경고 메시지
- **Error**: 에러 메시지

**구현 세부사항**:
- `EventLogFilterViewModel`: `Events` 컬렉션 변경 감지로 자동 View Refresh
- `EventLogTabControl`: `LoadingRow` 이벤트와 컬렉션 변경 감지로 자동 스크롤
- DataGrid에 가로/세로 스크롤바 지원

### 5. Map 관리 기능

**목적**: 네트워크 맵 구조 관리

**Map Object 타입**:
- **Root Subnet**: 루트 서브넷
- **Subnet**: 서브넷
- **Device**: 디바이스
- **Goto**: Goto 오브젝트

**기능**:
- Map Object 추가/삭제/편집
- 다중 선택 (Ctrl/Shift 클릭)
- 드래그 앤 드롭
- 상태 전파 (자식 → 부모)

---

## 개발 가이드

### 프로젝트 빌드 및 실행

```powershell
# 빌드
dotnet build SnmpNms.UI/SnmpNms.UI.csproj

# 실행
dotnet run --project SnmpNms.UI/SnmpNms.UI.csproj
```

### 코드 스타일

- **네이밍**: C# 표준 네이밍 규칙 준수
  - 클래스: PascalCase
  - 메서드: PascalCase
  - 속성: PascalCase
  - 필드: camelCase (private), _camelCase (private)
  - 상수: PascalCase
- **인코딩**: UTF-8
- **줄 끝**: CRLF (Windows)
- **들여쓰기**: 4 스페이스

### 아키텍처 규칙

1. **의존성 방향**: UI → Infrastructure → Core
2. **인터페이스 사용**: 모든 서비스는 인터페이스로 정의
3. **의존성 주입**: 현재는 수동 주입 (향후 DI 컨테이너 도입 가능)
4. **비동기 처리**: SNMP 요청은 비동기로 처리

### 새 기능 추가 가이드

#### 1. 새로운 서비스 추가

1. `SnmpNms.Core/Interfaces/`에 인터페이스 정의
2. `SnmpNms.Infrastructure/`에 구현체 작성
3. `SnmpNms.UI`에서 사용

#### 2. 새로운 UI 컴포넌트 추가

1. `SnmpNms.UI/Views/`에 UserControl 생성
2. 필요시 ViewModel 생성 (`SnmpNms.UI/ViewModels/`)
3. `MainWindow.xaml` 또는 적절한 위치에 추가

#### 3. 새로운 다이얼로그 추가

1. `SnmpNms.UI/Views/Dialogs/`에 Window 생성
2. XAML 및 Code-behind 작성
3. 적절한 위치에서 호출

### 테스트

현재는 단위 테스트 프로젝트가 없으나, 향후 추가 예정:
- `SnmpNms.Core.Tests`: Core 로직 테스트
- `SnmpNms.Infrastructure.Tests`: Infrastructure 구현 테스트
- `SnmpNms.UI.Tests`: UI 로직 테스트

### 디버깅

- **이벤트 로그**: 하단 Panel의 Event Log 탭에서 확인
- **디버그 출력**: `System.Diagnostics.Debug.WriteLine()` 사용
- **예외 처리**: 모든 예외는 적절히 처리하고 이벤트 로그에 기록

---

## 참고 문서

### 프로젝트 문서

- `Doc/0_index.md`: 문서 목차
- `Doc/1_dev_plan.md`: 개발 계획
- `Doc/1_1_snmpc_function.md`: SNMPc 기능 분석
- `Doc/2_0_dev_design.md`: 개발 설계
- `Doc/2_3_wpf_skeleton.md`: WPF 스켈레톤 구조
- `Doc/6_map_database.md`: Map Database 작업 정리
- `Doc/7.mib_database.md`: MIB Database 작업 정리
- `Doc/8_discovery_object.md`: Discovery 및 Map Object 관리 기능
- `Doc/9.vs_code_style.md`: VS Code 스타일 UI 변경 계획

### 외부 참고

- **SharpSnmpLib**: https://github.com/lextudio/sharpsnmplib
- **WPF 문서**: https://docs.microsoft.com/en-us/dotnet/desktop/wpf/
- **SNMPc 매뉴얼**: `Doc/intro_snmpc.pdf`

---

## 변경 이력

### 주요 변경사항

#### VS Code 스타일 UI 적용 (Phase 1-3)
- Activity Bar 추가
- Sidebar 구조 변경
- Bottom Panel 추가
- MIB 트리를 Sidebar에 통합
- 밝은 테마 적용

#### Discovery 기능 개선
- SNMP 버전 선택 옵션 추가
- Find Options를 Proto 탭으로 이동
- SNMP Check 로직 개선

#### Polling Protocol 지원
- SNMP, Ping, ARP, None 프로토콜 지원
- Map Object Properties에 Polling Protocol 옵션 추가
- 기본값: SNMP

#### MIB 트리 통합
- Sidebar에 MIB 트리 추가
- 컨텍스트 메뉴 지원
- SNMP 요청 실행 기능

#### TreeView 선택 색상 수정 (해결 완료 ✅)
- **문제**: MAP/MIB 트리 아이템 클릭 시 파란색 배경이 나타남
- **시도한 방법**: 
  - 시도 1-9: VSCodeSelection 색상 변경, 트리거 추가/제거 등 여러 방법 시도
  - 시도 10: `SystemColors.HighlightBrushKey` 오버라이드 (최종 해결)
- **해결 방법**: 
  - `SidebarMapView.xaml`과 `SidebarMibView.xaml`의 `TreeView.Resources`에 `SystemColors.HighlightBrushKey`를 `Transparent`로 오버라이드
  - `SystemColors.InactiveSelectionHighlightBrushKey`도 함께 설정
- **결과**: 
  - ✅ 파란색 완전 제거
  - ✅ WPF 기본 기능 유지 (확장/축소, 포커스 등 정상 작동)
  - ✅ 가장 간단하고 효과적인 방법 (실무에서 90% 사용)

#### Event Log 자동 업데이트 및 자동 스크롤 기능 개선 (2025-12-26)
- **목적**: 하단 Event 창에 Log가 더 잘 표시되도록 개선
- **구현 내용**:
  - `EventLogFilterViewModel`에서 `Events` 컬렉션 변경을 감지하여 View가 자동으로 Refresh되도록 구현
  - `EventLogTabControl`에서 새 로그가 추가될 때 자동으로 마지막 항목으로 스크롤되도록 구현
  - DataGrid에 스크롤바 추가 (가로/세로 스크롤 지원)
- **기술 세부사항**:
  - `Events.CollectionChanged` 이벤트 구독으로 자동 Refresh
  - `LoadingRow` 이벤트와 컬렉션 변경 감지를 통한 자동 스크롤
  - `ScrollViewer.HorizontalScrollBarVisibility="Auto"` 및 `ScrollViewer.VerticalScrollBarVisibility="Auto"` 추가
- **결과**:
  - ✅ 새 로그가 추가되면 자동으로 View가 Refresh됨
  - ✅ 새 로그가 추가되면 자동으로 마지막 항목으로 스크롤됨
  - ✅ 많은 로그가 있어도 스크롤바로 탐색 가능

---

## 향후 계획

### 단기 계획
- ✅ Trap Listener 구현 (완료)
- Polling 안정화 (재진입 방지, Retry 정책)
- MIB 경로/로딩 전략 개선
- MIB View 필터링 개선 (nel 하위 노드 사라지는 문제 해결)

### 중기 계획
- Alarm/Event 모델 고도화
- Performance Chart 구현
- MIB Table View 완성

### 장기 계획
- Topology Discovery
- Distributed Polling
- Web UI (Remote Console)

---

**문서 작성일**: 2025-12-26
**최종 업데이트**: 2025-12-26 (전체 문서 갱신 - Trap Listener, MIB View 필터링, SNMP Test 개선, VS Code 스타일 UI 반영)

