# SnmpNms.Infrastructure 개요

`SnmpNms.Infrastructure`는 **Core 인터페이스의 실제 구현**을 두는 프로젝트입니다.  
SharpSnmpLib 같은 외부 라이브러리 의존은 여기서만 가져가고, UI/Core에 새지 않게 유지합니다.

---

## 역할

- `ISnmpClient`, `IPollingService`, `IMibService`의 **구현 제공**
- 외부 SNMP 라이브러리(SharpSnmpLib) 어댑터 역할

---

## 의존성

- NuGet: `Lextm.SharpSnmpLib` (`12.5.7`)
- Target: `net9.0`

---

## 폴더/파일 구조(현재)

- `SnmpClient.cs` : `ISnmpClient` 구현
- `PollingService.cs` : `IPollingService` 구현
- `MibService.cs` : `IMibService` 구현(최소 정규식 파서)
- `Class1.cs` (템플릿 잔여: 추후 삭제/정리 권장)

---

## 클래스 트리(요약)

- `SnmpClient : ISnmpClient`
  - `GetAsync(target, oid)`
  - `GetAsync(target, oids)`
  - `GetNextAsync(target, oid)`
  - `WalkAsync(target, rootOid)`

- `PollingService : IPollingService`
  - `Start/Stop`
  - `AddTarget/RemoveTarget`
  - `SetInterval`
  - `event OnPollingResult`

- `MibService : IMibService`
  - `LoadMibModules(directoryPath)`
  - `GetOidName(oid)` (Longest match + .0 처리)
  - `GetOid(name)`

---

## 구현 개요(핵심 동작)

### 1) `SnmpClient`

- SharpSnmpLib의 동기 호출을 `Task.Run(...)`으로 감싸 **UI 스레드 블로킹을 회피**하는 형태.
- `GetAsync`
  - `Messenger.Get(...)` 사용
  - 수행 시간 측정 후 `SnmpResult.Success(...)` 반환
  - 예외는 `SnmpResult.Fail(ex.Message)`로 변환
- `GetNextAsync`
  - `GetNextRequestMessage` 생성 후 `GetResponse(...)`로 응답 수신
- `WalkAsync`
  - `Messenger.Walk(..., WalkMode.WithinSubtree)` 사용

### 2) `PollingService`

- `System.Timers.Timer` 기반(기본 3000ms)
- 내부 타겟 저장: `ConcurrentDictionary<string, ISnmpTarget>` (key = `"ip:port"`)
- 매 tick마다 등록된 모든 타겟을 동시 폴링: `Task.WhenAll(_targets.Values.Select(PollTargetAsync))`
- Alive 판단 OID: `sysUpTime` (`1.3.6.1.2.1.1.3.0`)
- 결과는 `OnPollingResult` 이벤트로 UI에 전달

### 3) `MibService`

- 목적: **OID ↔ Name** 변환 최소 구현
- 기본 표준 일부는 하드코딩 등록(`sysDescr`, `sysUpTime` 등)
- 파일 로딩: `.mib`, `.txt`를 전부 읽어서 정규식으로 `OBJECT-TYPE ::= { parent n }`만 파싱
- `GetOidName(oid)`
  - 정확히 일치 → 반환
  - `.0` 제거 후 base OID 검색
  - Longest match(접두사)로 인덱스가 붙은 OID도 일부 표시

---

## 현재 리스크/개선 포인트(정리)

- `PollingService`
  - **Timer 재진입 가능성**: 이전 tick이 끝나기 전에 다음 tick이 올 수 있음(중첩 폴링)
  - `ISnmpTarget.Retries`는 아직 실제 로직에 반영되지 않음
- `SnmpClient`
  - `Task.Run` 기반은 단순하지만, 대규모 장비에서 스레드풀 부하/취소/타임아웃 정책이 부족할 수 있음
- `MibService`
  - IMPORTS/모듈 의존성/ENUM/TYPE/테이블 해석은 아직 미구현(“이름 변환용 최소” 수준)


