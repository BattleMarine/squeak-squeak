# Unity 프로젝트 컨텍스트

<!-- unity-onboarding:generated:start -->

## 프로젝트 요약

- 프로젝트 루트: `C:\Users\ktsvc\Projecets\squeak-squeak`
- 마지막 분석: 2026-09-17 (KST)
- 마지막 분석 커밋: `19bb21a4662e2bac0ca51a024a0df1ffb26c84f2` — `docs: Step 0 검증 완료와 Step 1 전환 기록`
- 목표: 탑뷰 3D 환경에서 동전 하나를 청소·광택 처리해 결과 화면까지 진행하는 Windows용 MVP.
- 현재 단계: Step 0 완료, Step 1의 S1-01a~S1-01d 입력·표면 판정·카메라 제어 완료.

## 확인된 환경

- Unity: `6000.3.23f1` (`ProjectSettings/ProjectVersion.txt`)
- 렌더 파이프라인: Universal Render Pipeline 17.3.0 (`Packages/manifest.json`, `GraphicsSettings.asset`, `Assets/Settings/*RPAsset.asset`)
- 입력: 새 Input System 1.20.0. `activeInputHandler: 1`이며, 입력 에셋에는 템플릿 `Player`·`UI`와 `Cleaning` 맵이 있다. `Cleaning`은 포인터 위치·델타, 도구 사용, 중클릭 회전, 휠 줌, F 복원 바인딩을 제공한다.
- 목표 플랫폼: PC/Windows x64. 제품명 `Squeak Squeak`, 기본 창은 1280×720·크기 조절 불가 (`README.md`, `ProjectSettings/ProjectSettings.asset`).

## 주요 패키지와 프레임워크

| 영역 | 확인 사항 | 신뢰도 | 근거 |
| --- | --- | --- | --- |
| 렌더링 | URP 17.3.0, PC·Mobile 파이프라인 에셋 | 확인 | `Packages/manifest.json`, `Assets/Settings/` |
| 입력 | Input System 1.20.0 | 확인 | `Packages/manifest.json`, `Assets/InputSystem_Actions.inputactions` |
| UI | UGUI 2.0.0 | 확인 | `Packages/manifest.json` |
| 테스트 | Unity Test Framework 1.6.0 설치, 프로젝트 테스트 코드는 없음 | 확인 | `Packages/manifest.json`, `Assets/` 파일 목록 |
| 기타 | Timeline, AI Navigation, AI Assistant/Inference, Visual Scripting, Multiplayer Center가 설치됨 | 확인 | `Packages/manifest.json` |
| 네트워킹 | 런타임 네트워킹 사용은 확인되지 않음 | 확인 | 패키지·`Assets/` 코드 목록 |

## 디렉터리 구조

| 경로 | 역할 | 신뢰도 | 근거 |
| --- | --- | --- | --- |
| `Assets/Scenes/` | `CleaningRoom` 시작 씬과 보존용 `SampleScene` | 확인 | `EditorBuildSettings.asset` |
| `Assets/Materials/Prototype/` | 임시 동전·작업대 머티리얼 | 확인 | 파일 목록 |
| `Assets/Settings/` | URP 렌더러·파이프라인·볼륨 설정 | 확인 | 파일 목록 |
| `Assets/InputSystem_Actions.inputactions` | Player/UI 템플릿과 S1-01 청소 입력 | 확인 | 입력 에셋 |
| `Docs/` | 기획, Step 0 검증, Step 1 구현 지시 | 확인 | 문서 목록 |
| `Assets/Scripts/Cleaning/` | S1-01 표면 판정, 도구 커서, 회전·줌·복원 런타임 코드 | 확인 | `CoinSurfacePointer.cs`, `SurfaceToolCursor.cs` |
| `Assets/Editor/` | 아직 없음. 필요 시 Step 1 에디터 도구를 둔다 | 확인 | 파일 목록 |

## 어셈블리와 경계

- 사용자 정의 `.asmdef`·`.asmref`는 없으며, S1-01 런타임 코드는 기본 `Assembly-CSharp`의 `SqueakSqueak.Cleaning` 네임스페이스에 있다.
- Step 1은 입력/표면 판정·자세 제어, 도구 데이터, 오염·광택 상태, 완료·재시작, UI 책임을 분리한다. 범용 이벤트 버스나 서비스 프레임워크는 도입하지 않는다.

## 씬과 시작 흐름

- 빌드 활성 씬: `Assets/Scenes/CleaningRoom.unity`.
- 비활성·보존 씬: `Assets/Scenes/SampleScene.unity`.
- `CleaningRoom`의 확인된 계층: `Main Camera`, `Directional Light`, `Workbench`, `Coin/CoinBody`, `ToolCursor`, `CleaningController`.
- 동전 초기 위치는 `(0, 0.13, 0)`, 탑뷰 카메라는 `(0, 6, 0)`에 배치되어 있다. `CleaningController`는 Input Actions·카메라·CoinBody Collider·Coin 루트·ToolCursor를 Inspector 참조로 연결한다.

## 아키텍처와 규칙

- 런타임 아키텍처: S1-01은 `CoinSurfacePointer`가 Cleaning 맵 활성화, CoinBody 표면 Raycast, 중클릭 회전, 줌, 뷰 복원을 맡고 `SurfaceToolCursor`가 표면 커서를 맡는다. 이후 도구·오염·UI·결과 책임을 추가한다.
- 오염 표현: Step 1은 고정 A 오브젝트와 동전 전체 B 수치로 검증하며, 위치별 마스크·고급 셰이더는 Step 2 이후 범위다.
- 코드 배치: 런타임은 `Assets/Scripts/`, 에디터 전용 도구는 `Assets/Editor/`. Inspector 참조를 사용하고 매 프레임 이름 검색을 피한다.
- 스타일: 프로젝트 코드가 없어 코드 서식·네임스페이스 규칙은 미확정이다. `AGENTS.md`의 Unity 편집·검증·Git 규칙을 우선한다.

## 테스트와 검증

- EditMode/PlayMode 테스트 코드: 없음.
- Step 0 기록: Windows x64 빌드와 다른 PC의 Clone·빌드·실행은 확인됐으나, 실제 화면 육안 확인·수동 조작은 미검증이다.
- Step 1에서는 Unity Console, 씬 참조, Play Mode 동작, 필요한 EditMode 수치 테스트, Windows 빌드의 실제 입력을 구분해 검증한다.

## Unity 도구 상태

| 기능 | 상태 | 근거 |
| --- | --- | --- |
| Unity MCP 연결·에디터 버전·Console·씬 조회·Editor API 실행 | 사용 가능 | S1-01에서 Unity `6000.3.23f1`, `CleaningRoom`, Console·Play Mode·씬 저장을 실제 조회·검증 |
| 프로젝트 내 MCP 구성 | 연결 확인 | Unity MCP 조회와 `Unity_RunCommand`가 정상 실행됨 |
| Unity AI Assistant 패키지 | 설치됨, MCP 연결 여부는 미확인 | `Packages/manifest.json` |

## 중요한 제약과 미확인 사항

- 분석 시점에 사용자 로컬 변경이 존재한다. `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/UnityConnectSettings.asset`, `README.md`, `ROADMAP.md`는 작업 전 상태를 다시 확인하고 보존한다.
- Unity Editor의 실제 연결 프로젝트·활성 씬·Play Mode·Console·임포트 상태는 MCP가 연결된 뒤 읽기 도구로 재확인해야 한다.
- S1-01d까지 구현·Play Mode 검증을 완료했다. 상세 재개 지점과 검증 기록은 `Docs/AI/Step1ImplementationChecklist.md`를 따른다.

## 읽은 주요 근거

- `README.md`
- `AGENTS.md`
- `ROADMAP.md`
- `Docs/뽀득뽀득 기획서.md`
- `Docs/Step0-검증.md`
- `Docs/Step1-구현지시서.md`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `ProjectSettings/ProjectVersion.txt`, `EditorBuildSettings.asset`, `GraphicsSettings.asset`, `ProjectSettings.asset`
- `Assets/Scenes/CleaningRoom.unity`, `Assets/InputSystem_Actions.inputactions`

<!-- unity-onboarding:generated:end -->
