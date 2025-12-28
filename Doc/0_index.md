# Doc 인덱스 (문서 내비게이션)
실행 명령어
방법 1: 프로젝트 지정하여 실행 (권장)
dotnet run --project SnmpNms.UI/SnmpNms.UI.csproj
방법 2: UI 프로젝트 디렉토리로 이동 후 실행
cd SnmpNms.UIdotnet runcd ..
dotnet build .\SnmpNms.UI\SnmpNms.UI.csproj -c Debug
방법 3: 솔루션 빌드 후 실행
dotnet build SnmpNms.slndotnet run --project SnmpNms.UI/SnmpNms.UI.csproj
실행 전 확인사항
포트 162 권한: UDP 162는 관리자 권한이 필요할 수 있습니다.
관리자 권한으로 실행하거나
MainWindow.xaml.cs의 InitializeTrapListener()에서 포트를 1162로 변경
MIB 파일 경로: MainWindow.xaml.cs의 LoadMibs()에서 MIB 경로 확인
기본값: D:\git\snmpc\Mib
없으면 실행 파일 기준 ./Mib 폴더 사용
빠른 테스트 방법
애플리케이션 실행
SNMP Test 탭 이동
Trap Test 섹션에서:
Trap Target: 127.0.0.1
Trap Port: 162 (또는 설정한 포트)
Trap OID: 1.3.6.1.4.1.1.1.1.1
Send Trap 버튼 클릭
하단 Event Log에서 Trap 수신 확인
가장 간단한 방법은 방법 1입니다. 현재 디렉토리(d:\git\snmpc)에서 실행하면 됩니다.

이 폴더는 `snmpc` 프로젝트의 **계획/운영/로그/레퍼런스** 문서를 모아두는 곳입니다.

---

## 지금 이 프로젝트는 무엇인가

- **목표**: C#/.NET 9 + WPF로 **SNMPc 스타일의 경량 NMS(Network Management System)** 를 구현
- **아키텍처**: `SnmpNms.Core` → `SnmpNms.Infrastructure` → `SnmpNms.UI` (UI는 인터페이스만 사용)
- **주요 기능**:
  - SNMP 통신 (GET, GETNEXT, WALK)
  - 네트워크 디바이스 Discovery
  - 디바이스 상태 Polling (SNMP, Ping, ARP)
  - MIB 파일 로드 및 OID ↔ 이름 변환
  - Map View (네트워크 맵 시각화)
  - Event Log (이벤트 로깅 및 필터링)

---

## 문서 역할(무엇을 어디에 쓰나)

### 프로젝트별 개요 (코드 구조/흐름/리스크)

- **[SnmpNms.Core.md](SnmpNms.Core.md)**: Core 프로젝트 개요
  - 인터페이스 및 모델 정의
  - 도메인 모델 상세 설명
  - 설계 원칙 및 사용 예시

- **[SnmpNms.Infrastructure.md](SnmpNms.Infrastructure.md)**: Infrastructure 프로젝트 개요
  - Core 인터페이스 구현체
  - SharpSnmpLib 어댑터
  - PollingService, SnmpClient, MibService 상세

- **[SnmpNms.UI.md](SnmpNms.UI.md)**: UI 프로젝트 개요
  - WPF 앱 구조 및 화면 구성
  - MainWindow, ViewModel, 다이얼로그 상세
  - 실행 흐름 및 데이터 바인딩

### 기능별 상세 문서

- **[8_discovery_object.md](8_discovery_object.md)**: Discovery 및 Map Object 관리 기능
  - Discovery 설정 및 실행
  - 필터 및 Seed 관리
  - Polling Protocol 기능
  - Map Object 속성 편집
  - CIDR 기반 서브넷 자동 배치

- **[7.mib_database.md](7.mib_database.md)**: MIB 데이터베이스 기능
  - MIB 파일 로드 및 파싱
  - OID ↔ 이름 변환
  - MIB 트리 구조

- **[6_map_database.md](6_map_database.md)**: Map 데이터베이스 기능
  - Map 트리 구조
  - 디바이스/Subnet/Goto 관리
  - 상태 전파 및 표시

- **[5_dev_logs.md](5_dev_logs.md)**: 개발 로그 (SSOT)
  - 작업 기록 원장
  - 변경 이력 및 이슈 추적
  - 모든 작업 기록의 단일 원장

- **[4_dev_ops.md](4_dev_ops.md)**: 운영/개발 환경
  - 실행 방법
  - 빌드 및 배포
  - 트러블슈팅

- **[3_dev_detail.md](3_dev_detail.md)**: 개발 상세 사항 (코드 기준)
  - 코드 구조 및 클래스 트리
  - 주요 함수 및 실행 흐름
  - 구현 세부사항

- **[dev_detail.md](dev_detail.md)**: 개발 상세 문서 (포괄적)
  - 프로젝트 개요 및 아키텍처
  - 주요 컴포넌트 상세
  - 데이터 모델 및 UI 구조
  - 개발 가이드 및 변경 이력

### UI/디자인 문서

- **[9.vs_code_style.md](9.vs_code_style.md)**: VS Code 스타일 UI 변경 계획
  - VS Code 스타일 레이아웃 구조
  - Activity Bar, Sidebar, BottomPanel 구현
  - Phase별 구현 현황

- **[10_slidebar_map_min_color.md](10_slidebar_map_min_color.md)**: Sidebar Map/MIB 트리 선택 색상 변경 기록
  - 문제 상황 및 해결 과정
  - 시도한 모든 방법 기록
  - 최종 해결 방법

- **[11.renewal_gui.md](11.renewal_gui.md)**: GUI Renewal 계획
  - SNMPc 원래 화면 구조를 VS Code 스타일로 재구성
  - 최종 구조 및 레이아웃 계획

### 계획 및 설계 문서

- **[1_dev_plan.md](1_dev_plan.md)**: 개발 계획
  - MVP 정의 및 수용 기준
  - 체크리스트 및 로드맵

- **[1_1_snmpc_function.md](1_1_snmpc_function.md)**: SNMPc 기능 맵
  - SNMPc 기능 레퍼런스
  - 구현 대상 기능 목록

- **[2_0_dev_design.md](2_0_dev_design.md)**: 개발 설계
  - 아키텍처 설계
  - 기술 스택 및 구조

- **[2_3_wpf_skeleton.md](2_3_wpf_skeleton.md)**: WPF 스켈레톤
  - XAML 기본 구조
  - 레이아웃 템플릿

- **[2_4_wpf_detail.md](2_4_wpf_detail.md)**: WPF 상세
  - UI 컴포넌트 상세
  - 스타일 및 리소스

- **[2_5_wpf_mapview_event_handle.md](2_5_wpf_mapview_event_handle.md)**: MapView 이벤트 처리
  - MapView 이벤트 핸들링
  - 상호작용 로직

- **[2_6_function.md](2_6_function.md)**: 기능 구현
  - 기능별 구현 가이드

### 레퍼런스

- **[intro_snmpc.pdf](intro_snmpc.pdf)**: SNMPc UI 레퍼런스 (PDF)
- **[README.md](README.md)**: 문서 정리 가이드 및 빠른 참조

---

## 빠른 시작

### 개발 환경 설정

1. **필수 요구사항**
   - .NET 9 SDK
   - Visual Studio 2022 또는 VS Code
   - Windows (WPF 지원)

2. **프로젝트 클론**
   ```powershell
   git clone <repository-url>
   cd snmpc
   ```

3. **빌드**
   ```powershell
   dotnet build SnmpNms.sln
   ```

4. **실행**
   ```powershell
   dotnet run --project SnmpNms.UI/SnmpNms.UI.csproj
   ```

### 프로젝트 구조

```
snmpc/
├── SnmpNms.Core/              # 인터페이스 및 모델
├── SnmpNms.Infrastructure/    # 구현체 (SharpSnmpLib 어댑터)
├── SnmpNms.UI/                # WPF 앱
├── Doc/                       # 문서
└── Mib/                       # MIB 파일 (선택사항)
```

---

## 트러블슈팅(자주 터지는 케이스)

### WPF XAML Parse/connectionId/InvalidCast 오류

**증상**: XAML 로딩 시 `XamlParseException` 또는 `InvalidCastException` 발생

**원인**: 
- XAML 컴파일 캐시 문제
- ContextMenu 이벤트 바인딩 문제

**해결**:
```powershell
# bin/obj 폴더 삭제 후 클린 빌드
Remove-Item -Recurse -Force .\SnmpNms.UI\bin, .\SnmpNms.UI\obj -ErrorAction SilentlyContinue
dotnet clean SnmpNms.UI
dotnet build SnmpNms.UI
```

### 앱 실행 시 크래시

**증상**: 앱 실행 직후 크래시

**확인**:
- `SnmpNms.UI/bin/Debug/net9.0-windows/crash.log` 파일 확인
- `DispatcherUnhandledException` 핸들러가 예외를 로그에 기록

**일반적인 원인**:
- MIB 파일 경로 문제
- 설정 파일 읽기 오류
- 네트워크 권한 문제

### Discovery가 느림

**증상**: Discovery 실행 시 속도가 느림

**해결**:
- 병렬 처리로 개선됨 (v1.4)
- 필터를 사용하여 스캔 범위 축소
- Seed 범위를 적절히 설정

### Polling이 작동하지 않음

**증상**: Auto Polling을 시작해도 상태가 업데이트되지 않음

**확인**:
- Polling Protocol 설정 확인 (SNMP/Ping)
- Community String 확인
- 네트워크 연결 확인
- Event Log에서 오류 메시지 확인

---

## 주요 기능 가이드

### 1. SNMP 테스트

1. **SNMP Test 탭** 선택
2. IP 주소 입력 (예: `192.168.1.1`)
3. Community 입력 (예: `public`)
4. OID 입력 (예: `1.3.6.1.2.1.1.1.0` 또는 `sysDescr`)
5. **Get** 버튼 클릭
6. 결과 확인

### 2. Discovery 실행

1. **Find Map Objects** 버튼 클릭 (왼쪽 위 툴바)
2. **Seeds 탭**에서 네트워크 범위 추가
3. **Comm 탭**에서 Community String 추가
4. **Filters 탭**에서 필터 설정 (선택사항)
5. **Restart** 버튼 클릭
6. 발견된 디바이스 선택 후 **OK** 클릭

### 3. 디바이스 속성 편집

1. Map에서 디바이스 **우클릭** → **Properties**
   - 또는 툴바의 **Edit Object Properties** 버튼 클릭
2. **Attributes 탭**에서 Polling Protocol 변경
3. **Access 탭**에서 SNMP 설정 변경
4. **OK** 클릭

### 4. Auto Polling 시작

1. **SNMP Test 탭** 선택
2. IP 및 Community 입력
3. **Auto Poll** 체크박스 선택
4. 상태 업데이트 확인 (상단 상태 표시)

---

## 아키텍처 개요

### 레이어 구조

```
┌─────────────────┐
│   SnmpNms.UI    │  ← WPF 앱 (사용자 인터페이스)
│  (WPF 프로젝트)  │
└────────┬────────┘
         │ 참조
         ▼
┌─────────────────┐
│ SnmpNms.Core    │  ← 인터페이스 및 모델 (계약)
│  (순수 .NET)    │
└────────┬────────┘
         │ 구현
         ▼
┌─────────────────┐
│SnmpNms.Infra... │  ← 구현체 (SharpSnmpLib 어댑터)
│  (SharpSnmpLib) │
└─────────────────┘
```

### 의존성 방향

- **UI → Core**: 인터페이스만 참조
- **Infrastructure → Core**: 인터페이스 구현
- **UI → Infrastructure**: 구현체 사용 (DI)

### 핵심 원칙

1. **의존성 역전 원칙 (DIP)**: UI는 인터페이스만 참조
2. **단일 책임 원칙 (SRP)**: 각 레이어는 명확한 책임
3. **개방-폐쇄 원칙 (OCP)**: 확장에 열려있고 수정에 닫혀있음

---

## 개발 워크플로우

### 새 기능 추가

1. **Core에 인터페이스 정의** (필요시)
2. **Infrastructure에 구현** (필요시)
3. **UI에서 사용**
4. **문서 업데이트**

### 버그 수정

1. **이슈 확인** (Event Log 또는 crash.log)
2. **원인 분석**
3. **수정**
4. **테스트**
5. **문서 업데이트** (필요시)

### 문서 업데이트

- **코드 변경 시**: 관련 문서 자동 업데이트
- **기능 추가 시**: 기능 문서에 추가
- **이슈 해결 시**: 트러블슈팅 섹션에 추가

---

## 참고 자료

### 외부 라이브러리

- **SharpSnmpLib**: SNMP 통신 라이브러리
  - NuGet: `Lextm.SharpSnmpLib` (12.5.7)
  - 문서: https://sharpsnmplib.codeplex.com/

### SNMP 관련

- **SNMP RFC**: RFC 1157 (SNMPv1), RFC 3416 (SNMPv2c), RFC 3414 (SNMPv3)
- **MIB 파일**: 표준 MIB 파일은 IETF에서 제공

### WPF 관련

- **Microsoft WPF 문서**: https://docs.microsoft.com/dotnet/desktop/wpf/
- **MVVM 패턴**: https://docs.microsoft.com/dotnet/desktop/wpf/get-started/

---

## 변경 이력

### v1.0 (초기 구현)
- 기본 UI 구조
- SNMP 통신 기능
- Map Tree 및 Event Log

### v1.1 (Discovery 기능)
- Discovery/Polling Agents 다이얼로그
- Discovery 진행 다이얼로그
- 필터 및 Seed 관리

### v1.2 (Polling Protocol)
- PollingProtocol enum 추가
- MapObjectPropertiesDialog에 Polling Protocol 선택 추가
- PollingService에서 Protocol별 처리 구현

### v1.3 (Auto Polling 개선)
- Auto Polling 로그는 앱 시작 시에만 기록
- Start/Stop Poll 시 모든 기기 polling
- 필터링은 표시에만 관련, polling에는 영향 없음

### v1.4 (Discovery CIDR 기반 서브넷 배치)
- Discovery 후 기기를 CIDR 기반 서브넷에 자동 배치
- Seed 정보를 기반으로 적절한 서브넷 찾기/생성
- 서브넷 이름 형식: `네트워크주소/CIDR` (예: `192.168.0.0/24`)

---

## 문의 및 기여

- **이슈 리포트**: GitHub Issues 사용
- **기능 제안**: GitHub Discussions 사용
- **문서 개선**: Pull Request 환영

---

## 프로젝트 파일 구조 및 중요 클래스

### SnmpNms.Core 프로젝트

#### Interfaces (인터페이스)

**`ISnmpClient.cs`**
- **클래스**: `ISnmpClient` (인터페이스)
- **목적**: SNMP 통신 인터페이스 정의
- **주요 메서드**:
  - `Task<SnmpResult> GetAsync(ISnmpTarget target, string oid)` - 단일 OID GET 요청
  - `Task<SnmpResult> GetAsync(ISnmpTarget target, IEnumerable<string> oids)` - 다중 OID GET 요청
  - `Task<SnmpResult> GetNextAsync(ISnmpTarget target, string oid)` - GET-NEXT 요청
  - `Task<SnmpResult> WalkAsync(ISnmpTarget target, string rootOid)` - WALK 요청

**`ISnmpTarget.cs`**
- **클래스**: `ISnmpTarget` (인터페이스)
- **목적**: SNMP 타겟 디바이스 인터페이스 정의
- **주요 속성**: `IpAddress`, `Port`, `Community`, `Version`, `Timeout`, `Retries`, `PollingProtocol`

**`IPollingService.cs`**
- **클래스**: `IPollingService` (인터페이스)
- **목적**: 주기적 Polling 서비스 인터페이스 정의
- **주요 메서드**: `Start()`, `Stop()`, `AddTarget()`, `RemoveTarget()`, `SetInterval()`
- **이벤트**: `OnPollingResult`

**`IMibService.cs`**
- **클래스**: `IMibService` (인터페이스)
- **목적**: MIB 파일 파싱 및 OID 변환 서비스 인터페이스 정의
- **주요 메서드**: `LoadMibModules()`, `GetOidName()`, `GetOid()`, `GetMibTree()`

#### Models (데이터 모델)

**`DeviceStatus.cs`**
- **클래스**: `DeviceStatus` (enum)
- **값**: `Unknown`, `Up`, `Down`
- **목적**: 디바이스 상태 표시

**`PollingProtocol.cs`**
- **클래스**: `PollingProtocol` (enum)
- **값**: `SNMP`, `Ping`, `ARP`, `None`
- **목적**: Polling에 사용할 프로토콜 지정

**`SnmpVersion.cs`**
- **클래스**: `SnmpVersion` (enum)
- **값**: `V1`, `V2c`, `V3`
- **목적**: SNMP 버전 지정

**`SnmpResult.cs`**
- **클래스**: `SnmpResult`
- **목적**: SNMP 요청 결과 모델
- **주요 속성**: `IsSuccess`, `Variables`, `ResponseTime`, `ErrorMessage`
- **정적 메서드**: `Success()`, `Fail()`

**`SnmpVariable.cs`**
- **클래스**: `SnmpVariable`
- **목적**: SNMP 변수 모델
- **주요 속성**: `Oid`, `TypeCode`, `Value`

**`PollingResult.cs`**
- **클래스**: `PollingResult`
- **목적**: Polling 결과 모델
- **주요 속성**: `Target`, `Status`, `ResponseTime`, `Timestamp`, `Message`

**`MibTreeNode.cs`**
- **클래스**: `MibTreeNode`, `MibNodeType` (enum)
- **목적**: MIB 트리 노드 모델
- **주요 속성**: `Name`, `Oid`, `NodeType`, `Children`, `IsExpanded`, `IsSelected`, `Description`

---

### SnmpNms.Infrastructure 프로젝트

**`SnmpClient.cs`**
- **클래스**: `SnmpClient` (구현: `ISnmpClient`)
- **목적**: SharpSnmpLib을 사용한 SNMP 통신 구현
- **의존성**: `Lextm.SharpSnmpLib`
- **주요 기능**: GET, GET-NEXT, WALK 요청 처리, 응답 시간 측정, 예외 처리

**`PollingService.cs`**
- **클래스**: `PollingService` (구현: `IPollingService`)
- **목적**: 주기적 디바이스 상태 Polling 서비스 구현
- **주요 기능**: 
  - `System.Timers.Timer` 사용 (기본 3초 주기)
  - `ConcurrentDictionary`로 타겟 관리 (스레드 안전)
  - Protocol별 상태 확인 (SNMP: sysUpTime, Ping: ICMP, ARP: 미구현)
- **이벤트**: `OnPollingResult`

**`MibService.cs`**
- **클래스**: `MibService` (구현: `IMibService`)
- **목적**: MIB 파일 파싱 및 OID ↔ Name 변환 구현
- **주요 기능**: 
  - 정규표현식 기반 MIB 파일 파싱
  - 의존성 해결을 위한 반복 파싱 (최대 10회)
  - 기본 표준 MIB 하드코딩 (sysDescr, sysUpTime 등)
  - 트리 구조 생성 및 정렬

---

### SnmpNms.UI 프로젝트

#### Main Files (메인 파일)

**`App.xaml` / `App.xaml.cs`**
- **클래스**: `App` (Application)
- **목적**: WPF 애플리케이션 진입점
- **주요 기능**: 리소스 로드, 예외 처리

**`MainWindow.xaml` / `MainWindow.xaml.cs`**
- **클래스**: `MainWindow` (Window)
- **목적**: 메인 윈도우 및 애플리케이션 진입점
- **주요 속성**: 
  - `_snmpClient`, `_mibService`, `_pollingService`, `_vm`
  - `_sidebarMapView`, `_tvDevices`, `_treeMib`
- **주요 메서드**:
  - `InitializeVSCodeUI()` - VS Code 스타일 UI 초기화
  - `LoadMibs()` - MIB 파일 로드
  - `InitializeMibTree()` - MIB 트리 초기화
  - `PollingService_OnPollingResult()` - Polling 결과 처리
  - `ActivityBar_ViewChanged()` - Activity Bar 뷰 변경 처리

**`MainWindowCommands.cs`**
- **클래스**: `MainWindowCommands` (정적 클래스)
- **목적**: Map Tree ContextMenu용 RoutedUICommand 정의
- **주요 명령**: `MapProperties`, `MapOpen`, `MapQuickPoll`, `MapMibTable`, `MapDelete`

#### ViewModels (뷰모델)

**`MainViewModel.cs`**
- **클래스**: `MainViewModel` (구현: `INotifyPropertyChanged`)
- **목적**: 애플리케이션 상태 관리 및 데이터 바인딩
- **주요 속성**:
  - `MapRoots` - 맵 트리 루트 노드 컬렉션
  - `SelectedMapNodes` - 선택된 맵 노드들 (다중 선택)
  - `SelectedDeviceNode` - 현재 선택된 디바이스 노드
  - `SelectedDevice` - 현재 선택된 디바이스
  - `Events` - 이벤트 로그 엔트리 컬렉션
  - `CurrentLog`, `HistoryLog`, `Custom1Log` ~ `Custom8Log` - 필터 뷰모델들
- **주요 메서드**:
  - `AddEvent()` - 이벤트 추가
  - `AddSystemInfo()` - 시스템 정보 이벤트 추가
  - `AddDeviceToSubnet()` - 디바이스를 Subnet에 추가
  - `RemoveDeviceNode()` - 디바이스 노드 제거
  - `AddSubnet()` - Subnet 추가
  - `AddGoto()` - Goto 추가

**`EventLogFilterViewModel.cs`**
- **클래스**: `EventLogFilterViewModel` (구현: `INotifyPropertyChanged`)
- **열거형**: `EventLogScope`, `EventSeverityFilter`
- **목적**: 이벤트 로그 필터링 및 뷰 관리
- **주요 속성**:
  - `Name` - 필터 이름
  - `View` - 필터링된 ICollectionView
  - `Events` - 원본 이벤트 컬렉션
  - `Scope` - 필터 범위 (All, SelectedDevice)
  - `Severity` - 심각도 필터 (Any, Info, Warning, Error)
  - `SearchText` - 검색어
- **주요 메서드**: `Refresh()` - 필터 뷰 갱신

#### Models (UI 모델)

**`MapNode.cs`**
- **클래스**: `MapNode` (구현: `INotifyPropertyChanged`)
- **열거형**: `MapNodeType` (RootSubnet, Subnet, Device, Goto)
- **목적**: Map 트리 노드 모델
- **주요 속성**:
  - `NodeType` - 노드 타입
  - `Name` - 노드 이름
  - `Target` - 디바이스 타겟 (Device 타입일 때)
  - `Parent` - 부모 노드
  - `Children` - 자식 노드들
  - `IsExpanded` - 확장 상태
  - `IsSelected` - 선택 상태
  - `EffectiveStatus` - 유효 상태 (자식 노드들의 최고 우선순위 상태)
  - `DisplayName` - 표시 이름
- **주요 메서드**: `AddChild()`, `RemoveChild()`, `UpdateEffectiveStatus()`

**`UiSnmpTarget.cs`**
- **클래스**: `UiSnmpTarget` (구현: `ISnmpTarget`, `INotifyPropertyChanged`)
- **목적**: UI 표시용 SNMP 타겟 모델
- **주요 속성**:
  - `IpAddress` - IP 주소
  - `Port` - 포트 번호 (기본 161)
  - `Alias` - 별칭
  - `Device` - 디바이스 이름
  - `Community` - Community String
  - `Version` - SNMP 버전
  - `Timeout` - 타임아웃 (밀리초)
  - `Retries` - 재시도 횟수
  - `PollingProtocol` - Polling 프로토콜
  - `EndpointKey` - 고유 키 ("ip:port" 형식)
  - `DisplayName` - 표시 이름 (Alias 우선)
  - `Status` - 상태 (Up/Down/Unknown)

**`EventLogEntry.cs`**
- **클래스**: `EventLogEntry`
- **열거형**: `EventSeverity` (Info, Warning, Error)
- **목적**: 이벤트 로그 엔트리 모델
- **주요 속성**: `Timestamp`, `Severity`, `Device`, `Message`

#### Views (뷰)

**`ActivityBar.xaml` / `ActivityBar.xaml.cs`**
- **클래스**: `ActivityBar` (UserControl)
- **열거형**: `ActivityBarView` (Map, Mib, Search, EventLog, Settings)
- **목적**: VS Code 스타일 Activity Bar (왼쪽 세로 버튼 바)
- **주요 속성**: `CurrentView`, `ViewChanged` 이벤트

**`Sidebar.xaml` / `Sidebar.xaml.cs`**
- **클래스**: `Sidebar` (UserControl)
- **목적**: VS Code 스타일 Sidebar (Activity Bar + 콘텐츠 영역)
- **주요 속성**:
  - `HeaderText` (DependencyProperty) - 헤더 텍스트
  - `CurrentContent` (DependencyProperty) - 현재 표시할 컨텐츠
  - `CurrentView` - 현재 뷰
- **이벤트**: `ViewChanged`

**`SidebarMapView.xaml` / `SidebarMapView.xaml.cs`**
- **클래스**: `SidebarMapView` (UserControl)
- **목적**: Sidebar에 표시할 Map 트리 뷰
- **주요 기능**: Map 트리 표시, 다중 선택, 드래그 앤 드롭, 컨텍스트 메뉴
- **이벤트**: `MapNodeTextMouseLeftButtonDown`

**`SidebarMibView.xaml` / `SidebarMibView.xaml.cs`**
- **클래스**: `SidebarMibView` (UserControl)
- **목적**: Sidebar에 표시할 MIB 트리 뷰
- **주요 기능**: MIB 트리 표시, 노드 선택, 컨텍스트 메뉴 (Get, Get Next, Walk, View Table, Copy OID, Copy Name)
- **이벤트**: `MibTreeGetClick`, `MibTreeGetNextClick`, `MibTreeWalkClick`, `MibTreeViewTableClick`, `MibTreeCopyOidClick`, `MibTreeCopyNameClick`

**`Panel.xaml` / `Panel.xaml.cs`**
- **클래스**: `BottomPanel` (UserControl)
- **목적**: 하단 패널 (Event Log, Output, Terminal 탭)
- **주요 기능**: 탭 인터페이스, Event Log 콘텐츠 설정
- **주요 메서드**: `SetEventLogContent()`, `ToggleVisibility()`

**`EventLog/EventLogTabControl.xaml` / `EventLogTabControl.xaml.cs`**
- **클래스**: `EventLogTabControl` (UserControl)
- **목적**: Event Log 탭 컨트롤 (필터 + DataGrid)
- **주요 기능**: 
  - 필터 바 (Scope, Severity, Search)
  - DataGrid로 로그 표시
  - 자동 스크롤 (새 로그 추가 시)
- **주요 속성**: `dataGridLog` (DataGrid)

**`MapView/MapViewControl.xaml` / `MapViewControl.xaml.cs`**
- **클래스**: `MapViewControl` (UserControl)
- **목적**: Map View 컨트롤 (내부 창 Cascade 지원)
- **주요 기능**: Subnet 창 표시, Cascade 정렬, 내부 창 관리

#### Dialogs (다이얼로그)

**`Dialogs/DiscoveryPollingAgentsDialog.xaml` / `DiscoveryPollingAgentsDialog.xaml.cs`**
- **클래스**: `DiscoveryPollingAgentsDialog` (Window)
- **목적**: Discovery 및 Polling 설정 구성 다이얼로그
- **주요 기능**: Seed IP/Netmask 설정, Community String 관리, 필터 설정, SNMP 버전 선택, Find Options 설정, 설정 저장/로드

**`Dialogs/DiscoveryProgressDialog.xaml` / `DiscoveryProgressDialog.xaml.cs`**
- **클래스**: `DiscoveryProgressDialog` (Window)
- **목적**: Discovery 진행 상황 표시 및 결과 확인 다이얼로그
- **주요 기능**: 진행 상황 표시, 발견된 디바이스 목록 표시, 병렬 IP 스캔 처리, 필터 적용

**`Dialogs/MapObjectPropertiesDialog.xaml` / `MapObjectPropertiesDialog.xaml.cs`**
- **클래스**: `MapObjectPropertiesDialog` (Window)
- **목적**: Map Object (Device/Subnet/Goto) 속성 편집 다이얼로그
- **주요 기능**: General/Access/Attributes/Dependencies 탭, Lookup 기능, Ping 테스트

**`Dialogs/MapObjectType.cs`**
- **열거형**: `MapObjectType` (Device, Subnet, Goto)
- **목적**: Map Object 타입 정의

**`Dialogs/CompileMibsDialog.xaml` / `CompileMibsDialog.xaml.cs`**
- **클래스**: `CompileMibsDialog` (Window)
- **목적**: MIB 파일 컴파일 및 로드 다이얼로그

**`Dialogs/LookupPreviewDialog.xaml` / `LookupPreviewDialog.xaml.cs`**
- **클래스**: `LookupPreviewDialog` (Window)
- **목적**: 디바이스 Lookup 결과 미리보기 다이얼로그

**`Dialogs/PingLogWindow.xaml` / `PingLogWindow.xaml.cs`**
- **클래스**: `PingLogWindow` (Window)
- **목적**: Ping 테스트 로그 표시 창
- **주요 기능**: 연속 Ping (1초 간격), Stop 기능

#### Converters (변환기)

**`Converters/DeviceStatusToBrushConverter.cs`**
- **클래스**: `DeviceStatusToBrushConverter` (구현: `IValueConverter`)
- **목적**: 디바이스 상태 → 색상 변환 (Unknown=Gray, Up=Green, Down=Red)

**`Converters/StringToVisibilityConverter.cs`**
- **클래스**: `StringToVisibilityConverter` (구현: `IValueConverter`)
- **목적**: 문자열 → Visibility 변환

#### Resources (리소스)

**`Resources/VSCodeTheme.xaml`**
- **목적**: VS Code 스타일 테마 정의
- **주요 리소스**: 색상 브러시, 스타일, 패널 헤더 스타일, 탭 스타일 등

---

## 라이선스

(프로젝트 라이선스 정보)
