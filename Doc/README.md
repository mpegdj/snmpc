# Doc 폴더 문서 정리 가이드

## 문서 구조

### 📋 인덱스 및 개요
- **[0_index.md](0_index.md)**: 문서 내비게이션 및 프로젝트 개요
  - 프로젝트 목표 및 아키텍처
  - 문서 역할 및 빠른 시작 가이드
  - 트러블슈팅 및 주요 기능 가이드

### 📝 계획 및 설계 문서
- **[1_dev_plan.md](1_dev_plan.md)**: 개발 계획 및 로드맵
- **[1_1_snmpc_function.md](1_1_snmpc_function.md)**: SNMPc 기능 맵 및 레퍼런스
- **[2_0_dev_design.md](2_0_dev_design.md)**: 개발 설계 및 아키텍처
- **[2_3_wpf_skeleton.md](2_3_wpf_skeleton.md)**: WPF 스켈레톤 및 기본 구조
- **[2_4_wpf_detail.md](2_4_wpf_detail.md)**: WPF 상세 구현 가이드
- **[2_5_wpf_mapview_event_handle.md](2_5_wpf_mapview_event_handle.md)**: MapView 이벤트 처리
- **[2_6_function.md](2_6_function.md)**: 기능 구현 가이드

### 🔧 개발 상세 문서
- **[3_dev_detail.md](3_dev_detail.md)**: 코드 기준 상세 개요
  - 클래스 트리 및 주요 함수
  - 실행 흐름 및 구현 세부사항
- **[dev_detail.md](dev_detail.md)**: 개발 상세 문서 (포괄적)
  - 프로젝트 개요 및 아키텍처
  - 주요 컴포넌트 상세
  - 데이터 모델 및 UI 구조
  - 개발 가이드 및 변경 이력

### 🚀 운영 및 로그
- **[4_dev_ops.md](4_dev_ops.md)**: 개발/운영 환경
  - 빌드 및 실행 방법
  - 트러블슈팅
- **[5_dev_logs.md](5_dev_logs.md)**: 개발 로그 (SSOT)
  - 작업 기록 원장
  - 모든 작업 기록의 단일 원장

### 🎯 기능별 상세 문서
- **[6_map_database.md](6_map_database.md)**: Map 데이터베이스 기능
  - Map 트리 구조
  - 디바이스/Subnet/Goto 관리
  - 상태 전파 및 표시
- **[7.mib_database.md](7.mib_database.md)**: MIB 데이터베이스 기능
  - MIB 파일 로드 및 파싱
  - OID ↔ 이름 변환
  - MIB 트리 구조
- **[8_discovery_object.md](8_discovery_object.md)**: Discovery 및 Map Object 관리
  - Discovery 설정 및 실행
  - 필터 및 Seed 관리
  - Polling Protocol 기능
  - Map Object 속성 편집
  - **CIDR 기반 서브넷 자동 배치** (최신)

### 🎨 UI/디자인 문서
- **[9.vs_code_style.md](9.vs_code_style.md)**: VS Code 스타일 UI 변경 계획
  - VS Code 스타일 레이아웃 구조
  - Activity Bar, Sidebar, BottomPanel 구현
  - Phase별 구현 현황
- **[10_slidebar_map_min_color.md](10_slidebar_map_min_color.md)**: Sidebar 선택 색상 변경 기록
  - 문제 상황 및 해결 과정
  - 시도한 모든 방법 기록
- **[11.renewal_gui.md](11.renewal_gui.md)**: GUI Renewal 계획
  - SNMPc 원래 화면 구조를 VS Code 스타일로 재구성

### 📦 프로젝트별 개요
- **[SnmpNms.Core.md](SnmpNms.Core.md)**: Core 프로젝트 개요
  - 인터페이스 및 모델 정의
  - 도메인 모델 상세 설명
- **[SnmpNms.Infrastructure.md](SnmpNms.Infrastructure.md)**: Infrastructure 프로젝트 개요
  - Core 인터페이스 구현체
  - SharpSnmpLib 어댑터
- **[SnmpNms.UI.md](SnmpNms.UI.md)**: UI 프로젝트 개요
  - WPF 앱 구조 및 화면 구성
  - MainWindow, ViewModel, 다이얼로그 상세

### 📚 레퍼런스
- **[intro_snmpc.pdf](intro_snmpc.pdf)**: SNMPc UI 레퍼런스 (PDF)

---

## 최근 주요 변경사항

### v1.4 (Discovery CIDR 기반 서브넷 배치)
- Discovery 후 기기를 CIDR 기반 서브넷에 자동 배치
- Seed 정보를 기반으로 적절한 서브넷 찾기/생성
- 서브넷 이름 형식: `네트워크주소/CIDR` (예: `192.168.0.0/24`)

### v1.3 (Auto Polling 개선)
- Auto Polling 로그는 앱 시작 시에만 기록
- Start/Stop Poll 시 모든 기기 polling
- 필터링은 표시에만 관련, polling에는 영향 없음

### v1.2 (Polling Protocol)
- PollingProtocol enum 추가
- MapObjectPropertiesDialog에 Polling Protocol 선택 추가
- PollingService에서 Protocol별 처리 구현

---

## 문서 작성 가이드

### 문서 역할 구분
- **계획 문서**: 향후 작업 계획 및 로드맵
- **설계 문서**: 아키텍처 및 기술 설계
- **개발 문서**: 코드 구조 및 구현 상세
- **기능 문서**: 특정 기능의 상세 설명
- **로그 문서**: 작업 기록 및 변경 이력 (SSOT)

### 문서 업데이트 원칙
- 코드 변경 시 관련 문서 업데이트
- 기능 추가 시 기능 문서에 추가
- 이슈 해결 시 트러블슈팅 섹션에 추가
- 작업 로그는 `5_dev_logs.md`에만 기록 (SSOT)

---

## 빠른 참조

### 새 기능 추가 시
1. `1_dev_plan.md`에 계획 추가
2. `2_0_dev_design.md`에 설계 추가
3. 구현 완료 후 `5_dev_logs.md`에 기록
4. 관련 기능 문서 업데이트

### 버그 수정 시
1. `5_dev_logs.md`에 이슈 기록
2. 수정 후 `5_dev_logs.md`에 해결 기록
3. `0_index.md`의 트러블슈팅 섹션 업데이트 (필요시)

### 문서 정리 시
- 중복 내용 제거
- 역할이 명확하지 않은 문서는 역할 명시
- 인덱스 문서 업데이트

