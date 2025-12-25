# SnmpNms.UI 개요

`SnmpNms.UI`는 **WPF 실행 프로젝트(UI 레이어)** 입니다.  
원칙은 “UI는 화면/바인딩/사용자 흐름만 담당하고, 통신/폴링/파싱 구현은 Infrastructure로” 입니다.

---

## 역할

- WPF 앱 엔트리(`App.xaml`)
- 메인 화면(`MainWindow.xaml`)과 사용자 입력 흐름 제공
- Core 인터페이스(`ISnmpClient`, `IPollingService`, `IMibService`)를 사용해 기능을 호출

---

## 의존성/참조

- Target: `net9.0-windows` + WPF
- ProjectReference:
  - `SnmpNms.Core`
  - `SnmpNms.Infrastructure`
- (중요) SharpSnmpLib NuGet은 UI가 아닌 Infrastructure에서만 참조 중

---

## 폴더/파일 구조(현재)

- `App.xaml`, `App.xaml.cs` : WPF 앱
- `MainWindow.xaml`, `MainWindow.xaml.cs` : 메인 콘솔(현재는 Code-behind + ViewModel 혼합)
- `MainWindowCommands.cs` : Map Tree ContextMenu 커맨드(RoutedUICommand) 정의
- `Models/` : UI 표시용 모델(`UiSnmpTarget`, `MapNode`, `EventLogEntry`)
- `ViewModels/` : `MainViewModel`, `EventLogFilterViewModel`
- `Views/` : 공용 컨트롤(`EventLogTabControl`, `MapViewControl`)

---

## 클래스 트리(요약)

- `App : Application`
- `MainWindow : Window`
  - `BtnGet_Click` (SNMP GET)
  - `ChkAutoPoll_Checked/Unchecked` (Auto Poll 시작/중지)
  - `PollingService_OnPollingResult` (상태 표시)
  - `LoadMibs` (MIB 폴더 로드)
- `UiSnmpTarget : ISnmpTarget`
  - `DisplayName`(예: `ip:port`), `Status`(Up/Down/Unknown)
- `MapNode` (Subnet/Device/Goto 트리 노드 + 다중선택 상태)
- `MainViewModel` (MapRoots/SelectedDevice/SelectedMapNodes + Event Log)

---

## 화면 구성(현재 MainWindow)

- 상단: Menu/Toolbar(아이콘 버튼)
- 좌측: Selection Tool (Map Tree)
- 중앙: View Window Area(Tab 기반, Map View 내부 창 Cascade 지원)
- 하단: Event Log Tool(탭/필터/검색)
- SNMP Test 탭: IP/Community/OID 입력 + Get + Auto Poll + 상태 표시

---

## 실행 흐름(현재 코드 기준)

### 앱 시작

- `MainWindow` 생성 시 수동 DI로 아래 구현체 생성
  - `_snmpClient = new SnmpClient()`
  - `_mibService = new MibService()`
  - `_pollingService = new PollingService(_snmpClient)`
- `_pollingService.OnPollingResult` 이벤트를 UI 핸들러에 연결
- `LoadMibs()`로 `Mib` 폴더 로드 시도

### 1) Get 버튼

- 입력값으로 `UiSnmpTarget` 생성 → `_snmpClient.GetAsync(target, oid)` 호출
- OID가 숫자형이 아니면(예: `sysDescr`) `_mibService.GetOid(name)`로 변환 시도
- 출력 시 `_mibService.GetOidName(oid)`로 이름 표시를 보강

### 2) Auto Poll 체크

- 체크 시: 타겟을 추가하고 `_pollingService.Start()`
- 해제 시: `_pollingService.Stop()` 후 입력 IP 기준으로 `RemoveTarget(...)` 호출(현재는 단일 타겟 가정)
- 폴링 결과 이벤트는 `Dispatcher.Invoke(...)`로 UI 스레드에서 상태/로그 업데이트

---

## 최근 안정화 이슈(중요)

### `MenuItem -> TabItem` InvalidCast 크래시

- 증상: XAML 로딩 시 `connectionId` 관련 `XamlParseException` + `InvalidCastException`
- 원인: `TreeView.ItemContainerStyle` 내부 `ContextMenu`에서 `MenuItem Click="..."` 이벤트를 직접 연결하면,
  마크업 컴파일러가 `IComponentConnector.Connect()`에서 `AddHandler(MenuItem.ClickEvent, ...)`를 엮는 과정에서
  target 타입 매핑이 꼬여 크래시가 발생할 수 있음.
- 해결: ContextMenu `Click` 제거 + `Window.CommandBindings`/`RoutedUICommand`(=`MainWindowCommands`)로 전환

---

## 현재 리스크/개선 포인트(UI 관점)

- `MainWindow.xaml.cs`에 로직이 집중되어 있어, 장비 목록/알람 콘솔/맵/차트가 늘면 유지보수가 어려움
  - 권장: `ViewModels` 폴더를 만들고 MVVM로 점진 이동
- `LoadMibs()`에 개발 경로 하드코딩(`D:\git\snmpc\Mib`)이 있음
  - 권장: 실행 경로 기준 `AppDomain.CurrentDomain.BaseDirectory/Mib`로 통일 + 배포 시 포함 전략 수립
