# Discovery 및 Map Object 관리 기능

## 개요

SNMPc 스타일의 네트워크 디바이스 Discovery 기능과 Map Object 관리 기능을 구현했습니다. 이 기능을 통해 네트워크에서 SNMP 디바이스를 자동으로 검색하고, 발견된 디바이스를 Map에 추가할 수 있습니다. 또한 각 디바이스의 속성을 편집하고, 다양한 Polling Protocol을 사용하여 디바이스 상태를 모니터링할 수 있습니다.

---

## 주요 기능

### 1. Discovery/Polling Agents 다이얼로그

네트워크 디바이스를 검색하기 위한 설정을 구성하는 다이얼로그입니다.

#### 1.1 UI 구조

- **왼쪽 패널**: Polling Agents 리스트 (Address, Status)
- **오른쪽 패널**: 탭 인터페이스
  - **General 탭**: Discovery 및 Polling 기본 설정
  - **Proto 탭**: SNMP 버전 및 Find Options
  - **Seeds 탭**: 네트워크 범위 설정
  - **Comm 탭**: Community String 관리
  - **Filters 탭**: 검색 필터 설정

#### 1.2 툴바 버튼

- **첫 번째 버튼**: "Find Map Objects" (돋보기 아이콘: `&#xE721;`)
  - 클릭 시 Discovery/Polling Agents 다이얼로그 열기

---

## 2. Discovery 설정 구성

### 2.1 General 탭

#### Discovery Config
- **Enable Discovery**: Discovery 기능 활성화
- **Use Subnet Broadcasts**: 서브넷 브로드캐스트 사용
- **Ping Scan Subnets**: 서브넷 Ping 스캔
- **Auto Restart Time**: 자동 재시작 시간 (시간 단위)

#### Polling Config
- **Enable Status Polling**: 상태 Polling 활성화
- **Enable Service Polling**: 서비스 Polling 활성화

### 2.2 Proto 탭

#### Find SNMP Versions
- **Find SNMP V1**: SNMP v1 디바이스 검색
- **Find SNMP V2c**: SNMP v2c 디바이스 검색
- **Find SNMP V3**: SNMP v3 디바이스 검색

#### Find Options
- **Find Non-SNMP Nodes**: SNMP를 지원하지 않는 노드 검색
- **Find RMON Devices**: RMON 디바이스 검색
- **Find TCP Ports**: TCP 포트 검색
  - **WEB**: 웹 서버 (포트 80)
  - **SMTP**: 메일 서버 (포트 25)
  - **Telnet**: 텔넷 서버 (포트 23)
  - **FTP**: FTP 서버 (포트 21)

> **참고**: Find Options는 원래 General 탭에 있었으나, SNMP 버전 선택 옵션과 함께 Proto 탭으로 이동되었습니다.

### 2.3 Seeds 탭

네트워크 검색 범위를 정의하는 Seed IP 주소와 서브넷 마스크를 설정합니다.

#### 입력 필드
- **Seed IP/CIDR**: IP 주소 또는 CIDR 표기법 (예: `192.168.0.0/24`)
- **Netmask (optional)**: 서브넷 마스크 (CIDR 표기법 사용 시 자동 계산)

#### CIDR 표기법 지원
- `192.168.0.0/24` 형식으로 입력하면 자동으로 서브넷 마스크 계산
- 예: `192.168.0.0/24` → `255.255.255.0`
- 예: `192.168.0.0/23` → `255.255.254.0`

#### 기본 Seed 설정
- **Address**: `192.168.0.0`
- **Mask**: `255.255.254.0` (CIDR `/23`, 512개 IP 주소)

#### 기능
- **Add**: Seed 추가
- **Change**: 선택된 Seed 수정
- **Delete**: 선택된 Seed 삭제

### 2.4 Comm 탭

SNMP Community String을 관리합니다.

#### 기본 Community 설정
- `V1/crclab/crclab`
- `V1/public/netman`

#### 기능
- **Add**: Community 추가
- **Edit**: 선택된 Community 수정
- **Delete**: 선택된 Community 삭제

### 2.5 Filters 탭

검색 결과를 필터링하기 위한 필터를 설정합니다.

#### 필터 타입

1. **Address Range Filter**
   - IP 주소 범위 필터
   - 예: `192.168.0.100-101`, `192.168.1.0-255`

2. **Maker Filter**
   - 제조사 이름 필터 (sysDescr 기반)
   - 와일드카드 지원: `ntt*`

3. **Device Pattern Filter**
   - 디바이스 이름 패턴 필터 (sysName 기반)
   - 와일드카드 지원: `hv*`, `mv*`, `hc*`

#### UI 개선 사항

- **Add 버튼 위치**: 각 입력 필드 오른쪽에 배치하여 공간 효율성 향상
- **ListBox 크기**: Add 버튼 이동으로 필터 목록 표시 영역 확대
- **예시 패턴 표시**: Device Pattern 입력 필드 옆에 예시 패턴 표시 (`hv* hc* mv*`)

#### 기본 필터 설정

**Address Range Filters:**
- `192.168.0.100-101`
- `192.168.1.100-101`

**Maker Filter:**
- `ntt*`

**Device Pattern Filters:**
- `hv*`
- `mv*`
- `hc*`

#### 필터 동작 방식

- **Address 필터와 Maker/Device Pattern 필터는 AND 조건**
- Address 필터만 있으면: IP 범위만 확인
- Maker/Device Pattern 필터만 있으면: SNMP 응답 후 sysName/sysDescr 확인
- 둘 다 있으면: 두 조건 모두 통과해야 포함

#### 필터 매칭 로직

- **Address 필터**: IP 주소 범위 패턴 매칭
- **Maker 필터**: sysDescr OID (`1.3.6.1.2.1.1.1.0`) 값과 패턴 매칭
- **Device Pattern 필터**: sysName OID (`1.3.6.1.2.1.1.5.0`) 값과 패턴 매칭
- 와일드카드 `*` 지원, 대소문자 무시

### 2.6 하단 옵션

- **Layout 드롭다운**: 
  - Top Level/Incremental
  - Top Level/Complete
- **Enable Poll After Layout**: 레이아웃 후 Polling 활성화
- **Use full DNS name in map**: Map에 전체 DNS 이름 사용
- **Location discovery layout**: 위치 기반 Discovery 레이아웃

### 2.7 Default 버튼

모든 설정을 기본값으로 초기화하는 버튼입니다.

- 기본 Seed: `192.168.0.0/255.255.254.0`
- 기본 Community: `V1/crclab/crclab`, `V1/public/netman`
- 기본 필터: 위의 기본 필터 설정 참조

---

## 3. Discovery 진행 다이얼로그

Discovery 실행 시 표시되는 진행 상황 다이얼로그입니다.

### 3.1 UI 구성

- **로그 창**: 실시간 검색 진행 상황 표시
- **발견된 디바이스 목록**: DataGrid로 표시
  - 체크박스: 선택/해제 가능
  - 컬럼: IP Address, Status, Community, Version, Port
- **버튼**:
  - **Stop**: Discovery 중단
  - **Select All**: 모든 디바이스 선택
  - **Deselect All**: 모든 디바이스 선택 해제
  - **OK**: 선택된 디바이스 Map에 추가
  - **Cancel**: 취소

### 3.2 Discovery 로직

#### IP 범위 생성
1. Seed IP와 Netmask로 서브넷 범위 계산
2. CIDR 표기법 지원 (`/23`, `/24` 등)
3. 서브넷 마스크를 기반으로 호스트 IP 주소 생성

#### 필터 적용
1. **Address 필터**: IP 범위에 맞는 주소만 스캔
2. **Maker/Device Pattern 필터**: SNMP 응답 후 sysDescr/sysName 확인

#### 디바이스 검색 (병렬 처리)
- `SemaphoreSlim`을 사용한 동시성 제어
- 여러 IP 주소를 병렬로 스캔하여 성능 향상
- 각 IP에 대해:
  1. **Ping 확인** (Find Non-SNMP Nodes 옵션 시)
  2. **SNMP 확인**: 여러 Community String 시도
     - SNMP v1, v2c, v3 지원
     - sysUpTime OID (`1.3.6.1.2.1.1.3.0`) 조회
  3. **필터 확인**: Maker/Device Pattern 필터가 있으면 sysDescr/sysName 확인
  4. 발견된 디바이스 목록에 추가

#### 성능 최적화
- **병렬 처리**: `Task.WhenAll`과 `SemaphoreSlim`을 사용하여 동시에 여러 IP 스캔
- **SNMP 체크 최적화**: Address 필터만 있어도 SNMP 검사 수행 (필터 없을 때도 SNMP 검사)

### 3.3 디바이스 추가

OK 버튼 클릭 시:
- 선택된 디바이스들을 Map에 자동 등록
- `MainViewModel.AddDeviceToSubnet()` 호출
- 각 디바이스의 기본 Polling Protocol은 **SNMP**로 설정
- 이벤트 로그에 등록 정보 기록

---

## 4. 설정 저장 및 로드

### 4.1 설정 파일

- **파일명**: `discovery_config.json`
- **위치**: 실행 파일 디렉토리 (`AppDomain.CurrentDomain.BaseDirectory`)

### 4.2 저장 시점

- **OK 버튼 클릭 시**: 설정 저장
- **Restart 버튼 클릭 시**: Discovery 시작 전 설정 저장

### 4.3 저장 항목

```json
{
  "EnableDiscovery": true,
  "UseSubnetBroadcasts": true,
  "PingScanSubnets": true,
  "AutoRestartTimeHours": 1,
  "EnableStatusPolling": true,
  "EnableServicePolling": true,
  "FindNonSnmpNodes": true,
  "FindRmonDevices": true,
  "FindSnmpV1": true,
  "FindSnmpV2": true,
  "FindSnmpV3": true,
  "FindWeb": true,
  "FindSmtp": true,
  "FindTelnet": false,
  "FindFtp": false,
  "Seeds": [
    {
      "IpAddr": "192.168.0.0",
      "Netmask": "255.255.254.0"
    }
  ],
  "Communities": [
    {
      "Version": "V1",
      "ReadCommunity": "crclab",
      "WriteCommunity": "crclab"
    }
  ],
  "Filters": [
    {
      "Type": "Include",
      "Range": "192.168.0.100-101",
      "FilterCategory": "Address"
    }
  ]
}
```

---

## 5. Map Object Properties 다이얼로그

디바이스의 속성을 편집하는 다이얼로그입니다.

### 5.1 접근 방법

1. **우클릭 메뉴**: Map에서 디바이스 우클릭 → Properties
2. **툴바 버튼**: 우상단 툴바의 두 번째 버튼 (Property 아이콘)

### 5.2 탭 구성

#### Attributes 탭
- **Alias**: 디바이스 별칭
- **Device**: 디바이스 이름
- **Address**: IP 주소 및 포트 (4개 입력 필드로 분리)
- **Icon Name**: 아이콘 파일명
- **Polling Protocol**: Polling 프로토콜 선택
  - **SNMP**: SNMP로 상태 확인 (기본값)
  - **Ping**: ICMP Ping으로 상태 확인
  - **ARP**: ARP로 상태 확인 (미구현)
  - **None**: Polling 비활성화

#### General 탭
- **Node Group 1/2**: 노드 그룹
- **Description**: 설명

#### Access 탭
- **Read Access Mode**: SNMP 버전 선택 (V1, V2c, V3)
- **Read Community**: Read Community String
- **Read/Write Access Mode**: SNMP 버전 선택
- **Read/Write Community**: Read/Write Community String

#### Polling 탭
- **Poll Interval**: Polling 간격 (초)
- **Poll Timeout**: Polling 타임아웃 (밀리초)
- **Poll Retries**: Polling 재시도 횟수

### 5.3 기능

#### 기존 디바이스 편집
- 기존 `UiSnmpTarget`의 모든 속성을 다이얼로그에 로드
- 수정 후 OK 클릭 시 `UiSnmpTarget` 업데이트

#### 새 디바이스 추가
- Map에 새 디바이스 추가 시 사용
- Address 입력 후 Lookup 버튼으로 자동 정보 채우기

---

## 6. Polling Protocol 기능

### 6.1 PollingProtocol Enum

```csharp
public enum PollingProtocol
{
    SNMP = 0,    // SNMP로 상태 확인
    Ping = 1,    // ICMP Ping으로 상태 확인
    ARP = 2,     // ARP로 상태 확인 (미구현)
    None = 3     // Polling 비활성화
}
```

**위치**: `SnmpNms.Core/Models/PollingProtocol.cs`

### 6.2 ISnmpTarget 인터페이스 확장

```csharp
public interface ISnmpTarget
{
    // ... 기존 속성들 ...
    PollingProtocol PollingProtocol { get; }
}
```

**위치**: `SnmpNms.Core/Interfaces/ISnmpTarget.cs`

### 6.3 UiSnmpTarget 모델 확장

```csharp
public class UiSnmpTarget : ISnmpTarget
{
    // ... 기존 속성들 ...
    public PollingProtocol PollingProtocol { get; set; } = PollingProtocol.SNMP;
}
```

**위치**: `SnmpNms.UI/Models/UiSnmpTarget.cs`

### 6.4 PollingService 구현

`PollingService`에서 각 Polling Protocol에 따라 다른 방식으로 디바이스 상태를 확인합니다.

#### SNMP Polling (기본)
- sysUpTime OID (`1.3.6.1.2.1.1.3.0`) 조회
- 응답 성공 시 `DeviceStatus.Up`
- 실패 시 `DeviceStatus.Down`

#### Ping Polling
- `System.Net.NetworkInformation.Ping` 사용
- 타임아웃: 1000ms
- 응답 성공 시 `DeviceStatus.Up`, 응답 시간 표시
- 실패 시 `DeviceStatus.Down`

#### ARP Polling
- 현재 미구현
- `DeviceStatus.Unknown` 반환

#### None
- Polling 비활성화
- `DeviceStatus.Unknown` 반환

**위치**: `SnmpNms.Infrastructure/PollingService.cs`

### 6.5 사용 예시

#### 기본 디바이스 (127.0.0.1)
- 테스트 목적으로 기본 디바이스는 Ping Protocol 사용 권장
- SNMP를 지원하지 않는 경우에도 상태 확인 가능

#### Discovery로 추가된 디바이스
- 기본적으로 SNMP Protocol 사용
- Properties에서 필요 시 Ping 등으로 변경 가능

---

## 7. 사용 방법

### 7.1 Discovery 실행

1. **Find Map Objects 버튼 클릭** (왼쪽 위 툴바 첫 번째 버튼)
2. **Seeds 탭**에서 검색할 네트워크 범위 추가
   - CIDR 표기법 사용 가능: `192.168.0.0/24`
   - 또는 IP와 Netmask 입력: `192.168.0.0`, `255.255.255.0`
3. **Comm 탭**에서 Community String 추가
4. **Filters 탭**에서 필터 설정 (선택 사항)
   - Address Range: `192.168.0.100-101`
   - Maker Pattern: `ntt*`
   - Device Pattern: `hv*`, `mv*`, `hc*`
5. **Proto 탭**에서 SNMP 버전 선택
6. **Restart 버튼 클릭**
7. Discovery 진행 다이얼로그에서 진행 상황 확인
8. 발견된 디바이스 중 원하는 항목 선택
9. **OK 클릭**하여 Map에 추가

### 7.2 디바이스 속성 편집

1. **Map에서 디바이스 우클릭** → Properties
   - 또는 **툴바의 Property 버튼 클릭**
2. **Attributes 탭**에서 Polling Protocol 변경
   - SNMP: SNMP 에이전트가 있는 디바이스
   - Ping: SNMP를 지원하지 않는 디바이스
3. **Access 탭**에서 SNMP Community 설정
4. **Polling 탭**에서 Polling 간격 및 타임아웃 설정
5. **OK 클릭**하여 변경 사항 저장

### 7.3 설정 초기화

1. Discovery/Polling Agents 다이얼로그 열기
2. **Default 버튼 클릭**
3. 모든 설정이 기본값으로 초기화됨

---

## 8. 기술적 세부 사항

### 8.1 파일 구조

```
SnmpNms.UI/
├── Views/
│   └── Dialogs/
│       ├── DiscoveryPollingAgentsDialog.xaml
│       ├── DiscoveryPollingAgentsDialog.xaml.cs
│       ├── DiscoveryProgressDialog.xaml
│       ├── DiscoveryProgressDialog.xaml.cs
│       ├── MapObjectPropertiesDialog.xaml
│       └── MapObjectPropertiesDialog.xaml.cs
├── Models/
│   └── UiSnmpTarget.cs
└── MainWindow.xaml.cs

SnmpNms.Core/
├── Models/
│   └── PollingProtocol.cs
└── Interfaces/
    └── ISnmpTarget.cs

SnmpNms.Infrastructure/
└── PollingService.cs
```

### 8.2 주요 클래스

#### DiscoveryPollingAgentsDialog
- Discovery 설정을 관리하는 다이얼로그
- 설정 저장/로드 기능
- Seed, Community, Filter 관리

#### DiscoveryProgressDialog
- Discovery 진행 상황 표시
- 병렬 IP 스캔 처리
- 발견된 디바이스 목록 표시

#### MapObjectPropertiesDialog
- 디바이스 속성 편집
- Polling Protocol 선택
- SNMP 설정 관리

#### PollingService
- 백그라운드 Polling 서비스
- Protocol별 상태 확인 로직
- 이벤트 기반 결과 전달

### 8.3 성능 최적화

- **병렬 처리**: `SemaphoreSlim`과 `Task.WhenAll`을 사용한 동시 IP 스캔
- **필터 최적화**: Address 필터로 스캔 범위 사전 축소
- **비동기 처리**: 모든 네트워크 작업을 비동기로 처리

### 8.4 에러 처리

- 네트워크 오류 시 예외 처리
- SNMP 타임아웃 처리
- 설정 파일 읽기/쓰기 오류 처리

---

## 9. 향후 개선 사항

- [ ] ARP Polling 구현
- [ ] TCP Port Polling 구현
- [ ] DNS 이름 자동 해석
- [ ] Discovery 스케줄링 기능
- [ ] 필터 정규식 지원 확장
- [ ] Discovery 결과 내보내기/가져오기

---

## 10. 참고 사항

### 10.1 기본값 요약

- **기본 Seed**: `192.168.0.0/255.255.254.0` (512개 IP)
- **기본 Community**: `V1/crclab/crclab`, `V1/public/netman`
- **기본 필터**: 
  - Address: `192.168.0.100-101`, `192.168.1.100-101`
  - Maker: `ntt*`
  - Device Pattern: `hv*`, `mv*`, `hc*`
- **기본 Polling Protocol**: SNMP

### 10.2 주의 사항

- Discovery는 네트워크 트래픽을 발생시킬 수 있습니다
- 대규모 네트워크 스캔 시 시간이 오래 걸릴 수 있습니다
- SNMP를 지원하지 않는 디바이스는 Ping Protocol 사용을 권장합니다
- 127.0.0.1은 테스트 목적으로만 사용하는 것을 권장합니다

---

## 11. 변경 이력

### v1.0 (최초 구현)
- Discovery/Polling Agents 다이얼로그 기본 구조
- Seed, Community, Filter 관리 기능
- Discovery 진행 다이얼로그
- 기본 필터 설정

### v1.1 (UI 개선)
- Filter UI 개선 (Add 버튼 위치 변경)
- Device Name → Device Pattern 변경
- 예시 패턴 표시 추가

### v1.2 (CIDR 지원)
- CIDR 표기법 지원
- 기본 Seed 설정 변경 (`192.168.0.0/23`)

### v1.3 (기능 추가)
- Default 버튼 추가
- SNMP 버전 선택 옵션 추가
- Find Options를 Proto 탭으로 이동

### v1.4 (성능 개선)
- 병렬 처리로 Discovery 속도 향상
- SNMP 체크 로직 개선

### v1.5 (Polling Protocol)
- PollingProtocol enum 추가
- MapObjectPropertiesDialog에 Polling Protocol 선택 추가
- PollingService에서 Protocol별 처리 구현
- Device Properties 다이얼로그 열기 기능 추가
