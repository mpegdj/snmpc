# Cursor 작업 로그 및 계획

이 문서는 AI Assistant(Cursor)와의 협업을 통해 진행되는 SNMP NMS 프로젝트의 작업 내역, 계획, 그리고 진행 상황을 기록합니다.

## 📅 작업 로그 (History)

### 2025-12-25 (프로젝트 초기화)
- **초기 생성**: `SnmpManager` WPF 프로젝트 생성
- **라이브러리 추가**: `Lextm.SharpSnmpLib` 설치
- **PoC 구현**: 기본 UI 및 SNMP GET 기능 구현
- **문서화**: `devops.md` 생성
- **Git 설정**: `.gitignore` 생성

### 2025-12-25 (PHASE 0: 솔루션 구조 재편)
- **솔루션 생성**: `SnmpNms.sln`
- **프로젝트 분리**: `Core`, `Infrastructure`, `UI`
- **참조 관계 설정**: UI -> Infrastructure -> Core
- **패키지 정리**: Infrastructure에만 SNMP 라이브러리 설치

### 2025-12-25 (PHASE 1: SnmpClient Core 구현)
- **Core 정의**:
  - `SnmpNms.Core/Interfaces/ISnmpTarget.cs`
  - `SnmpNms.Core/Interfaces/ISnmpClient.cs`
  - `SnmpNms.Core/Models/SnmpResult.cs`
  - `SnmpNms.Core/Models/SnmpVariable.cs`
  - `SnmpNms.Core/Models/SnmpVersion.cs`
- **Infrastructure 구현**:
  - `SnmpNms.Infrastructure/SnmpClient.cs`
- **UI 연결**:
  - `SnmpNms.UI/Models/UiSnmpTarget.cs`
  - `SnmpNms.UI/MainWindow.xaml.cs` (네임스페이스 정리)

### 2025-12-25 (PHASE 1.5: 통신 테스트 검증)
- **테스트**: LAN 장비 대상 통신 성공 확인

### 2025-12-25 (PHASE 2: MIB Parser & Loader)
- **Core 정의**:
  - `SnmpNms.Core/Interfaces/IMibService.cs`
- **Infrastructure 구현**:
  - `SnmpNms.Infrastructure/MibService.cs` (Regex 기반 구현)
- **UI 연결**:
  - `SnmpNms.UI/MainWindow.xaml.cs` (MIB 로드 및 이름 변환 적용)

### 2025-12-25 17:40 (PHASE 3: Polling Scheduler 구현)
- **Core 정의**:
    - `SnmpNms.Core/Models/DeviceStatus.cs`: `Up`, `Down`, `Unknown` Enum 정의
    - `SnmpNms.Core/Models/PollingResult.cs`: Target, Status, ResponseTime, Message 포함
    - `SnmpNms.Core/Interfaces/IPollingService.cs`: `Start`, `Stop`, `AddTarget`, `OnPollingResult` 정의
- **Infrastructure 구현**:
    - `SnmpNms.Infrastructure/PollingService.cs`: 
      - `System.Timers.Timer` 기반(기본 3초). `ISnmpClient`를 사용하여 `sysUpTime` 주기적 조회.
      - 비동기(`Task.WhenAll`)로 다수 장비 동시 Polling 구조 구현

---

## 🚀 현재 계획 (Current Plan)

### PHASE 3: Polling Scheduler (UI 연결)
- **목표**: UI에서 Polling 기능을 켜고(Start) 상태 변화(Alive/Dead)를 실시간으로 확인
- **상태**: ⏳ 진행 중

#### 변경 예정 파일 목록
1.  **`SnmpNms.UI/MainWindow.xaml`**:
    - `Auto Poll` CheckBox 추가
    - `Status` Label 추가 (색상 표시용)
2.  **`SnmpNms.UI/MainWindow.xaml.cs`**:
    - `IPollingService` 초기화 (`PollingService`)
    - 체크박스 이벤트 핸들러 구현 (`Start/Stop`, `Add/RemoveTarget`)
    - `OnPollingResult` 이벤트 핸들러 구현 (UI 업데이트)

---

## 📝 다음 요청 사항 (Next Request)
- `SnmpNms.UI/MainWindow.xaml`에 체크박스를 추가하고 로직을 연결해도 될까요?
