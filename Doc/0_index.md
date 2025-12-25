# Doc 인덱스 (문서 내비게이션)

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

- **[7.mib_database.md](7.mib_database.md)**: MIB 데이터베이스 기능
  - MIB 파일 로드 및 파싱
  - OID ↔ 이름 변환
  - MIB 트리 구조

- **[6_map_database.md](6_map_database.md)**: Map 데이터베이스 기능
  - Map 트리 구조
  - 디바이스/Subnet/Goto 관리
  - 상태 전파 및 표시

- **[5_dev_logs.md](5_dev_logs.md)**: 개발 로그
  - 작업 기록 원장 (SSOT)
  - 변경 이력 및 이슈 추적

- **[4_dev_ops.md](4_dev_ops.md)**: 운영/개발 환경
  - 실행 방법
  - 빌드 및 배포
  - 트러블슈팅

- **[3_dev_detail.md](3_dev_detail.md)**: 개발 상세 사항
  - 구현 세부사항
  - 기술적 결정 사항

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

---

## 문의 및 기여

- **이슈 리포트**: GitHub Issues 사용
- **기능 제안**: GitHub Discussions 사용
- **문서 개선**: Pull Request 환영

---

## 라이선스

(프로젝트 라이선스 정보)
