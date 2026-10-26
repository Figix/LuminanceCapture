# 함정과 비자명한 동작

## 캡처

- 증상: 흑백을 켠 채 찍은 "컬러" 파일이 회색이다. 원인: GDI(`Graphics.CopyFromScreen`)와 DXGI Desktop Duplication은 화면 색상 변환이 적용된 최종 출력을 돌려준다(순수 빨강 창이 (54,54,54)로 읽힘). Windows.Graphics.Capture만 적용 전 실제 색을 준다. 대응: 컬러 획득은 `WgcCaptureSource` 하나로 유지하고, 검증할 때는 필터를 켠 채 빨간 창을 찍어 컬러 파일 픽셀이 (255,0,0), 흑백 파일 픽셀이 (54,54,54)인지 읽는다.
- 증상: 캡처에 마우스 포인터나 노란 테두리가 찍힌다. 원인: 세션 기본값은 커서·테두리 포함. 대응: 세션에서 `IsCursorCaptureEnabled=false`, `IsBorderRequired=false`를 설정한다(`IsBorderRequired`는 윈도우 11 TFM `10.0.22000` 이상에서만 컴파일된다).
- 증상: DPI 배율이 다른 모니터에서 포인터 위치로 고른 모니터가 틀리거나 이미지 크기가 논리 픽셀이다. 원인: DPI 가상화된 좌표. 대응: 앱 매니페스트가 PerMonitorV2를 선언하고, `MonitorLocator`는 호출 스레드를 일시적으로 PerMonitorV2로 바꿔 좌표를 읽는다. 저장 해상도는 모니터의 물리 픽셀이다.
- `CaptureCompleted`는 작업자 스레드에서 발생한다. 토스트·창·트레이를 만지기 전에 UI 디스패처로 넘겨야 한다(`AppHost`가 처리).
- WGC 프레임은 행 피치 패딩이 있다. 버퍼를 그대로 PNG로 인코딩하면 사선으로 어긋난다. 반드시 패딩을 제거해 촘촘한 BGRA32로 만든 뒤 알파를 255로 고정한다.
- `WgcCaptureSource`는 첫 프레임을 기다리다 시간 초과(기본 3초)하면 실패를 보고한다. 장치 손실은 복구하지 않고 다음 요청에서 같은 장치를 재사용한다.

## 흑백 필터

- Magnification 효과는 프로세스 수명에 묶여 있어, 흑백이 켜진 채 작업 관리자로 강제 종료해도 즉시 컬러로 돌아온다(실제로 확인함). 그래서 감시 프로세스나 시작 시 초기화 단계를 만들지 않는다.
- 절전 복귀·잠금 해제 뒤 효과가 사라질 수 있어 `GrayscaleFilter.Reapply()`가 시스템 이벤트에서 호출된다. 새 이벤트 경로를 추가할 때는 켜짐 상태일 때만 재적용하는지 확인한다.
- 윈도우 접근성 색 필터가 이미 켜져 있을 때의 상호작용은 건드리지 않는다. 그 기능을 대신 끄거나 켜지 않는다.
- 색상 변환 행렬은 `[R G B A 1] * M` 행 벡터 규약이다. 출력 R, G, B 계수는 행렬의 열 0, 1, 2에 들어간다(행이 아님). 행/열을 바꾸면 흑백이 아닌 이상한 색이 나온다.

## 단축키

- `RegisterHotKey`는 다른 프로그램이 이미 점유한 조합이면 실패한다. 이때 이전 단축키를 먼저 해제하지 않으면 변경 실패 후 아무 단축키도 안 남는다. `HotkeyApplier.Change`가 실패 시 이전 단축키를 유지하도록 만들어져 있다. 이 흐름을 단순화하지 않는다.
- 일반 입력 창에서 Alt 조합은 `Key.System`으로 들어오고 실제 키는 `SystemKey`에 있다. `HotkeyCapture`가 이를 처리한다.
- 메시지 전용 창은 UI(STA) 스레드에서 만들어야 `WM_HOTKEY`를 받는다. `HotkeyService`를 작업자 스레드에서 만들지 않는다.

## 자동 실행·단일 실행

- 자동 실행 값은 시작할 때마다 설정과 동기화된다(경로가 바뀌었으면 다시 쓰고, 설정이 꺼져 있으면 지움). 프로그램을 다른 폴더로 옮겨도 다음 실행 때 자동 실행이 새 경로를 가리킨다.
- 두 번째 실행은 첫 실행에 이름 있는 이벤트로 신호를 보내고 곧바로 종료 코드 0으로 끝난다. 첫 실행이 아직 시작 중이면 신호가 사라질 수 있다.
- 검증할 때는 `LUMINANCECAPTURE_NO_AUTOSTART=1`과 `LUMINANCECAPTURE_SETTINGS`로 실제 Run 키와 설정 파일을 건드리지 않는다.

## 빌드

- WPF를 켜면 암묵적 `using System.IO`가 빠진다. Core csproj가 `System.IO`를 전역 using으로 추가해 둔다.
- App은 WinForms(트레이 아이콘용)와 WPF를 함께 쓰므로 `Application` 이름이 모호해진다. App csproj는 암묵적 `System.Windows.Forms`/`System.Drawing` using을 제거해 둔다. 새 파일에서는 WinForms 형식을 완전한 이름으로 쓴다.
- `app.manifest`의 DPI 선언은 WinForms 분석기 경고(WFO0003)를 낸다. 의도된 선언이다.
- 아이콘 `app.ico`는 `tools/make-icon.ps1`로 다시 만든다. 설치 파일과 앱이 같은 파일을 쓴다.
