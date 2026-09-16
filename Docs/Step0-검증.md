# Step 0 구성 및 검증

## 기본 구성

- Unity `6000.3.23f1`, URP `17.3.0`, Input System `1.20.0`.
- 입력 백엔드: 새 Input System (`activeInputHandler: 1`). 패키지 추가 없음.
- 씬: `Assets/Scenes/CleaningRoom.unity`. 기본 `SampleScene`은 보존한다.
- 폴더: `Assets/Scenes`, `Assets/Settings`, `Assets/Materials/Prototype`. 스크립트·프리팹 폴더는 실제 구현 시 추가한다.
- 외부 아트 에셋 도입과 전체 에셋 추적 점검은 사용자 요청으로 보류한다. 이번 임시 도형용 머티리얼과 씬에는 Unity가 생성한 `.meta`를 포함한다.

| 오브젝트 | 구성 |
| --- | --- |
| Main Camera | 위치 `(0, 6, 0)`, 회전 `(90, 0, 0)`, Orthographic Size `2.4`, AudioListener, URP Camera Data |
| Directional Light | 회전 `(50, -30, 0)`, 강도 `1.8`, Soft Shadows, URP Light Data |
| Workbench | Cube, 위치 `(0, -0.2, 0)`, 크기 `(12, 0.4, 8)`, BoxCollider |
| Coin | 회전 중심, 위치 `(0, 0.13, 0)`, 초기 회전 `(0, 0, 0)` |
| Coin/CoinBody | Cylinder, 로컬 크기 `(2, 0.1, 2)`, Convex MeshCollider |

임시 머티리얼은 URP Lit 기반 `Workbench_Prototype.mat`, `Coin_Prototype.mat`이다. 동전 문양·오염·게임 UI는 아직 없다.

## 확정 입력 기준

| 입력 | 동작 |
| --- | --- |
| Mouse Move | 청소 도구 이동 |
| LMB Hold / Drag | 현재 도구 사용 |
| MMB Drag | 동전을 잡아 자유 회전 |
| Mouse Wheel | 확대 / 축소 |
| F | 동전 위치·회전·Zoom을 초기 상태로 복원 |

위 표는 사용자 확정 사양이다. Step 0에서는 키맵을 정의하며, Input Actions 구성과 런타임 연결은 Step 1에서 구현한다. 현재 기본 씬에서는 위 조작이 동작하지 않는다.

Step 1 구현 시 청소와 회전 입력의 동시 처리, 회전 후 표면 좌표·충돌 판정, 줌 제한, UI 위 입력 처리를 함께 검증한다. F의 기준값은 시작 시 동전 Transform과 카메라 Orthographic Size로 한다.

## 프로젝트 설정 상태

- 사용자 확정 설정: 기본 해상도 **1280×720 (16:9)**, `Windowed`, **창 크기 조절 불가**, 제품명 **Squeak Squeak**.
- 초기 개발 단계의 시작 씬은 `CleaningRoom`이다. Build Settings의 첫 활성 씬으로 등록하고 `SampleScene`은 파일을 보존한 채 빌드에서 제외했다.
- Unity Editor API로 적용한 뒤 `ProjectSettings.asset`과 `EditorBuildSettings.asset`의 저장 값을 재확인했다.

## 검증 기록 — 2026-09-15

- 기준 커밋: `98181e3`. 시작 시 작업 트리 변경 없음.
- 에디터: 대상 프로젝트 경로 일치, Edit Mode, 컴파일·임포트 대기 없음, 기존 씬 저장 상태 확인.
- 사전 Console: 오류 0, Unity MCP의 Codex 실행 파일 서명 수집 경고 1. 기존 경고로 기록.
- 씬: 카메라 렌더 이미지에서 중앙 동전·작업대 확인. 활성 상태·머티리얼·Collider 참조 정상, Missing Script 0.
- Play Mode: `CleaningRoom` 실행, 중앙 카메라 Raycast가 `CoinBody`의 윗면 `(0, 0.23, 0)`에 적중. 이후 Console 오류·경고 0 확인, Edit Mode 복귀.
- 자동 테스트: 게임 로직이 없는 기본 씬이므로 별도 테스트 코드는 추가하지 않았다.
- Windows 빌드: **성공**, `StandaloneWindows64`, Mono, 일반 빌드(`BuildOptions.None`). 최초 약 3분 51초, 설정 확정 후 재빌드 약 10초. 두 빌드 모두 출력 약 128.6 MB, 오류 0·기존 패키지 경고 485.
- 최초 실행: 그래픽을 활성화한 `-batchmode`로 약 63초 실행·응답 확인. Input System·PhysX·Direct3D 12 초기화 완료, 예외·크래시 기록 없음.
- 확정 설정 실행: 일반 Player를 숨김 창으로 실행해 제목 `Squeak Squeak`, 클라이언트 영역 **1280×720**, 크기 조절 테두리·최대화 버튼 비활성을 Win32 조회로 확인했다. Windows 배율 150%(144 DPI)를 고려한 픽셀 측정이다.
- 종료: 검증용 창에 정상 종료 요청을 보내 프로세스 종료와 Input System·물리 모듈 정리 로그를 확인했다.
- 실행 제한: 실제 화면 육안 확인·수동 조작은 미검증이다. Player 로그에 `d3d12: failed to query info queue interface (0x80004002)`가 있으나 이후 그래픽 장치 초기화와 실행은 진행됐다.

### 빌드 재현과 경고

최신 빌드는 `BuildPipeline.BuildPlayer`에 저장된 Build Settings의 활성 씬 목록과 아래 옵션을 전달했다. 최초 설정 확정 전 빌드는 `Builds/Windows/Step0/`에 별도로 보존한다.

```text
scenes: Assets/Scenes/CleaningRoom.unity
target: StandaloneWindows64
options: None
locationPathName: Builds/Windows/Step0-Configured/SqueakSqueak.exe
```

- 최신 실행 파일: `Builds/Windows/Step0-Configured/SqueakSqueak.exe`
- 결과·근거: 같은 폴더의 `build-report.txt`, `player-smoke.log`, `window-size-check.txt`, `window-size-check.log`.
- 확정 설정 실행 명령: `SqueakSqueak.exe -logFile <로그 절대 경로>` (배치 모드·해상도 덮어쓰기 없이 검증).
- 셰이더 경고 485건은 기존 `com.unity.ai.inference` 패키지에서 발생했다. ConvGeneric 460, ConvTranspose 6, GridSample 3, Pad 1, ScatterElements 2, ScatterND 9, SliceSet 4건이다. 정수 연산 성능·지원하지 않는 일부 변형 관련 경고이며 패키지를 수정하거나 경고를 숨기지 않았다.
- MCP 호출은 경고 때문에 오류 응답을 반환했으나 실제 BuildReport는 `Succeeded`, 오류 0이며 실행 파일 생성·실행까지 확인했다.

### 작업 중 발생한 자동 변경

Unity 저장·빌드 과정에서 다음 파일 변경이 관찰됐다. 의도한 화면·시작 씬 설정 변경과 구분하며, 임의로 되돌리지 않았다.

- `DefaultVolumeProfile.asset`: 볼륨 직렬화 필드와 참조 목록 갱신.
- `PC_RPAsset.asset`: 셰이더 사전 필터 값 갱신.
- `UniversalRenderPipelineGlobalSettings.asset`: 런타임 설정 참조 목록 생성.
- `ProjectSettings.asset`: 기존 Input Actions의 preloaded 참조와 Standalone 배칭 기본값 직렬화.
- `UnityConnectSettings.asset`: `m_Enabled`가 0에서 1로 변경됨. 이 작업의 명령에서 직접 수정하지 않았으며 변경 주체는 확정하지 못했다. Step 0 커밋에서는 제외하고 로컬 변경을 보존한다.

성능 테스트 패키지가 빌드 중 생성한 `Assets/Resources/PerformanceTestRun*.json`은 빌드 후 패키지가 자동 정리했다. 빌드 출력은 `.gitignore`의 `Builds/` 제외 규칙에 포함된다.

## 다른 PC 재현 — 사용자 확인

### 기존 빌드 실행 성공

- 시험일: **2026-09-15**, 사용자 보고일: 2026-09-16.
- 대상: 기존 `Step0-Configured` Windows 빌드.
- 환경: x86-64, Intel Core i7-12700K, NVIDIA RTX 3070, Windows 11 Education.
- 결과: 사용자 보고 기준 정상 실행 및 화면의 임시 동전 표시 확인. 현재는 입력 동작이 없는 기본 씬이므로 조작이 없는 것이 정상이다.
- 범위: 실행·화면 표시 확인이며, 해당 PC의 Player 로그와 상세 창 속성은 별도로 확인하지 않았다.

### 소스 Clone·재빌드 성공

- 사용자 보고일: **2026-09-16**.
- 결과: 다른 컴퓨터에서 소스를 Clone해 빌드 테스트 완료. **빌드 오류 없이 성공했고, 생성한 빌드도 정상 실행됨**을 사용자가 확인했다.
- 근거 범위: 사용자 보고에 따른 완료 기록이며, 해당 PC의 상세 로그·빌드 경고 수는 별도로 수집하지 않았다.
- 판정: 개발 환경 재현 항목을 완료하고 **Step 0 완료, Step 1 구현 대기**로 전환한다.

### 향후 재현 절차

1. 변경 파일이 포함된 저장소를 Clone하고 Unity Hub에서 `6000.3.23f1` 및 Windows Build Support를 준비한다.
2. 프로젝트를 열고 패키지 복원·임포트·컴파일이 끝날 때까지 기다린다.
3. `Assets/Scenes/CleaningRoom.unity`를 열어 Play Mode에서 동전·작업대 표시와 Console 오류를 확인한다.
4. Build Settings의 시작 씬 `CleaningRoom`과 1280×720 창 모드·크기 조절 불가 설정을 확인하고 Windows x64로 빌드·실행한다. 출력 경로는 `Assets` 밖의 `Builds/Windows/`를 사용한다.
5. Unity 버전, 빌드 성공 여부, 실행 화면, Console·Player 로그 오류 유무를 전달한다.

Git의 `Builds/`는 제외 경로이므로 Clone에는 실행 파일이 포함되지 않는다. 빌드 파일만 시험하려면 실행 파일과 데이터 폴더·DLL을 포함한 출력 폴더 전체를 복사한다.
