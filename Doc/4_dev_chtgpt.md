# 작업 로그 (ChatGPT 단일 원장)

1_dev_plan.md 을 진행중이야 2_dev_ops_log.md 환경 설정했고 3_dev_mvp_log.md까지 진행했어니가 프로젝트를  분석해서 개요를 다시 정리해주고 파악해줘

지금은 문서(계획/운영로그/MVP로그) + 실제 코드 구조를 함께 훑어서, “무엇을 만들고 있고(목표) / 현재 어디까지 됐고(진척) / 남은 핵심 작업이 뭔지(다음 단계)”를 한 장짜리 개요로 다시 정리하겠습니다. 먼저 1_dev_plan.md, 2_dev_ops_log.md, 3_dev_mvp_log.md, README.md를 읽고, 이어서 솔루션/프로젝트 파일과 핵심 클래스 몇 개를 확인할게요.
좋아요. 문서상으로는 PHASE 3(Polling Scheduler)까지 Core/Infrastructure 구현이 끝났고, UI 연결이 “진행 중”으로 보이네요. 이제 실제 코드(SnmpNms.Core/Infrastructure/UI)를 확인해서 문서 내용이 코드에 어떻게 반영됐는지, 현재 동작 경로/의존성/빈 구멍(Trap, MIB 품질, UI 바인딩 등)을 정확히 짚어 개요를 재정리하겠습니다.
좋은 발견이 있었어요: 문서에 적힌 “UI → Infrastructure → Core” 구조가 실제 솔루션/프로젝트 참조로 그대로 구현돼 있고, UI에 Auto Poll 체크박스/상태 라벨까지 이미 붙어있습니다. 이제 Core의 모델/인터페이스 정의까지 확인해서 “현재 구현 범위”를 정확히 확정한 뒤, 남은 기능(Trap/다중장비/알람/저장소/MIB 품질)을 우선순위로 재정리해 드릴게요.
프로젝트 목표(한 줄)
C#/.NET 9 + WPF로 “SNMPc 스타일의 경량 NMS(Manager)”를 만들되, Core/Infra/UI를 분리해서 확장 가능한 구조로 가는 것입니다.
현재 솔루션 구조(아키텍처)
SnmpNms.Core (순수 도메인/계약)
인터페이스: ISnmpClient, ISnmpTarget, IMibService, IPollingService
모델: SnmpResult, SnmpVariable, PollingResult, DeviceStatus, SnmpVersion
SnmpNms.Infrastructure (구현/외부 라이브러리 의존)
Lextm.SharpSnmpLib(SharpSnmpLib) 사용
구현체: SnmpClient, MibService, PollingService
SnmpNms.UI (WPF 실행/화면)
MainWindow.xaml(.cs)에서 수동 DI로 Infra 구현을 생성해서 사용
UiSnmpTarget이 ISnmpTarget 구현
의존성 방향은 문서대로 UI → Infrastructure → Core로 깔끔하게 되어 있어요.
지금 “구현되어 실제로 동작하는 기능”
SNMP GET (UI 버튼)
UI에서 IP/Community/OID 입력 → ISnmpClient.GetAsync() 호출 → 결과를 텍스트 박스에 출력
OID 입력이 숫자가 아니라면(sysDescr 같은) IMibService.GetOid()로 OID 변환 시도
출력 시 IMibService.GetOidName()으로 “이름(oid)” 형태로 표시
MIB 로딩(최소)
앱 시작 시 Mib 폴더를 읽어 .mib/.txt에서 OBJECT-TYPE ::= { parent n } 패턴만 정규식으로 파싱
표준 일부(sysDescr, sysUpTime 등)는 하드코딩 등록
Polling Scheduler(기본형) + UI 연결
PollingService가 System.Timers.Timer(기본 3초)로 등록된 타겟들을 동시에 폴링(Task.WhenAll)
Alive 체크는 sysUpTime(1.3.6.1.2.1.1.3.0) GET으로 판단
UI에 Auto Poll (3s) 체크박스 + Status 라벨이 이미 붙어 있고, 이벤트로 Up/Down 표시까지 됩니다
즉, 3_dev_mvp_log.md에 “PHASE 3 UI 연결 진행 중”이라고 적혀있지만, 코드 기준으로는 이미 1차 연결은 완료 상태입니다(개선/확장만 남음).
실행 흐름(현재 코드 기준)
앱 시작(MainWindow)
SnmpClient + MibService + PollingService 생성
MIB 로드 시도
사용자 동작
Get 버튼: 입력값 → GetAsync → 결과 출력(+OID 이름 변환)
Auto Poll 체크: AddTarget 후 Start() → 이벤트로 상태 갱신 / 해제 시 Stop() + RemoveTarget
현재 상태에서 보이는 “빈 구멍/리스크”(다음 작업 후보)
Trap Receiver/Listener(UDP 162) 미구현: 문서에서 말한 NMS 축 중 하나가 아직 없음
MIB 파서 품질: IMPORTS/계층/상수/테이블/ENUM/타입 해석은 아직 “최소 정규식” 수준
MIB 경로 하드코딩: D:\git\snmpc\Mib가 박혀 있어 다른 PC/배포에서 깨질 가능성 큼
Polling 안정성
Timer 재진입(이전 폴링이 끝나기 전에 다음 tick이 올 수 있음) 가능성
Retries는 ISnmpTarget에 있으나 아직 동작 로직에 반영되지 않음
UI 확장성
지금은 단일 대상(입력칸의 IP) 중심이라 “다중 장비/목록/상태표/알람 콘솔”로 가려면 화면 구조 개편(MVVM 권장)이 필요
“지금까지 파악한 개요” 한 장 요약
완료: 솔루션 분리(Core/Infra/UI), SNMP GET, 최소 MIB 로딩/이름 변환, 기본 Polling(Alive/Dead), UI에 Auto Poll/Status 표시
다음 큰 축: Trap Listener + 이벤트/알람 모델(최소), 다중 장비 관리 UI(목록), Polling 안정화(재진입/Retry/Timeout 정책), MIB 해석 고도화
원하시면 다음 중 하나로 “다음 정리”를 더 정확히 잡아드릴게요(선택해 주세요).
A) “MVP 기준”으로 앞으로의 PHASE를 다시 재작성(체크리스트/우선순위)
B) “SNMPc 기능맵” 기준으로 현재 구현된 항목/미구현 항목 매트릭스 표로 정리
C) 당장 다음 개발 타겟 1개만 정해서(Trap vs 다중장비 UI vs Polling 안정화 vs MIB 개선) 세부 설계까지 확정

이 파일(`4_dev_chtgpt.md`)은 앞으로 **모든 작업 기록의 단일 원장(SSOT)** 입니다.  
매 작업 종료 시 아래 포맷으로 누적 기록합니다.

---

## 고정 로그 포맷(항상 사용)

- **날짜시간(분)**: `YYYY-MM-DD HH:mm`
- **제목**
- **작업내용**: 무엇을/왜/결과가 무엇인지
- **변경사항(파일/라인)**:
  - `파일명` : `Lx-Ly` (무엇이 바뀌었는지)
- **변경된 파일 목록(사이드바 대체)**:
  - `파일명1`
  - `파일명2`

---

## 2025-12-25 (시간 미확인) — 프로젝트 분석/개요 재정리

- **작업내용**
  - 문서(`1_dev_plan.md`, `2_dev_ops_log.md`, `3_dev_mvp_log.md`, `snmpc_function.md`)와 코드(`SnmpNms.Core/Infrastructure/UI`)를 함께 확인해서 현재 구현 범위/아키텍처/빈 구멍을 재정리함.
  - 솔루션 의존성 방향이 문서대로 **UI → Infrastructure → Core**로 구현되어 있음을 확인.
  - `SnmpNms.UI`에 **Auto Poll 체크박스/Status 라벨**까지 이미 연결되어 있어, 문서상 “PHASE 3 UI 연결 진행 중” 대비 코드 기준으로는 **1차 연결이 완료 상태**임을 확인.

- **현재 구현되어 동작하는 기능(요약)**
  - SNMP GET (UI 버튼)
  - 최소 MIB 로딩/이름 변환(정규식 기반 + 일부 하드코딩)
  - Polling Scheduler(기본 3초 Timer) + UI 상태 표시(Up/Down)

- **미구현/리스크(다음 작업 후보)**
  - Trap Receiver/Listener(UDP 162) 미구현
  - MIB 파서 품질(IMPORTS/ENUM/TYPE/테이블 등) 미흡
  - MIB 경로 하드코딩(`D:\git\snmpc\Mib`)으로 환경 의존
  - Polling 재진입/안정성(이전 tick 미완료 시 다음 tick 가능), Retries 미반영
  - 다중 장비 관리 UI(목록/상태표/알람 콘솔)로 확장 필요

- **변경사항(파일/라인)**
  - `4_dev_chtgpt.md` : `L1-L(끝)` (작업 로그 원장/템플릿/첫 로그 엔트리 추가)

- **변경된 파일 목록(사이드바 대체)**
  - `4_dev_chtgpt.md`

---

## 2025-12-25 (시간 미확인) — SNMPc 스타일 참고 링크 요청 대응

- **작업내용**
  - SNMPc UI 스타일(좌측 트리/우측 탭, 맵, 알람 콘솔 등) 참고 링크 요청을 받음.
  - 웹 검색으로 “스크린샷/사용자 가이드”의 정확한 하위 URL을 제공하려 했으나, 검색 실행이 승인되지 않아(사용자 거부) 링크 검증이 불가했음.
  - 우선 안전한 공식 루트 링크만 제공하고, 추후 웹 검색 허용 시 정확한 스크린샷/가이드 페이지 링크로 보강하기로 함.

- **참고(공식 루트)**
  - `https://www.snmpc.com/`

- **변경사항(파일/라인)**
  - `4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)

- **변경된 파일 목록(사이드바 대체)**
  - `4_dev_chtgpt.md`
  결론은 “전체 GUI(셸) 뼈대는 빨리 만들고, 기능은 모듈부터 채우는 하이브리드”가 제일 안전합니다.
왜 하이브리드가 맞나(지금 프로젝트 상황 기준)
이미 모듈이 먼저 만들어진 상태예요: SnmpClient / MibService / PollingService + UI에 Get / Auto Poll까지 연결됨.
지금부터 UI를 너무 뒤로 미루면: “장비 목록/알람 콘솔/차트/트랩” 같은 최종 UX가 코어 설계를 끌고 다니게 되어, 나중에 화면 갈아엎는 비용이 커져요.
반대로 GUI를 기능보다 먼저 다 완성하려 하면: 데이터 모델/이벤트(폴링/트랩/알람) 구조가 확정되기 전이라 UI가 금방 깨져요.
추천 진행 순서(실행 가능한 단계)
1) 전체 GUI ‘셸’ 먼저: MainWindow를 “SNMPc처럼” 좌측 Device Explorer + 우측 탭(Details/Alarms/Performance/MIB) 정도로 비어있는 뼈대만 만듦.
2) 모듈은 지금 방식대로: Polling/Trap/MIB 같은 코어를 계속 확장.
3) 각 모듈마다 UI에 “최소 1개 화면/흐름”만 연결: 예) 폴링 결과를 장비 리스트에 색으로 표시, 트랩을 알람 그리드에 쌓기.
지금 당장 하나만 고르라면
전체 GUI를 “완성”하는 건 X
전체 GUI의 “틀(네비게이션/레이아웃)”을 먼저 만들고, 그 안을 모듈로 채우는 게 O
원하면, 다음 중 어떤 UX를 목표로 할지 먼저 정하고(1개만) 그 기준으로 MainWindow.xaml 셸을 바로 잡아줄게요:
A) SNMPc 스타일(좌측 트리 + 우측 탭)
B) NMS 콘솔 스타일(상단 요약 + 좌측 목록 + 중앙 차트/알람)
C) 개발툴 우선(MIB Browser/Walk/Get 중심)

---

## 2025-12-25 (시간 미확인) — SNMPc 스타일 참고 자료 확정(공식 Getting Started PDF)

- **작업내용**
  - 사용자가 SNMPc UI 스타일 참고 자료로 공식 “Getting Started” PDF를 공유함.
  - 이 문서를 “SNMPc 콘솔 레이아웃/흐름”의 기준 레퍼런스로 채택(좌측 트리/우측 뷰, 맵 DB, 이벤트/알람, 장비 MIB 데이터 뷰).

- **레퍼런스 링크**
  - [SNMPc Enterprise Getting Started (Version 10, Aug 2020)](https://www.castlerock.com/pdf/intro_snmpc.pdf)

- **우리 WPF GUI에 그대로 매핑할 핵심 UI 요소(체크리스트)**
  - **Console Elements**: 메인 프레임 구조(좌측 탐색 + 우측 작업영역 + 메뉴/툴바) 기준
  - **Map Database**: “장비/서브맵/맵 뷰” 개념(Topology/Map 화면의 근간)
  - **Viewing Device Mib Data**: 장비 선택 시 “MIB 데이터(변수) 조회” 탭/창
  - **Event/Alarm 흐름**: 이벤트 기록/알람 표시(알람 콘솔/이벤트 로그로 이어지는 축)
  - **Threshold/Statistics**: 장기 통계/임계치 알람(성능 차트/임계치 엔진의 목표 상태)

- **변경사항(파일/라인)**
  - `4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)

- **변경된 파일 목록(사이드바 대체)**
  - `4_dev_chtgpt.md`

---

## 2025-12-25 (시간 미확인) — UI 위치 원칙 확정(`SnmpNms.UI`)

- **결론**
  - WPF UI(View/XAML/리소스)는 **`SnmpNms.UI`** 프로젝트에 두는 것이 맞음.

- **UI 프로젝트에 포함(OK)**
  - `Views`(XAML), `ViewModels`(MVVM), UI 전용 `Models`(표시용 DTO), `Converters/Behaviors`, 리소스(Styles/Theme)

- **UI 프로젝트에 미포함(지양)**
  - SNMP 통신/폴링/트랩 수신 같은 **비즈니스 로직 구현**
  - SharpSnmpLib 같은 **외부 통신 라이브러리 직접 의존**
  - 파일/DB 저장 로직(가능하면 Infrastructure로)

- **의존성 원칙(유지)**
  - `SnmpNms.UI` → `SnmpNms.Infrastructure` → `SnmpNms.Core`
  - UI는 `ISnmpClient`, `IPollingService`, `IMibService` 같은 **인터페이스로만 사용**(구현은 Infrastructure)

- **변경사항(파일/라인)**
  - `4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)

- **변경된 파일 목록(사이드바 대체)**
  - `4_dev_chtgpt.md`

---

## 2025-12-25 (시간 미확인) — 프로젝트별 코드 개요 문서 3종 생성

- **작업내용**
  - 프로젝트 3개(`SnmpNms.Core`, `SnmpNms.Infrastructure`, `SnmpNms.UI`)에 대해
    - 역할/의존성/폴더 구조
    - 클래스·인터페이스 트리(요약)
    - 주요 실행 흐름 및 현재 리스크
    를 각각 별도 Markdown 문서로 정리해 고정함.

- **생성 파일**
  - `SnmpNms.Core.md`
  - `SnmpNms.Infrastructure.md`
  - `SnmpNms.UI.md`

- **변경사항(파일/라인)**
  - `SnmpNms.Core.md` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.Infrastructure.md` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI.md` : `L1-L(끝)` (신규 생성)
  - `4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)

---

## 2025-12-25 (20:13) — 메인 GUI(SNMPc 스타일) 셸 구축

- **작업내용**
  - `MainWindow`를 SNMPc 스타일 콘솔 레이아웃으로 개편:
    - 상단 Menu/Toolbar
    - 좌측 Device Explorer(트리)
    - 우측 메인 Tab(대시보드/디바이스/알람/성능/MIB/SNMP Test)
    - 하단 Event Log + 상태바(Status)
  - 기존 기능(SNMP GET / Auto Poll)은 **SNMP Test 탭**으로 이동해 계속 동작하도록 유지.
  - Device Explorer에서 Quick Add/선택/삭제, Toolbar에서 Poll 시작/중지/로그 삭제 등 최소 조작 추가.
  - 빌드 확인: `SnmpNms.sln` Debug 빌드 **성공(오류 0)**.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/MainWindow.xaml` : (레이아웃 전체 개편)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (ViewModel 연결 + Device Explorer/Toolbar 이벤트 핸들러 추가)
  - `SnmpNms.UI/ViewModels/MainViewModel.cs` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI/Models/UiSnmpTarget.cs` : (DisplayName 추가)
  - `4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)
---

## 2025-12-25 (시간 미확인) — 문서 `Doc/` 폴더로 정리 + Map View(Cascade) / Event Log 탭 확장

- **작업내용**
  - 문서(`dev_plan/dev_ops/dev_mvp/레퍼런스/로그`)를 `Doc/` 폴더로 정리하는 방향을 기준으로 이후 작업을 맞추기로 함.
  - **View Window Area는 탭 기반 유지**하되, `Map View` 탭 내부에서 SNMPc처럼 **겹치는 내부 창**을 띄우고 **Cascade 정렬** 가능하게 최소 구현.
  - 하단 **Event Log Tool** 탭을 `Custom 3 ~ Custom 8`까지 확장(필터/전체/장비별 로직은 추후 구현).

- **변경사항(파일/라인)**
  - `SnmpNms.UI/MainWindow.xaml` : (Map View에 내부 창 컨트롤 연결, Event Log 탭 확장)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (Window/Cascade → Map View Cascade 연결)
  - `SnmpNms.UI/Views/MapView/MapViewControl.xaml` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI/Views/MapView/MapViewControl.xaml.cs` : `L1-L(끝)` (신규 생성)
  - `Doc/4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)

---

## 2025-12-25 (20:50) — Tool Bar/Edit Bar 아이콘 버튼화 + Log Window 리사이즈

- **작업내용**
  - 상단 **Tool Bar**를 텍스트 버튼에서 **아이콘 버튼 스타일**로 통일(SNMPc 느낌 강화).
  - 우측 **Edit Bar**를 툴바가 아닌 **세로 아이콘 버튼(스택)**으로 구성(버튼 크기/간격 통일).
  - 하단 **Event Log Tool** 영역을 `GridSplitter`로 분리해, 경계를 드래그하면 **높이가 동적으로 변경**되도록 개선.
  - 빌드 확인: `SnmpNms.sln` Debug 빌드 **성공(오류 0)**.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/MainWindow.xaml` : (아이콘 버튼 스타일, Edit Bar 버튼화, Log 리사이즈 GridSplitter 추가)
  - `Doc/4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)

- **변경된 파일 목록(사이드바 대체)**
  - `SnmpNms.UI/MainWindow.xaml`
  - `Doc/4_dev_chtgpt.md`

---

## 2025-12-25 (21:15) — Event Log Tool “필터링” 구현(전체/선택장비/Severity/검색) + 탭별 독립 필터

- **작업내용**
  - 하단 Event Log를 `TextBox AppendText` 방식에서 **구조화된 이벤트 리스트(DataGrid)**로 전환.
  - `Current/History/Custom1~Custom8` 각 탭에 **독립 필터**를 부여:
    - **Scope**: `All` / `SelectedDevice`
    - **Severity**: `Any / Info / Warning / Error`
    - **Search**: device/message 문자열 검색
  - 기존 UI의 “로그 출력” 코드를 `_vm.AddEvent(...)`로 리다이렉트해, Polling/GET/장비 추가/선택 등 이벤트가 표 형태로 누적되도록 변경.
  - 빌드 확인: `SnmpNms.sln` Debug 빌드 **성공(오류/경고 0)**.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/Models/EventLogEntry.cs` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI/ViewModels/EventLogFilterViewModel.cs` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI/ViewModels/MainViewModel.cs` : (Events/필터VM 추가, AddEvent/ClearEvents 추가)
  - `SnmpNms.UI/Views/EventLog/EventLogTabControl.xaml` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI/Views/EventLog/EventLogTabControl.xaml.cs` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI/MainWindow.xaml` : (Event Log Tool 탭을 공용 컨트롤로 교체)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (txtResult 제거, AddEvent 기반으로 로그 기록)
  - `Doc/4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)

---

## 2025-12-25 (21:25) — Map Selection Tree(SNMPc 매뉴얼 동작) 구현

- **작업내용**
  - Map Selection Tree를 “단일 디바이스 리스트”에서 **Subnet/Device/Goto 계층 트리**로 전환.
  - SNMPc 매뉴얼 동작을 최소 구현:
    - **상태 색상**: 디바이스는 자신의 상태(Up/Down/Unknown), Subnet/Root는 하위 객체 중 **가장 높은 우선순위 상태**(Down > Unknown > Up) 표시
    - **더블클릭(Subnet)**: Map View 내부 창으로 Open + Cascade 정렬
    - **Ctrl/Shift 다중 선택**
    - **Delete 키**: 선택 객체 삭제(서브넷은 비어있을 때만)
    - **Drag & Drop**: 선택된 디바이스(복수 가능)를 다른 Subnet으로 이동
    - **우클릭 메뉴**: Properties/Open Map/Quick Poll/MIB Table/Delete (일부 Todo)
  - 빌드 확인: `SnmpNms.sln` Debug 빌드 **성공(오류/경고 0)**.

- **변경사항(파일/라인)**
  - `SnmpNms.UI/Models/MapNode.cs` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI/Converters/DeviceStatusToBrushConverter.cs` : `L1-L(끝)` (신규 생성)
  - `SnmpNms.UI/Models/UiSnmpTarget.cs` : (Status 추가 + INotifyPropertyChanged)
  - `SnmpNms.UI/ViewModels/MainViewModel.cs` : (MapRoots/RootSubnet/SelectedMapNodes 추가)
  - `SnmpNms.UI/MainWindow.xaml` : (Map Tree HierarchicalDataTemplate + 이벤트 핸들러/컨텍스트메뉴)
  - `SnmpNms.UI/MainWindow.xaml.cs` : (다중선택/삭제/드래그드롭/더블클릭/상태 업데이트 구현)
  - `SnmpNms.UI/Views/MapView/MapViewControl.xaml.cs` : (OpenSubnet 추가)
  - `Doc/4_dev_chtgpt.md` : (하단에 로그 엔트리 추가)

