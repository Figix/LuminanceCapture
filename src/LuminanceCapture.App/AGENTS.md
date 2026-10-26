# LuminanceCapture.App

## 담당 범위
실행 파일 `LuminanceCapture.exe`: 시작 흐름(`App.xaml.cs`: 단일 실행 확인, `--background` 처리, 최상위 예외 처리), 서비스 조립(`AppHost`), 설정 창(`MainWindow`), 트레이(`TrayController`, `IconArt`), 저장 알림(`ToastWindow`), 매니페스트(PerMonitorV2), 앱 아이콘.

## 이 모듈의 범위가 아닌 것
휘도·캡처·저장·단축키 해석·설정 파싱 로직(Core의 몫이며 여기서 다시 구현하지 않는다). 설치 파일(`installer/`)과 빌드 스크립트(`tools/`).

## 불변식
- 서비스 조립은 `AppHost` 한 곳이다. 흑백 필터는 시작 시 꺼짐이고 켜짐 상태를 저장하지 않는다.
- 정상 종료 경로(`Exit`, 프로세스 종료·처리되지 않은 예외 훅)는 모두 `GrayscaleFilter`를 해제해 화면을 컬러로 되돌린다. 예외가 나도 프로세스는 로그를 남기고 계속 동작한다.
- 창 닫기(X)는 숨기기이며, 프로그램 종료는 트레이 "종료"뿐이다. `--background`로 시작하면 창을 만들되 보이지 않는다(단축키 등록 실패가 있으면 창을 연다).
- 토스트는 화면 캡처 제외 속성이 적용된 비활성(포커스를 뺏지 않는) 최상위 창이고 최신 알림이 이전 것을 대체한다. 설정 창은 캡처에서 제외하지 않는다.
- 단축키 변경이 실패하면 설정은 저장하지 않고 이전 단축키를 유지하며 사유를 창에 표시한다.
- 캡처 완료 이벤트는 작업자 스레드에서 오므로 UI 스레드로 넘긴 뒤 토스트·소리를 처리한다.
- 자동 실행 값은 시작할 때 설정과 동기화된다(`LUMINANCECAPTURE_NO_AUTOSTART=1`이면 건드리지 않음).

## 구현 패턴
- WPF 창에 트레이는 WinForms `NotifyIcon`을 쓴다. 두 UI 스택이 섞이므로 csproj가 `System.Windows.Forms`/`System.Drawing` 암묵적 using을 제거해 두었다. WinForms 형식은 완전한 이름으로 쓴다.
- 트레이 아이콘 그림은 코드로 그린다(`IconArt`). 앱 아이콘 `app.ico`는 `tools/make-icon.ps1`로 만든다.
- UI 문구는 한국어다.

## 테스트 지침
화면 모양·트레이·토스트는 자동 테스트로 대신하지 못한다. 변경 후 `--background`로 실제 실행해 흑백 단축키 전환, 캡처 쌍 생성, 두 번째 실행의 창 복귀, 창 닫기 후 단축키 유지, 강제 종료 후 컬러 복귀를 확인하고, 모양 변경은 사람이 눈으로 확인한다. 검증 중에는 `LUMINANCECAPTURE_SETTINGS`와 `LUMINANCECAPTURE_NO_AUTOSTART=1`을 설정한다.
