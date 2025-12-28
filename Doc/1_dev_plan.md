# 개발 계획(업그레이드)

이 문서는 **“다음에 무엇을 할지”**를 정리하는 계획 문서입니다.  
작업 기록은 `Doc/5_dev_logs.md`에만 남깁니다(SSOT).

---

## 현재 완료된 범위(요약)

- **솔루션 구조**: `SnmpNms.Core` / `SnmpNms.Infrastructure` / `SnmpNms.UI`
- **SNMP**: Get/GetNext/Walk/Set (테스트 탭), MIB 이름 변환, MIB 트리 뷰
- **Polling**: 기본 3초 폴링 + 상태(Up/Down/Unknown), 다중 프로토콜 지원(SNMP/Ping/ARP/None)
- **Trap Listener**: UDP 162 수신, Trap 파싱 및 Event Log 기록, 실제 네트워크 IP 자동 감지
- **Discovery**: 네트워크 디바이스 자동 검색, CIDR 기반 서브넷 자동 배치, Maker 정보 추출
- **MIB View**: 디바이스 선택에 따른 Enterprise OID 필터링, 컨텍스트 메뉴 지원
- **UI 셸**: VS Code 스타일 레이아웃 (Activity Bar, Sidebar, Bottom Panel) + Map Selection Tree + Event Log Tool
- **안정화**: WPF `MenuItem -> TabItem` InvalidCast 크래시 해결(커맨드 바인딩 전환), TreeView 선택 색상 개선

---

## 다음 우선순위(추천 순서)

### 1) Polling 안정화

- 타이머 재진입 방지(중첩 폴링 방지)
- Retry/Timeout 정책 반영(`ISnmpTarget.Retries`)
- 타겟별/그룹별 폴링 주기 모델(향후 확장)

### 2) MIB 경로/로딩 전략

- 개발 경로 하드코딩 제거(실행 경로 기준 `./Mib`)
- 로딩 실패/누락 시 UX 정리(이벤트 로그 + 설정 UI)

### 3) MIB View 필터링 개선

- nel 하위 노드 사라지는 문제 해결
- 필터링 로직 개선 (부모 노드 예외 처리)

### 4) Alarm/Event 모델 고도화

- Severity, Dedup, Ack/Clear(최소)
- Polling/Trap을 동일 모델로 합치기

### 5) Trap 설정 고도화

- MVE5000/MVD5000 전용 Trap Destination Table 완전 구현
- Trap 설정 검증 기능 (SNMP GET으로 현재 설정 조회)

---

## MVP v0.1 수용 기준(체크리스트)

- ✅ 앱 실행 시 크래시 없이 메인 화면 표시
- ✅ 장비 추가/삭제/선택(Map Tree) 정상
- ✅ Event Log에 사용자 동작/폴링 결과가 기록
- ✅ SNMP Get/GetNext/Walk 테스트 가능
- ✅ Auto Poll 켜면 상태가 주기적으로 갱신
- ✅ Trap 수신 및 Event Log 기록
- ✅ MIB View 필터링 (디바이스 선택 기반)
- ✅ Discovery 기능 (네트워크 디바이스 자동 검색)

---

## MVP v0.1 (핵심 사용자 시나리오)

- ✅ 장비를 등록한다(Quick Add 또는 Discovery)
- ✅ 장비를 선택/다중 선택한다(Map Selection Tree)
- ✅ SNMP GET/GetNext/Walk을 테스트한다(SNMP Test 탭)
- ✅ Auto Poll로 상태가 갱신된다(Up/Down/Unknown)
- ✅ 발생한 이벤트가 Event Log에 누적된다(필터/검색 가능)
- ✅ Trap을 수신하고 Event Log에 기록한다
- ✅ MIB View에서 디바이스 관련 OID를 필터링한다
- ✅ Discovery로 네트워크 디바이스를 자동 검색한다

## MVP 현재 상태(요약)

- **달성**: 위 MVP v0.1 시나리오 모두 구현 완료
- **추가로 필요한 안정화**: Polling 재진입/Retry 정책, MIB 경로/설정 UX, MIB View 필터링 개선
