# 구성

## 구성 요소와 의존 방향

`LuminanceCapture.App`(WPF 실행 파일, 어셈블리 이름 `LuminanceCapture`) → `LuminanceCapture.Core`(.NET 라이브러리). 테스트 프로젝트 `LuminanceCapture.Tests`는 Core만 참조한다. 대상은 `net10.0-windows10.0.22621.0`(윈도우 11 전용 API 사용). Core는 App을 모른다.

Core의 구성 요소(이름 · 역할):

- `Luminance` · 휘도 계수의 단일 정의. 여기서 (a) 화면 색상 변환용 5x5 행렬, (b) BGRA32 버퍼를 흑백으로 바꾸는 함수가 같은 계수로 나온다.
- `GrayscaleFilter` · 윈도우 Magnification API의 전체 화면 색상 변환(`MagSetFullscreenColorEffect`)으로 모든 모니터에 흑백을 건다. 켜기/끄기/토글, `Changed` 이벤트, 절전 복귀·잠금 해제·화면 구성 변경 뒤 재적용. 끄기는 단위 행렬 적용이다.
- `WgcCaptureSource` · Windows.Graphics.Capture로 모니터 한 대의 필터 적용 전 실제 컬러 프레임을 얻는다(커서·테두리 끔). 단일 작업자 전용.
- `CaptureService` · `Request()`가 포인터 위치 모니터·시각·저장 폴더를 호출 즉시 확정하고 직렬 큐에 넣는다. 작업자 스레드가 프레임 획득 → `CapturePairWriter` 저장 → `CaptureCompleted` 이벤트.
- `CapturePairWriter` · 컬러 PNG와 흑백 PNG를 임시 파일로 쓴 뒤 덮어쓰기 없이 이름을 바꿔 들여놓는다. 하나라도 실패하면 둘 다 지운다.
- `PairNameAllocator` · `yyyy-MM-dd_HHmmss_color.png` / `_gray.png` 한 쌍의 이름을 정하고, 충돌하면 쌍 전체에 `_2`, `_3`을 붙인다.
- `HotkeyService` / `HotkeyParser` / `HotkeyApplier` / `HotkeyCapture` · 메시지 전용 창에서 전역 단축키(RegisterHotKey)를 등록하고, 텍스트 표기("Ctrl+Alt+F9")를 해석·검증한다.
- `AppSettings` / `SettingsStore` · 사용자별 JSON 설정.
- `AutoStartManager` · HKCU Run 값 등록/해제. `SingleInstance` · 이름 있는 뮤텍스와 이벤트. `ErrorLog` · 오류 로그 파일. `MonitorLocator` · 포인터가 있는 모니터 확정. `WindowCaptureExclusion` · 창을 화면 캡처에서 제외.

App의 구성 요소: `AppHost`(서비스를 한 곳에서 조립), `MainWindow`(설정 창), `TrayController`(트레이 아이콘·메뉴), `ToastWindow`(저장 알림), `IconArt`(코드로 그리는 트레이 아이콘).

## 대표 흐름

캡처: 사용자가 캡처 단축키 → 메시지 전용 창이 `WM_HOTKEY` 수신 → `HotkeyService.Pressed` → `AppHost.CaptureNow` → `CaptureService.Request()`가 그 순간 포인터의 모니터를 확정 → 작업자 스레드의 `WgcCaptureSource`가 그 모니터의 실제 컬러 프레임(물리 해상도, 알파 255) 획득 → `Luminance`로 흑백 버퍼 계산 → `CapturePairWriter`가 두 PNG 저장 → `CaptureCompleted`(작업자 스레드) → UI 스레드로 넘겨 토스트 표시(성공이면 설정에 따라 소리).

흑백 토글: 토글 단축키·창 버튼·트레이 메뉴 → `GrayscaleFilter.Toggle()` → Magnification 전체 화면 효과 → `Changed` → UI 스레드에서 트레이 아이콘·메뉴 글자·창 상태 갱신.

## 경계

- 프로세스 밖으로 나가는 것은 화면 색상 효과(프로세스 종료 시 운영체제가 되돌림)와 사용자가 정한 폴더의 PNG 파일, `%AppData%`의 설정, HKCU Run 값, `%LocalAppData%`의 로그뿐이다. 네트워크 통신 없음.
- 설정 창, 토스트, 트레이는 UI 스레드 전용이다. 캡처 완료 이벤트는 작업자 스레드에서 오므로 UI로 넘기기 전에는 UI 객체를 만지지 않는다.
- 단일 인스턴스: 두 번째 실행은 첫 실행에 신호만 보내고 종료한다.
