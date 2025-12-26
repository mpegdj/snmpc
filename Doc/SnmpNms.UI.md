# SnmpNms.UI 개요

`SnmpNms.UI`는 **WPF 실행 프로젝트(UI 레이어)** 입니다.  
원칙은 "UI는 화면/바인딩/사용자 흐름만 담당하고, 통신/폴링/파싱 구현은 Infrastructure로" 입니다.

---

## 역할

- **WPF 앱 엔트리**: `App.xaml` 및 `App.xaml.cs`
- **메인 화면**: `MainWindow.xaml` 및 사용자 입력 흐름 제공
- **Core 인터페이스 사용**: `ISnmpClient`, `IPollingService`, `IMibService`를 사용해 기능 호출
- **UI 바인딩**: ViewModel과 데이터 바인딩을 통한 UI 업데이트
- **사용자 인터랙션**: 버튼 클릭, 메뉴 선택, 다이얼로그 표시 등

---

## 의존성/참조

- **Target**: `net9.0-windows` + WPF
- **ProjectReference**:
  - `SnmpNms.Core` (인터페이스 및 모델)
  - `SnmpNms.Infrastructure` (구현체)
- **중요**: SharpSnmpLib NuGet은 UI가 아닌 Infrastructure에서만 참조 중
- **외부 라이브러리**: WPF 프레임워크만 사용 (순수 .NET)

---

## 폴더/파일 구조

```
SnmpNms.UI/
├── App.xaml, App.xaml.cs              # WPF 앱 엔트리
├── MainWindow.xaml, MainWindow.xaml.cs # 메인 콘솔
├── MainWindowCommands.cs                # Map Tree ContextMenu 커맨드 (RoutedUICommand)
├── Models/
│   ├── UiSnmpTarget.cs                 # UI 표시용 SNMP 타겟 모델
│   ├── MapNode.cs                      # Map 트리 노드 모델
│   └── EventLogEntry.cs                # 이벤트 로그 엔트리 모델
├── ViewModels/
│   ├── MainViewModel.cs                # 메인 ViewModel
│   └── EventLogFilterViewModel.cs      # 이벤트 로그 필터 ViewModel
├── Views/
│   ├── Dialogs/
│   │   ├── DiscoveryPollingAgentsDialog.xaml/.cs      # Discovery 설정 다이얼로그
│   │   ├── DiscoveryProgressDialog.xaml/.cs          # Discovery 진행 다이얼로그
│   │   ├── MapObjectPropertiesDialog.xaml/.cs        # Map Object 속성 편집 다이얼로그
│   │   ├── CompileMibsDialog.xaml/.cs                 # MIB 컴파일 다이얼로그
│   │   ├── LookupPreviewDialog.xaml/.cs               # Lookup 미리보기 다이얼로그
│   │   └── PingLogWindow.xaml/.cs                    # Ping 로그 창
│   ├── EventLog/
│   │   └── EventLogTabControl.xaml/.cs               # 이벤트 로그 탭 컨트롤
│   └── MapView/
│       └── MapViewControl.xaml/.cs                    # Map View 컨트롤
└── Converters/
    └── (데이터 변환기들)
```

---

## 주요 클래스 상세

### MainWindow

메인 콘솔 창입니다. SNMPc 스타일의 전체 레이아웃을 제공합니다.

#### 주요 속성

```csharp
public partial class MainWindow : Window
{
    private readonly ISnmpClient _snmpClient;
    private readonly IMibService _mibService;
    private readonly IPollingService _pollingService;
    private readonly MainViewModel _vm;
}
```

#### 주요 메서드

**초기화**
- `MainWindow()`: 서비스 초기화, MIB 로드, 기본 디바이스 추가

**Discovery 관련**
- `FindMapObjects_Click()`: Discovery/Polling Agents 다이얼로그 열기
- `ShowAddMapObjectDialog()`: Map Object 추가 다이얼로그 표시
- `ShowEditMapObjectDialog()`: Map Object 속성 편집 다이얼로그 표시

**SNMP 테스트**
- `BtnGet_Click()`: SNMP GET 요청 실행
- `BtnGetNext_Click()`: SNMP GETNEXT 요청 실행
- `BtnWalk_Click()`: SNMP WALK 요청 실행

**Polling 관련**
- `ChkAutoPoll_Checked()`: Auto Polling 시작
- `ChkAutoPoll_Unchecked()`: Auto Polling 중지
- `PollingService_OnPollingResult()`: Polling 결과 처리

**MIB 관련**
- `LoadMibs()`: MIB 파일 로드
- `InitializeMibTree()`: MIB 트리 초기화
- `LoadMibTableData()`: MIB 테이블 데이터 로드

**Map 관련**
- `SelectNode()`: Map 노드 선택
- `DeleteSelectedNodes()`: 선택된 노드 삭제
- `GetSelectedSubnetOrDefault()`: 선택된 Subnet 또는 기본 Subnet 반환

#### 화면 구성

**상단**
- Menu Bar: File, Edit, View, Tools, Help
- Toolbar: 아이콘 버튼들
  - Find Map Objects (Discovery)
  - Add Device/Subnet/Goto
  - Edit Object Properties
  - 기타 도구들

**좌측**
- Selection Tool: Map Tree (Root Subnet → Subnet → Device/Goto)
  - 다중 선택 지원 (Ctrl/Shift)
  - 우클릭 컨텍스트 메뉴
  - 드래그 앤 드롭 지원

**중앙**
- View Window Area: Tab 기반
  - Map View (내부 창 Cascade 지원)
  - Device Details
  - MIB Tree
  - MIB Table
  - MIB Graph
  - SNMP Test
  - Event Log

**하단**
- Event Log Tool: 탭/필터/검색
  - Current, History, Custom 1-8 탭
  - Severity 필터 (Info, Warning, Error, Critical)
  - 검색 기능
  - 자동 업데이트 및 자동 스크롤 (새 로그 추가 시)

---

### MainViewModel

메인 ViewModel입니다. Map 트리, 디바이스 목록, 이벤트 로그를 관리합니다.

#### 주요 속성

```csharp
public class MainViewModel : INotifyPropertyChanged
{
    // Map 트리
    public ObservableCollection<MapNode> MapRoots { get; }
    public MapNode RootSubnet { get; }
    public MapNode DefaultSubnet { get; }
    
    // 선택 상태
    public ObservableCollection<MapNode> SelectedMapNodes { get; }
    public MapNode? SelectedDeviceNode { get; set; }
    public UiSnmpTarget? SelectedDevice { get; set; }
    
    // 디바이스 목록
    public ObservableCollection<MapNode> DeviceNodes { get; }
    
    // 이벤트 로그
    public ObservableCollection<EventLogEntry> Events { get; }
    
    // 이벤트 로그 필터 (탭별)
    public EventLogFilterViewModel CurrentLog { get; }
    public EventLogFilterViewModel HistoryLog { get; }
    public EventLogFilterViewModel Custom1Log { get; }
    // ... Custom2-8Log
}
```

#### 주요 메서드

- `AddDeviceToSubnet()`: 디바이스를 Subnet에 추가
- `RemoveDeviceNode()`: 디바이스 노드 제거
- `AddSubnet()`: Subnet 추가
- `AddGoto()`: Goto 추가
- `AddEvent()`: 이벤트 추가
- `AddSystemInfo()`: 시스템 정보 이벤트 추가
- `ClearEvents()`: 이벤트 로그 클리어

---

### UiSnmpTarget

UI 표시용 SNMP 타겟 모델입니다. `ISnmpTarget` 인터페이스를 구현합니다.

#### 주요 속성

```csharp
public class UiSnmpTarget : ISnmpTarget, INotifyPropertyChanged
{
    public string IpAddress { get; set; }           // IP 주소
    public int Port { get; set; }                  // 포트 (기본 161)
    public string Alias { get; set; }              // 별칭
    public string Device { get; set; }             // 디바이스 이름
    public string Community { get; set; }          // Community String
    public SnmpVersion Version { get; set; }        // SNMP 버전
    public int Timeout { get; set; }               // 타임아웃
    public int Retries { get; set; }               // 재시도 횟수
    public PollingProtocol PollingProtocol { get; set; }  // Polling 프로토콜
    
    public string EndpointKey { get; }             // "ip:port" 형식 키
    public string DisplayName { get; }             // 표시 이름 (Alias 또는 EndpointKey)
    public DeviceStatus Status { get; set; }       // 상태 (Up/Down/Unknown)
}
```

#### 특징

- `INotifyPropertyChanged` 구현으로 UI 자동 업데이트
- `Status` 변경 시 UI에 자동 반영
- `DisplayName`은 Alias가 있으면 Alias, 없으면 EndpointKey 사용

---

### MapNode

Map 트리 노드 모델입니다. Subnet, Device, Goto를 표현합니다.

#### 주요 속성

```csharp
public class MapNode : INotifyPropertyChanged
{
    public MapNodeType NodeType { get; }           // RootSubnet, Subnet, Device, Goto
    public string Name { get; set; }               // 노드 이름
    public UiSnmpTarget? Target { get; }          // Device인 경우 Target
    public MapNode? Parent { get; private set; }   // 부모 노드
    public ObservableCollection<MapNode> Children { get; }  // 자식 노드
    
    public bool IsExpanded { get; set; }           // 확장 상태
    public bool IsSelected { get; set; }           // 선택 상태
    public DeviceStatus EffectiveStatus { get; }   // 유효 상태 (자식 노드 포함)
    public string DisplayName { get; }             // 표시 이름
}
```

#### 특징

- 계층 구조: RootSubnet → Subnet → Device/Goto
- 상태 전파: 자식 노드의 상태가 부모 노드에 반영
- 다중 선택 지원: `SelectedMapNodes` 컬렉션으로 관리

---

## 다이얼로그 상세

### DiscoveryPollingAgentsDialog

Discovery 및 Polling 설정을 구성하는 다이얼로그입니다.

**주요 기능:**
- Seed IP/Netmask 설정 (CIDR 표기법 지원)
- Community String 관리
- 필터 설정 (Address, Maker, Device Pattern)
- SNMP 버전 선택 (v1, v2c, v3)
- Find Options 설정
- 설정 저장/로드 (`discovery_config.json`)

**자세한 내용**: `Doc/8_discovery_object.md` 참조

### DiscoveryProgressDialog

Discovery 진행 상황을 표시하는 다이얼로그입니다.

**주요 기능:**
- 실시간 검색 진행 상황 표시
- 발견된 디바이스 목록 표시
- 디바이스 선택/해제
- 병렬 IP 스캔 처리
- 필터 적용

**자세한 내용**: `Doc/8_discovery_object.md` 참조

### MapObjectPropertiesDialog

Map Object (Device/Subnet/Goto)의 속성을 편집하는 다이얼로그입니다.

**주요 기능:**
- Attributes 탭: Alias, Device, Address, Polling Protocol
- General 탭: Node Group, Description
- Access 탭: SNMP 버전, Community 설정
- Polling 탭: Polling 간격, 타임아웃, 재시도

**생성자:**
- `MapObjectPropertiesDialog(MapObjectType type, ISnmpClient? snmpClient)`: 새 객체 추가용
- `MapObjectPropertiesDialog(MapObjectType type, UiSnmpTarget target, ISnmpClient? snmpClient)`: 기존 객체 편집용

**자세한 내용**: `Doc/8_discovery_object.md` 참조

---

## 실행 흐름

### 앱 시작

1. `App.xaml` → `App.xaml.cs` 실행
2. `MainWindow` 생성
3. 서비스 초기화 (수동 DI)
   ```csharp
   _snmpClient = new SnmpClient();
   _mibService = new MibService();
   _pollingService = new PollingService(_snmpClient);
   _vm = new MainViewModel();
   ```
4. Polling 이벤트 연결
   ```csharp
   _pollingService.OnPollingResult += PollingService_OnPollingResult;
   ```
5. MIB 파일 로드
   ```csharp
   LoadMibs();
   ```
6. MIB 트리 초기화
   ```csharp
   InitializeMibTree();
   ```
7. 기본 디바이스 추가 (127.0.0.1)
8. UI 표시

### SNMP GET 요청

1. 사용자가 IP, Community, OID 입력
2. `BtnGet_Click()` 호출
3. `BuildTargetFromInputs()`로 `UiSnmpTarget` 생성
4. OID가 이름이면 `_mibService.GetOid()`로 변환
5. `_snmpClient.GetAsync()` 호출
6. 결과 표시
   - OID 이름 변환: `_mibService.GetOidName()`
   - 결과를 텍스트 박스에 표시

### Auto Polling

1. 사용자가 "Auto Poll" 체크박스 선택
2. `ChkAutoPoll_Checked()` 호출
3. `_pollingService.AddTarget()` 호출
4. `_pollingService.Start()` 호출
5. 주기적으로 `PollingService_OnPollingResult` 이벤트 발생
6. UI 스레드에서 상태 업데이트
   ```csharp
   Dispatcher.Invoke(() => {
       SetDeviceStatus(endpointKey, status);
   });
   ```

### Discovery 실행

1. "Find Map Objects" 버튼 클릭
2. `DiscoveryPollingAgentsDialog` 표시
3. 설정 구성 (Seed, Community, Filter 등)
4. "Restart" 버튼 클릭
5. `DiscoveryProgressDialog` 표시
6. 병렬 IP 스캔 시작
7. 발견된 디바이스 목록 표시
8. 사용자가 선택한 디바이스만 Map에 추가

### Map Object 속성 편집

1. Map에서 디바이스 우클릭 → Properties
   - 또는 툴바의 "Edit Object Properties" 버튼 클릭
2. `ShowEditMapObjectDialog()` 호출
3. `MapObjectPropertiesDialog` 표시 (기존 값 로드)
4. 사용자가 속성 수정
5. OK 클릭 시 `UiSnmpTarget` 업데이트

---

## 데이터 바인딩

### MainWindow → MainViewModel

```xml
<Window DataContext="{Binding RelativeSource={RelativeSource Self}, Path=DataContext}">
    <!-- MainWindow.xaml.cs에서 DataContext = _vm 설정 -->
</Window>
```

### Map Tree 바인딩

```xml
<TreeView ItemsSource="{Binding MapRoots}">
    <TreeView.ItemTemplate>
        <HierarchicalDataTemplate ItemsSource="{Binding Children}">
            <TextBlock Text="{Binding DisplayName}"/>
        </HierarchicalDataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

### Event Log 바인딩

```xml
<DataGrid x:Name="dataGridLog"
          ItemsSource="{Binding View}"
          ScrollViewer.HorizontalScrollBarVisibility="Auto"
          ScrollViewer.VerticalScrollBarVisibility="Auto">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Time" Binding="{Binding Timestamp, StringFormat=HH:mm:ss}" Width="90"/>
        <DataGridTextColumn Header="Severity" Binding="{Binding Severity}" Width="90"/>
        <DataGridTextColumn Header="Device" Binding="{Binding Device}" Width="140"/>
        <DataGridTextColumn Header="Message" Binding="{Binding Message}" Width="*"/>
    </DataGrid.Columns>
</DataGrid>
```

**자동 업데이트 및 스크롤**:
- `EventLogFilterViewModel`에서 `Events` 컬렉션 변경 시 자동으로 View Refresh
- `EventLogTabControl`에서 새 로그 추가 시 자동으로 마지막 항목으로 스크롤

---

## 이벤트 처리

### Command Binding

Map Tree의 ContextMenu는 `RoutedUICommand`를 사용합니다.

```csharp
// MainWindowCommands.cs
public static class MainWindowCommands
{
    public static RoutedUICommand MapProperties = new("Properties", "Properties", typeof(MainWindow));
    public static RoutedUICommand MapOpen = new("Open", "Open", typeof(MainWindow));
    // ...
}
```

```xml
<Window.CommandBindings>
    <CommandBinding Command="{x:Static local:MainWindowCommands.MapProperties}" 
                   Executed="CmdMapProperties_Executed"/>
</Window.CommandBindings>
```

**이유**: XAML 컴파일 시 `MenuItem.Click` 이벤트를 직접 연결하면 `InvalidCastException` 발생 가능

### Polling 이벤트

```csharp
_pollingService.OnPollingResult += PollingService_OnPollingResult;

private void PollingService_OnPollingResult(object? sender, PollingResult e)
{
    Dispatcher.Invoke(() => {
        // UI 업데이트
    });
}
```

---

## 최근 안정화 이슈

### MenuItem → TabItem InvalidCast 크래시

**증상**: XAML 로딩 시 `connectionId` 관련 `XamlParseException` + `InvalidCastException`

**원인**: `TreeView.ItemContainerStyle` 내부 `ContextMenu`에서 `MenuItem Click="..."` 이벤트를 직접 연결하면, 마크업 컴파일러가 `IComponentConnector.Connect()`에서 `AddHandler(MenuItem.ClickEvent, ...)`를 엮는 과정에서 target 타입 매핑이 꼬여 크래시 발생

**해결**: ContextMenu `Click` 제거 + `Window.CommandBindings`/`RoutedUICommand`로 전환

---

## 현재 리스크/개선 포인트

### 아키텍처

1. **MainWindow.xaml.cs에 로직 집중**
   - 장비 목록/알람 콘솔/맵/차트가 늘면 유지보수 어려움
   - **권장**: `ViewModels` 폴더를 만들고 MVVM로 점진 이동

2. **수동 DI**
   - 현재는 생성자에서 직접 인스턴스 생성
   - **권장**: DI 컨테이너 도입 고려 (Microsoft.Extensions.DependencyInjection)

### 하드코딩

1. **MIB 경로**
   - `LoadMibs()`에 개발 경로 하드코딩 (`D:\git\snmpc\Mib`)
   - **권장**: 실행 경로 기준 `AppDomain.CurrentDomain.BaseDirectory/Mib`로 통일 + 배포 시 포함 전략 수립

2. **기본 디바이스**
   - 127.0.0.1이 기본으로 추가됨
   - **권장**: 설정 파일 또는 옵션으로 제어 가능하게

### 성능

1. **UI 스레드 블로킹**
   - MIB 파일 로딩 시 UI 블로킹 가능
   - **권장**: 비동기 로딩으로 개선

2. **이벤트 로그**
   - 대량 이벤트 시 성능 저하 가능
   - **권장**: 가상화 또는 페이징 구현

---

## 버전 이력

### v1.0 (초기 구현)
- 기본 UI 구조 및 SNMP 테스트 기능
- Map Tree 및 Event Log 기본 기능

### v1.1 (Discovery 기능)
- Discovery/Polling Agents 다이얼로그
- Discovery 진행 다이얼로그
- 필터 및 Seed 관리

### v1.2 (Polling Protocol)
- MapObjectPropertiesDialog에 Polling Protocol 선택 추가
- Device Properties 편집 기능

---

## 참고 사항

- UI 프로젝트는 **Core 인터페이스만 참조**합니다
- Infrastructure 구현 세부사항은 UI에서 알 수 없습니다
- 이렇게 하면 UI 변경/교체, SNMP 라이브러리 교체가 쉬워집니다
- 테스트 시 Mock 객체를 사용할 수 있습니다
