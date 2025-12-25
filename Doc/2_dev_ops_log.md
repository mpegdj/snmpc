# C# WPF SNMP Manager 개발 로그

## 1. 사전 준비 및 도구 선정
개발 시작 전, 다음과 같은 도구와 라이브러리를 선정하고 설치했습니다.

### 1.1 언어 및 프레임워크: C# / .NET 9.0
- **선정 이유**: 사용자가 C# 기반 개발을 요청하였으며, 최신 Long Term Support(혹은 최신) 버전인 .NET 9.0을 사용하여 성능과 유지보수성을 확보하고자 했습니다.
- **확인 작업**:
  ```bash
  dotnet --version
  # 결과: 9.0.300
  ```

### 1.2 GUI 프레임워크: WPF (Windows Presentation Foundation)
- **선정 이유**: Windows 환경에서 SNMP Manager와 같은 데스크톱 애플리케이션을 만들 때, WinForms보다 강력한 데이터 바인딩과 유연한 UI 커스터마이징이 가능하기 때문입니다.
- **프로젝트 생성 명령어**:
  ```bash
  dotnet new wpf -n SnmpManager
  ```

### 1.3 SNMP 라이브러리: Lextm.SharpSnmpLib
- **선정 이유**: SNMP 프로토콜(v1, v2c, v3)을 직접 소켓 프로그래밍으로 구현하는 것은 복잡하고 오류 가능성이 높습니다. `SharpSnmpLib`은 C# 생태계에서 가장 널리 쓰이고 검증된 오픈소스 라이브러리이므로 이를 채택했습니다.
- **설치 명령어**:
  ```bash
  dotnet add SnmpManager/SnmpManager.csproj package Lextm.SharpSnmpLib
  ```

---

## 2. 프로젝트 개요
- **목표**: C# WPF를 사용하여 SNMP NMS(Network Management System) Manager 개발
- **기능**: IP 주소, 커뮤니티(Community String), OID를 입력받아 SNMP GET 요청 수행 및 결과 출력

## 3. 구현 내용

### 3.1 UI 구성 (MainWindow.xaml)
- **Grid Layout**: 입력 필드와 버튼을 배치하기 위해 Grid 사용
- **Input Fields**:
  - IP Address (`txtIp`)
  - Community (`txtCommunity`)
  - OID (`txtOid`)
- **Action**: Get 버튼 (`btnGet`)
- **Output**: 결과 출력용 TextBox (`txtResult`)

### 3.2 SNMP GET 로직 (MainWindow.xaml.cs)
- `Lextm.SharpSnmpLib.Messaging.Messenger.Get` 메서드 사용
- **주요 코드 흐름**:
  1. 입력된 IP 주소 유효성 검사 (`IPAddress.TryParse`)
  2. `OctetString` (Community), `ObjectIdentifier` (OID) 객체 생성
  3. `Messenger.Get` 호출 (Version 2c 사용, Timeout 3000ms)
  4. 응답 결과를 텍스트 박스에 출력
  5. 예외 발생 시 에러 메시지 출력

## 4. 실행 방법
프로젝트 루트 디렉토리에서 아래 명령어로 실행:
```bash
dotnet run --project SnmpManager
```
