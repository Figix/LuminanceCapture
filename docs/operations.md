# 운영

## 준비물

윈도우 11, .NET SDK 10(`dotnet --list-sdks`에 10.x가 있어야 함), 설치 파일을 만들 때만 Inno Setup 6(`ISCC.exe`).

## 빌드·테스트 (저장소 루트에서)

```
dotnet build LuminanceCapture.slnx
dotnet test LuminanceCapture.slnx
```

`build`가 먼저다(테스트 프로젝트가 Core를 참조). 두 명령 모두 종료 코드 0이어야 한다. 처음 실행하면 NuGet 복원이 네트워크를 쓴다(xUnit, Vortice).

## 실행해서 확인

```
dotnet run --project src/LuminanceCapture.App
```

검증용으로 실제 설정과 자동 실행 등록을 건드리지 않으려면 환경 변수를 준다.

```
$env:LUMINANCECAPTURE_SETTINGS = "$env:TEMP\lc-settings.json"
$env:LUMINANCECAPTURE_NO_AUTOSTART = "1"
dotnet run --project src/LuminanceCapture.App -- --background
```

기본 단축키: 흑백 토글 `Ctrl+Alt+F9`, 캡처 `Ctrl+Alt+F10`. 흑백을 켠 채 확인할 때는 반드시 다시 끄거나 프로세스를 종료한다(종료하면 컬러로 돌아온다).

## 설치 파일 만들기

```
powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1 -Version 1.0.0
```

순서: (1) `dotnet publish`(win-x64, self-contained)로 `dist/publish`를 만든다. (2) `ISCC.exe`가 `%LocalAppData%\Programs\Inno Setup 6`, `Program Files (x86)\Inno Setup 6`, `Program Files\Inno Setup 6` 중 하나에 있으면 `installer/LuminanceCapture.iss`를 컴파일해 `dist/LuminanceCapture-Setup-<버전>.exe`를 만든다. 없으면 경고를 내고 종료 코드 2로 끝나며 `dist/publish`만 남는다. 순서를 바꾸면(게시 전에 컴파일) 파일이 없어 실패한다.

설치 파일의 동작: 관리자 권한 없이 사용자 단위 설치(`{autopf}`), 시작 메뉴 항목 1개(검색으로 실행), 설치 시작 전 실행 중인 앱 종료, 제거 시 실행 중인 앱 종료와 HKCU Run 값 삭제. 제거해도 캡처 이미지와 `%AppData%\LuminanceCapture\settings.json`은 남는다.

## 환경 설정

- `LUMINANCECAPTURE_SETTINGS`: 설정 파일 전체 경로. 설정하면 `%AppData%` 대신 이 파일을 읽고 쓴다(검증 전용).
- `LUMINANCECAPTURE_NO_AUTOSTART`: `1`이면 시작 프로그램 레지스트리를 읽지도 쓰지도 않는다(검증 전용).
- 오류 로그: `%LocalAppData%\LuminanceCapture\log.txt`.
