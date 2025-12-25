구현 내용
1. 왼쪽 위 툴바 첫 번째 버튼 변경
"Add Device" → "Find Map Objects"로 변경
아이콘: &#xE721; (돋보기 아이콘)
ToolTip: "Find Map Objects"
2. Discovery/Polling Agents 다이얼로그 생성
파일: DiscoveryPollingAgentsDialog.xaml 및 .xaml.cs
구조:
왼쪽: Polling Agents 리스트 (Address, Status)
오른쪽: 탭 인터페이스 (General, Proto, Seeds, Comm, Filters)
3. 탭 구성
General 탭
Discovery Config: Enable Discovery, Use Subnet Broadcasts, Ping Scan Subnets, Auto Restart Time
Polling Config: Enable Status Polling, Enable Service Polling
Find Options: Find Non-SNMP Nodes, Find RMON Devices, Find TCP Ports (WEB, SMTP, Telnet, FTP)
Seeds 탭
Seed IP Addr / Seed Netmask 입력
Seeds 리스트 (Add/Change/Delete)
Comm 탭
Community Strings 리스트 (Add/Edit/Delete)
Community Edit 다이얼로그 포함
Filters 탭
Address Range 입력
Include/Exclude 필터 리스트 (Add/Change/Delete)
4. 하단 옵션
Layout 드롭다운 (Top Level/Incremental, Top Level/Complete)
Enable Poll After Layout
Use full DNS name in map
Location discovery layout
5. 기본 데이터
기본 에이전트: localhost (connected)
기본 커뮤니티: V1/crclab/crclab, V1/public/netman
기본 필터: Include 198...22-88, 207.100-211., 207.212.33.
사용 방법
왼쪽 위 툴바의 첫 번째 버튼(돋보기 아이콘) 클릭
"Discovery/Polling Agents" 다이얼로그가 열림
각 탭에서 Discovery/Polling 설정 구성
OK 클릭하여 설정 저장
애플리케이션을 실행하고 "Find Map Objects" 버튼을 클릭하면 다이얼로그가 표시됩니다.


구현 내용
1. Discovery 설정 저장
파일: discovery_config.json (실행 파일 위치에 저장)
저장 시점:
OK 버튼 클릭 시
Restart 버튼 클릭 시 (Discovery 시작 전)
저장 항목:
Discovery Config (Enable Discovery, Use Subnet Broadcasts, Ping Scan Subnets 등)
Polling Config
Find Options
Seeds, Communities, Filters 리스트
2. Restart 버튼 → Discovery 진행 다이얼로그
DiscoveryProgressDialog 생성
기능:
실시간 검색 진행 상황 표시 (로그 창)
발견된 디바이스 목록 표시 (DataGrid)
각 디바이스 선택/해제 가능 (체크박스)
Select All / Deselect All 버튼
Stop 버튼으로 중단 가능
3. Discovery 로직
Seeds에서 IP 범위 생성 (네트워크/서브넷 마스크 기반)
필터 적용 (Include/Exclude 패턴 매칭)
각 IP에 대해:
Ping 확인 (Find Non-SNMP Nodes 옵션)
SNMP 확인 (여러 Community String 시도)
발견된 디바이스 목록에 추가
4. OK 버튼 → 디바이스 등록
선택된 디바이스들을 Map에 자동 등록
MainViewModel.AddDeviceToSubnet() 호출
이벤트 로그에 등록 정보 기록
완료 메시지 표시
사용 방법
"Find Map Objects" 버튼 클릭
Seeds 탭에서 IP 주소/서브넷 마스크 추가
Comm 탭에서 Community String 추가
General 탭에서 Discovery 옵션 설정
Restart 버튼 클릭 → Discovery 진행 다이얼로그 표시
검색 완료 후 원하는 디바이스 선택
OK 클릭 → 선택된 디바이스가 Map에 자동 등록
애플리케이션을 다시 실행하고 "Find Map Objects" 버튼을 클릭해 테스트하세요.

구현 내용
1. 필터 기본값 변경
기존 필터 제거
Address Range 필터 기본값:
192.168.0.100-101
192.168.1.100-101
Name Pattern 필터 기본값:
ntt*
hv*
mv*
hc*
2. 필터 UI 개선
Address Range Filter와 Maker/Device Name Filter를 별도 그룹으로 분리
Name Pattern 입력 필드 추가
필터 목록에 체크박스로 필터 타입 표시 (Address/Name)
3. Name 필터 로직
sysDescr와 sysName을 SNMP로 조회
Name 필터는 sysName 우선, 없으면 sysDescr 사용
와일드카드 패턴 매칭 (* 지원)
대소문자 무시 (RegexOptions.IgnoreCase)
4. 필터 동작 방식
Address 필터와 Name 필터는 AND 조건
Address 필터만 있으면 IP만 확인
Name 필터만 있으면 디바이스 이름만 확인
둘 다 있으면 둘 다 통과해야 포함
사용 방법
Filters 탭에서:
Address Range: IP 주소 범위 입력 후 "Add Address Range" 클릭
Name Pattern: 메이커/기기명 패턴 입력 후 "Add Name Pattern" 클릭 (예: ntt*, hv*)
Discovery 실행 시:
Address 필터에 매칭되는 IP만 스캔
SNMP 응답이 있으면 sysName/sysDescr로 Name 필터 확인
두 필터 모두 통과한 디바이스만 목록에 추가
애플리케이션을 실행해 테스트하세요. 기본값으로 192.168.0.100-101, 192.168.1.100-101과 ntt*, hv*, mv*, hc* 필터가 적용됩니다.