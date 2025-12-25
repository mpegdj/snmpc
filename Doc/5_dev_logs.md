# 작업 로그 (단일 원장 / SSOT)

이 파일(`5_dev_logs.md`)은 Cursor 기반 **모든 작업 기록의 단일 원장(SSOT)** 입니다.  
매 작업 종료 시 아래 포맷으로 누적 기록합니다.

---

## 고정 로그 포맷(항상 사용)

- **날짜시간(분)**: `YYYY-MM-DD HH:mm`
- **제목**
- **작업내용**: 무엇을/왜/결과가 무엇인지
- **변경사항(파일/라인)**:
  - `파일명` : `Lx-Ly` (무엇이 바뀌었는지)

---

## 레거시 로그 이관(참고)

- **원칙**
  - 과거 로그에서 핵심 마일스톤만 이 파일로 이관했습니다.
  - 앞으로 작업 로그는 `Doc/5_dev_logs.md`만 사용(SSOT).

---

## 2025-12-25 (시간 미확인) — SNMPc 스타일 참고 자료 확정(공식 Getting Started PDF)

- **작업내용**
  - SNMPc UI/흐름 레퍼런스로 공식 Getting Started PDF를 기준 문서로 채택.
  - 콘솔 레이아웃(좌측 탐색/우측 작업영역), Map DB 개념, 이벤트/알람 흐름, MIB 데이터 뷰를 WPF GUI 설계에 매핑.

- **변경사항(파일/라인)**
  - `Doc/intro_snmpc.pdf` : (레퍼런스 추가)

---

## 2025-12-25 (시간 미확인) — UI 위치 원칙 확정(`SnmpNms.UI`)

- **작업내용**
  - WPF UI(View/XAML/리소스)는 `SnmpNms.UI` 프로젝트에 위치시키고,
    통신/폴링/트랩 수신 같은 구현은 Infrastructure로 유지하는 원칙을 확정.
  - 의존성 방향: `SnmpNms.UI` → `SnmpNms.Infrastructure` → `SnmpNms.Core`

- **변경사항(파일/라인)**
  - `Doc/5_dev_logs.md` : (레거시 원칙 이관 기록)

---

## 2025-12-25 (시간 미확인) — 프로젝트별 코드 개요 문서 3종 생성

- **작업내용**
  - 프로젝트 3개(`SnmpNms.Core`, `SnmpNms.Infrastructure`, `SnmpNms.UI`)의 역할/의존성/구조/리스크를 개요 문서로 고정.

- **변경사항(파일/라인)**
  - `Doc/SnmpNms.Core.md` : (생성/정리)
  - `Doc/SnmpNms.Infrastructure.md` : (생성/정리)
  - `Doc/SnmpNms.UI.md` : (생성/정리)

---

## 2025-12-25 20:13 — 메인 GUI(SNMPc 스타일) 셸 구축

- **작업내용**
  - `MainWindow`를 SNMPc 스타일 콘솔 레이아웃으로 개편(상단 메뉴/툴바, 좌측 트리, 우측 탭, 하단 로그/상태바).
  - 기존 기능(SNMP GET / Auto Poll)은 SNMP Test 탭으로 이동해 계속 동작 유지.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/MainWindow.xaml` : (레이아웃 개편)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (이벤트/연결 코드 정리)

---

## 2025-12-25 (시간 미확인) — Map View(Cascade) / Event Log 탭 확장

- **작업내용**
  - Map View 탭 내부에서 내부 창(겹침) + Cascade 정렬 최소 구현.
  - 하단 Event Log Tool 탭 확장(Custom 탭 다수).

- **변경사항(파일/라인)**
  - `SnmpNms.UI/Views/MapView/MapViewControl.xaml` : (추가/연결)
  - `SnmpNms.UI/Views/MapView/MapViewControl.xaml.cs` : (Cascade/Window 관리)

---

## 2025-12-25 20:50 — Tool Bar/Edit Bar 아이콘 버튼화 + Log Window 리사이즈

- **작업내용**
  - 상단 Tool Bar를 아이콘 버튼 스타일로 통일.
  - 하단 Event Log Tool 영역을 GridSplitter로 리사이즈 가능하게 개선.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/MainWindow.xaml` : (툴바/리사이즈 UI)

---

## 2025-12-25 21:15 — Event Log Tool 필터링 구현(탭별 독립 필터)

- **작업내용**
  - Event Log를 단순 텍스트 출력에서 DataGrid 기반 구조화된 리스트로 전환.
  - 탭별로 Scope/Severity/Search 필터를 독립 적용하도록 구현.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/Models/EventLogEntry.cs` : (추가)
  - `SnmpNms.UI/ViewModels/EventLogFilterViewModel.cs` : (추가)
  - `SnmpNms.UI/Views/EventLog/EventLogTabControl.xaml` : (추가)
  - `SnmpNms.UI/Views/EventLog/EventLogTabControl.xaml.cs` : (추가)
  - `SnmpNms.UI/ViewModels/MainViewModel.cs` : (Events/필터/누적)
  - `SnmpNms.UI/MainWindow.xaml` : (Event Log 영역 교체)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (로그 기록 방식 전환)

---

## 2025-12-25 21:25 — Map Selection Tree(SNMPc 매뉴얼 동작) 구현

- **작업내용**
  - Map Tree를 Subnet/Device/Goto 계층 트리로 전환.
  - Ctrl/Shift 다중 선택, Drag&Drop 이동, Delete 삭제, Subnet 더블클릭으로 Map View Open/Cascade 동작 최소 구현.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/Models/MapNode.cs` : (추가)
  - `SnmpNms.UI/Converters/DeviceStatusToBrushConverter.cs` : (추가)
  - `SnmpNms.UI/Models/UiSnmpTarget.cs` : (Status/INPC)
  - `SnmpNms.UI/MainWindow.xaml` : (Tree Template/ContextMenu 등)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (다중선택/드래그드롭/삭제/더블클릭)

---

## 2025-12-25 (22:00) — 프로젝트 파악 및 Opus 작업 로그 초기화

- **작업내용**
  - 사용자 요청: 기존 개발 로그를 참고하여 현재 프로젝트 상태 파악
  - 프로젝트 개요 분석 완료:
    - **목표**: C#/.NET 9 + WPF로 SNMPc 스타일 경량 NMS 개발
    - **아키텍처**: `SnmpNms.Core` → `SnmpNms.Infrastructure` → `SnmpNms.UI` (의존성 방향)
  - 현재 구현 완료된 기능:
    - SNMP GET (UI 버튼)
    - 최소 MIB 로딩/이름 변환
    - Polling Scheduler (3초 Timer) + Up/Down 상태 표시
    - SNMPc 스타일 GUI 셸 (좌측 트리 + 우측 탭)
    - Map View (내부 창 + Cascade 정렬)
    - Event Log Tool (필터링: 전체/선택장비/Severity/검색)
    - Map Selection Tree (계층 구조, 상태 색상, 다중선택, 드래그&드롭)
  - 미구현/다음 작업 후보 확인:
    - Trap Receiver/Listener (UDP 162)
    - MIB 파서 품질 개선
    - MIB 경로 하드코딩 해결
    - Polling 안정성 (재진입/Retry/Timeout)
    - 다중 장비 관리 UI 확장
  - 작업 로그 파일(`5_dev_logs.md`)을 단일 원장(SSOT)으로 사용

- **변경사항(파일/라인)**
  - `Doc/5_dev_logs.md` : `L1-L(끝)` (작업 로그 원장/SSOT)

---

## 다음 작업 후보 (사용자 선택 대기)

| 옵션 | 작업 | 설명 |
| --- | --- | --- |
| A | Trap Receiver | UDP 162 리스너 구현 (NMS 핵심 축) |
| B | MIB 경로 설정 | 하드코딩 제거 + 동적 로딩 UI |
| C | Polling 안정화 | 재진입 방지, Retry 로직 |
| D | 기타 | 사용자 요청사항 |

---

## 2025-12-25 22:03 — `dotnet run` 크래시(XamlParse/InvalidCast) 원인 수정 및 실행 확인

- **작업내용**
  - 사용자 보고 크래시: 앱 시작 시 `XamlParseException` / `InvalidCastException` (connectionId 설정 중 예외) 이슈 대응.
  - `MainWindow.xaml`의 `TreeView.ItemContainerStyle` 구간에 **디버그 메모(예: `161->`)가 XAML 태그 앞에 실제 텍스트로 삽입**되어 있어,
    XAML 파싱/연결 단계에서 예외를 유발할 수 있는 상태를 확인하고 제거.
  - `TreeViewItem.IsSelected`는 현재 구조(코드비하인드에서 다중 선택을 직접 관리)와 충돌/부작용 가능성이 있어
    **TwoWay → OneWay**로 완화하여 안정화.
  - 빌드 및 실행(3초 스모크 테스트)로 “시작 즉시 크래시”가 사라졌음을 확인.

- **결과**
  - `dotnet build SnmpNms.UI` : **성공(오류 0, 경고 0)**
  - `dotnet run` : **시작 즉시 크래시 없음(3초 실행 후 강제 종료로 확인)**

- **변경사항(파일/라인)**
  - `SnmpNms.UI/MainWindow.xaml` : (TreeViewItem 스타일의 `IsSelected` 바인딩 안정화 + 잘못 삽입된 텍스트 제거)
  - `Doc/5_dev_logs.md` : (본 로그 엔트리 추가)

---

## 2025-12-25 22:05 — 클린 빌드(산출물 정리)로 “예전 크래시 메시지” 재발 방지 확인

- **작업내용**
  - 사용자 재보고 크래시 메시지(`XamlParseException/InvalidCastException`)가 “현재 소스”가 아닌 **기존 산출물(bin/obj) 또는 마크업 컴파일 캐시**를 타고 있을 가능성을 고려.
  - `SnmpNms.UI`의 `bin/obj`를 완전 삭제 후 `dotnet clean/build`로 마크업 컴파일 산출물을 재생성.
  - 클린 빌드 직후 `dotnet run --project SnmpNms.UI --no-build`로 5초 스모크 테스트(정상 실행이면 강제 종료) 수행.

- **결과**
  - `dotnet clean SnmpNms.UI` : 성공(오류 0, 경고 0)
  - `dotnet build SnmpNms.UI` : 성공(오류 0, 경고 0)
  - `dotnet run` : 시작 즉시 크래시 없음(5초 실행 OK)

- **변경사항(파일/라인)**
  - `Doc/5_dev_logs.md` : (본 로그 엔트리 추가)

---

## 2025-12-25 22:12 — `MenuItem -> TabItem` InvalidCast 크래시 원인 확정 및 근본 해결(CommandBinding 전환)

- **작업내용**
  - `crash.log`로 크래시 원인을 확정:
    - `System.InvalidCastException: Unable to cast object of type 'MenuItem' to type 'TabItem'`
    - 발생 지점: `MainWindow.xaml`의 ContextMenu 영역(줄 161 부근), `IComponentConnector.Connect(connectionId, target)` 내부.
  - 원인: `TreeView.ItemContainerStyle`(Style/Setter) 내부에서 `MenuItem Click="..."` 이벤트를 직접 연결하면
    WPF 마크업 컴파일러가 이벤트를 상위 컨테이너에 `AddHandler(MenuItem.ClickEvent, ...)`로 연결하는 과정에서
    connectionId/target 타입 매핑이 꼬이며 `MenuItem -> TabItem` 캐스팅 예외가 발생할 수 있음.
  - 해결: ContextMenu의 `Click="..."` 이벤트를 **전부 제거**하고,
    `Window.CommandBindings` + `RoutedUICommand` 기반으로 액션을 처리하도록 전환.
    - `ContextMenu`는 VisualTree에 없어서 DataContext가 깨질 수 있으므로
      `ContextMenu DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}"`로 보정.

- **결과**
  - `dotnet build SnmpNms.UI` : 성공(오류 0, 경고 0)
  - `dotnet run` 스모크 테스트(8초) : 시작 즉시 크래시 재현 안됨
  - `crash.log` : 생성되지 않음(= DispatcherUnhandledException 미발생)

- **변경사항(파일/라인)**
  - `SnmpNms.UI/MainWindow.xaml` : (ContextMenu Click 제거, Command/CommandParameter 적용, CommandBindings 추가)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (CmdMap* Executed 핸들러 추가)
  - `SnmpNms.UI/MainWindowCommands.cs` : (신규, RoutedUICommand 정의)
  - `Doc/5_dev_logs.md` : (본 로그 엔트리 추가)

---

## 2025-12-25 22:44 — Map Object 등록 팝업(General/Access/Attributes) 구현 + Edit Button Bar 연결

- **작업내용**
  - SNMPc 매뉴얼의 “Device Object Properties” 흐름을 따라, Map Object 등록 팝업을 신규 구현.
  - 우측 **Edit Button Bar**의 버튼에 Click 핸들러를 부여:
    - 1) Add Device Object
    - 1) Add Subnet  ← (사용자 요청: “두번째 버튼”)
    - 1) Add Goto
  - 팝업 OK 시 현재 선택된 Subnet(없으면 Default)에 객체를 추가하고, Event Log에 기록되도록 연결.
  - 초기 지원 타입: `Device / Subnet / Goto` (Link/Network/GeoSubnet은 후속)

- **결과**
  - `dotnet build SnmpNms.UI` : 성공(오류 0, 경고 0)
  - `dotnet run` 스모크 테스트: 시작 즉시 크래시 없음

- **변경사항(파일/라인)**
  - `SnmpNms.UI/MainWindow.xaml` : (Edit Button Bar 버튼에 Click 연결)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (EditAdd* 핸들러 + 다이얼로그 처리 + Map Tree 추가)
  - `SnmpNms.UI/ViewModels/MainViewModel.cs` : (AddSubnet/AddGoto 추가)
  - `SnmpNms.UI/Views/Dialogs/MapObjectPropertiesDialog.xaml` : (신규, General/Access/Attributes/Dependencies UI)
  - `SnmpNms.UI/Views/Dialogs/MapObjectPropertiesDialog.xaml.cs` : (신규, 입력 검증/파싱/Result)
  - `SnmpNms.UI/Views/Dialogs/MapObjectType.cs` : (신규, Device/Subnet/Goto)
  - `Doc/5_dev_logs.md` : (본 로그 엔트리 추가)

---

## 2025-12-25 (시간 미확인) — PingLogWindow 연속 Ping + Clear→Stop 변경 + Ping 버튼 동작 개선

- **작업내용**
  - Ping 로그 창이 4회만 출력하고 끝나던 동작을, **창이 열려있는 동안 연속 Ping(1초 간격)** 으로 변경.
  - PingLogWindow의 상단 버튼을 **Clear → Stop**으로 변경하고, Stop 클릭 시 **즉시 Ping 루프 중지**.
  - Map Object Properties의 Ping 버튼은 “4회 Ping을 직접 수행”하지 않고, **PingLogWindow를 열기만** 하도록 정리(반복 Ping은 창 내부에서 수행).

- **변경사항(파일/라인)**
  - `SnmpNms.UI/Views/Dialogs/PingLogWindow.xaml` : (Clear→Stop, Loaded/Closing 이벤트 연결)
  - `SnmpNms.UI/Views/Dialogs/PingLogWindow.xaml.cs` : (연속 Ping 루프 + Stop/Closing cancel)
  - `SnmpNms.UI/Views/Dialogs/MapObjectPropertiesDialog.xaml.cs` : (Ping_Click에서 4회 루프 제거, 창 열기만 수행)

---

## 2025-12-26 (시간 미확인) — Map View: Subnet 창에 Map Objects 표시(Children 바인딩)로 “+add 후 맵에 안 보임” 해결

- **작업내용**
  - 기존 Map View 내부 창이 “Todo 텍스트만 표시”하던 상태라, 트리/로그에만 추가되고 맵에는 아무것도 보이지 않았음.
  - `MapViewControl`에서 Subnet 내부 창을 열 때, 해당 Subnet의 `MapNode.Children`를 `ItemsControl`로 렌더링하도록 구현.
  - `ObservableCollection` 바인딩 기반이라, +add(OK) 시 **추가된 Device/Subnet/Goto가 즉시 Map View 창에 반영**됨.
  - 초기 화면에서 `Default` subnet 창을 자동으로 열도록 변경.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/Views/MapView/MapViewControl.xaml` : (Loaded 이벤트 연결)
  - `SnmpNms.UI/Views/MapView/MapViewControl.xaml.cs` : (Subnet 창 렌더링/중복 오픈 방지/Default 자동 오픈)
