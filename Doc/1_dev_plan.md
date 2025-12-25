# 개발 계획(업그레이드)

이 문서는 **“다음에 무엇을 할지”**를 정리하는 계획 문서입니다.  
작업 기록은 `Doc/5_dev_logs.md`에만 남깁니다(SSOT).

---

## 현재 완료된 범위(요약)

- **솔루션 구조**: `SnmpNms.Core` / `SnmpNms.Infrastructure` / `SnmpNms.UI`
- **SNMP**: Get(테스트 탭), 최소 MIB 이름 변환
- **Polling**: 기본 3초 폴링 + 상태(Up/Down/Unknown)
- **UI 셸**: SNMPc 스타일 레이아웃 + Map Selection Tree + Event Log Tool
- **안정화**: WPF `MenuItem -> TabItem` InvalidCast 크래시 해결(커맨드 바인딩 전환)

---

## 다음 우선순위(추천 순서)

### 1) Trap Listener (UDP 162)

- Trap 수신 → 이벤트/알람 기록(Event Log로 적재)
- 최소 파싱(버전/커뮤니티/VarBind)
- MIB 이름 변환 연동(가능한 범위)

### 2) Polling 안정화

- 타이머 재진입 방지(중첩 폴링 방지)
- Retry/Timeout 정책 반영(`ISnmpTarget.Retries`)
- 타겟별/그룹별 폴링 주기 모델(향후 확장)

### 3) MIB 경로/로딩 전략

- 개발 경로 하드코딩 제거(실행 경로 기준 `./Mib`)
- 로딩 실패/누락 시 UX 정리(이벤트 로그 + 설정 UI)

### 4) Alarm/Event 모델 고도화

- Severity, Dedup, Ack/Clear(최소)
- Polling/Trap을 동일 모델로 합치기

---

## MVP v0.1 수용 기준(체크리스트)

- 앱 실행 시 크래시 없이 메인 화면 표시
- 장비 추가/삭제/선택(Map Tree) 정상
- Event Log에 사용자 동작/폴링 결과가 기록
- SNMP Get 테스트 가능
- Auto Poll 켜면 상태가 주기적으로 갱신

---

## MVP v0.1 (핵심 사용자 시나리오)

- 장비를 등록한다(Quick Add)
- 장비를 선택/다중 선택한다(Map Selection Tree)
- SNMP GET을 테스트한다(SNMP Test 탭)
- Auto Poll로 상태가 갱신된다(Up/Down/Unknown)
- 발생한 이벤트가 Event Log에 누적된다(필터/검색 가능)

## MVP 현재 상태(요약)

- **달성**: 위 MVP v0.1 시나리오 대부분 구현 완료
- **추가로 필요한 안정화**: Polling 재진입/Retry 정책, MIB 경로/설정 UX
