# 작업 로그 (Opus 단일 원장)

이 파일(`5_dev_opus.md`)은 Cursor + Claude Opus 4.5 기반 **모든 작업 기록의 단일 원장(SSOT)** 입니다.  
매 작업 종료 시 아래 포맷으로 누적 기록합니다.

---

## 고정 로그 포맷(항상 사용)

- **날짜시간(분)**: `YYYY-MM-DD HH:mm`
- **제목**
- **작업내용**: 무엇을/왜/결과가 무엇인지
- **변경사항(파일/라인)**:
  - `파일명` : `Lx-Ly` (무엇이 바뀌었는지)

---

## 2025-12-25 (22:00) — 프로젝트 파악 및 Opus 작업 로그 초기화

- **작업내용**
  - 사용자 요청: `4_dev_chtgpt.md`를 읽고 현재 프로젝트 상태 파악
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
  - Opus 전용 작업 로그 파일(`5_dev_opus.md`) 생성

- **변경사항(파일/라인)**
  - `Doc/5_dev_opus.md` : `L1-L(끝)` (신규 생성 — Opus 작업 로그 원장)

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
  - `Doc/5_dev_opus.md` : (본 로그 엔트리 추가)

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
  - `Doc/5_dev_opus.md` : (본 로그 엔트리 추가)

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
  - `Doc/5_dev_opus.md` : (본 로그 엔트리 추가)
