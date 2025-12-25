# Map Database 작업 정리

이 문서는 **Map Database(맵 오브젝트/계층/속성)** 관련 구현 내용을 정리합니다.

---

## 1) 현재 구현된 기능(요약)

### 1.1 Map Object 등록(팝업)

- **지원 타입(현재)**: `Device / Subnet / Goto`
- **진입점**: 우측 Edit Button Bar
  - Add Device Object(1번째)
  - Add Subnet(2번째)
  - Add Goto(3번째)
- **반영 위치**: OK 시 Map Selection Tree(Subnet/Device/Goto)에 즉시 추가

### 1.2 Object Properties 구성(탭)

- **General**
  - Name, Address, Icon(`auto.ico`), Description, Node Groups(텍스트)
- **Access**
  - Read Access Mode, Read/Write Mode, Read/Write Community(입력)
- **Attributes**
  - Poll Interval(sec), Poll Timeout(ms), Poll Retries
- **Dependencies**
  - placeholder(후속)

### 1.3 Add Device Object 자동 채움(Enter/Lookup)

- Address 입력 후 **Enter** 또는 **Lookup 버튼**으로:
  - Ping + SNMP GET(`sysDescr / sysObjectID / sysName`)을 실행
  - **Lookup Debug/Preview 창**에서 “명령/결과 로그”를 확인한 뒤
    **OK(적용) / Cancel(미적용)** 으로 `ObjectName`/`Description` 반영 여부를 결정

### 1.4 Ping 로그 창

- General 탭(Address 옆)의 **Ping 버튼** 클릭 시 `Ping Log` 창을 띄움
- `Ping Log` 창은 **열려있는 동안 연속 Ping(1초 간격)** 을 시간과 함께 누적 출력
- 상단 버튼: **Stop** (Stop 클릭 또는 창 닫기 시 Ping 중지)

---

## 2) 파싱/UX 이슈 및 해결

### 2.1 IP 입력이 잘려 Ping 되던 버그

- 증상: `192.168.0.100` 입력 후 Ping 시 `192.168.0`으로 Ping 시도
- 원인: `x.x.x.x.port` 형태를 과도하게 허용해 IPv4를 Port로 오인
- 해결: “IPv4 4개 세그먼트 + Port 1개 세그먼트(총 5개)” 패턴만 `ip.port`로 인정

### 2.2 하단 버튼 레이아웃

- `OK/Cancel`은 **창 하단(오른쪽) 고정**
- `Ping`은 **General(Address) 영역으로 이동**하여 하단 여백/우측 빈 공간 문제를 해소

---

## 3) 관련 코드(파일)

- UI/팝업
  - `SnmpNms.UI/Views/Dialogs/MapObjectPropertiesDialog.xaml`
  - `SnmpNms.UI/Views/Dialogs/MapObjectPropertiesDialog.xaml.cs`
  - `SnmpNms.UI/Views/Dialogs/MapObjectType.cs`
- Ping 로그
  - `SnmpNms.UI/Views/Dialogs/PingLogWindow.xaml`
  - `SnmpNms.UI/Views/Dialogs/PingLogWindow.xaml.cs`
- 연결(MainWindow)
  - `SnmpNms.UI/MainWindow.xaml` (Edit Button Bar 버튼 Click)
  - `SnmpNms.UI/MainWindow.xaml.cs` (다이얼로그 오픈/OK 처리/SNMP client 주입)
- Map Tree 반영
  - `SnmpNms.UI/ViewModels/MainViewModel.cs` (AddSubnet/AddGoto/AddDeviceToSubnet)

---

## 4) 다음 작업(후속)

- **Map Database 저장소**: 오브젝트 속성(Icon/Groups/Access/Attributes)을 구조적으로 저장/로드(DB/파일)
- **Geo Subnet/Link/Network**: 오브젝트 타입 확장 + Map View 캔버스 배치/이동
- **Polling 정책 반영**: Poll Interval/Timeout/Retry를 타겟별로 실제 엔진에 반영
