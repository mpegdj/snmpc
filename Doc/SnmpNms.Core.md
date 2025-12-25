# SnmpNms.Core 개요

`SnmpNms.Core`는 **도메인 모델 + 인터페이스(계약)**만 포함하는 프로젝트입니다.  
UI/WPF, SharpSnmpLib 같은 외부 구현 디테일은 여기에 들어오지 않도록 유지하는 게 핵심입니다.

---

## 역할

- **계약(Interface) 정의**: UI/Infrastructure가 “무엇을 할 수 있는지”만 표현
- **도메인 모델 정의**: SNMP 결과, 폴링 결과, 상태 등 공통 데이터 구조

---

## 의존성

- **외부 NuGet 없음**(순수 .NET)
- Target: `net9.0`

---

## 폴더 구조

- `Interfaces/`
  - `ISnmpClient`
  - `ISnmpTarget`
  - `IPollingService`
  - `IMibService`
- `Models/`
  - `SnmpResult`
  - `SnmpVariable`
  - `PollingResult`
  - `DeviceStatus`
  - `SnmpVersion`
- `Class1.cs` (현재는 템플릿 잔여: 추후 삭제/정리 권장)

---

## 클래스/인터페이스 트리(요약)

- **Interfaces**
  - `ISnmpClient`
    - `GetAsync(ISnmpTarget target, string oid)`
    - `GetAsync(ISnmpTarget target, IEnumerable<string> oids)`
    - `GetNextAsync(ISnmpTarget target, string oid)`
    - `WalkAsync(ISnmpTarget target, string rootOid)`
  - `ISnmpTarget`
    - `IpAddress`, `Port`, `Community`, `Version`, `Timeout`, `Retries`
  - `IPollingService`
    - `Start()`, `Stop()`, `AddTarget()`, `RemoveTarget()`, `SetInterval()`
    - `event OnPollingResult`
  - `IMibService`
    - `LoadMibModules()`, `GetOidName()`, `GetOid()`

- **Models**
  - `SnmpResult`
    - `IsSuccess`, `ErrorMessage`, `Variables`, `ResponseTime`
    - `Success(...)`, `Fail(...)` (팩토리)
  - `SnmpVariable`
    - `Oid`, `Value`, `TypeCode`
  - `PollingResult`
    - `Target`, `Status`, `ResponseTime`, `Timestamp`, `Message`
  - `DeviceStatus` (enum): `Unknown`, `Up`, `Down`
  - `SnmpVersion` (enum): `V1`, `V2c`, `V3`

---

## 현재 설계 의도(핵심 원칙)

- UI는 Core의 **인터페이스만 참조**한다.
- Infrastructure는 Core의 **인터페이스를 구현**한다.
- 이렇게 하면 UI 변경/교체, SNMP 라이브러리 교체가 쉬워진다.

---

## 현재 빈 구멍(추가될 가능성이 큰 Core 영역)

- Trap 관련 계약(예: `ITrapListener`, `TrapEvent`, `IEventService`)
- Alarm/Event 도메인 모델(Severity, Ack/Clear, Dedup 등)
- Polling 정책(재시도, 스케줄링, 그룹별 OID 세트 등)을 표현하는 모델


