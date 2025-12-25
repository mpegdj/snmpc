# Doc 인덱스 (문서 내비게이션)

이 폴더는 `snmpc` 프로젝트의 **계획/운영/로그/레퍼런스** 문서를 모아두는 곳입니다.

---

## 지금 이 프로젝트는 무엇인가

- **목표**: C#/.NET 9 + WPF로 **SNMPc 스타일의 경량 NMS(Manager)** 를 구현
- **아키텍처**: `SnmpNms.Core` → `SnmpNms.Infrastructure` → `SnmpNms.UI` (UI는 인터페이스만 사용)

---

## 문서 역할(무엇을 어디에 쓰나)

- **작업 로그 원장(SSOT)**: `5_dev_logs.md`
  - 앞으로 작업 기록은 여기만 누적(단일 원장)
- **개발 계획(업데이트된 체크리스트)**: `1_dev_plan.md`
- **운영/개발 환경/실행 방법**: `4_dev_ops.md`
- **MVP 정의/수용 기준**: `1_dev_plan.md`에 포함(통합)
- **프로젝트별 개요(코드 구조/흐름/리스크)**:
  - `SnmpNms.Core.md`
  - `SnmpNms.Infrastructure.md`
  - `SnmpNms.UI.md`
- **SNMPc 기능 맵(레퍼런스)**: `1_1_snmpc_function.md`
- **SNMPc UI 레퍼런스(PDF)**: `intro_snmpc.pdf`

---

## 빠른 실행(개발 PC)

프로젝트 루트(`D:\git\snmpc`)에서:

```powershell
dotnet build SnmpNms.UI
dotnet run --project SnmpNms.UI
```

---

## 트러블슈팅(자주 터지는 케이스)

- **WPF XAML Parse/connectionId/InvalidCast가 나는 경우**
  - 가장 먼저 `SnmpNms.UI/bin` + `SnmpNms.UI/obj`를 삭제하고 클린 빌드

```powershell
Remove-Item -Recurse -Force .\SnmpNms.UI\bin, .\SnmpNms.UI\obj -ErrorAction SilentlyContinue
dotnet clean SnmpNms.UI
dotnet build SnmpNms.UI
```

- **crash.log**
  - 앱이 `DispatcherUnhandledException`을 잡아 `SnmpNms.UI/bin/Debug/net9.0-windows/crash.log`에 남길 수 있음(원인 추적용)
