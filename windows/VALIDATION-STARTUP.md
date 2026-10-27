# Windows 로그인 시 자동 실행 실기기 검증

2026-10-27 KST, SIYONG Windows 11 Pro 빌드 26300, 설치본 `LIMOD.XBar.Dev 0.3.1.1`에서 검증했다. 코드 수정 없이 설치된 앱의 `설정 → 시스템 → Windows 로그인 시 시작`을 체크하고 저장했다. 시험 후에도 **자동 실행 On**을 유지한다.

## 실제 로그아웃·재로그인 결과

| 확인 항목 | 결과 |
| --- | --- |
| 자동 시작 활성화 | 설정 UI On, Windows StartupTask State 2, 재로그인 후 UserEnabledStartupOnce 1 |
| 실제 로그아웃 | 16:20:07.372, Windows 세션 이벤트 23. `shutdown.exe /l`로 수행했으며 강제 종료 옵션은 사용하지 않음 |
| 실제 Windows 로그인 | 16:22:59.295, 이벤트 21. 사용자가 같은 계정으로 직접 인증 |
| 새 세션 | Session 1 → 2. Explorer PID 11140 → 7996 |
| 앱 자동 실행 | 16:23:35.075, X-Bar PID 13088 → 16064. Windows AppModel 실행 이벤트 201과 프로세스 시작 시각 일치 |
| 소요 시간 | Windows 로그인 → 앱 프로세스 시작 **35.780초**, 새 오늘 노출 읽기 **46.758초**. 이 기기에서 관찰한 값이며 다른 PC의 실행 시간을 보장하지 않음 |
| 자동 표시 | 바탕화면 위젯 350×440 표시. 설정 창과 X 로그인 창 자동 표시 없음 |
| X 로그인 유지 | 로그인 창을 열거나 인증을 다시 입력하지 않고 새 오늘 노출과 게시물 3개 수집, Ready |
| 보드 연동 | 앱 자동 실행·새 수집을 확인한 뒤 Win+W로 보드를 열어 302×306 카드와 같은 값·오늘 게시물 3개 확인 |
| 설정 보존 | settings.json과 updates.json SHA-256이 로그아웃 전후 동일. Both/Today와 기존 언어·갱신 설정 유지 |
| 종료 상태 | 자동 시작 On 유지, 실제 앱 프로세스 1개. 시험용 예약 작업 제거 |

관찰용 로그인 트리거는 X-Bar를 실행하지 않는다. 프로세스·창·저장된 결과를 읽고 새 프로세스 시작 이후의 수집 시각을 요구해, 이전 세션의 Ready 캐시를 성공으로 오인하지 않도록 했다. 앱의 자동 시작과 수집 성공을 먼저 확인한 뒤 보드를 열었다.

## 증거와 실행

로컬 `outputs/windows/startup-validation-20261027/`에 다음 증거를 보관한다. 계정 데이터가 포함된 실제 화면과 snapshot은 공개 저장소에 포함하지 않는다.

- `before.json`, `enabled.json`: 활성화 전 상태와 저장 결과.
- `signout-request.json`, `session-events-final.json`: 실제 로그아웃·로그인 시각과 Windows 이벤트.
- `timeline.json`, `after-logon.json`: 수동 실행 없는 관찰, 새 프로세스·새 수집, 위젯 표시 결과.
- `startup-events.json`: Windows AppModel의 실행 이벤트.
- `desktop-after-logon.png`, `board-after-logon.png`, `startup-enabled.png`: 실기기 화면.
- `final-setting.json`, `cleanup.json`: 자동 실행 On 재확인과 시험 작업 제거.

주요 실행: 앱 설정 UI에서 자동 시작 활성화, 로그인 세션의 `shutdown.exe /l`, `Get-WinEvent`와 `Get-Process`로 세션·프로세스 검사, 임시 `New-ScheduledTaskTrigger -AtLogOn`으로 읽기 전용 관찰, UI Automation으로 창·설정 검사 및 실제 화면 캡처, `Unregister-ScheduledTask`로 시험 작업 정리.

## 한계

이번 검증은 기존 PC의 실제 로그아웃·재로그인 1회다. 전원 종료 후 부팅, 물리 절전, 장기 실행, 다른 일반 Windows 출시 빌드, 공개 서명 패키지와 새 PC 설치는 별도로 검증한다. Windows 자동 로그인을 설정하거나 암호·PIN을 저장하지 않았고, 인증서 신뢰·Developer Mode도 변경하지 않았다.
