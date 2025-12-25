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
- **변경된 파일 목록(사이드바 대체)**:
  - `파일명1`
  - `파일명2`

---

## 2025-12-25 (시간 미확인) — 프로젝트 파악 및 Opus 작업 로그 초기화

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

- **변경된 파일 목록(사이드바 대체)**
  - `Doc/5_dev_opus.md`

---

## 다음 작업 후보 (사용자 선택 대기)

| 옵션 | 작업 | 설명 |
|------|------|------|
| A | Trap Receiver | UDP 162 리스너 구현 (NMS 핵심 축) |
| B | MIB 경로 설정 | 하드코딩 제거 + 동적 로딩 UI |
| C | Polling 안정화 | 재진입 방지, Retry 로직 |
| D | 기타 | 사용자 요청사항 |

---

