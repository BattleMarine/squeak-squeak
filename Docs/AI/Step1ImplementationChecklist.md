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
