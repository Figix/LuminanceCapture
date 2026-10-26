# LuminanceCapture

윈도우 11 전용 개인 도구. 아트 작업 중 화면 전체를 인간 지각 휘도 기준 흑백으로 바꿔 보는 토글과, 같은 장면을 컬러+흑백 한 쌍으로 저장하는 캡처를 어떤 프로그램 위에서든 전역 단축키로 제공한다. 트레이 상주, 사용자 한 명, 오프라인, 네트워크·계정 없음. C# + WPF(.NET 10), 설치 파일은 Inno Setup.

## 프로젝트 구조

```
LuminanceCapture/
├── CLAUDE.md / AGENTS.md          ← 진입점 (내용 동일)
├── docs/
│   ├── architecture.md           ← 구성 요소와 흐름
│   ├── business-rules.md         ← 흑백·캡처·저장 규칙
│   ├── security.md               ← 보호 대상과 허용/금지 동작
│   ├── standards.md              ← 어기면 깨지는 규칙
│   ├── engineering-notes.md      ← 함정과 비자명한 동작
│   ├── operations.md             ← 빌드·테스트·배포 명령
│   ├── contracts.md              ← 사용자가 접하는 외부 약속(단축키, 실행 인자, 파일, 설정)
│   └── tracking/
│       ├── status.md             ← 현재 진행 상태
│       ├── decisions/            ← 트레이드오프가 있는 결정 기록
│       └── findings.md           ← 지금 풀 수 없는 문제
├── src/
│   ├── LuminanceCapture.Core/
│   │   └── AGENTS.md             ← 휘도·필터·캡처·단축키·설정 로직
│   └── LuminanceCapture.App/
│       └── AGENTS.md             ← WPF 창, 트레이, 알림, 서비스 조립
├── tests/
│   └── LuminanceCapture.Tests/
│       └── AGENTS.md             ← 단위 테스트 범위와 작성 규칙
├── installer/                    ← Inno Setup 스크립트
└── tools/                        ← 아이콘 생성, 게시·설치 파일 빌드 스크립트
```

## 하드 게이트

1. 컬러 원본은 흑백 필터 상태와 무관하게 항상 실제 컬러다. 컬러는 Windows.Graphics.Capture로만 얻고, GDI·화면 복제 API는 필터가 적용된 픽셀을 주므로 쓰지 않는다.
2. 흑백 파일은 항상 컬러 캡처 바이트에서 소프트웨어로 계산한다. 화면 변환 행렬과 파일 변환은 `Luminance`의 같은 계수(0.2126/0.7152/0.0722, 감마 인코딩 값에 직접 적용)에서 나온다.
3. 프로그램이 끝나면(강제 종료 포함) 화면은 컬러여야 한다. 시작 시 흑백은 항상 꺼짐이고 켜짐 상태는 저장하지 않는다.
4. 한 번의 캡처 = 컬러 1 + 흑백 1, 둘 다 저장되거나 둘 다 없다. 기존 파일은 덮어쓰지 않는다. 저장 이미지에 마우스 포인터와 프로그램 자신의 알림창이 들어가면 안 된다.
5. 관리자 권한 요구 금지, 네트워크 통신 금지.

## 작업 전 읽을 것

- 항상: `docs/standards.md`, `docs/engineering-notes.md`, 건드리는 모듈의 `AGENTS.md`.
- 휘도 계수·흑백 변환을 만지기 전: `docs/business-rules.md`의 휘도 정의와 `docs/tracking/decisions/0003-*.md`.
- 캡처 경로(`WgcCaptureSource`, `CapturePairWriter`)를 만지기 전: `docs/tracking/decisions/0001-*.md`와 engineering-notes의 캡처 함정.
- 단축키·자동 실행·단일 실행을 만지기 전: `docs/contracts.md`와 engineering-notes의 해당 항목.
- 설치 파일을 만지기 전: `docs/operations.md`의 설치 절차.

## 문제 발생 시

- 즉시 사용자에게 보고: 흑백이 프로그램 종료 뒤에도 화면에 남는 경우, 컬러 파일이 흑백으로 저장되는 경우, 캡처 한 쌍 중 한쪽만 남는 경우, 기존 캡처 파일이 덮어써진 경우.
- 그 외 풀지 못한 문제는 `docs/tracking/findings.md`에 기록한다.
