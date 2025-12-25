
좋아 — **공식 SNMPc Enterprise Getting Started 문서(공식 PDF)** 내용을 기반으로 해서,
📌 **기능 실현 기획서 (정확하고 상세, PDF 원문 참고)**를 다시 정리해준다. ([Castle Rock][1])

---

# 📌 SNMPc 기반 **기능 실현 기획서 (정확판)**

이 기획서는 PDF *intro_snmpc.pdf* 내용을 정리한 것으로,
제품 기능을 **정확하게 구현할 수 있도록 세부 설계 측면**에서 작성했다. ([Castle Rock][1])

---

## 1) 아키텍처 & 컴포넌트

### 📌 시스템 구조

```
 +-----------------------------+
 | Admin/Console UI           |
 |  (Remote Web / Windows)    |
 +-------------+---------------+
               |
     +---------+-----------+
     |   Central Server     |
     |  Event Routing / DB  |
     +---------+-----------+
               |
     +---------+-----------+
     |  Polling Agents      |
     |  (Distributed)       |
     +----------------------+
               |
         Managed Network Devices
      (SNMP / ICMP / TCP services)
```

**특징 요약**

* Polling과 Server가 다중 시스템에서 실행 가능 ([Castle Rock][1])
* Remote Console과 Java 기반 UI 지원 ([Castle Rock][1])
* Enterprise는 **25,000대 장비 관리** 가능 ([Castle Rock][1])

---

## 2) 접근 모드 & SNMP 지원

### 📌 지원 프로토콜

| Mode            | 설명                    |
| --------------- | --------------------- |
| None (TCP only) | TCP Polling만, SNMP 불가 |
| ICMP            | Ping 응답으로 상태 확인       |
| SNMP v1/v2c     | 표준 Polling            |
| SNMP v3         | 인증 & 암호화 보안 Polling   |

SNMPc는 자동으로 최적 접근방식을 선택하도록 설계되어야 한다. ([Castle Rock][1])

---

## 3) UI 콘솔 레이아웃 (정확판)

PDF 원문은 **기본 콘솔 요소 구조**를 상세히 설명하고 있다 —
이는 UI 개발에서 반드시 반영해야 하는 주요 화면 분해다. ([Castle Rock][1])

### 📌 콘솔 화면 기본 구조

```
┌─────────────────────────────────────────────────┐
| Main Menu Bar                                     |
├─────────────────────────────────────────────────┤
| Main Button Bar  | Edit Button Bar                |
| (Action Buttons) | (Map/Edit Mode)                |
├─────────────────────────────────────────────────┤
| Selection Tool Tabs  | View Windows (MDI Area)     |
|  - Map                |  - Map View                  |
|  - MIB                |  - MIB Table View            |
|  - Trend Report       |  - Graph View                |
|  - Event              |                              |
|  - Menu               |                              |
├─────────────────────────────────────────────────┤
| Event Log Tool (Filterable)                       |
└─────────────────────────────────────────────────┘
```

📌 다중 뷰는 **MDI (Multi Document Interface)** 로 표시되며,
사용자는 필요에 따라 Map, Table, Graph를 동시에 열 수 있다. ([Castle Rock][1])

---

## 4) 콘솔 UI 구성 설명

### ☑ Main Button Bar

빠른 기능 실행 버튼

* Zoom In / Zoom Out
* Pan / Zoom Rectangle
* Map Navigation
* MIB Browser Launch
* Quick Poll
* Add Device / Network / Links
* Graph/Table View

(버튼 이름/기능은 PDF 원문 도식 참고) ([Castle Rock][1])

---

### ☑ Edit Button Bar

맵 오브젝트 추가, 삭제, 수정

* Add Subnet
* Add Goto Object
* Add Link
* Add Bus Network
* Add Ring Network

각 툴은 Map View에 추가적인 오브젝트를 생성하는데 사용한다. ([Castle Rock][1])

---

## 5) Selection Tool (탐색/관리 트리)

📌 이 패널은 PDF에 매우 상세하게 나온 구조다. ([Castle Rock][1])

### 탭 구성

| 탭         | 기능                              |
| --------- | ------------------------------- |
| Map Tab   | 모든 맵 오브젝트 탐색                    |
| MIB Tab   | MIB, Custom Tables, Expressions |
| Trend Tab | 리포트/통계 정의                       |
| Event Tab | 이벤트 필터링                         |
| Menu Tab  | Custom 메뉴 관리                    |

**구현 상세**

* 리스트 트리 구조
* 우클릭 Context 메뉴
* 선택 아이템 빠른 이동

---

## 6) View Window Area (MDI 메인 영역)

다양한 데이터 뷰가 동시에 표시될 수 있다 — 중요한 구현 포인트다. ([Castle Rock][1])

### 📌 뷰 타입

| 뷰 이름         | 설명            |
| ------------ | ------------- |
| Map View     | 토폴로지/아이콘 기반 맵 |
| MIB Table    | SNMP 변수 값 테이블 |
| MIB Graph    | SNMP 값 그래프    |
| Event Log    | 이벤트 리스트       |
| Trend Report | 장기 통계         |

---

## 7) Network Discovery (자동 탐지)

PDF 가이드에는 상세 시퀀스가 나온다: ([Castle Rock][1])

**기능**
✔ Seed 장비 기반 서브넷 탐지
✔ SNMP, ICMP 기반 탐색
✔ 토폴로지 생성 (자동/수동)
✔ 장비/서브넷 자동 배치

**구현 요구**

* 탐지 스케줄러
* 장애 재시도 전략
* 발견 결과 Map 자동 반영

---

## 8) Threshold & 이벤트 처리

### 📌 경보 처리

SNMPc는 이벤트를 단순 Log가 아니라 **룰 기반 알람 시스템**으로 처리한다. ([SNMPC][2])

✔ 알람 필터
✔ Ignore 조건
✔ Email / SMS 통보
✔ Duplicate Ignore

**구현**

* Policy 엔진
* 필터/중복 처리
* 외부 알림 플러그인 (Email/SMS/Webhook)

---

## 9) 보고/리포트 기능

SNMPc는 자체적으로 **OnLine Reporting Web 엔진**과 통합된다: ([Castle Rock][3])

* 스케줄 보고
* 트렌드/상태 요약
* Web/Dashboard

실제로 PDF에서는 *Trend Report profiles*이 Map 및 이벤트와 같이 UI로 구성된다. ([Castle Rock][1])

---

## 10) MIB Browser & SNMP Views

### 📌 기능

✔ MIB 변수 조회/설정
✔ Table/Graph 보기
✔ Multi-device 연동
✔ Custom Expressions

이를 구현하기 위해서는 SNMP 라이브러리와 Tree/Table/Graph UI가 필요하다. ([Castle Rock][4])

---

## 11) Distributed Polling & Failover

* Polling Agents를 곳곳에 배포
* 상태 변화만 전송 (효율적) ([Castle Rock][5])
* Live/Standby Server Support ([Castle Rock][6])

**기획 포인트**

* 중앙/원격 Poller 계층 설계
* 자동 장애 전환
* Secure channel

---

## 12) 유저/보안/감사

SNMPc Enterprise에는 **User Audit Trail** 기능이 있다. ([Castle Rock][6])

✔ 사용자 로그인/활동 로그
✔ 권한 기반 UI 접근
✔ 감사 로그

구현 시 RBAC 기반 설계 필요

---

# 🧠 요약 — 반드시 반영해야 할 기능 체크리스트

| 기능                   | 요구 반영 |                    |
| -------------------- | ----- | ------------------ |
| Auto Discovery       | ✔     |                    |
| Distributed Polling  | ✔     |                    |
| MIB Browser          | ✔     |                    |
| Graph/Trend          | ✔     |                    |
| Event/Alarm          | ✔     |                    |
| Reporting            | ✔     |                    |
| Multiple Views (MDI) | ✔     |                    |
| Custom Expressions   | ✔     |                    |
| Remote Console       | ✔     |                    |
| Failover             | ✔     |                    |
| Security (SNMPv3)    | ✔     | ([Castle Rock][1]) |

---

원하면 다음으로 아래 문서를 만들 수 있다:

📌 콘솔 UI 와이어프레임
📌 화면별 XAML 스켈레톤 샘플 (WPF)
📌 API/백엔드 아키텍처 상세 설계

어떤 문서부터 해줄까?

[1]: https://www.castlerock.com/pdf/intro_snmpc.pdf "Getting Started"
[2]: https://www.snmpc.org/pdf/snmpc-datasheet.pdf?utm_source=chatgpt.com "Network Monitoring and Management"
[3]: https://www.castlerock.com/products/snmpc/?utm_source=chatgpt.com "SNMPc Enterprise - Products - Castle Rock Computing"
[4]: https://www.castlerock.com/products/snmpc/snmpc_views.htm?utm_source=chatgpt.com "Device SNMP Views - SNMPc Enterprise - Products - Castle Rock Computing"
[5]: https://www.castlerock.com/products/snmpc/scalability.htm?utm_source=chatgpt.com "Scalability - SNMPc Enterprise - Products - Castle Rock Computing"
[6]: https://www.castlerock.com/products/snmpc/reliability.htm?utm_source=chatgpt.com "Reliability - SNMPc Enterprise - Products - Castle Rock Computing"
