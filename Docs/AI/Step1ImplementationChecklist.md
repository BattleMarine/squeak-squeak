# Step 1 구현 체크리스트 (에이전트용)

> 이 문서는 Step 1 구현을 진행하는 에이전트의 작업 순서·재개 지점·검증 기준을 위한 내부 메모다.
> 프로젝트 소개, 사용자 안내, 요구사항의 정본은 수정하지 않는다. 정본은 `Docs/Step1-구현지시서.md`다.

## 공통 작업 규칙

각 세부 단계에서 다음 순서를 지킨다.

1. Unity MCP의 실제 노출 도구와 입력 스키마를 먼저 확인한다.
2. 읽기 도구로 프로젝트·에디터·활성 씬·Play Mode·Console·대상 오브젝트 상태를 조회한다.
3. 필요한 범위만 편집하고 저장한다.
4. 대상 계층·컴포넌트·참조를 재조회한다.
5. Console과 Play Mode 동작을 확인하고, 통과/실패/미실행을 기록한다.

도구가 연결되지 않았다면 파일 근거로 진행 가능한 범위와 에디터에서 남은 검증을 명확히 구분한다. 사용자 로컬 변경을 보존하고, 씬·프리팹·설정은 충분히 조회한 뒤에만 수정한다.

## 세부 작업 순서

| ID | 작업 | 완료 기준 |
| --- | --- | --- |
| P0 | MCP 연결, 프로젝트 경로, 활성 씬, Play Mode, Console, Build Settings 확인 | `CleaningRoom`과 기존 로컬 변경을 안전하게 확인 |
| S1-01a | `CleaningRoom`의 계층, Coin Collider, Camera, 머티리얼 참조 상세 점검 | 구현 대상과 Inspector 참조를 확정 |
| S1-01b | 청소 전용 Input Action Map 구성 | 기존 Player/UI 맵과 GUID를 보존하고 마우스·휠·F 바인딩 확인 |
| S1-01c | 표면 Raycast와 도구 커서 구현 | 회전 후에도 동전 표면 적중과 커서 표시가 정상 |
| S1-01d | MMB 동전 회전, 휠 줌, F 복원 구현 | UI 입력 분리, F 후 청소 진행 보존 |
| S1-02a | 도구 데이터와 선택 상태 구현 | 세 도구의 A/B/광택 수치 차이를 Inspector에서 확인 |
| S1-02b | Canvas, EventSystem, 도구 버튼 연결 | 버튼 클릭이 청소·회전 입력으로 전달되지 않음 |
| S1-03a | A 타입 오염의 고정 배치와 초기화 구현 | Coin 하위에 부착되어 회전 시 함께 이동 |
| S1-03b | A 타입 접촉 제거와 제거율 계산 구현 | 보이는 접촉 오염만 시간 기반으로 제거 |
| S1-04a | B 타입 오염 수치와 색상 변화 구현 | 유효 접촉에서만 감소하고 값 범위가 정상 |
| S1-04b | 광택 수치와 개별 머티리얼 표현 구현 | 공유 머티리얼 오염 없이 광택·오염 표현이 공존 |
| S1-05a | HUD 진행률과 타이머 구현 | A/B/광택 수치와 화면 표시가 일치 |
| S1-05b | 완료 조건과 결과 화면 구현 | 세 조건 동시 충족 시 한 번만 완료 |
| S1-05c | 재시작 상태 복원 구현 | 오염, UI, 타이머, 입력, 자세, 줌을 새 게임 상태로 복구 |
| S1-06a | Edit/Play Mode 통합 검증 | 완료→재시작→재완료, 회전·줌·UI 예외를 점검 |
| S1-06b | Windows 빌드 및 결과 문서화 | 실제 입력으로 한 판을 검증하고 결과를 기록 |

## 진행 기록

- **P0 — 통과 (2026-09-16, KST):** Unity MCP 읽기 조회에 성공했다. Unity `6000.3.23f1`, 활성 씬 `Assets/Scenes/CleaningRoom.unity`, Edit Mode, 컴파일·업데이트 중 아님을 확인했다. Build Settings에는 `CleaningRoom`만 활성화되어 있고 `SampleScene`은 비활성이다. `Main Camera`, `Directional Light`, `Workbench`, `Coin/CoinBody`가 존재하며 `CoinBody`에는 활성 `MeshCollider`와 `MeshRenderer`가 연결돼 있다. 카메라는 Orthographic Size 2.4, Coin 초기 위치는 `(0, 0.13, 0)`이다.
- **Console 기준선:** 오류 0건, Unity AI Assistant 구독 관련 Info 5건 및 Codex 실행 파일 서명 수집 관련 Warning 2건이 기존 로그로 존재한다. P0의 읽기 전용 명령은 컴파일·실행에 성공했고 새 오류를 만들지 않았다.
- **보존:** 사용자 작업 트리 변경은 유지한다. 이 단계에서는 Unity 씬·에셋·프로젝트 설정을 수정하지 않았다.
- **S1-01a — 통과 (2026-09-16, KST):** `CleaningRoom`은 저장된 상태다. `Coin`은 루트 오브젝트이며 위치 `(0, 0.13, 0)`, `Coin/CoinBody`는 로컬 스케일 `(2, 0.1, 2)`의 Cylinder다. `CoinBody`에는 활성·Convex·비트리거 `MeshCollider`와 `MeshRenderer`가 있고, `Assets/Materials/Prototype/Coin_Prototype.mat`의 URP Lit 머티리얼을 참조한다. 현재 Base Color는 `(0.72, 0.46, 0.16, 1)`, Smoothness 0.55, Metallic 0.65다. Camera는 Orthographic, Size 2.4, 위치 `(0, 6, 0)`, 회전 `(90, 0, 0)`이다. `Workbench`의 비트리거 `BoxCollider`는 Coin보다 아래에 있다.
- **S1-01a 준비 결과:** Canvas/EventSystem은 아직 없고, 입력 에셋에는 `Player`·`UI` 맵만 있다. 청소 입력·런타임 스크립트·UI는 다음 구현 단계에서 추가해야 한다. Unity MCP 읽기 명령은 컴파일·실행에 성공했고 Console에 새 오류가 없다.
- **S1-01b — 통과 (2026-09-16, KST):** `Assets/InputSystem_Actions.inputactions`에 `Cleaning` Action Map을 추가하고 Unity에서 재임포트했다. 기존 `Player`·`UI` 맵과 GUID는 보존됐다. 새 맵은 `PointerPosition`(`<Mouse>/position`), `PointerDelta`(`<Mouse>/delta`), `UseTool`(`<Mouse>/leftButton`), `RotateCoin`(`<Mouse>/middleButton`), `Zoom`(`<Mouse>/scroll`), `ResetView`(`<Keyboard>/f`)의 여섯 액션으로 구성되며 모두 `Keyboard&Mouse` 제어 스킴에 속한다.
- **S1-01b 활성 상태:** `Cleaning` 맵은 현재 비활성이다. 다음 런타임 제어 컴포넌트가 `OnEnable`/`OnDisable`에서 명시적으로 활성화·해제해야 하며, UI 맵과 겹치는 좌클릭·중클릭·휠 입력은 이후 EventSystem 포인터 검사로 분리한다.
- **S1-01b 검증:** Unity MCP에서 파일 재임포트 후 맵 3개·`Cleaning` 액션 6개·각 바인딩을 재조회했고 입력 에셋은 Dirty가 아니다. 최종 검증 명령은 컴파일·실행에 성공했으며 Console에 새 오류가 없다.
- **S1-01c — 통과 (2026-09-16, KST):** `Assets/Scripts/Cleaning/CoinSurfacePointer.cs`와 `SurfaceToolCursor.cs`를 추가했다. `CoinSurfacePointer`는 `Cleaning/PointerPosition`을 읽어 `RaycastNonAlloc`으로 지정된 `CoinBody` Collider만 선택하고, 가장 가까운 적중의 위치·법선을 `SurfaceToolCursor`에 전달한다. 커서는 표면 법선 방향으로 배치되고 미적중이면 숨겨진다.
- **S1-01c 씬 연결:** `CleaningRoom`에 `CleaningController`와 `ToolCursor`를 추가했다. `CleaningController`는 Input Actions·Main Camera·CoinBody MeshCollider·ToolCursor 참조를 Inspector 직렬화로 연결한다. `ToolCursor`는 Collider 없이 URP Unlit `Assets/Materials/Prototype/ToolCursor_Prototype.mat`을 사용하며, 기본 렌더러 상태는 비활성이다.
- **S1-01c 검증:** Unity 컴파일·재임포트, Edit Mode의 참조·저장 상태, Play Mode를 확인했다. Play Mode에서 Cleaning 맵 활성화 후 Coin 중심 좌표로 표면 처리 경로를 실행하면 `HasSurfaceHit`, 커서 상태, Renderer가 모두 활성화됐고, 화면 밖 좌표에서는 셋 모두 비활성화됐다. 이후 Edit Mode로 복귀했고 씬은 저장된 상태이며 Console에 새 오류는 없다. 실제 사용자의 마우스 움직임을 통한 Game View 수동 검증은 S1-01d 이후 입력 통합 검증에서 수행한다.
- **S1-01d — 통과 (2026-09-16, KST):** `CoinSurfacePointer`에 `RotateCoin`·`PointerDelta`·`Zoom`·`ResetView` 액션 처리를 추가했다. 중클릭이 동전 표면에서 시작된 경우에만 회전 상태로 진입하며, 누르는 동안에는 포인터가 동전 밖으로 나가도 카메라 기준 축으로 동전을 회전한다. 회전 중에는 표면 적중 상태와 도구 커서를 해제해 이후 청소 처리와 충돌하지 않게 했다. EventSystem이 존재할 때 UI 위 포인터 입력은 회전 시작과 줌 처리를 막는다.
- **S1-01d 씬 연결·값:** `CleaningController/CoinSurfacePointer`의 `Coin Transform` 참조를 `Coin` 루트로 연결했다. 줌은 Orthographic Size 기준 최소 `1.6`, 최대 `3.2`, 감도 `0.0025`이며, 시작 시 Coin의 위치·회전과 카메라 Size를 저장한다. `F` 입력은 그 세 값과 회전 상태만 복원하므로 향후 청소 진행 상태에는 영향을 주지 않는다.
- **S1-01d 검증:** Play Mode에서 Coin 중심 표면 적중, 회전 적용, 최소·최대 줌 제한, 초기 위치·회전·줌 복원을 자동 검증해 모두 통과했다. 종료 후 Edit Mode에서 Coin `(0, 0.13, 0)`·회전 `(0, 0, 0)`·카메라 Size `2.4`·비표시 ToolCursor 상태를 재확인하고 `CleaningRoom`을 저장했다. Console 오류는 0건이다. 실제 Game View에서 물리 마우스 중클릭·휠·F를 연속 조작하는 최종 수동 검증은 S1-06a 통합 검증에 남긴다.
- **S1-02a — 통과 (2026-09-17, KST):** `CleaningToolDefinition` ScriptableObject와 `CleaningToolSelection`을 추가했다. 도구 데이터는 ID·표시 이름·브러시 반경·A 개별 오염 제거량/초·B 표면 오염 제거량/초·광택 증가량/초를 Inspector에 직렬화한다. 선택 상태는 `CleaningController`가 단일 소유하며, 허용 목록 밖 도구는 선택하지 않는다. 이후 UI와 재시작은 `TrySelect`·`ResetToDefaultTool` 및 `ToolSelected` 이벤트를 사용한다.
- **S1-02a 데이터·씬 연결:** `Assets/Data/CleaningTools/`에 마른 천 `(반경 0.32, A 0.35, B 0.12, 광택 0.03)`, 작은 솔 `(0.18, 1.00, 0.03, 0)`, 광택천 `(0.28, 0.15, 0.06, 0.18)`을 생성했다. `CleaningController/CleaningToolSelection`의 허용 목록에 세 에셋을 순서대로 연결하고 마른 천을 기본 도구로 설정했다.
- **S1-02a 검증:** Unity 컴파일·임포트 후 세 에셋의 Inspector 직렬화 값과 씬 참조를 재조회했다. Play Mode에서 마른 천 기본 선택, 작은 솔·광택천 선택, 기본값 복귀, 잘못된 ID 거부, 세 번의 선택 변경 이벤트와 상대 역할 수치를 자동 검증해 모두 통과했다. Edit Mode로 복귀한 뒤 씬 저장 상태·Console 오류 0건을 확인했다. Canvas/EventSystem과 실제 버튼 클릭 검증은 S1-02b에 남긴다.
- **S1-02b — 통과 (2026-09-17, KST):** `ToolSelectionCanvas`를 Screen Space Overlay로 추가하고 기준 해상도 `1280×720`에 맞춘 하단 패널에 마른 천·작은 솔·광택천 버튼을 배치했다. `CleaningToolButton`은 `CleaningToolSelection`의 선택 이벤트를 구독해 선택 도구를 강조하고, 비활성화 시 구독을 해제한다. 버튼 클릭은 선택 상태만 변경하며 별도 게임 입력을 직접 호출하지 않는다.
- **S1-02b 입력·씬 연결:** `EventSystem`에는 기존 `UI` Action Map을 사용하는 `InputSystemUIInputModule`을 연결했다. Navigate·Submit·Cancel·Point·Click·RightClick·MiddleClick·ScrollWheel 액션 참조는 `Assets/Data/UiInputActions/`에 영속 에셋으로 저장했다. Canvas에는 `GraphicRaycaster`가 있고, 각 버튼은 해당 도구 데이터·선택 상태·Image·Button 참조를 Inspector에 연결한다.
- **S1-02b 검증:** Unity 컴파일·임포트 후 EventSystem·UI 모듈·필수 UI 액션·Canvas·GraphicRaycaster·버튼 3개를 재조회했다. Play Mode에서 GraphicRaycaster가 작은 솔·광택천 버튼을 적중시키고 EventSystem `PointerClick` 경로가 선택 상태와 강조 색상을 갱신하는 것을 확인했다. UI·Cleaning Action Map은 함께 활성화됐고 클릭 후 `CoinSurfacePointer.IsRotating`은 거짓이었다. Edit Mode 복귀 후 씬 저장 상태, Missing Script 없음, Console 오류 0건을 확인했다. 실제 물리 마우스의 버튼 클릭과 이후 청소 효과의 완전한 상호 배제는 S1-06a 통합 검증에 남긴다.
- **S1-03 — 통과 (2026-09-21, KST):** `ObjectDirt`와 `ObjectDirtCleaner`를 추가했다. 각 A 오염은 내구도를 시간 기반으로 감소시키고, 남은 내구도에 따라 최소 25%까지 크기를 줄인 뒤 0에서 비활성화된다. `ObjectDirtCleaner`는 초기·제거 개수와 제거율을 소유하며, 다음 재시작 단계에서 사용할 `ResetAllDirt`를 제공한다.
- **S1-03 입력·가시성 보완:** `CoinSurfacePointer`가 `UseTool` 액션을 해석해 현재 포인터 좌표를 즉시 다시 Raycast하는 `TryGetToolUseSurfaceHit`을 제공한다. LMB가 눌리고 UI 밖의 유효 표면에 있을 때만 처리하며, 회전 중 또는 MMB가 함께 눌린 프레임은 차단한다. `ObjectDirtCleaner`는 현재 선택 도구의 A 제거력과 브러시 반경을 적용하고, 카메라에서 Coin 표면 바로 앞인지 별도 Raycast로 검사해 반대편 오염을 거리만으로 제거하지 않는다.
- **S1-03 씬 연결:** `Coin/FixedDirt` 아래에 Collider 없는 고정 A 오염 `ObjectDirt_01~05`를 배치했다. 모두 Coin의 로컬 자식이므로 동전 회전에 함께 움직이며, `Assets/Materials/Prototype/ObjectDirt_Prototype.mat`을 공유한다. `CleaningController/ObjectDirtCleaner`에는 `CoinSurfacePointer`, `CleaningToolSelection`, 오염 5개를 Inspector 참조로 연결했다.
- **S1-03 검증:** Unity 컴파일 후 씬 계층·참조·Missing Script를 재조회했다. Play Mode에서 마른 천의 부분 감소, 작은 솔의 한 오염 제거·제거율 `1/5`, 180도 회전한 반대편 오염 차단, 5개 오염의 복원을 자동 검증했다. 실제 Game View의 물리 LMB Hold·Drag 및 UI 위 클릭 차단은 S1-06a 통합 검증에 남긴다.
- **S1-04 — 통과 (2026-09-21, KST):** `CoinBody/CoinSurfaceContamination`을 추가했다. B 오염은 `0~1` 단일 수치로, 광택은 별도 `0~1` 수치로 관리한다. 유효 LMB 표면 접촉에서만 선택 도구의 B 제거력·광택 상승량을 시간 기반으로 적용하며, 광택천은 남은 B 오염이 적을수록 효율이 높다.
- **S1-04 시각 표현·씬 연결:** `CoinSurfaceContamination`은 기존 `Coin_Prototype` 공유 머티리얼을 변경하지 않고 `MaterialPropertyBlock`으로 `_BaseColor`·`_Metallic`·`_Smoothness`를 합성한다. B 오염이 줄면 더 깨끗한 동전색·금속성이 복원되고, 광택이 오르면 Smoothness가 추가 상승한다. Inspector에는 `CoinSurfacePointer`, `CleaningToolSelection`, `CoinBody Renderer`를 연결했다.
- **S1-04 검증:** Unity 컴파일, 씬 참조, Console 오류 0건을 확인했다. Play Mode에서 마른 천과 작은 솔의 B 제거량 차이, 표면 B 오염 완전 제거, 광택천의 Smoothness 증가, 공유 머티리얼 원본 값 보존, B 오염·광택 초기화 복원을 자동 검증했다. 실제 Game View LMB·UI 경계 입력과 시각적 육안 검증은 S1-06a 통합 검증에 남긴다.

### 2026-09-29 — S1-06 진행 중간 기록

- **S1-06a API 통합 검증:** Unity MCP로 프로젝트 경로·`CleaningRoom`·Edit Mode·저장 상태를 확인했다. Play Mode 공개 API로 완료→결과 버튼 `onClick` 재시작→재완료를 2회 반복해 각 판의 완료 1회 제한, A/B/광택·타이머·결과 UI 초기화를 확인했다. `EditorApplication.Step()` 단일 프레임에서 타이머가 약 0.02초 증가했다. 이는 연속 시간 흐름과 실제 입력 검증은 아니다.
- **경사면 가시성 보완:** Orthographic 화면 Ray가 `CoinBody`를 맞혀도 기존 카메라 위치 기준 Ray 때문에 X축 75°의 A 오염 5개가 모두 가려진 것으로 판정됐다. `CoinSurfacePointer`의 화면 좌표 기반 Ray 판정과 씬의 `visibleSurfaceTolerance` 0.12로 수정했다. Play Mode에서 0°·45°·75°는 5/5 가시, 135°·180°는 0/5 가시를 확인했다.
- **입력 검증 한계:** Computer Use의 비연속 포인터 이동·드래그 주입으로 얻은 LMB 눌림 및 B 감소 관찰은 실제 마우스 동작의 근거로 삼지 않는다. 이 과정에서 시도한 추정성 입력 가드는 되돌렸다. 물리 LMB Hold/Drag, UI 경계, MMB 동시 입력, 포커스 이탈·복귀, F, 완료 시간 고정, 1280×720 화면은 아직 미검증이다.
- **S1-06b 1차 빌드:** `CleaningRoom`만 포함한 Windows x64 `BuildPipeline.BuildPlayer` 보고서는 `Succeeded`, 오류 0, 경고 485, 128,670,657바이트였다. 경고는 `com.unity.ai.inference` 셰이더 관련이며 실행 파일 생성은 확인했다. Computer Use의 플레이어 실행 권한이 거부되어 빌드의 실제 마우스 한 판은 미검증이다. 도구 수치 조정 후 재빌드하고 최종 결과를 별도로 기록한다.
- **S1-06 도구 수치 조정:** Unity Editor의 직렬화 API로 기존 도구 에셋을 수정·저장했다. 반경은 유지하고 초당 `(A, B, 광택)`을 마른 천 `(0.04, 0.012, 0.003)`, 작은 솔 `(0.125, 0.003, 0)`, 광택천 `(0.015, 0.006, 0.012)`로 조정했다. 선택 도구를 순서대로 사용한다는 단순 계산상 완료 기준에 필요한 접촉 시간은 약 2.5~3분이며, 실제 이동·도구 전환 시간은 포함하지 않는다. 목표 3~5분 충족 여부는 실측 전까지 미판정이다.
- **조정 후 API 재검증:** 작은 솔로 A 5개를 각각 8초 상당 처리하고, 마른 천 57초·광택천을 완료까지 적용하는 합성 접촉 시나리오를 2회 실행했다. 두 판 모두 완료 이전 판정은 거짓, A/B 100%·광택 70.3%에서 152초 상당의 접촉 후 1회 완료, 중복 호출 거짓, 타이머 정지 및 재시작 초기화가 통과했다. `152초`는 API에 전달한 시간 합계이지 플레이 실측 시간이 아니다.
- **S1-06b 재빌드·구성 점검:** 조정 후 `Builds/Windows/Step1-20260929-tuned/SqueakSqueak.exe`를 새 경로로 빌드했다. BuildReport `Succeeded`, 오류 0·경고 485·128,670,657바이트이며 실행 파일을 확인했다. 빌드 후 Console 오류 0·AI Inference 셰이더 경고 25개(조회된 로그), 컴파일·임포트 대기 없음. `CleaningRoom`만 빌드 활성, 씬 35개 오브젝트의 Missing Script 0, 핵심 직렬화 참조 연결, 제품명 `Squeak Squeak`·기본 1280×720·크기 조절 불가 설정을 확인했다. Edit Mode에서 원본 씬 저장 상태다.
- **현 단계 판정:** S1-06a·b 모두 **부분 완료**. Windows 빌드의 실제 입력 한 판, LMB/UI/MMB/포커스 경계, F·줌 연속 조작, 결과 시간, 1280×720 실제 화면 및 3~5분 실측은 미실행이다. Computer Use 포인터 주입으로 이를 대체하지 않는다. 구체적 인계는 `Docs/Step1-구현결과.md`에 남긴다.

## 재개 전 확인

- 현재 Git 상태와 사용자 변경을 다시 조회한다.
- Unity MCP가 연결되어 있으면 저위험 읽기 호출로 연결부터 증명한다.
- `Docs/Step1-구현지시서.md`의 확정 요구·제외 범위·검증 체크리스트를 다시 확인한다.
- 한 번에 여러 단계를 완료 처리하지 말고, 각 단계의 씬 연결과 검증 근거를 남긴다.

## 스텝별 검토 결과

검토 후 이 문서 최하단에 날짜·대상 스텝·판정·후속 조치를 간략히 기록한다. 구현자의 진행 기록과 별도로 실제 검토 범위와 미실행 검증을 구분한다. 지적 사항은 수정 후 재검토 근거를 남겨 해소 처리한다. 각 검토 항목 앞에 `s1-02-f1` 형식의 고유 태그를 붙인다(스텝 번호 + 해당 스텝 내 검토 일련번호). 세부 단계 a/b는 본문에 명시하며, 새 항목은 다음 번호를 사용한다. 재검토·해소 후에도 기존 태그를 유지하고 재사용하지 않는다.

### 2026-09-17 — S1-02b까지 경량 검토

- **범위:** 체크리스트·관련 코드와 Unity Edit Mode 씬 참조를 대조했다. 도구 3개·기본 도구·버튼 참조 연결을 확인했고 Missing Script 및 조회된 Console 오류·경고는 0건이었다. 이번 검토에서 Play Mode·실제 마우스 조작·빌드는 실행하지 않았다.
- **[s1-01-f1] S1-01d — 보완 완료 (2026-09-17, KST):** `UpdateRotation()`이 중클릭 입력 프레임에 `PointerPosition`으로 표면을 다시 판정하도록 수정했다. 따라서 이전 프레임에 남은 `HasSurfaceHit`가 아니라 실제 중클릭 시작 좌표가 회전 진입을 결정한다. Play Mode에서 동전 중심·경계 밖 좌표와 이전 적중 상태를 조합해 회전 시작 여부를 검증했다. 실제 마우스로 동전 경계를 가로지르는 수동 조작은 S1-06a에 남긴다.
- **[s1-02-f1] S1-02a — 검토 범위 내 이상 없음:** 도구 데이터·선택 상태의 분리와 씬 연결을 확인했다. 선택 이벤트 등 기존 Play Mode 통과 기록은 이번에 재검증하지 않았다.
- **[s1-02-f2] S1-02b — 코드 보완 완료, 통합 검증 대기 (2026-09-17, KST):** `CoinSurfacePointer.CanUseTool`을 `HasSurfaceHit && !IsPointerOverUi`로 제공했다. S1-03 청소 처리는 이 상태만을 진입 조건으로 사용해 UI 위에서의 청소를 차단한다. 현재 단계에는 청소 처리가 아직 없으므로 실제 도구 효과의 상호 배제와 물리 마우스 UI 클릭·드래그 검증은 S1-06a에서 수행한다.

### 2026-09-17 — 조치 후 재검토

- **범위:** 조치 기록과 `CoinSurfacePointer.cs`의 실제 변경·호출 흐름을 정적으로 대조했다. 이번 재검토에서는 Unity 컴파일·Console·Play Mode·물리 입력·빌드를 재검증하지 않았다. 위 Play Mode 통과 내용은 구현자의 기록이다.
- **[s1-01-f1] — 코드상 해소 확인:** `UpdateRotation()` → `TryBeginRotation(현재 좌표)` → 표면 재판정 순서로 변경되어 이전 프레임 적중 상태에 의존하던 문제를 해소했다. 경계 이동 중 실제 중클릭 검증은 S1-06a에 유지한다.
- **[s1-02-f1] — 기존 판정 유지:** 이번 변경은 포인터 처리에 한정되며 도구 데이터·선택 상태에 대한 추가 지적은 없다.
- **[s1-02-f2] — 코드 보완 확인, 통합 검증 대기 유지:** UI 위에서 거짓이 되는 `CanUseTool`을 확인했다. 아직 이를 소비하는 청소 코드는 없다. 위 기록의 “이 상태만을 진입 조건”은 포인터 유효성 검사로 한정한다. S1-03에서는 LMB 사용 입력과 회전 차단도 함께 확인해야 하며, 청소가 포인터 갱신 후의 상태를 사용하도록 실행 순서를 정할 것. S1-06a에서 UI 경계 진입·이탈 프레임을 포함해 실제 입력 차단을 확인한 뒤 최종 해소한다.

### 2026-09-21 — S1-03 검토

- **범위·판정:** S1-03 코드・씬 참조・Play Mode의 제거/복원/가림 판정을 확인했다. **보완 필요(Failed)**: 아래 가림 오류로 “보이는 오염만 제거” 기준을 충족하지 못한다. S1-04는 이번 검토에서 제외했다. 검토 전후 Console 오류·경고 0건, 컴파일 실패 상태 없음. 종료 후 Edit Mode·씬 Dirty=false를 확인했다. 소스·씬 수정 없이 이 검토 기록만 추가한다.
- **[s1-03-f1] — 보완 필요 / 작업대 뒤 오염 제거 재현:** Coin 초기 높이 0.13에서 X축 45° 회전 시 `ObjectDirt_01` 위치는 약 `(-0.38, 0.03, 0.27)`이다. 해당 화면 좌표의 첫 Physics 적중은 Workbench인데 `IsWorldPointVisibleOnCoinSurface(..., 0.08)`은 true를 반환했다. Coin Collider 적중을 `ApplyCleaningAtSurfaceHit`에 전달하고 작은 솔 1초를 적용하자 내구도가 1→0으로 감소했다. 포인터의 `TryFindCoinSurfaceHit`와 새 가시성 검사 모두 Coin 이외 Collider를 무시하기 때문이다. 두 경로에 가림물 우선 적중 검사를 적용하고, 자유 회전 시 동전·오염이 작업대에 묻히지 않도록 높이/배치도 조정할 것. Orthographic 화면 Ray 기준을 일치시키고 45° 등 중간 각도·180°·F 복원으로 재검증한다. 재현은 Play Mode API 호출이며 물리 LMB 입력은 미실행.
- **[s1-03-f2] — 기본 제거·복원 통과:** 실제 씬의 오염 5개와 참조 연결을 확인했다. 초기 자세에서 `ObjectDirt_01`에 마른 천 1초 적용 시 내구도 1→0.65, 작은 솔 1초 추가 시 제거 개수 1/5·제거율 0.2였다. 180° 회전 시 해당 오염의 가시성은 false였고, `ResetAllDirt` 후 5개 활성·내구도 1·제거 개수 0을 확인했다. 검증 중 변경한 자세·도구·오염 상태는 복원하고 Play Mode를 종료했다.
- **[s1-02-f2] — 입력 코드 보완 확인, 통합 검증 대기 유지:** 청소 Update가 `TryGetToolUseSurfaceHit`를 호출하며 LMB·MMB·회전 상태·UI를 검사하고 현재 위치를 재판정한다. 이전 인계 사항은 코드에 반영됐다. 실제 LMB Hold/Drag, UI 경계 진입·이탈, MMB+LMB 동시 입력 및 포커스 복귀는 이번에 실행하지 않았으며 S1-06a에 유지한다. Windows 빌드도 이번 검토에서는 미실행.

### 2026-09-21 — S1-03 조치 및 재검증

- **[s1-03-f1] — 해소:** `CoinSurfacePointer`가 모든 Physics 적중 중 최단 적중을 먼저 선택하는 `TryFindClosestPhysicsHit`을 공통으로 사용하도록 변경했다. 따라서 화면 포인터의 Coin 표면 판정과 A 오염의 월드 위치 가시성 판정 모두 Workbench 등 가림 Collider를 Coin보다 우선한다. 동전 초기 높이는 `0.13`에서 `1.2`로 조정해 0°·45°·180° 회전에서 Coin·A 오염 Renderer/Collider가 작업대 상면보다 위에 있도록 했다. 탑뷰에서 높이 차가 큰 그림자로 보이지 않도록 CoinBody와 A 오염 Renderer의 그림자 투사는 끈다.
- **[s1-03-f1] 검증:** Play Mode에서 기존 재현 상태(높이 `0.13`, X축 `45°`)의 첫 적중이 Workbench이고, 가시성이 거짓이며, Coin 적중을 강제로 전달해도 작은 솔 1초 적용 시 A 오염 내구도가 변하지 않음을 확인했다. 저장 높이에서는 X축 `45°` 앞면 가시성, X축 `180°` 반대면 차단, `ResetView`(F 경로)의 초기 위치·자세 복원을 확인했다. 실제 물리 LMB Hold·Drag와 UI 경계 입력은 S1-06a 통합 검증에 남긴다.

### 2026-09-21 — S1-03 수정 후 독립 재검토

- **[s1-03-f1] — 기존 가림 오류 해소 확인:** 코드의 최단 적중 공통 처리와 저장 높이 1.2를 확인했다. Play Mode에서 기존 조건(높이 0.13·X축 45°)을 다시 만들자 첫 적중 Workbench·오염 가시성 false였고, Coin 적중을 강제로 전달한 작은 솔 1초 처리에도 내구도는 1→1로 유지됐다. 저장 높이의 X축 0°·45°·90°·180°에서 Collider와 오염 Renderer의 최저 Y가 작업대 상면 0보다 높았다. 45° 앞면은 가시성 true, 180° 반대편은 false였다. 원래 재현 오류는 해소 판정한다.
- **[s1-03-f2] — 회귀 검증 통과:** 초기 자세에서 마른 천 1초 후 내구도 0.65, 작은 솔 추가 적용 후 제거율 0.2, 복원 후 제거 개수 0·오염 5개 활성을 재확인했다.
- **검증 한계·상태:** 위 결과는 Play Mode 공개 API 호출 검증이다. F의 비공개 메서드 호출 시도는 Unity 도구의 Reflection 제한으로 실행되지 않았으며, 구현자의 F 검증 기록과 구분한다. 물리 LMB·MMB·F·UI 경계·Windows 빌드 및 그림자 변경의 시각적 평가는 미실행이다. 가시성 Ray는 아직 카메라 위치에서 오염으로 향하므로 Orthographic 화면 Ray와 동일하지 않다. 경사·가장자리에서의 화면 일치는 S1-06a에서 추가 확인한다. `s1-02-f2` 통합 검증 대기는 유지한다. 검토 전후 Console 오류·경고 0건, 컴파일 실패 없음, 종료 시 Edit Mode·씬 Dirty=false를 확인했다. 검증용 자세·도구·오염 상태는 복원했으며 코드·씬은 저장하거나 수정하지 않았다.

### 2026-09-22 — S1-04 작업·검토 요약

- **[s1-04-f1] — 해소:** 초기 B 오염량을 무시하던 제거율과 진행 중 분모 변경을 수정했다. `ResetSurface`에서 `roundInitialSurfaceDirt`를 저장하고 `Clamp01(1 - 현재 / 판 초기량)`으로 계산한다(초기량 0이면 1). Play Mode에서 초기→마른 천 1초→완전 제거→복원의 제거율은 초기값 1에서 `0→0.12→1→0`, 0.5에서 `0→0.24→1→0`, 0에서 모두 1로 통과했다. 진행 중 Inspector 초기값 0.5→1 변경에도 제거율 0.24를 유지하고, 다음 복원부터 B=1·제거율 0으로 적용됨을 독립 재검증했다.
- **[s1-04-f2] — 통과:** 씬 참조·도구별 B 제거·광택 효과·수치 제한·초기화·공유 머티리얼 보존을 Play Mode 공개 API로 확인했다. 초기 B=1에서 1초 후 마른 천 B=0.88, 작은 솔 B=0.97·광택=0. 광택천의 1초 광택 증가는 오염 상태 0.0108, 깨끗한 상태 0.18이었다. 색상 유지와 Smoothness 증가를 확인했으며 색상·광택 효율은 절대 오염량 기준을 유지한다.
- **[s1-04-f3] — 통합 검증 대기:** 실제 LMB Hold/Drag·UI 경계·MMB 동시 입력·포커스 복귀·F·시각적 확인은 S1-06a, Windows 빌드는 S1-06b에 남긴다. API 검증으로 실제 입력 검증을 대체하지 않는다.
- **검증 상태:** 최종 재검증 전후 Console 오류·경고 0건, 컴파일 실패 없음. 설정·도구 복원 후 Edit Mode·씬 Dirty=false 확인. 최초 검토에서 관찰한 기존 RenderTexture.active 경고 1건은 해결 여부 미판정.

### 2026-09-23 — S1-05a HUD 진행률·타이머

- `CleaningProgressTracker`가 A/B 제거율·광택·청결도를 기존 청소 상태에서 읽어 계산한다. 오염값의 별도 사본은 만들지 않으며, 첫 유효 표면 청소부터 `unscaledDeltaTime`을 누적하고 앱 포커스가 없을 때는 시간을 누적하지 않는다.
- `ToolSelectionCanvas/ProgressPanel`에 A 제거율·B 제거율·광택·경과 시간을 표시하는 HUD를 추가했다. 정수 퍼센트와 초 단위로만 갱신하며, HUD 그래픽은 Raycast 대상이 아니므로 청소 입력을 가로막지 않는다.
- **검증:** Unity 컴파일 성공, 저장 후 추적기·HUD의 모든 Inspector 참조 연결과 Missing Script 0건, Console 오류·경고 0건을 확인했다. Play Mode에서 A=20%, B=12%, 광택=0% 상태가 HUD에 같은 값으로 표시됨을 확인했다. 실제 LMB Hold/Drag로 타이머 시작·포커스 이탈 시간을 포함한 통합 입력 검증은 S1-06a에 남긴다.

### 2026-09-23 — S1-05b 완료 조건·결과 화면

- `CleaningCompletionController`가 Inspector 기본값 A 90%·B 80%·광택 70%를 동시에 비교해 완료 상태로 한 번만 전환한다. 완료 시 `CoinSurfacePointer`를 비활성화해 청소·회전·줌 입력을 멈추고, `CleaningProgressTracker` 타이머도 정지한다.
- `CleaningResultPanel`과 전체 화면 `ResultPanel` 오버레이를 추가했다. 완료 순간의 청결도·광택도·경과 시간을 결과 텍스트로 표시하며, 시작 시 결과 패널은 비활성이다.
- **검증:** Unity 컴파일 성공, 저장된 Inspector 참조와 결과 패널 초기 비활성을 확인했다. Play Mode에서 기본 기준값 `0.90/0.80/0.70`과 A·B·광택 100% 상태로 완료가 1회 발생하고, 두 번째 호출은 거짓·타이머 정지·포인터 비활성·결과 표시임을 확인했다. A=80%와 광택=0%에서는 결과가 열리지 않았고, 런타임 검증 인스턴스에서 다른 두 기준을 0으로 낮춘 B 단독 비교도 B=60%에서는 거짓, B=100%에서는 참이었다. 실제 마우스 입력과 완료 시간 흐름은 S1-06a에 남긴다.

### 2026-09-23 — S1-05c 재시작 상태 복원

- 결과 화면에 `새 청소 시작` 버튼을 추가했고, `CleaningCompletionController.RestartRound`만 호출하도록 연결했다. 완료 상태에서만 재시작되며, A 오염·B 오염·광택·진행 타이머·결과 UI·입력·기본 도구·자세·Zoom을 새 판 상태로 되돌린다.
- `CoinSurfacePointer.ResetViewToInitialState`를 공개해 F와 재시작이 같은 자세·Zoom 복원 경로를 사용한다. 이 메서드는 청소 진행과 도구·완료 상태를 변경하지 않는다.
- **검증:** Unity 컴파일 성공 후 모든 재시작 Inspector 참조를 확인했다. Play Mode에서 완료 후 결과 화면의 실제 `Button.onClick` 이벤트를 호출해 A 오염 5개 활성·내구도 1·제거 0/5, B=1·광택=0, 마른 천 선택, 완료/결과 해제, 포인터 활성, 타이머 0·정지 해제, 초기 자세·Zoom 복원을 확인했다. 이어 자세·Zoom만 변경한 뒤 F 경로 공개 메서드를 호출해 A 내구도·B 오염·광택이 보존됨을 확인했다. 실제 마우스 클릭과 완료→재시작→재완료 2회 반복은 S1-06a에 남긴다.

### 2026-09-23 — S1-05 검토 요약

- **범위·판정:** 코드·씬 연결·Play Mode API 검증에서 완료/재시작의 새 결함은 확인하지 못했다. 실제 입력·타이머·HUD 연속 갱신은 검증 미완료이므로 S1-05 전체 통과로 확대하지 않는다. 필수 참조와 Missing Script 0건, 검토 전후 Console 오류·경고 0건·컴파일 실패 없음 확인. 종료 시 Edit Mode·씬 Dirty=false이며 기존 코드·씬 변경은 보존했다.
- **[s1-05-f1] — 완료·결과 처리 통과(검증 범위 내):** A 미충족, B/광택 미충족, 광택만 미충족 상태에서 완료가 차단됐다. A/B/광택 100%에서 완료 1회·재호출 거짓·타이머 정지 플래그·포인터 비활성·결과 표시와 청결도/광택도 100%를 확인했다. 정확한 임계값 직전/동일/직후 및 0 오염 구성은 추가 검증 대상이다.
- **[s1-05-f2] — 재시작·F 경로 통과:** 완료→결과 Button.onClick→재완료를 2회 반복했다. 매 재시작 후 A 5개 활성·내구도 1, A/B 제거율·광택·시간 0, 시작/완료/타이머 정지 해제, 결과 숨김, 마른 천·포인터 활성·초기 자세/Zoom 복원을 확인했다. 진행 중 재시작은 거짓이었다. F 공개 경로 호출 시 B=0.88·A 내구도 0.7이 보존되고 자세/Zoom만 복원됐다. 물리 버튼/키 입력 검증은 아니다.
- **[s1-05-f3] — HUD·타이머 통합 검증 미완료:** 코드상 현재 상태 참조·첫 표면 사용 시 타이머 시작·포커스 이탈 중 누적 중지는 구현됐다. 그러나 API로 B=0.88을 만든 뒤 조회한 HUD는 0%였고, 임시 가상 마우스 LMB 주입 후에도 HasStarted=false·시간 0이었다. 입력 소비와 프레임 갱신이 검증되지 않아 구현 결함인지 도구 실행 환경 영향인지 판정하지 않는다. 임시 장치는 제거했다. S1-06a에서 실제 Game View로 최초 청소→HUD/시간 증가→포커스 이탈/복귀→완료 시간 고정→재시작 후 첫 입력을 연속 확인할 것. UI 경계·MMB 동시 입력·눌림 상태 초기화·1280×720 가독성도 포함하며 Windows 빌드는 S1-06b에 남긴다.

### 2026-09-23 — S1-05-f3 조치

- `CleaningProgressTracker`는 포인터 입력을 별도로 재조회하지 않고, A/B 오염 상태가 실제로 변할 때 발생하는 이벤트로 첫 청소를 시작한다. `CleaningProgressHud`는 같은 진행 이벤트와 `LateUpdate`에서 최신 수치를 갱신하므로, A/B 처리 순서와 HUD Update 순서가 달라도 한 프레임의 오래된 값에 의존하지 않는다. 포커스 제외는 `Application.isFocused`를 현재 프레임에 직접 조회한다.
- 새 Play Mode의 공개 API 검증에서 B 오염 `1 → 0.88` 적용 직후 `HasStarted=true`, HUD `B 표면 제거: 12%`를 확인했다. MCP 명령 실행 환경은 Game View 프레임을 지속 실행하지 않아 2초 대기 뒤에도 타이머 `Update` 누적은 재현하지 못했다. 따라서 실제 LMB Hold/Drag·포커스 이탈/복귀·완료 시간 고정·재시작 후 첫 입력은 계속 S1-06a에서 검증한다.
- 이 조치 중 Play Mode 런타임 스크립트 재컴파일 직후 `CoinSurfacePointer.OnEnable`의 Input System 액션 맵 상태 오류 4건을 Console 조회에서 관찰했다. 새 Play Mode에서는 같은 오류가 추가되지 않았고, 최종 Edit Mode Console 조회는 오류·경고 0건이다.

### 2026-09-23 — S1-05-f3 후속 기록 확인

- **[s1-05-f3] — 코드 보완 확인, 통합 검증 대기 유지:** 문서와 diff에서 A/B 청소 이벤트→타이머 시작·HUD 즉시 갱신, LateUpdate 보완, 현재 포커스 조회를 확인했다. `HasStarted=true`·HUD 12%는 구현자의 새 Play Mode API 검증 기록이며 이번에는 독립 실행하지 않았다. 실제 시간 누적·포커스 복귀·완료 시간 고정·재시작 첫 입력은 여전히 미검증이므로 S1-06a에서 확인한다. 재컴파일 직후 Input System 오류 4건은 새 Play Mode에서 추가되지 않았다는 기록일 뿐, 원인 해결로 판정하지 않는다.

### 2026-09-23 — S1-05-f3 독립 재검토

- **[s1-05-f3] — 이벤트·HUD·단일 프레임 누적 통과, 연속 입력 검증 대기:** 새 Play Mode에서 B 청소 1초 적용 직후 HasStarted=true·HUD 12%·진행 이벤트 1회를 확인했다. 추적기/HUD 비활성→재활성 후 추가 청소도 HUD 24%·이벤트 1회로 중복 구독이 없었다. 완료 시 정지, 재시작 시 시작=false·시간 0·HUD 0%, 다음 A 청소 시 시작=true·HUD 20%·이벤트 1회를 확인했다.
- **프레임 진단:** 명령 사이에는 frameCount=1·시간 0이 유지됐다. `EditorApplication.Step()`으로 한 프레임 진행 후 frameCount=2·시간 약 0.02초를 확인해 타이머 Update 누적 자체는 검증했다. 이 결과는 연속 실행이나 포커스 제외 검증을 대체하지 않는다. 실제 LMB/UI/포커스 전환·완료 시간 고정은 S1-06a, 빌드는 S1-06b에 유지한다.
- **정리:** 임시 이벤트 구독을 해제하고 비일시정지 Edit Mode·씬 Dirty=false로 복귀했다. 검토 전후 Console 오류·경고 0건, 컴파일 실패 없음. 과거 런타임 재컴파일 오류의 재현·원인 검증은 수행하지 않았다. 코드·씬 파일 수정 없이 검토 기록만 추가했다.

### 2026-09-29 — Step 1 전체 최종 검토

- **범위·근거:** `0c28fd2`와 현재 미커밋 변경을 기준으로 구현 지시서, S1-01~06 기록, 런타임 코드 전체, 저장 씬·입력·도구·빌드 설정을 대조했다. 이번 세션에는 Unity MCP가 제공되지 않아 새 컴파일·Play Mode·Console·빌드·플레이어 검증은 수행하지 않았다. 기존 API 통과와 BuildReport는 구현 기록을 인용한 것이며 이번 독립 실행 결과가 아니다.
- **양호:** 입력/도구/오염/진행/UI 책임 분리, 시간 기반 A/B·광택 처리, 판 시작 B 분모 고정, 공유 머티리얼을 보존하는 PropertyBlock, 세 조건 동시 완료·중복 방지, 재시작과 F의 범위 분리를 확인했다. Orthographic 가시성 수정도 코드에 반영돼 있다. 저장 씬은 오브젝트 35개이며 비영(非零) 내부 fileID의 누락 참조는 없었다(에디터 Missing Script 검사를 대체하지 않음). 입력 6개 바인딩, 시작 씬·제품명·해상도·창 크기 설정, 빌드 실행 파일 존재 및 Git 제외를 확인했다. `git diff --check`는 오류 없이 통과했다.
- **[s1-06-f1] 중간 / 확인 — 커서의 도구 반경 표시 누락:** 구현 지시서 §5는 커서에 도구 반경·사용 가능 여부 표시를 제안하지만 `SurfaceToolCursor.ShowAt`은 위치·회전·표시만 갱신한다. 씬 커서 크기는 `(0.12, 0.005, 0.12)`로 고정되고 실제 A 판정 반경은 마른 천 0.32·작은 솔 0.18·광택천 0.28이다. 도구를 바꿔도 화면에서 효과 범위를 알 수 없다. 선택 도구의 반경을 커서에 연결하고, UI 차단 시 표시도 사용 불가 상태와 일치시킬 것. 세 도구 전환·줌·회전에서 범위 표시와 실제 A 접촉을 확인한다. 고정 접촉점 커서를 의도한 대안으로 유지하려면 원안과의 차이를 명시한다.
- **[s1-06-f2] 완료 판정 보류 / 미검증 — Windows 실제 입력 루프:** 기존 `s1-02-f2`, `s1-04-f3`, `s1-05-f3`의 통합 검증 대기는 유지한다. 빌드 성공(기록상 오류 0·경고 485)과 API 합산 152초는 실제 플레이 검증이 아니다. 최종 빌드에서 LMB Hold/Drag·UI 경계·MMB 동시 입력·F·줌 극값·포커스 왕복·타이머 정지·화면 가독성을 확인하고 완료→재시작→재완료를 반복할 것. 첫 유효 청소부터의 실측 시간과 포커스 이탈 시간을 따로 기록해 3~5분 목표 및 시간 제외를 판정한다. 실패가 확인되기 전에는 구현 결함으로 단정하지 않는다.
- **[s1-06-f3] 낮음 / 검증 근거 부족 — 수치 경계 회귀 확인:** 조건 미달과 100% 완료 기록은 있으나 각 완료 기준의 직전·정확한 경계·직후, 초기 A=0을 포함한 전체 경계 검증 근거는 부족하다. 다른 조건을 충족한 상태에서 A/B/광택을 각각 검사하고 초기 A/B=0도 확인할 것. 고정 A 5개에서는 제거율이 20% 단위여서 A 90% 조건은 실제로 5개 모두 제거해야 충족한다. 이는 현재 개수 기반 기본안의 결과이며 결함이 아니다. 기존 초기 B=0 검증은 유효하다.
- **최종 판단·인계:** 핵심 MVP 로직은 구성되어 있고 이번 정적 검토에서 치명적 결함은 발견하지 못했다. 다만 커서 표시 보완과 필수 실행 검증이 남아 **Step 1 부분 완료, Step 2 전환 보류**로 판정한다. `f1` 처리 → 경계 검사 → 변경 시 재빌드 → `Docs/Step1-구현결과.md`의 수동 순서로 `f2` 검증 → 결과 기록 순으로 마무리한다. 이번 변경은 이 검토 기록뿐이며 구현·씬 수정과 커밋은 하지 않았다.

### 2026-09-29 — 최종 검토 조치 및 재검증

- **[s1-06-f1] — 범위 표시 보완, 실제 UI 입력 확인 대기:** 기존 Play Mode에서 세 도구의 반경이 각각 0.32·0.18·0.28인데 커서 지름이 모두 0.12인 것을 재현했다. `SurfaceToolCursor`에 기존 선택 상태를 연결하고 작은 중심 커서는 유지하면서 `LineRenderer` 링에 현재 도구의 실제 A 접촉 반경을 표시했다. `CoinSurfacePointer`는 UI 위에 있을 때 표면 적중·커서를 숨긴다. Play Mode에서 세 도구의 링 월드 반경이 각각 0.320·0.180·0.280으로 일치하고, 회전 0°·75° 및 Zoom 1.6·3.2에서도 유지됨을 확인했다. 숨김 시 중심·링 모두 비활성이고 카메라 캡처에서 링을 육안 확인했다. UI 위 실제 포인터 이동·경계 왕복은 `f2` 수동 검증에 남긴다.
- **[s1-06-f3] — 경계 API 검증 완료:** 다른 조건 충족 상태에서 기본 A 기준 90%는 4/5=80%일 때 완료 차단, 5/5=100%일 때 완료를 확인했다. B는 79.9%에서 차단·80.0%에서 완료, 광택은 69.9%에서 차단·70.0%와 70.1%에서 완료됐다. 런타임 임시 오염 목록이 비어 있는 A=0과 초기 B=0은 모두 제거율 100%이며 완료 판정이 가능했다. 검증용 직렬화 값·참조를 복원하고 Play Mode를 종료했다. 이는 실제 입력 테스트가 아닌 공개 API 경계 검증이다.
- **[s1-06-f2] — 미검증 유지:** Computer Use의 비연속 드래그로 플레이 시간을 측정하지 않는다. Windows 빌드에서 실제 LMB Hold/Drag·UI 경계·MMB 동시 입력·F·줌·포커스 왕복·완료→재시작→재완료 및 3~5분 실측이 필요하다. 변경 사항을 포함한 재빌드 결과는 후속 기록한다.
- **에디터 상태:** `CleaningRoom` 원본 씬을 저장했으며 Edit Mode·Dirty=false, Unity 컴파일/임포트 진행 없음, Console 오류 0을 확인했다. 최초 컴포넌트 전체 조회 도구가 Edit Mode의 `renderer.material`·`MeshFilter.mesh`에 접근해 오류 2건을 남겼으나, 이는 Unity MCP 조회 도구의 부작용이며 씬을 재로드한 후 새 Play Mode에서 재발하지 않았다.
- **재빌드 및 최종 상태:** 조치 후 `Builds/Windows/Step1-20260929-review/SqueakSqueak.exe`를 새 경로로 빌드했다. BuildReport `Succeeded`, 오류 0·기존 `com.unity.ai.inference` 셰이더 경고 485·128,671,729바이트이며 실행 파일 생성 확인. 빌드 후 Console 오류 0·조회 경고 25, `CleaningRoom` 저장됨·Missing Script 0·커서 참조 연결을 확인했다. **`f1` 코드/씬·에디터 검증 해소, `f3` API 경계 검증 해소, `f2` 실제 Windows 입력 루프 미검증으로 Step 1 부분 완료 유지.**

### 2026-09-29 — 수정 후 최종 재검토

- **범위·한계:** 현재 미커밋 코드·씬과 위 조치 기록을 재대조했다. Unity 도구가 이번 세션에도 제공되지 않아 새 Play Mode·Console·빌드 검증은 하지 않았다. 아래 API·빌드 성공은 개발자 기록에 근거하며 독립 재실행과 구분한다.
- **[s1-06-f1] — 코드·씬 수정 해소:** `SurfaceToolCursor`가 현재 도구 반경으로 48점 링을 갱신하며, 씬 `LineRenderer`는 월드 좌표·루프 사용 및 선택 상태 참조가 연결돼 있다. 중심 커서 스케일과 반경 계산이 분리됐고 `Hide`는 양쪽 Renderer를 숨긴다. UI 위에서는 `CoinSurfacePointer`가 적중·커서를 초기화한다. 저장 씬 내부 fileID 누락은 없었고 이번 정적 검토에서 새 수정 결함은 발견하지 못했다. 실제 UI 경계 왕복은 `f2`에 유지한다.
- **[s1-06-f3] — 검증 기록 보완 해소:** 개발자 API 기록의 A 80% 차단/100% 완료, B 79.9% 차단/80% 완료, 광택 69.9% 차단/70%·70.1% 완료 및 초기 A/B=0 처리는 현재 비교식·0 분모 처리와 일치한다. 현재 A 5개 구성에서 도달 가능한 경계를 확인한 근거로 수용한다. 모든 수치 조합을 자동 회귀 테스트한 것으로 확대하지 않는다.
- **[s1-06-f2] — 최종 완료 보류 유지:** 수정 후 `Step1-20260929-review` 실행 파일과 게임 DLL 존재, 소스 수정 이후 DLL 생성 시각, 빌드 경로 Git 제외를 확인했다. 개발자 기록은 BuildReport 성공·오류 0·기존 경고 485이나 실제 Windows 입력·포커스·연속 타이머·2회 루프·3~5분 실측은 여전히 미실행이다. 최종 수동 검증에는 이전 `tuned` 빌드 대신 `review` 빌드를 사용한다.
- **판정:** 이번 수정에 추가 코드 변경을 요구할 새 결함은 발견하지 못했다. **수정 사항 검토 통과(정적 검토 및 개발자 API 기록 범위), Step 1 전체는 실제 플레이 검증 대기**다. `Docs/Step1-구현결과.md`의 수동 순서를 완료하고 결과·Player 로그를 기록한 뒤 Step 2 전환을 판단한다. `git diff --check` 통과. 이번에는 검토 기록만 추가했으며 코드·씬 수정 및 커밋은 하지 않았다.
