# 규칙

## 검증 게이트 (병합·완료 전 필수)

- `dotnet build LuminanceCapture.slnx` 와 `dotnet test LuminanceCapture.slnx` 가 둘 다 종료 코드 0이어야 한다. 테스트가 0개 발견되는 상태는 실패다.
- 사람이 화면으로 확인해야 하는 동작(실제 흑백 전환, 단축키, 캡처 결과, 트레이·창 모양, 설치·제거)은 자동 테스트로 대신하지 않는다. 확인한 증거(실행 결과 또는 저장 파일)를 남기고 상태 문서에 "검증됨/미검증"을 구분해 적는다.

## 휘도·캡처 규칙 (어기면 사용자가 보는 결과가 틀어진다)

- 휘도 계수와 변환은 `Luminance` 한 곳에만 정의한다. 화면 행렬과 파일 변환이 같은 상수에서 나와야 하며, 테스트가 이를 확인한다. 계수를 복제하거나 선형광 변환을 한쪽에만 넣지 않는다.
- 컬러 원본 획득에는 Windows.Graphics.Capture만 쓴다. GDI(`CopyFromScreen`/BitBlt)나 DXGI Desktop Duplication으로 바꾸지 않는다(필터가 켜져 있으면 흑백 픽셀이 저장된다).
- 흑백 PNG는 컬러 캡처 바이트에서 `Luminance`로 계산한다. 필터가 적용된 화면에서 읽어 만들지 않는다.
- 파일 쓰기는 `CapturePairWriter`를 통한다: 같은 폴더의 임시 파일 → 덮어쓰기 없는 이름 변경 → 실패 시 쌍 전체 삭제. 캡처 파일을 `File.Copy(overwrite: true)`나 `File.Move(overwrite: true)`로 쓰지 않는다.
- 캡처 대상 모니터·시각·폴더는 `CaptureService.Request()` 안에서 확정한다. 작업자 스레드에서 다시 조회하지 않는다.
- `GrayscaleFilter`는 시작 시 효과를 적용하지 않고 켜짐 상태를 어디에도 저장하지 않는다. 설정 모델에 흑백 상태 필드를 추가하지 않는다.
- 프로그램이 소유한 UI 중 캡처에 찍히면 안 되는 창(토스트)은 `WindowCaptureExclusion.Apply`를 호출한다. 설정 창에는 적용하지 않는다.

## 구조 규칙

- Core는 App을 참조하지 않는다. WPF 창·트레이 등 UI 타입은 App에만 둔다. 서비스 조립은 `AppHost` 한 곳이다.
- 레지스트리와 시스템 호출을 쓰는 로직은 테스트에서 가짜로 바꿀 수 있게 키 경로·이름·API 인터페이스(`IColorEffectApi`, `ICaptureSource`)를 주입받는다. 테스트가 실제 HKCU Run 키나 실제 화면 효과를 건드리면 안 된다.
- 단위 테스트는 `tests/LuminanceCapture.Tests`에 Core 로직 단위로 둔다. 화면에 효과를 거는 코드를 테스트에서 실제로 실행하지 않는다.
- 새 Core 파일에서 `System.IO`는 프로젝트 전역 using으로 제공된다(WPF 활성화로 기본 using에서 빠지기 때문).

## 설정·환경 규칙

- 설정은 `%AppData%\LuminanceCapture\settings.json`(camelCase 키 5개: `toggleHotkey`, `captureHotkey`, `saveFolder`, `autoStart`, `captureSound`). 저장은 임시 파일 후 교체로 한다.
- 실행 인자 `--background`는 창 없이 트레이로 시작한다. 자동 실행 명령은 `"<exe 경로>" --background` 형태여야 한다.
- 환경 변수 `LUMINANCECAPTURE_SETTINGS`(설정 파일 경로 대체)와 `LUMINANCECAPTURE_NO_AUTOSTART=1`(레지스트리 접근 생략)은 검증용이다. 검증 중 실제 설정과 Run 키를 건드리지 않기 위해 쓴다.

## 커밋

- 커밋 메시지는 변경 내용 한 줄 요약으로 쓴다. 빌드 산출물(`bin`, `obj`, `dist`, `publish`)은 커밋하지 않는다.
