# 관찰 전용 청소 커서 수정·검증

2026-10-23. 수동 결과 `Docs/Step3-수동테스트_2610230838.md`의07-M06 후속. root가07c 판정·상위 문서·독립 검토·커밋을 담당하며 구현 담당은 stage/commit/push하지 않는다.

## 기준선·계약

- main/c7fc949(ahead8). 사용자 Recovery/메타와10/23 수동 결과만 untracked이며 보존한다. 사용자 보고 M01~05/07/08·E01~06 통과는 그대로 구분하고16개 상한·외부 체험 등 이월은 새로 통과 처리하지 않는다.
- MCP 실제 조회: 올바른 프로젝트/Unity6000.3.23f1/Edit/컴파일·임포트 없음/CleaningRoom Dirty=false/루트10. 착수 Console 표본6개에 기존 Package Manager 조회 취소 오류1개와 MCP Windows 서명 수집 경고5개가 있다. 전체 수량으로 확대하지 않으며 로그를 지우지 않는다.
- 관찰(Stored/Viewing)에서는 파란 도구 점·범위 링을 숨긴다. 표면 적중과 MMB/Wheel/F 관찰 조작은 유지한다. 새 실험 청소와 일반 모드로 돌아오면 커서를 복원한다. 씬/직렬화/저장 형식은 바꾸지 않는다.
- 검사 저장은 기존 개발 fixture의 새 `.utmp` 복사본만 사용한다. 사용자 저장과 Recovery를 열거나 수정하지 않으며 필요 없는 씬 저장은 하지 않는다. 시작 씬·머티리얼·설정·Recovery hash는 C06 종료 기준과 같다.

## 가설·계획

- H1: SetWork(false,observe:true)가 관찰 조작을 위해 pointer.enabled=true로 남긴다. 이후 UpdateSurfaceFromScreenPosition이 매번 toolCursor.ShowContact를 호출해 일시 Hide로는 해결되지 않는다. Store/View API와 실제 프레임 hover로 재현한다.
- 수정은 포인터의 커서 표시 허용 상태만 분리하고 관찰 상태 소유자가 이를 설정하는 최소 범위로 한다. 입력/청소 허용/데이터 원본은 기존 가드를 유지한다.
- 수정 전 관찰 hover 재현 → 같은 회귀(구·신 결과/앞뒤옆면/Stored/새 실험/일반 모드), 관찰 조작·데이터 불변 → Unity Console/Windows 검증·정식 빌드 → define 제외/설정·hash 보존을 확인한다.

## 재현·최소 수정

- `.utmp/observation-repro-1792712750854/probe-observation-repro.txt`에서 관찰 pointer enabled/청소 차단 상태인데 dot/range Renderer와 IsVisible이 모두 true임을 실제 프레임·hover 경로로 재현했다. `before-observation.png`의 파란 점/링을 직접 확인했다. H1 확인.
- 포인터에 비직렬화 기본 true 표시 허용 상태를 추가하고 false 지정 즉시 Hide, 이후 hover에서도 Show 대신 Hide를 유지한다. 관찰 상태 소유자는 SetWork의 observe 인자로 이 정책을 지정한다. MMB/Wheel/F·적중·청소 허용 정책은 바꾸지 않는다.
- SetWork(false,observe:false)에서 기본 표시 허용으로 돌아가므로 Leave/일반 단계 복귀·일반 종료 취소의 기존 pointer 재활성 경로를 유지한다. 일반 모드 종료 취소도 새 회귀에 포함했다. 새 실험은 기존 준비→작업 경로에서 true로 복원한다.

## 재개·미저장 상태 보존

- 재개 시 Edit/컴파일·임포트 없음이나 Dirty=true였다. 원본을 저장하지 않고 `.utmp/observation-dirty-preserved-20261023.unity`에 사본을 남겼다. 전체 YAML 차이는 기존 자동 배치 UI 7개의 RectTransform anchor/position/size28줄뿐이다. 참조를 경로/GUID로 정규화한 live/Preview 비교도263개 중 같은7개만 차이였다. 레이아웃 재계산만으로 값이 복원되지 않아 임의로 dirty 해제하지 않았다.
- 진단의 Regex 사용 MCP 명령1회는 컴파일 단계에서 거부돼 실행되지 않았다. 문자열 기반 참조 정규화 명령으로 비교했으며 Preview는 finally에서 닫았다. 제품 C# 컴파일 오류가 아니다.
- 사용자가 이번 사본 보존 후 원본 재로드를 직접 승인했다. 재로드 후 Dirty=false/루트10, 원본 디스크 diff0. Recovery를 열지 않았으며 사본은 유지한다.

## Editor 수정 후 회귀

- 최종 `.utmp/observation-editor-1792713319109/probe-observation-cursor.txt`: **48조건 PASS**. CleaningRoom 실제 Play 프레임과 compiled probe의 제품 hover/회전/zoom/reset 경로, 실제 파일 IO를 사용했다.
- 구양면/신옆면 결과 각각 앞·뒤·옆 hover에서 점·범위 링 숨김, pointer 활성/청소·도포·도구 변경 차단, 데이터 revision/snapshot/저장 파일 bytes 불변을 검사했다. Stored도 동일하며 새M1/M2·Leave→일반 단계·일반 종료 취소는 커서와 입력이 복원됐다.
- GameView focus에서 가상 InputSystem Mouse/Keyboard의 MMB 드래그·Wheel·F를 실제 프레임으로 소비해 회전/확대/복원과 숨김을 확인했다. OS 물리 입력 검사가 아니다. 가상 장치/current/filter는 검사 종료·파괴에서 복원한다.
- `before-observation.png`와 수정 후 `after-observation.png`를 직접 대조했다. 같은 관찰 화면 중앙의 파란 점/링만 사라졌다. 임시 카메라 RT 캡처이며 실제 native Overlay 전체 품질 인증으로 확대하지 않는다.
- 첫 수정 후 fixture `.utmp/observation-editor-1792713205967`은 종료 취소 버튼 탐색에서 FAIL이었다. Leave가 시작한 비동기 일반 진행 저장 중 RequestQuit는 버튼 없는 대기창을 내는 정상 동작이다. 제품을 바꾸지 않고 PersistenceBusy 완료를 기다린 뒤 검사하도록 probe만 수정했다. 실패 report도 보존한다.

## Windows 검증·정식 산출물

- 검증 빌드: `.utmp/observation-player-build-1792713371152/SqueakSqueak.exe`, Windows64/Mono2x/BuildOptions.None/시작 씬 CleaningRoom/추가 define `SQUEAK_SAVE_VALIDATION`. **Succeeded, 오류0·경고485, 129,736,846bytes, 33.5217240초**. MCP는 경고 때문에 부분 실행 오류로 반환했으나 같은 원본 응답의 BuildReport 요약은 성공이다. 기존 Sentis 패키지 경고군(Pad/ConvTranspose/ScatterND/GridSample/ScatterElements/SliceSet)이며 자체 셰이더 변경은 없다.
- 격리 실행: `.utmp/observation-player-1792713429181/probe-observation-cursor.txt`, `-batchmode -force-d3d11 -saveProbeDirectory "<위 새 fixture>" -saveProbePhase observation-cursor -logFile "<fixture>/player.log"`. **48조건 PASS·process exit0**. 이번 Player도 focus 조건을 충족해 가상 MMB/Wheel/F 실제 프레임3조건까지 통과했다. Editor와 동일한 커서/상태/불변/복원 검사를 실행했다. 제품 사용자 저장은 읽거나 쓰지 않았다.
- Player 로그의 기존 Unity 분석/설정 호스트 DNS Curl6은3건이다. 관리 예외·probe FAIL은 없으며 DNS 오류를 제품 입력/저장 결함으로 분류하지 않는다. Player의 `after-observation.png`도 직접 확인했다.
- 최신 정식 빌드: **`Builds/Windows/Step3-20261023-observation/SqueakSqueak.exe`**. Windows x64(PE8664)/Mono2x/None/CleaningRoom, 검증 define 없음. **Succeeded, 오류0·경고0, 129,638,889bytes, 16.8957150초**. 폴더 전체를 유지한다. 이전 사용자 검사 빌드는 덮어쓰지 않았다.
- 정식 exe SHA256: `82EFD5A8C297A5046F6BB72C240DAB8972E64003851935EBA0A46AD121CE0305`.
- 정식 Assembly-CSharp.dll SHA256: `2E6192B7AB78EB1C5C25BF00349691A05CAE35CDA7CCB29F61F0C218E0E9E26E`.
- 검증 DLL SHA256: `6426C2B24D02AD9706ED44D5B5AEB4C1EFB90C707D79EE9A89258D26B3F33492`.
- DLL 검사: SavePlayerProbe/RunObservationCursor/UseValidationFiles 및 UTF16 `-saveProbeDirectory`는 검증 DLL에만 있고 정식 DLL에는 없다. exe launcher hash가 기존과 같아도 변경된 DLL hash로 제품 코드 갱신을 구분한다. 실제 사용자 저장 보호를 위해 정식 exe는 자동 실행하지 않았다.

## 정리·판정

- 빌드 hook이 디스크에만 추가한 InputSystem preload1은 메모리0을 재확인하고 해당 블록만 기준선 `preloadedAssets: []`로 복원했다. 전역 SaveAssets·씬 원본 저장·Recovery 조작 없음.
- 최종 Edit/Play=false/컴파일·임포트·빌드 없음/CleaningRoom Dirty=false/루트10. controller 직렬화 ObjectReference22개/null0, 시작 씬만 enabled·SampleScene disabled. override 빈 값/background=false/preload0, 전역 define `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY` 유지. Console Error/Warning 필터 각각 현재 조회0이며 과거 경고/실패 증거는 이 기록과 격리 산출물에 보존한다.
- 씬 `9CC49039…80A1FB`, Coin_Surface `7F41BE54…111D32`, ProjectSettings `CD1C9897…A4B867`, Recovery3개 hash는 기존 기준과 동일하다. 설정·패키지 실질 diff0, git diff --check 통과, 새 probe .meta 존재. 검사 Player 종료, 임시 Resources 잔여 없음, Build/.utmp 산출물 ignore 확인. root 소유 문서 변경과 사용자 수동 원본은 보존한다.
- **개발 수정·검증: Ready with limitations.** 보고된 관찰 커서 결함과 인접 복원 경로는 Editor/Windows에서 확인했다. 48은 텍스트 레이아웃 등 반복 조건을 포함한 검사 수이며48개 별도 기능이 아니다. 합성 fixture/완료/API·가상 입력·RT 캡처를 물리 입력/실측 청소/전체 시각 품질로 확대하지 않는다.
- 최신 정식 수정본에 대한 사용자 직접 M06 재확인은 별도이며 원본10/23 사용자 결과의 보고 범위는 바꾸지 않는다.16개 상한 사용자 확인·외부 체험·실측 성능·상세 소리/광택 품질·외부PC 글꼴 등은 이월 상태다. root가 독립 검토·상위 문서·07c 및 Step3 종료 판정·커밋을 담당한다. 구현 담당은 stage/commit/push하지 않았다.
