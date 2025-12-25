# 개발/운영 환경 (업그레이드)

이 문서는 **개발 PC에서 빌드/실행/문제 해결**에 필요한 “운영 지식”을 정리합니다.

---

## 전제

- OS: Windows
- .NET SDK: 9.x
- UI: WPF (`SnmpNms.UI`, `net9.0-windows`, `UseWPF=true`)

---

## 빌드/실행

프로젝트 루트(`D:\git\snmpc`)에서:

```powershell
dotnet build SnmpNms.UI
dotnet run --project SnmpNms.UI
```

---

## 자주 쓰는 명령

- **클린 빌드**

```powershell
Remove-Item -Recurse -Force .\SnmpNms.UI\bin, .\SnmpNms.UI\obj -ErrorAction SilentlyContinue
dotnet clean SnmpNms.UI
dotnet build SnmpNms.UI
```

- **솔루션 전체 빌드**

```powershell
dotnet build SnmpNms.sln
```

---

## 트러블슈팅

### WPF XAML Parse/connectionId/InvalidCast

- XAML 수정 후에도 “예전 크래시”가 보이면, 대부분 `obj`/BAML 산출물이 꼬인 케이스입니다.
- 위의 **클린 빌드**를 먼저 수행합니다.

### crash.log

- `SnmpNms.UI/App.xaml.cs`에서 `DispatcherUnhandledException`을 잡아 예외를 남길 수 있습니다.
- 위치(기본): `SnmpNms.UI/bin/Debug/net9.0-windows/crash.log`
