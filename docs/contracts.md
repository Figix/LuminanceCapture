# 사용자가 접하는 약속

이 프로그램에는 네트워크 API가 없다. 사용자와 다른 프로그램이 의존하는 인터페이스는 아래 다섯 가지다.

## 1. 전역 단축키

| 동작 | 기본 조합 | 입력 | 결과 | 오류 |
|---|---|---|---|---|
| 흑백 토글 | `Ctrl+Alt+F9` | 단축키 한 번 | 모든 모니터 흑백 켜짐↔꺼짐 | 적용 실패 시 "흑백 전환에 실패했습니다." 알림, 상태 불변 |
| 캡처 | `Ctrl+Alt+F10` | 단축키 한 번 | 포인터가 있는 모니터의 컬러+흑백 PNG 한 쌍 저장, 저장 알림 | 저장 실패 시 오류 알림, 반쪽 파일 없음 |

조합 표기는 `Ctrl+Alt+F9`처럼 `+`로 잇는다(`Ctrl`, `Alt`, `Shift`, `Win` + 키 하나, 수정키 최소 하나). 설정 창에서 바꾸며 등록 가능 여부는 즉시 알려 준다. 다른 프로그램이 이미 쓰는 조합이면 어떤 단축키가 실패했는지 보여 주고 이전 설정을 유지한다.

## 2. 실행 인자와 단일 실행

- 인자 없음: 설정 창을 보여 준다. `--background`: 창 없이 트레이로만 시작(자동 실행이 이 인자를 쓴다).
- 이미 실행 중이면 새 프로세스는 기존 창을 앞으로 가져오게 신호하고 종료 코드 0으로 끝난다.

## 3. 저장 파일

- 위치: 설정의 저장 폴더(기본 `사진\LuminanceCapture`). 형식: PNG.
- 이름: `yyyy-MM-dd_HHmmss_color.png`, `yyyy-MM-dd_HHmmss_gray.png`. 한 쌍은 같은 시각 문자열을 공유하고, 충돌하면 시각 뒤에 `_2`, `_3`을 붙여 쌍 전체가 새 이름을 쓴다. 파일명 사전 정렬이 시간순이다.
- 내용: 대상 모니터 전체의 물리 해상도, 포인터와 프로그램 알림 없음. 컬러 파일은 항상 실제 컬러, 흑백 파일은 R=G=B(휘도 정의는 Rec.709 감마값 직접 적용).

## 4. 설정 파일

`%AppData%\LuminanceCapture\settings.json`, camelCase 키 다섯 개:

```
{ "toggleHotkey": "Ctrl+Alt+F9", "captureHotkey": "Ctrl+Alt+F10", "saveFolder": null, "autoStart": true, "captureSound": false }
```

`saveFolder`는 절대 경로 문자열 또는 `null`(기본 폴더). 없는 파일·깨진 JSON은 전부 기본값, 필드 단위로 잘못된 값은 그 필드만 기본값. 흑백 켜짐 상태는 이 파일에 없다.

## 5. 시스템에 남기는 흔적

- 자동 실행: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`의 `LuminanceCapture` 값, 내용은 `"<exe 경로>" --background`.
- 단일 실행용 이름 있는 뮤텍스/이벤트: `Local\LuminanceCapture.SingleInstance.Mutex` / `.Show`.
- 설치: 시작 메뉴 항목 `LuminanceCapture`, 사용자 단위 설치 폴더. 제거해도 캡처 이미지와 설정 파일은 남는다.
