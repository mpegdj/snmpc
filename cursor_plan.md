# Cursor 작업 로그 및 계획

이 문서는 AI Assistant(Cursor)와의 협업을 통해 진행되는 SNMP NMS 프로젝트의 작업 내역, 계획, 그리고 진행 상황을 기록합니다.

## 📅 작업 로그 (History)

### 2025-12-25 (프로젝트 초기화)
- **초기 생성**: .NET 9.0 WPF 프로젝트 `SnmpManager` 생성
  ```bash
  dotnet new wpf -n SnmpManager
  ```
- **라이브러리 추가**: `Lextm.SharpSnmpLib` 설치
  ```bash
  dotnet add SnmpManager/SnmpManager.csproj package Lextm.SharpSnmpLib
  ```
- **PoC 구현**: 기본 UI(`MainWindow.xaml`) 및 SNMP GET 기능(`MainWindow.xaml.cs`) 구현 및 테스트 완료
- **문서화**: `devops.md`에 개발 환경 및 초기 구현 내용 기록
- **Git 설정**: 표준 .NET용 `.gitignore` 파일 생성
  ```bash
  dotnet new gitignore
  ```

### 2025-12-25 (PHASE 0: 솔루션 구조 재편)
- **솔루션 생성**
  ```bash
  dotnet new sln -n SnmpNms
  ```
- **프로젝트 생성 (Core, Infrastructure)**
  ```bash
  dotnet new classlib -n SnmpNms.Core
  dotnet new classlib -n SnmpNms.Infrastructure
  ```
- **솔루션에 프로젝트 추가**
  ```bash
  dotnet sln SnmpNms.sln add SnmpNms.Core/SnmpNms.Core.csproj SnmpNms.Infrastructure/SnmpNms.Infrastructure.csproj
  ```
- **기존 UI 프로젝트 이동 및 이름 변경**
  ```bash
  move SnmpManager SnmpNms.UI
  mv SnmpNms.UI/SnmpManager.csproj SnmpNms.UI/SnmpNms.UI.csproj
  dotnet sln SnmpNms.sln add SnmpNms.UI/SnmpNms.UI.csproj
  ```
- **참조 관계 설정**
  ```bash
  # UI -> Core, Infrastructure
  dotnet add SnmpNms.UI/SnmpNms.UI.csproj reference SnmpNms.Core/SnmpNms.Core.csproj SnmpNms.Infrastructure/SnmpNms.Infrastructure.csproj
  
  # Infrastructure -> Core
  dotnet add SnmpNms.Infrastructure/SnmpNms.Infrastructure.csproj reference SnmpNms.Core/SnmpNms.Core.csproj
  ```
- **패키지 정리 (Infrastructure에만 SNMP 라이브러리 설치)**
  ```bash
  # Infrastructure에 설치
  dotnet add SnmpNms.Infrastructure/SnmpNms.Infrastructure.csproj package Lextm.SharpSnmpLib
  
  # UI에서는 제거 (직접 의존성 끊기)
  dotnet remove SnmpNms.UI/SnmpNms.UI.csproj package Lextm.SharpSnmpLib
  ```
- **빌드 확인**
  ```bash
  dotnet build SnmpNms.sln
  ```

### 2025-12-25 (PHASE 1: SnmpClient Core 구현)
- **Core 정의 (인터페이스 및 모델)**
  - `ISnmpTarget`, `ISnmpClient`
  - `SnmpResult`, `SnmpVariable`, `SnmpVersion` (Enum)
- **Infrastructure 구현 (실제 통신 로직)**
  - `SnmpClient`: `SharpSnmpLib`의 `Messenger` 클래스를 활용하여 비동기(`Task.Run`) 패턴으로 `Get`, `GetNext`, `Walk` 구현
- **UI 리팩토링 및 연결**
  - `UiSnmpTarget`: `ISnmpTarget` 구현체 추가
  - `MainWindow.xaml.cs`: `ISnmpClient`를 사용하여 SNMP 요청 수행하도록 변경
  - 네임스페이스 정리 (`SnmpManager` -> `SnmpNms.UI`)
- **최종 빌드**: 정상 동작 확인 완료
- **초기 실행 테스트**: 프로그램 실행 성공. 로컬 호스트(`127.0.0.1`) 테스트 시 `Connection forcibly closed` 오류 확인 (정상: 로컬 SNMP 서비스 미가동 상태).

### 2025-12-25 (PHASE 1.5: 통신 테스트 검증)
- **외부 장비 테스트**: LAN에 있는 Encoder/Decoder 장비(`192.168.0.100`, `192.168.0.101`) 대상으로 SNMP GET 성공.
  - 응답 결과: `NEL MVE5000`, `NEL MVD5000` (sysDescr)
  - 응답 시간: 3ms ~ 6ms (매우 양호)
- **결론**: `SnmpClient` 통신 모듈 정상 동작 검증 완료.

### 2025-12-25 (PHASE 2: MIB Parser & Loader)
- **Mib 파일 확인**: `D:\git\snmpc\Mib` 경로에 장비별 MIB 파일(MVD5000, MVE5000) 존재 확인.
- **Core 정의**: `IMibService` 인터페이스 정의 완료 (`LoadMibModules`, `GetOidName`, `GetOid`)
- **Infrastructure 구현 (Regex 방식)**:
  - `SharpSnmpLib`의 `ObjectRegistry` 의존성 제거 (버전 호환성 문제 해결)
  - `MibService` 내 `Dictionary<string, string>` 기반 매핑 구현
  - Regex를 이용한 단순 MIB 파싱 구조 준비 (추후 고도화 필요)
- **UI 연결**: `MainWindow`에서 MIB 폴더 로드 및 결과 표시 로직 추가
- **빌드 및 실행**: 실행 중인 프로세스 강제 종료 후 빌드 성공.

---

## 🚀 현재 계획 (Current Plan)

### PHASE 2: MIB Parser & Loader (최종 확인)
- **목표**: MIB 로드 및 OID 이름 변환이 잘 되는지 UI 실행 테스트
- **상태**: ⏳ 대기 중

#### 세부 작업 항목
1.  프로그램 실행 (`dotnet run`)
2.  `192.168.0.100` 장비 대상으로 GET 요청 (자동으로 `Mib` 폴더 로드됨)
3.  결과에 `1.3.6.1.2.1.1.1` 대신 `sysDescr` 또는 장비 MIB 이름이 나오는지 확인

---

## 📝 다음 요청 사항 (Next Request)
- `dotnet run --project SnmpNms.UI` 명령어로 프로그램을 실행하고, MIB 파싱 결과가 올바르게 나오는지 확인해주시겠습니까?
