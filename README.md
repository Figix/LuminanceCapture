# LuminanceCapture

아트 작업 중 **화면 전체를 흑백으로 바꿔 보고**, 그 장면을 **컬러+흑백 한 쌍으로 저장**하는 윈도우 11용 트레이 도구입니다.
작업 프로그램 안의 흑백 기능을 찾아 켜고 끌 필요 없이, 어떤 프로그램 위에서든 단축키 하나로 동작합니다.

## 주요 기능

- **화면 전체 흑백 토글** — 모든 모니터에 적용, 인간 지각 휘도(Rec.709) 기준
- **컬러+흑백 한 쌍 캡처** — 흑백을 켠 채로 찍어도 컬러 원본은 실제 컬러로 저장
- 마우스 포인터가 있는 모니터를 포인터 없이 캡처, 누를 때마다 한 폴더에 시간순으로 쌓임
- 트레이 상주, 윈도우 시작 시 자동 실행(설정에서 끌 수 있음)

기본 단축키: 흑백 토글 `Ctrl+Alt+F9`, 캡처 `Ctrl+Alt+F10` (설정 창에서 변경)

## 설치

[Releases](https://github.com/Figix/LuminanceCapture/releases)에서 `LuminanceCapture-Setup-*.exe`를 받아 실행하세요.
관리자 권한이 필요 없고, 설치 후 윈도우 키로 `LuminanceCapture`를 검색하면 실행됩니다.

## 소개 페이지

https://figix.github.io/LuminanceCapture/ (`site/index.html`, 기획 의도와 기능 소개)

## 빌드

```
dotnet build LuminanceCapture.slnx
dotnet test LuminanceCapture.slnx
powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1 -Version 1.0.0
```

.NET SDK 10이 필요하며, 설치 파일을 만들 때 Inno Setup 6이 없으면 스크립트가 자동으로 설치합니다.
