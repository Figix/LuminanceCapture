# LuminanceCapture.Core

## 담당 범위
화면과 파일에 관한 모든 비-UI 로직: 휘도 정의와 변환(`Luminance`), 전체 화면 흑백(`GrayscaleFilter`), 화면 캡처와 쌍 저장(`WgcCaptureSource`, `CaptureService`, `CapturePairWriter`, `PairNameAllocator`, `MonitorLocator`), 전역 단축키(`HotkeyService`, `HotkeyParser`, `HotkeyApplier`, `HotkeyCapture`), 설정(`AppSettings`, `SettingsStore`), 자동 실행(`AutoStartManager`), 단일 실행(`SingleInstance`), 오류 로그(`ErrorLog`), 창 캡처 제외(`WindowCaptureExclusion`).

## 이 모듈의 범위가 아닌 것
창, 트레이, 토스트, 한국어 UI 문구 조립과 서비스 조립(App 모듈의 몫). 이 모듈은 App을 참조하지 않고, WPF 창 타입을 만들지 않는다(`HwndSource` 메시지 전용 창은 예외). 설치 파일도 범위 밖이다.

## 불변식
- 휘도 계수 `0.2126/0.7152/0.0722`는 `Luminance`에만 있고, 행렬(`CreateMagColorMatrix`)과 버퍼 변환(`ToGrayscale`)이 같은 상수를 쓴다. 입력이 R=G=B이면 출력은 입력과 같다.
- 행렬은 `[R G B A 1] * M` 규약: R/G/B 계수는 열 0·1·2, `M[3,3]`와 `M[4,4]`는 1.
- `GrayscaleFilter`는 생성 시 효과를 적용하지 않는다(꺼짐). 실패한 적용은 상태와 `Changed`를 바꾸지 않는다. `Dispose`는 단위 행렬을 적용하고 API를 해제한다.
- `CaptureService.Request()`는 모니터·시각·폴더를 호출 순간 확정하고 요청 순서대로 처리하며 하나도 버리지 않는다. 완료 이벤트는 작업자 스레드에서 발생한다.
- `CapturePairWriter`는 쌍을 임시 파일 → 덮어쓰기 없는 이름 변경으로 들여놓고, 실패하면 이 호출이 만든 파일을 전부 지운다. 이미 있던 파일은 건드리지 않는다.
- 컬러 프레임은 알파 255의 촘촘한 BGRA32이며 흑백은 그 바이트에서 계산한다.
- `SettingsStore.Load`는 예외를 던지지 않고 기본값으로 수렴한다. 흑백 켜짐 상태는 설정에 없다.
- `HotkeyParser`는 수정키 없는 조합, 키 2개 이상, 알 수 없는 키를 거부한다. `ValidatePair`는 같은 조합을 거부한다.

## 구현 패턴
- 시스템 호출은 인터페이스(`IColorEffectApi`, `ICaptureSource`)나 주입 가능한 경로(레지스트리 키, 설정 경로, 존재 확인 함수)를 통해 쓰고 테스트가 가짜를 꽂는다.
- 대상은 `net10.0-windows10.0.22621.0`. WPF를 켠 프로젝트라 `System.IO`는 csproj의 전역 using으로 들어 있다.
- WinRT/COM 객체(캡처 세션, 프레임, D3D 텍스처)는 사용 후 반드시 해제한다.

## 테스트 지침
계수·반올림 기대값(빨강 54, 초록 182, 파랑 18), 행렬과 버퍼 변환의 일치, 파일명 충돌 규칙, 설정 부분 손상 복구, 쌍 저장의 원자성(두 번째 쓰기 실패 시 흔적 없음), 20회 연속 요청의 순서·무손실, 단축키 표기 해석 경계를 반드시 다룬다. 실제 화면에 효과를 거는 코드를 테스트에서 실행하지 않는다.
