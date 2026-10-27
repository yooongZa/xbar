# Windows 0.3.1 UX·UI 검증

검증일: 2026-10-27. SIYONG, Windows 11 Pro x64 빌드 26300, 로그인 세션 1, 실제 화면 96 DPI, .NET SDK 10.0.107. 0.3.1은 설정·보드 화면 개선이며 계정·수집·순위·업데이트 설치 규칙을 유지한다. 개발용 시험 설치와 공개 배포는 구분한다.

## 개선한 문제

| 이전 문제 | 개선 |
| --- | --- |
| 일반 설정은 저장, 자동 업데이트 확인·자동 시작은 즉시 적용 | 모든 선택 값을 저장 시 적용. 닫기는 취소. Windows가 자동 시작 변경을 거부하면 다른 선택 값을 적용하지 않고 창과 안내를 유지 |
| 저장 대기 중 닫기·중복 클릭 | 저장 중 조작과 창 닫기를 막고 한 번만 적용. 최신 계정·기간 상태에 설정 필드만 합침 |
| 설정 아래쪽으로 긴 스크롤 | 위젯 / 계정·데이터 / 앱 업데이트 / 시스템의 네 탭. 저장·닫기 고정. 최소 크기에서는 내용만 스크롤 |
| 연결된 계정에도 연결 버튼 | 연결 상태와 계정 이름을 표시하고 ‘연결 관리’ 버튼 제공 |
| 업데이트 설명 반복 | 설치 버전·확인 상태와 자동 확인 설명을 분리하고 하루 한 번 안내는 한 곳에 표시 |
| Windows 보드의 혼잡한 목록 | 큰 요약 수치, 계정·확인 시각, 같은 폭의 기간 선택, 별도 순위 열과 오른쪽 정렬 수치. 큰 크기에서 5개 게시물·보조 지표 표시 |
| 상태 안내와 RT 알림이 함께 나올 때 공간 부족 위험 | 한 줄로 합쳐 하단 동작 공간 확보. 선택한 시간대의 확인 시각 표시 |
| 밝은 테마 시험에서 기본 컨트롤 색상 불일치 | Fluent 기본 컨트롤과 앱 색상을 함께 전환해 시험하고 실제 문자 대비 검사 추가 |

Windows 보드의 외곽 프레임·기본 색상·버튼 형태는 Windows 호스트가 그린다. 앱은 동일한 정보 순서와 강조 체계를 적용한다. 실제 캡처의 게시물·수치는 해당 시점의 X 계정 데이터다.

## 검증

- Core **76개**, Windows 회귀 **59개**, 업데이트 **22개**, 설정 **20개**: **177개 모두 통과**, 빌드 경고·오류 0개. Python 배포 도구 시험 1개도 통과.
- 설정 시험: 저장 전 변경 보류, 닫기 취소, 저장 적용, 동시에 바뀐 기간 보존, Windows 자동 시작 거부, 저장 중 중복 클릭·닫기 방지. 거부·지연은 격리된 대역으로 재현.
- 한·영·일 × 밝음·어두움 × 네 설정 탭을 캡처. 최소 창 크기와 기본 컨트롤 글자 대비 검사. 테마는 시험 프로세스에서 전환하며 OS 테마를 변경하지 않음.
- 실제 설치본 UI에서 자동 업데이트 확인·자동 시작을 선택한 뒤 저장 전 파일 해시 불변, 닫기 취소, 다시 열었을 때 자동 시작 Off 유지 확인. 자동 확인은 저장으로 끈 뒤 다시 켜서 원래 파일 해시로 복구.
- 실제 보드 Small 302×148, Medium 302×306, Large 302×464에서 버튼이 카드 안에 표시됨. 보드의 7일 선택이 실제 수집과 바탕화면 위젯에 반영되고 게시물 5개를 확인한 뒤 Today/Medium으로 복구.
- 0.3.0.0 → 0.3.1.0 실제 MSIX 교체 후 로그인 유지, X 재수집 Ready. settings.json SHA-256 `70664079DF7310AA4953B99A40550944739745BF618D60C3A74CF7A0CBC0B605` 유지. 마지막 여백 보완 패키지는 0.3.1.1로 구분한다.

## 실행과 증거

```text
dotnet test XBar.Core.Tests/XBar.Core.Tests.csproj -c Release -p:RestoreLockedMode=true
dotnet build XBar.WindowsTests/XBar.WindowsTests.csproj -c Release -r win-x64 -p:RestoreLockedMode=true
dotnet XBar.WindowsTests.dll <result.json> --settings-only
dotnet XBar.WindowsTests.dll <result.json> --updates-only
dotnet XBar.WindowsTests.dll <result.json>
python3 -m unittest discover -s windows/scripts -p 'test_*.py'
dotnet publish XBar.App/XBar.App.csproj -c Release -r win-x64 --self-contained true
dotnet publish XBar.WidgetProvider/XBar.WidgetProvider.csproj -c Release -r win-x64 --self-contained true
package.ps1 -Version 0.3.1.1
signtool verify /pa XBar-0.3.1.1-x64.msix
```

로그인 세션의 임시 예약 작업으로 실행했다. 로컬 `outputs/windows/ux-polish-20261027/`에 실제 화면과 `tests/`의 TRX·결과 JSON, `evidence/`의 설치·UI 조작 결과를 보관한다. 실제 화면과 격리된 시험 데이터 캡처를 구분한다. 일반 빌드 스크립트와 GitHub CI에도 설정 검증을 추가했다.

## 검증 한계와 공개 배포

실제 OS 테마·고대비 전환, 물리적 DPI 변경, 모니터 분리, 물리 절전·장시간 연속 운용·ARM64는 이번에 확인하지 않았다. 기존 배율 시험은 LayoutTransform 모사, 네트워크·절전 복구는 앱 입력 모사다. 현재 Windows 빌드 26300 한 대의 결과이며 일반 출시 빌드 여러 PC를 대표하지 않는다.

이번 설치는 임시 개발 인증서의 MSIX로 외부 설치한 시험이다. 공개 신뢰 서명과 새 일반 PC 설치, 공개 GitHub 다운로드에서 시작하는 자동 업데이트는 별도 배포 조건으로 남는다. SignPath 승인 후 그 조건을 충족해야 공개 Windows 릴리스를 게시한다. macOS 소스·공개 릴리스·appcast는 변경하지 않는다.
