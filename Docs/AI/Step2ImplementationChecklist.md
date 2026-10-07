# Step 2 구현 체크리스트 (에이전트용)

작성: 2026-10-07. 요구사항 정본: [Step 2 구현 지시서](../Step2-구현지시서.md).
이 문서는 작업 순서·재개 지점·검토 기록 전용이다. 상위 문서에 세부 이력을 복사하지 않는다.

## 현재 상태

- **S2-02a/b/c 통과 (2026-10-07, CPU/컴포넌트 API·GPU 대조·Windows64 빌드 기준), 독립 최종 검토 통과.** B 집계·HUD/완료를 CPU 마스크로 이관했다. 실제 입력/체험·성능은 미검증이다.
- **S2-03a/b 통과 (2026-10-07, Editor/API·GPU·카메라·Windows64 빌드 기준), 독립 최종 검토 통과.** 지역 양면 광택과 태양/물결 문양·테두리·금속 반사·조명을 연결했다. 확대 그림자 결함을 수정하고 독립 회귀·자원 해제까지 확인했다. 실제 입력/외부PC·3~5분·성능은 미검증이며 다음 단계는 S2-04a 전달 대기다.
- 사용자 승인: Step 1 실제 플레이 검증을 보류하고 Step 2 선행. `s1-06-f2`는 계속 미검증이며 Step 2 첫 실행 가능한 빌드에서 함께 확인한다.
- 기준점: Step 1 변경·씬·도구·결과 기록은 `34b68b3` (`fix: [s1-06]`), Step 2 준비 문서는 `d0380ee` (`docs: [s2-00]`)에 보존했다. 기존 빌드는 Git 제외 상태로 보존하며 초기 상태는 작업 시작 때 다시 조회한다.

## 기록 규칙

- 상태는 `미착수 / 진행 중 / 구현 완료·검증 대기 / 통과 / 실패 / 차단`을 쓴다. 미실행 검사가 있으면 구현과 검증 상태를 분리한다.
- 각 단계에서 조회 → 범위 확정 → 편집 → 참조 재조회·저장 → Console·관련 검증 → 기록 순서를 따른다. Unity 연결·상태 확인 등 공통 절차는 AGENTS를 따른다.
- 아래 체크는 완료 기준을 증거로 확인한 뒤 채운다. 도구/API 검증과 실제 입력 검증을 구분하고 사용자 체험 대기 때문에 독립 작업을 멈추지 않는다.

## 작업 단위

| 완료 | ID | 작업 | 검증·인계 기준 |
| --- | --- | --- | --- |
| [x] | S2-00 | Git·Unity 프로젝트/씬·Console·패키지·빌드 기준선 | 2026-10-07 실제 MCP 조회·핵심 참조·보존 확인. 아래 기준선 기록; f2 미검증 유지 |
| [x] | S2-01a | 앞/뒤/옆 청소 범위와 좌표 계약 확정 | 2026-10-07 로컬 XZ·면 ID·법선·월드 거리·스케일/입력 실패 계약 확정, 독립 수치 76개 통과; Unity/b 구현 검증 미수행 |
| [x] | S2-01b | 표면 좌표·브러시 위치 실험 | 2026-10-07 코드/Physics 920개·카메라 Raycast 30개 통과 및 재실행; 앞뒤·옆면·회전·줌·비균일 배율·커서 검사. 실제 마스크 쓰기는 S2-02 대기 |
| [x] | S2-02a | 마스크 생성·고정 초기 패턴·Reset/해제 | 2026-10-07 CPU 212조건·395,046 초기 텍셀 대조, Play 생성/Reset 74·해제25조건 통과; 기존 스칼라·HUD 유지 |
| [x] | S2-02b | 접촉 기반 B 제거·드래그 보간 | 2026-10-07 CPU68·실제 소유자 API11조건 통과; 시간/경로·면/반경·재진입 검사. 물리 입력은 보류 |
| [x] | S2-02c | B 집계·HUD·완료 이관 | 2026-10-07 집계/GPU/완료 API106·뷰 복원5조건 및 새 Windows64 빌드 통과; 실제 입력 보류 |
| [x] | S2-03a | 지역 광택·통합 셰이더 | 2026-10-07 CPU 광택61·집계/GPU/완료110·셰이더19·수명74/25 통과; 지역 양면 화면·면적 평균, 한 점 완료 불가. 실제 입력/빌드는 보류 |
| [x] | S2-03b | 동전 외형·조명·재질 정리 | 2026-10-07 기본 조명 외형7조건·동일UV 회전 반사·캡처15장·확대 그림자48표본, GPU19조건 및 새 Windows64 빌드 통과. 원판/A5/공유 에셋 보존; 실제 입력 보류 |
| [ ] | S2-04a | 도구별 접촉·A 제거 시각 효과 | 도구 차이 구분, 커서 범위 유지, 효과가 Raycast·진행률에 간섭하지 않음 |
| [ ] | S2-04b | 마찰·제거·완료 소리 | 라이선스/임시 상태 기록, 중첩·끊김 확인, UI/MMB/포커스/완료·재시작에서 지속음 정지 |
| [ ] | S2-05a | 조작·도구 역할·앞뒤 안내와 HUD | 1280×720에서 읽힘, 개발자 설명 없이 다음 행동 파악, UI 클릭의 청소 누출 없음 |
| [ ] | S2-05b | 결과·최소 보관·재시작 | 완료 1회, 결과 고정, 동전 1개 연출, 연출 중 재시작·중복 클릭 안전, 모든 상태 복원 |
| [ ] | S2-06a | 수치 회귀·통합·성능 검사 | 아래 검사표 및 2판 반복, 30/60/120Hz 비교, 자원·이벤트 누적 없음, 새 오류 분류 |
| [ ] | S2-06b | Windows x64 빌드·실제 입력 | 새 제외 경로, BuildReport·Player 로그, 실제 f2 시나리오·셰이더·소리·화면 검사 |
| [ ] | S2-06c | 외부 체험·시간·튜닝 | 비개발자 1명 이상 5~10분 체험, 한 판 3~5분 실측·도구 이해·만족도 관찰, 문제 수정 후 재시험 |
| [ ] | S2-06d | 결과 문서·인계 | 구현 결과·빌드·미검증 기록, README/ROADMAP 상태 일치, f2 해소 시 검증 빌드 근거 연결 |

의존: 00 → 01a/b → 02a/b/c → 03a/b → 04·05 → 06. 첫 실행 가능한 빌드는 02 이후부터 만들고 f2를 조기에 확인한다. 에셋·외부 체험 대기는 해당 항목에만 표시한다.

## 필수 검사표

| 검사 | 기대 결과 | 상태 / 근거 |
| --- | --- | --- |
| 앞뒤·가장자리·옆면·줌·회전 | 해당 접촉 영역만 변화, 대상 밖 집계/쓰기 없음 | 좌표·커서/Physics 통과(01b), CPU 쓰기 통과(02b/03a); 03a 실제 Collider→셰이더2302표본·카메라 지역 전후·옆면 제외 확인. 실제 입력 보류 |
| 같은 위치 Hold / 빠른 Drag | 시간 기반 제거, 연속 경로, 보간에 의한 과다 제거 없음 | CPU 같은 시간/경로·18/36표본 통과(02b); 실제 입력 보류 |
| 동전 밖·UI·MMB·포커스·면/도구 전환 | 해당 차단 상태에서 입력·효과 정지, 전환 시 접촉 이력 초기화, 버튼 해제 후 자동 청소·재진입 연결선 없음 | 단절·면/도구·포커스/비활성/Reset API 통과(02b), 기존 UI/MMB gate 재사용 확인; 실제 입력 보류 |
| B 초기 0·부분·전체 / 광택 | 분모 고정·범위 정상·화면/HUD 일치, 초기 B=0에서도 광택 가능 | 03a 지역 광택 CPU/HUD/GPU와 초기B0 통과; B0 고정 지역 광택만으로 실제 PBR 반사 변화 확인 |
| 완료 기준별 직전·동일·직후 | 다른 조건 충족 시 경계대로 1회 완료 | 합성 경계 API 통과(02c): B.8/광택.7, A4/5=.8의 기준값 전후 및 기본.9에서4/5 미완료·5/5 완료 |
| 30/60/120Hz 동일 시간·경로 | 제거율·광택 차이 기본 1%p 이내, 초과 원인 기록 | B CPU 최대0.00824%p(02b); 지역 B/광택 결합 Hold/Drag/빠른 Drag 최대 텍셀0.00486%p·표본18/36 시간 예산 통과(03a) |
| F / 재시작 / 연출 중 재시작 | F는 자세·줌만, 재시작은 전체 복원·연출 취소 | 뷰/상태 복원 API 통과(02c); 물리F 미검증·연출은05 미구현 |
| 첫 청소·포커스 왕복·완료 시간 | 실제 변화부터 시작, 포커스 이탈 제외, 완료 후 고정 | 실제 변경 이벤트→타이머 시작·합성 경과 시간 고정 API 통과(02c); 실제 시간 누적/포커스 왕복 미검증 |
| 완료→재시작→재완료 2판 | 입력·UI·이벤트·마스크·효과·소리 잔류/누적 없음 | 브러시/완료/재시작 API2판·B 텍스처 재사용 통과(02c); 물리 입력/효과/소리 미검증 |
| Windows 화면·제품명·소리·프레임 | 1280×720 고정창, 셰이더 정상, 기기·측정 구간 포함 성능 기록 | 03b Windows64 비개발 빌드 성공·제품/화면 설정 유지·동전 셰이더 경고0; 플레이어 실행 화면/소리/성능 미검증 |
| 설명 없는 외부 체험 | 키·도구 역할·앞뒤·완료 흐름 이해, 시간·막힌 지점·만족도 기록 | 미실행 |

`s1-06-f2`는 입력·F·포커스·시간·반복·Windows 화면 및 플레이 실측 근거를 모아 별도로 해소 판정한다. 이 표의 API 검사 통과만으로 해소하지 않는다.

## 진행 기록

항목당 아래 형식으로 요약하고 긴 도구 로그는 복사하지 않는다.

```text
날짜 / 단계 ID / 상태
변경: 파일·씬 대상, 선택한 기본안과 차이
검증: 환경·빌드, 방법(API/실제 입력/사용자 보고), 기대→실제, 로그 근거
남음: 실패·미실행·담당, 다음 시작 ID
```

### 2026-10-07 / S2-00 / 통과

- **변경·보존:** 본 체크리스트만 갱신했다. 브랜치 `main`, HEAD `0c28fd2`. 시작 시 기존 tracked 변경 13개(도구 3종·CleaningRoom·청소 코드 6개·Step 1 체크리스트·README·ROADMAP)와 미추적 문서 3개(Step 1 결과·Step 2 지시서/체크리스트)가 있었다. `Assets`·`Packages`·`ProjectSettings`의 tracked 파일 113개를 조회 전후 SHA-256으로 대조해 변경/누락 0개를 확인했다. 코드·씬·설정 편집, Play Mode 전환, 저장·재임포트·빌드·커밋·push는 수행하지 않았다.
- **연결·상태:** Unity MCP `GetProjectRoot`의 경로가 `C:/Users/ktsvc/Projecets/squeak-squeak`와 일치했다. 시작·종료 모두 `Assets/Scenes/CleaningRoom.unity`, Edit Mode·비일시정지·컴파일/임포트 진행 없음·Dirty=false다. 활성 씬 root 8개, GameObject 35개, 청소 컴포넌트 18개에서 Missing Script 0·청소 직렬화 참조 누락 0을 확인했다.
- **Console 기준선:** 시작·종료 모두 오류 0·경고 3. 경고는 Unity MCP의 Codex 실행 파일 Windows 서명 수집 실패이며 기존 연결 로그다. Console을 지우지 않았다. 최초 읽기 진단 명령의 결과 패널 필드명 오류는 명령 반환에만 나타났고, 실제 필드 `resultRoot`로 나머지 조회를 보완했다. 후속 진단 명령 컴파일·실행 성공, 새 Console 오류 없음. 프로젝트 전체 재컴파일이나 런타임 성공을 이 결과로 대신 판정하지 않는다.
- **환경·빌드:** 파일과 등록 패키지를 대조해 Unity `6000.3.23f1`, URP `17.3.0`(`Assets/Settings/PC_RPAsset.asset`), Input System `1.20.0`, Test Framework `1.6.0`, AI Assistant/MCP `2.18.0-pre.2`를 확인했다. 사용자 `.asmdef`/`.asmref`·테스트 코드는 없다. 현재 대상 `StandaloneWindows64`; Build Settings는 `CleaningRoom` 활성(0), `SampleScene` 비활성. 제품명 `Squeak Squeak`, Windowed·1280×720·resizable=false. 기존 `Builds/Windows/Step1-20260929-review/SqueakSqueak.exe` 존재·Git 제외를 확인했으나 이번에 실행/재빌드하지 않았다. 기존 오류 0·셰이더 경고 485는 Step 1 기록의 BuildReport 근거다.

| 점검 대상 | 실제 참조·설정 기준선 |
| --- | --- |
| 동전·Collider | `Coin` 위치 `(0,1.2,0)`·회전 0·scale 1. `Coin/CoinBody` local 위치/회전 0·scale `(2,0.1,2)`; Renderer/Convex·비트리거 MeshCollider가 같은 기본 Cylinder Mesh를 참조. Mesh local 반경 0.5·반높이 1이므로 현재 월드 반경 1·반두께 0.1. UV0 88개는 존재하나 면 분리·겹침·Collider 좌표 적합성은 미검증 |
| 카메라·포인터 | `Main Camera` `(0,6,0)`, 회전 `(90,0,0)`, Orthographic Size 2.4·Zoom 범위 1.6~3.2. `CoinSurfacePointer`의 입력 에셋·카메라·CoinBody Collider·Coin Transform·ToolCursor 참조 연결. Cleaning 맵 6개 바인딩(Mouse 위치/델타·LMB·MMB·휠·F) 유지 |
| 커서·도구·UI 입력 | `ToolCursor` 중심 MeshRenderer·월드 좌표 LineRenderer·선택 상태 참조 연결, 두 Renderer 초기 비활성·Collider 없음. 기본 마른 천 및 3개 도구 에셋/버튼 참조 연결. 반경/초당 A·B·광택: 마른 천 `0.32 / 0.04·0.012·0.003`, 작은 솔 `0.18 / 0.125·0.003·0`, 광택천 `0.28 / 0.015·0.006·0.012`. EventSystem UI 모듈 활성, 입력 에셋 및 필수 UI 액션 참조 8개 연결; XR 관련 null은 마우스 범위 밖 |
| 오염·머티리얼 | `Coin/FixedDirt/ObjectDirt_01~05` 모두 활성·내구도 1·local Y=0.13으로 앞면 배치. ObjectDirtCleaner 오염 5개·포인터/선택 참조·가시성 허용 거리 0.12. CoinSurfaceContamination 초기 B=1·광택=0·포인터/선택/Renderer 연결. 공유 Coin_Prototype은 URP Lit의 `_BaseColor`·`_Metallic`·`_Smoothness` 계약이며 현재 B/광택은 위치 없는 스칼라·PropertyBlock 방식 |
| 진행·완료·재시작 | Tracker→A/B 상태, HUD→Tracker·텍스트 4개, Completion→Tracker·Pointer·결과·A/B·선택, 결과→패널·텍스트 3개, RestartButton→Completion·Button 연결. 기준 `0.9/0.8/0.7`, 결과 패널 초기 비활성, Canvas 기준 1280×720. 버튼 listener는 런타임 등록 구조이며 이번에는 클릭/호출하지 않았다 |

- **후속 실행 가능 범위:** 에디터 상태·씬/빌드 설정·Console 조회와 읽기 전용 `Unity_RunCommand`를 실제로 사용했다. 씬/에셋 편집·Play/Stop·Camera Capture·Editor API 실행 도구도 노출되어 후속 구현·좌표 실험·API 검증·빌드에 사용할 수 있다. 전용 테스트/Profiler MCP 도구는 노출되지 않았다. Test Framework는 설치되어 있으므로 마스크 순수 로직 테스트 구성·실행 방법은 해당 단계에서 확인한다. 실제 Windows 입력·소리·성능·외부 체험은 별도 근거가 필요하다.
- **유지할 미검증:** `s1-06-f2`는 사용자 결정에 따른 보류이며 통과/해소로 바꾸지 않았다. S2-02 이후 첫 실행 가능한 Step 2 빌드에서 LMB/UI/MMB/F/Wheel·포커스·타이머·2판 반복·Windows 화면·3~5분 실측을 함께 검증한다. 이번 S2-00에서는 기존 API 검증도 재실행하지 않았으며 위 필수 검사표는 모두 미실행을 유지한다.
- **다음 재개 지점 — S2-01a:** 현재 `RaycastHit.point/normal`을 기준으로 앞/뒤/옆 면 ID와 로컬 평면 좌표 계약부터 확정한다. 비균일 CoinBody scale `(2,0.1,2)`에서 월드 브러시 반경을 변환하는 방법, 옆면 B/광택 제외·커서 사용 불가, 반대면 쓰기 차단을 기록한 뒤 S2-01b의 회전/줌 실험으로 이어간다. 이 계약의 구현·확정은 아직 수행하지 않았다. 오래된 `UnityProjectContext.md`(2026-09-17)보다 본 기준선·현재 코드·Step 2 지시서를 우선한다.

### 2026-10-07 / Step 2 착수 전 커밋 정리

- **변경:** 사용자 요청으로 기존 미커밋 내역을 확인하고 Step 1 코드·씬·도구·검토/결과 기록을 `34b68b3`에 커밋했다. README·ROADMAP·Step 2 지시서·본 체크리스트는 별도 문서 커밋으로 구분한다. 새 기능 구현과 push는 수행하지 않는다.
- **검증:** Unity MCP 경로 일치, `CleaningRoom` Edit Mode·Dirty=false·컴파일/임포트 진행 없음, Console 오류 0·기존 서명 수집 경고 3을 재확인했다. `.meta`와 커서 공유 머티리얼은 이미 tracked이며 staged diff·공백 검사를 통과했다. 이번에는 Play Mode 테스트·빌드를 재실행하지 않았다.
- **남음:** S2-00 통과와 S2-01a 미착수 상태를 유지한다. `s1-06-f2`는 보류·미검증이며 S2-02 이후 첫 실행 가능한 빌드에 이관한다. 다음 작업은 앞/뒤/옆 좌표 계약 확정이다.

### 2026-10-07 / S2-01a / 통과 (계약 확정 기준)

- **범위·변경:** 시작 `main d0380ee`·작업 트리 깨끗함을 확인했다. [표면 좌표 계약](Step2SurfaceCoordinateContract.md)을 작성하고 본 기록만 갱신했다. a는 문서로 계약을 확정하며 b에서 무상태 매핑·Pointer/커서 연결을 구현한다. 런타임 코드·씬·에셋·설정·README/ROADMAP은 변경하지 않았고 S2-02의 마스크/청소 이관도 하지 않았다.
- **계약:** CoinBody Collider 로컬 XZ, Front=1(+Y)/Back=2(-Y)/Side=3/Invalid=0; 양면 동일 U=+X/V=+Z. 월드→로컬 법선은 M의 전치, 반대 방향은 역전치다. 비균일 배율의 월드 변위 거리로 R을 판정하며 직교·proper rotation 배율만 지원하고 shear/반사/잘못된 입력은 거부한다. Side/Invalid·반대면은 B/광택 쓰기 금지, 기존 A 및 전체 B 경로 변경은 이 단계 범위 밖이다.
- **검증:** 저장 파일의 Renderer/Convex Collider 동일 Mesh fileID 10206·CoinBody `(2,.1,2)`·Pointer 대상 참조를 대조했다. 임시 PowerShell 독립 수치 검사 76개(면/실패 11, UV 왕복 8, 도구 거리 39, 법선 3, 스케일 9, 면 선택 6)가 최종 통과했다. 초기 검증 스크립트의 배열 산술 문법 오류를 고친 뒤 전체 재실행했다. 이 결과는 Unity 코드 테스트가 아니다. `git diff --check` 통과; Assets/Packages/ProjectSettings에 diff 없음.
- **연결 제약:** Unity MCP 첫 `GetProjectRoot` 미응답으로 읽기 묶음을 취소했다. 같은 호출 재시도·패키지 변경은 하지 않았으며 `Get-Process -Name Unity`는 결과 없음. **이번 연결·Editor 상태·활성 씬·Console·Physics/Play Mode·빌드는 미검증**이다. S2-00의 과거 조회 결과를 이번 실행 근거로 바꾸지 않는다.
- **b 준비:** 설치 파일 `C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe` 존재·ProductVersion `6000.3.23f1_09d2ecc7fb28`을 읽기로 확인했다. b에서는 기존 작업과 충돌 여부를 재확인하고 필요하면 해당 프로젝트 에디터를 시작한다. 실제 bounds/cap·side 법선·0/45/75/180°/복합 회전·줌·포인터/커서 오차를 검증한다. a만 별도 로컬 커밋 후 전달을 기다리며 push하지 않는다. `s1-06-f2`는 보류·미검증 유지.

### 2026-10-07 / S2-01b / 통과 (코드·Physics 검증 기준)

- **범위·변경:** 시작 `main 9c051ee`·작업 트리 깨끗함 확인. `CoinSurfaceMapper`의 읽기 전용 면/접촉·형상 초기 검사·순수 좌표/거리 변환, Pointer의 hover 연결·향후 B 접촉 API, 옆면/Invalid 커서 숨김과 재사용 가능한 Editor 검사를 구현했다. 새 스크립트/Editor 폴더 `.meta`는 Unity가 생성했다. 직렬화 필드·씬·도구 수치·공유 에셋·패키지·설정은 변경하지 않았다. 기존 A RaycastHit/가시성·입력 차단 및 스칼라 B 경로는 유지하며 S2-02 마스크는 구현하지 않았다.
- **계약 구현:** CoinBody 로컬 XZ·양면 동일 U/V, 전치/역전치 법선, 현재 행렬의 월드 거리, Front/Back만 청소 가능. 초기 검증한 Mesh/bounds를 캐시하도록 `TryMapHit` 인자를 확장했다. 다른 Mesh·같은 Mesh의 런타임 정점 변경은 재검증/초기화가 필요하며 초기 정점 검사가 임의 토폴로지를 완전히 인증하지는 않는다. 정확한 브러시 경계의 float 오차에는 거리 제곱 기준 상대 `1e-5`만 허용한다. Side/Invalid·반대면·잘못된 반경/변환은 새 접촉·거리 API가 거부한다. 기존 스칼라 B에 옆면 쓰기 금지를 적용했다고 판정하지 않는다.
- **연결·경고 대응:** 제한 환경의 프로세스 조회가 실제 Unity 존재를 반영하지 못했고 CIM 조회는 접근 거부였다. 숨김 시작을 시도한 뒤 권한 있는 읽기로 기존 해당 프로젝트 에디터를 확인해 재사용했다. 사용자 프로세스는 종료하지 않았다. MCP 지연 중 `generators.ai.unity.com` 인증서 호스트 불일치 경고를 사용자가 **No**로 닫은 후 GetProjectRoot가 정상 반환했다. 인증서 검증 우회·패키지 교체는 하지 않았다. Unity `6000.3.23f1`·프로젝트 경로 일치·CleaningRoom Edit Mode·Dirty=false를 확인했다. 실제 sharedMesh bounds는 중심 0·extents `(.5,1,.5)`, Collider 전용 진단에서 앞/뒤 local `±Y` 캡과 측면 법선을 확인했다. 이 진단은 가시성 근거로 사용하지 않고 아래 가장 가까운 카메라 Raycast로 별도 확인했다.
- **검증:** Unity 컴파일/임포트 종료 후 새 Play → Pause에서 실제 런타임 코드를 호출했다. 최종 **920개 검사·카메라 Raycast 30개 통과**, 같은 최종 검사를 재실행해 동일 통과했다. 0/45/75/180°·복합 회전과 두 X/Z 배율의 앞뒤 UV/법선·투영, 세 반경의 X/Z 99.9%/정확/100.1% 경계·반대면, 잘못된 입력·shear/반사/준0·다른 Collider/미적중을 검사했다. 실제 카메라에서는 두 줌 극값·중심/편심/가장자리 적중 → Pointer hover → 도구 3종의 48점 월드 링을 대조했다. 옆면 hover 커서 숨김·청소 불가 접촉과 기존 A 5개 가시성(0/45/75°=5, 180°=0)을 확인했다. 최대 오차: UV `6.53616e-7`, 월드 위치 `1.019457e-6`, 링 반경 `1.192093e-7`.
- **상태·Console:** 시작 오류 0·경고 6(기존 서명 수집 3, Account API·인증서·토큰 시간초과). 도구 진단의 `Mesh` 이름 충돌은 `UnityEngine.Mesh`로 한정해 수정했으며 프로젝트 코드 오류가 아니었다. 직접 Console을 지우지 않았다. 최종 Stop 뒤 컴파일/임포트 진행 없음·오류 0·Account API 경고 1. Edit Mode 객체 35·Missing Script 0·청소 참조 누락 0·Dirty=false, 회전 0·CoinBody `(2,.1,2)`·Zoom 2.4·Mesh 참조 일치·Pointer 활성·커서 두 Renderer 비활성으로 복원했다. 씬/Data/Packages/ProjectSettings diff 없음.
- **재실행:** 새 CleaningRoom Play를 Pause하고 실제 포인터를 UI 밖에 둔 뒤(마우스 버튼 해제) `Squeak Squeak/Validation/S2-01 Surface Mapping (Paused Play Mode)` 메뉴 또는 MCP `SqueakSqueak.EditorValidation.CoinSurfaceMappingValidation.Run()`을 실행한다. Editor 전용 검사이며 런타임 상태를 try/finally로 복원하고 저장하지 않는다. 기존 세션의 완료/진행 상태에 영향을 주지 않도록 새 Play에서 실행한다.
- **남음·인계:** 실제 마우스 Hold/Drag·UI/MMB/포커스 차단 재현·청소 한 판·마스크 쓰기·성능 측정·Windows 새 빌드는 미실행이다. API/Physics 통과를 물리 입력 검증으로 대신하지 않으며 `s1-06-f2`는 보류·미검증 유지한다. `korean-git-commit` 스킬로 b 관련 코드·검사·문서·meta만 별도 로컬 커밋한다. push하지 않고 오케스트레이션 검토 후 S2-02a 전달을 기다린다.

### 2026-10-07 / S2-02a / 통과 (CPU·자원 수명 기준)

- **기준선:** `main 526c893`·작업 트리 깨끗함, Unity MCP 경로 일치·CleaningRoom Edit Mode·Dirty=false·컴파일/임포트 없음. 객체 35·Missing Script 0·청소 참조 누락 0, Console 오류 0·기존 Account API 경고 1. 초기 읽기 명령이 소유자를 CleaningController로 잘못 조회해 도구 내 NullReference를 냈으며, 타입 조회로 실제 `Coin/CoinBody`의 CoinSurfaceContamination(ID 64732)과 기존 참조를 확인했다. 읽기 명령은 씬을 변경하지 않았다.
- **구현안:** 작은 CPU 데이터 객체가 앞뒤 독립 초기/현재 오염 배열·공통 원판 유효영역·고정 초기 합을 소유한다. 기본 면당256²·재현 가능한 두 면의 고정 패치 패턴이며 Uniform 옵션으로 0/부분/전체 초기 상태도 검사한다. 기존 CoinSurfaceContamination이 데이터와 생성 Texture2D 2개를 소유하고 Awake/Reset/OnDestroy에 연결한다. 텍스처는 CPU의 파생 표현만이며 공유 머티리얼에는 아직 연결하지 않는다.
- **경계·검증 계획:** 이번 a 동안 기존 스칼라 B/광택·HUD·완료 이벤트는 유지한다. 배열 생성·유효영역/면별 독립·초기 분모 불변·Reset 동일성·텍스처 동일성/해제/재생성을 Editor 및 Play 수명 검사로 실제 코드에서 확인한다. 매 프레임 GPU 읽기·텍스처 재생성·패키지 추가는 하지 않는다. b/c·새 셰이더·실제 마우스·Windows 빌드는 이번 범위 밖이며 `s1-06-f2` 보류를 유지한다.
- **구현·연결:** `CoinSurfaceMaskData.cs`는 CPU 원본과 초기 스냅샷·고정 분모·증분 현재 합·Revision·원판 밖/Side/Invalid 쓰기 거부·0~1 clamp를 제공한다. `CoinSurfaceContamination.cs`에 해상도/패턴 직렬화 설정(기본256/FixedPatches)과 런타임 데이터·텍스처 소유를 추가했다. 기존 `Coin/CoinBody` 인스턴스의 Awake→Reset, 완료 재시작의 기존 ResetSurface, OnDestroy에 연결되며 새 컴포넌트/Inspector 참조는 필요 없다. 설정이 같으면 Reset 시 텍스처를 재사용하고 Release는 멱등이며 Reset으로 재생성한다. Disable/Enable은 상태를 유지한다. 텍스처는 linear RGBA32·mipmap 없음·Clamp·Point, R=CPU 오염의 8bit 표현·A=원판 유효영역·G/B=0이다. CPU 변경 Revision이 있을 때만 명시적으로 Upload하며 텍스처를 재생성하거나 GPU를 읽는 Update 루프는 없다.
- **중간 상태:** 마스크는 실제 동전 오염 인스턴스에 생성되지만 기존 `ApplyCleaning`·URP Lit PropertyBlock·HUD/완료는 여전히 스칼라 B/광택을 사용한다. 새 텍셀 setter는 데이터 연산만 제공하며 입력/접촉/시간 처리는 하지 않는다. 새 마스크를 화면·HUD의 원본으로 이관했다고 판정하지 않는다. 기본 고정 패턴 초기 합은 Front 약19054.958321·Back 약18192.329665, 유효 텍셀 면당51468이며 양면 텍셀은 같은 면적 가중치다. Uniform 옵션의 0/부분/전체 상태와 패치 패턴은 각각 재현 가능하다.
- **실제 코드 검증:** Unity 컴파일 후 `CoinSurfaceMaskValidation.RunData()` **212조건·초기 텍셀395,046개 대조 통과** 및 최종 재실행 통과. 4/17/256 해상도×Uniform/FixedPatches×0/.4/1 강도, 독립 정수 원판 판정·양면 독립·유효영역 합·고정 분모·0 분모100%·잘못된 설정/입력 거부·clamp·Reset/새 데이터 비트 동일성을 검사했다. 새 CleaningRoom Play→Pause에서 `BeginLifecycle()` **74조건**, 두 Step 뒤 `VerifyLifecycleCleanup()` **25조건** 통과; 최종 버전 반복도 동일 통과했다. 실제 소유자 반복 Reset/Release 재생성, 0/.4/1×두 패턴의 임시 소유자6개 Awake/Reset/OnDestroy와 CPU→텍스처 전 픽셀 오차(최대 양자화 허용1/510)를 검사했다. 해제 대상 텍스처18개와 임시 소유자6개가 사라지고 실제 동전의2개만 남았다. 기존 `CoinSurfaceMappingValidation.Run()` **920조건·카메라 Raycast30개 통과**로 좌표/커서/A 가시성 회귀도 확인했다. 자체 Editor/API 검사이며 Unity Test Runner나 물리 마우스 검증이 아니다.
- **종료·Console:** 검사 코드의 구식 InstanceID 조회 경고2개를 Unity 객체 수명 비교로 수정한 뒤 다시 컴파일했다. 수동 Console 삭제 없이 최종 오류0·경고0. Stop 후 Edit Mode·컴파일/임포트 없음·Dirty=false, 객체35·Missing Script0·청소 참조 누락0·런타임 마스크 텍스처0·임시 소유자0. 회전0·CoinBody scale(2,.1,2)·Zoom2.4 및 새 기본 설정256/패턴0을 재조회했다. 씬·Data·Materials·Packages·ProjectSettings 변경 없음; 새 스크립트2개 `.meta`는 Unity가 생성했다.
- **재실행·한계:** Edit Mode에서 `Squeak Squeak/Validation/S2-02a Mask Data (Edit Mode)` 또는 `CoinSurfaceMaskValidation.RunData()`를 실행한다. 실제 수명은 청소 전 새 Play→Pause→`BeginLifecycle()`→Editor Step 두 번(각 프레임 처리 후)→`VerifyLifecycleCleanup()`이며 같은 이름의 메뉴도 제공한다. LMB/MMB를 해제하고 기존 좌표 검사 때는 포인터를 UI 밖에 둔다. 실패하면 Stop으로 테스트 런타임 상태를 폐기하고 새 Play에서 재실행한다. 마스크 렌더링·드래그 보간·HUD/완료 이관·지역 광택·성능 측정·Windows 빌드·실제 입력은 미실행/후속 범위다. Play 중 스크립트 hot reload·런타임 설정 변경은 이번 수명 검증 범위가 아니다. `s1-06-f2` 보류 유지, a 관련 코드/meta/문서만 한국어 별도 로컬 커밋하며 push 없이 b 전달을 기다린다.

### 2026-10-07 / S2-02b / 통과 (CPU·컴포넌트 API 기준)

- **기준·계획:** `main 9b0b848`·깨끗한 작업 트리, 해당 Unity/CleaningRoom Edit Mode·Dirty=false·컴파일/임포트 없음·Console 오류/경고0. 무상태 매핑과 CPU 원본을 재사용해 작은 stroke 객체가 동일 면·도구·변환의 연속 접촉만 보간한다. 월드 반경의1/4 이하 간격·중점 표본에 프레임 dt를 나누고 부드러운 원형 가중치로 제거한다. 비정상적으로 많은 표본이 필요한 프레임은 거부·단절해 작업량을 제한한다.
- **범위:** 기존 입력 gate를 소비하고 Pointer 초기화/포커스·도구 선택·비활성·Reset에서 이력을 끊는다. 기존 전체 B 감소를 제거하고 위치 없는 ApplyCleaning은 입력 gate를 거치는 래퍼로 유지한다. b 동안 기존 스칼라 B 표시/HUD는 초기값, 기존 전역 광택은 종전 방식이며 지역 광택/셰이더는 구현하지 않는다. 순수 시간/경로·단절·면/배율 검사와 Unity 소유자 연결을 확인한 뒤 별도 커밋하고 c 전달을 기다린다.
- **구현:** `CoinSurfaceBrushStroke`는 `rate*dt/sampleCount*(1-distance²/R²)²`를 해당 면의 유효 texel에만 적용한다. 현재 행렬/로컬 bounds·월드 X/Z 거리와 제한된 후보 영역을 사용하며 매 프레임 scratch 배열을 생성하지 않는다. 표본 간격 기본R/4, 최대256표본 초과·잘못된 dt/radius/rate/contact는 프레임 전체를 거부·단절한다. 면·도구·배율/행렬·속도·데이터/Revision 변경 후에는 보간하지 않는다. 기존 소유자가 `TryGetToolUseContact`를 소비하며 Pointer 무효화·포커스 콜백·도구 선택·OnDisable·Reset/Release에 이력 단절을 연결했다. 공개 `ApplyCleaning(dt)`도 input gate를 거치며 옛 전체 B 감소 코드는 삭제했다. 마스크 변화/기존 광택 변화는 기존 CleaningApplied 이벤트로1회 알린다. 씬·직렬화 참조·도구 수치/패키지는 변경하지 않았다.
- **검증:** `CoinSurfaceBrushValidation.RunData()` CPU **68조건 통과**. 양면×세 반경×기본/상이한X/Z/복합 회전의 Hold 가중치, 반대면/원판 밖 보존, 30/60/120Hz의 같은 Hold/Drag/빠른 Drag, 8종 단절 후 재진입·잘못된 입력·과도한 표본을 검사했다. 최대 Hold 오차 `5.10037e-7`, 주파수별 텍셀 차이 `8.237362e-5`(0.00824%p)·제거율 차이 `1.210719e-8`. 18/36표본 제거율 차이 `2.328306e-10`, 텍셀 시간 예산 상한 통과. 새 Play→Pause의 `RunIntegration()` **11조건 통과**: 실제 소유자의 마스크/텍스처 변경·반대면 보존·이벤트/타이머·스칼라 중복 감소 없음·도구/포커스/비활성/F/Reset 단절과 gate 래퍼를 확인했다. 유효 접촉의 소유자 적용은 private 실제 경로를 검사에서 호출한 것이며 물리 LMB 입력 통과가 아니다. UI/MMB 각각의 실제 입력 재현 대신 기존 gate 재사용과 공통 Clear 무효화 연결을 확인했다.
- **회귀·복원:** a의 CPU212·자원 생성/Reset74·해제25조건, S2-01 좌표920·카메라 Raycast30개 통과. 해제 텍스처18개와 임시 소유자6개 제거를 확인했다. 수동 Console 삭제 없이 최종 오류/경고0, Stop 후 Edit Mode·Dirty=false·컴파일/임포트 없음·객체35·Missing Script/참조 누락0·런타임 텍스처/임시 소유자0, 회전0·scale(2,.1,2)·Zoom2.4로 복원했다. Scene/Data/Materials/Packages/ProjectSettings diff 없음, 새 C#2개 meta는 Unity가 생성했다.
- **재실행·남음:** `Squeak Squeak/Validation/S2-02b Brush Data` 메뉴 또는 `CoinSurfaceBrushValidation.RunData()`; 새 CleaningRoom Play→Pause(마우스 버튼 해제)→`RunIntegration()` 또는 같은 Integration 메뉴. 검사 후 상태를 Reset하며 Stop으로 종료한다. 자체 Editor/API 검사이고 Test Runner/Windows/물리 입력 검증이 아니다. b 동안 HUD·완료의 B는 초기 스칼라라 완료 루프를 정상 판정하지 않는다. c에서 기존 프로퍼티·HUD/완료를 마스크 원본으로 이관하고 S2-03에서 지역 광택/표현을 구현한다. 성능 측정·새 빌드·`s1-06-f2`는 미검증 유지. b 관련 파일만 별도 로컬 커밋·push 없음, c 전달 대기.

### 2026-10-07 / S2-02c / 통과 (코드/API·GPU 대조·Windows64 빌드 기준)

- **기준·계획:** `main abf9105`·깨끗한 작업 트리, Unity 해당 프로젝트/CleaningRoom Edit Mode·Dirty=false·컴파일/임포트 없음·Console 오류/경고0. B 제거율은 CPU 마스크의 고정 초기 합 대비 제거량, `SurfaceDirt`는 유효 양면 잔여 평균으로 이관한다. Tracker/HUD/완료는 기존 참조를 유지하고 전역 광택 효율·전체 색상 임시표현을 이 평균에서 파생한다. 지역 광택/셰이더는 구현하지 않는다.
- **검증 계획:** 초기0/부분/전체와 판 중 설정 변경·분모 고정, 실제 변화 이벤트/타이머와 HUD, 완료 세 조건의 직전/동일/직후·완료 후 gate·재시작2판·GPU 텍스처 대조를 Editor/API로 검사한다. a/b의 스칼라 중간상태 기대만 최종 마스크 계약으로 갱신하고 변경된 통합 검사만 재실행한다. Windows64를 새 Git 제외 경로에 빌드하고 BuildReport/경고 원인·Edit Mode 복원·씬/설정 보존을 확인한 뒤 별도 로컬 커밋한다. 물리 마우스/외부 PC·f2 보류 유지.
- **구현:** B의 유일한 원본은 `SurfaceMask`다. `SurfaceDirtRemovalRatio`는 생성 때 고정된 `InitialDirtSum`을 사용하는 마스크의 `1-currentSum/initialSum`(초기 합0이면1)이다. `SurfaceDirt`는 `currentSum/(2*ValidTexelCount)`의 잔여 면적 평균이며 설정 필드가 판 중 상태/분모에 관여하지 않는다. 미초기화/해제 상태의 제거율은0으로 빈 마스크와 구분한다. 기존 Tracker/HUD/완료의 참조는 그대로 소비하고 전역 광택 효율 `1-SurfaceDirt`와 PropertyBlock 색상/금속성/거칠기도 이 평균을 사용한다. float 연산으로 광택이 실제 변하지 않으면 이벤트를 보내지 않는다. 지역 셰이더 미구현이라 화면은 **동전 전체의 집계 기반 임시 색상/광택**이며 닦은 위치를 직접 보여주지 않는다. 입력 gate와 접촉 B만 쓰는 경로는 보존했다.
- **검증:** Unity 컴파일 후 `CoinSurfaceAggregateValidation.Run()` **106조건 통과**, 별도 `RunViewReset()` **5조건 통과**. 두 패턴×초기0/.4/1의 독립 배열 합·제거율·잔여 평균·Tracker/HUD, 판 중 강도/패턴/해상도 변경 후 기존 분모/자원 유지·다음 Reset 반영, B/광택 동시 변화 이벤트1회·미변경/포화/float 무변경 이벤트0·타이머 시작을 확인했다. 초기B0 광택과 변경 후 잔여 평균 기반 전역 효율, PropertyBlock 임시표현 수치를 대조했다. 실제 생성 텍스처를 linear RenderTexture로 Blit→ReadPixels하여 양면 모든 픽셀의 R/alpha/G/B를 CPU와 대조했고 최대 오차 `0.001960747`(허용 `1/510+1e-7`, RGBA32 양자화)였다. GPU 읽기는 Editor 검사에만 있으며 런타임에는 추가하지 않았다.
- **완료·복원:** B .8·광택 .7의 직전/동일/직후를 검사했다. A는5개라20% 단위이므로 실제4/5=.8에 대해 필요 기준 .8001/.8/.7999로 경계 비교를 검사하고, 별도 기본 .9 기준에서4개 제거는 미완료·5개 제거는 완료를 확인했다. 경계용 마스크/광택 및 결과 시간12.75초는 합성 상태이며 입력/시간 누적 증거가 아니다. 두 판의 B는 실제 private 접촉 브러시 경로로 양면을 제거하고 완료1회·공개 ApplyCleaning의 완료 후 차단·결과 수치/시간 고정·재시작 중복 방지·A/B/광택/타이머/UI/도구/이력 복원·텍스처 재사용을 확인했다. F/API는 B·광택·시간을 보존하고 자세/줌만 복원, 완료 재시작은 변경된 자세/줌/도구까지 복원했다.
- **회귀·편집 복원:** 최종 마스크 의미에 맞춰 a/b 통합 기대를 갱신한 뒤 b Integration11·a 생성/Reset74·해제25조건 통과(텍스처18개·임시 소유자6개 해제, 실제 동전2개만 유지). 변경 없는 CPU/좌표 검사는 b에서 통과한 근거를 유지하고 반복하지 않았다. Stop 후 Dirty=false·Edit Mode·Missing Script/청소 참조 누락0·객체35·런타임 B 텍스처/임시 소유자0·Coin 위치(0,1.2,0)/회전0·body scale(2,.1,2)·Zoom2.4, 빌드 직전 Console 오류/경고0. 씬/Data/Materials/Packages/ProjectSettings diff 없음. 새 검사 meta는 Unity가 생성했다.
- **Windows 빌드:** `BuildPipeline.BuildPlayer`로 `Builds/Windows/Step2-S2-02-20261007/SqueakSqueak.exe`를 새 Git 제외 경로에 생성했다. CleaningRoom만 활성, StandaloneWindows64·Mono·BuildOptions.None(비개발), D3D12/D3D11·URP PC 설정 보존. 실제 최신 BuildReport **Succeeded·오류0·경고485·128,686,641바이트·38.8426637초**; exe/Data/UnityPlayer.dll/게임 Assembly-CSharp.dll 존재 확인. 경고를 오류로 취급하는 MCP는 `UNEXPECTED_ERROR` 형식으로 반환했으나 최신 BuildReport와 산출물을 별도 조회해 성공을 확인했고 재빌드하지 않았다. 경고485는 기존 AI Inference/Sentis 패키지 셰이더 계열: 정수 나머지 성능7·정수 나눗셈 성능2·loop shadowing1·signed/unsigned15, `ConvGeneric.compute`의 NON_UNIFORM_CONVGROUP_PER_OC+TRANSPOSE_OUTPUT 미지원 변형460. 패키지를 수정/제거하거나 경고를 숨기지 않았다. 빌드 후 Console 오류0·경고25(픽셀 셰이더 계열)이며 로그를 수동 삭제하지 않았다.
- **빌드 설정 보존:** Input System의 `ProjectWideActionsBuildProvider`가 빌드 중 InputSystem_Actions를 preloadedAssets에 추가하고 후처리에서 메모리상 제거하지만 디스크에 추가 상태가 남았다. 원래 빈 목록·유일한 변경 GUID·후처리 코드·실제 메모리 빈 목록을 확인한 뒤 `PlayerSettings.SetPreloadedAssets(empty)`/`AssetDatabase.SaveAssets`로 복원했다. 씬은 저장하지 않았고 최종 ProjectSettings diff 없음. 새 설정 변경을 커밋하지 않는다.
- **최종 재조회:** 빌드/설정 복원 후에도 해당 CleaningRoom Edit Mode·비일시정지·컴파일/임포트 없음·Dirty=false, 객체35·Missing Script/청소 참조 누락0·런타임 텍스처/임시 소유자0, 기본 자세/scale/Zoom 유지. Console 오류0·기존 빌드 경고25를 보존했고 소스·문서·필수 meta만 커밋한다.
- **재실행·인계:** 청소 전 새 CleaningRoom Play→Pause(마우스 버튼 해제)에서 `Squeak Squeak/Validation/S2-02c Aggregate Integration (Paused Play Mode)` 또는 `CoinSurfaceAggregateValidation.Run()`, 별도 View Reset 메뉴/`RunViewReset()`를 실행하고 Stop한다. a/b 통합 검사는 갱신된 메뉴/API를 사용한다(수명 검사는 새 Play에서 Begin→Step2회→Verify). 재빌드는 활성 CleaningRoom·저장된 Edit Mode에서 BuildPlayer(Windows64/None)를 또 다른 새 제외 경로로 출력하고 BuildReport/설정 보존을 재확인한다. 통과는 자체 Editor/API와 GPU 읽기/빌드에 한정하며 Test Runner·실제 LMB/UI/MMB/F/Wheel/포커스·Windows 플레이어 실행·외부 PC·3~5분/성능 실측은 미실행이다. 특히 광택은 여전히 전역이며 한 점만으로 올라갈 수 있는 중간 상태다. `s1-06-f2` 보류 유지, S2-02만 별도 한국어 로컬 커밋·push 없이 독립 최종 검토를 기다린다. S2-03은 별도 전달 전 착수하지 않는다.

### 2026-10-07 / S2-03a / 통과 (Editor/API·GPU·카메라 기준)

- **기준·선택 계약:** `main db44b84`·깨끗한 작업 트리, 해당 Unity/CleaningRoom Edit Mode·Dirty=false·컴파일/임포트 없음·Console 오류/경고0. 기존 양면 CPU 데이터/브러시/gate/Revision/Reset/수명을 확장해 지역 광택을 같은 텍셀에 저장하고 유효 양면 면적 평균을 HUD/완료에 제공한다. 텍스처2개를 유지하며 R=B·G=광택·A=유효 영역으로 묶는다. 표본별 시간 예산을 B/광택에 동일 적용하고 광택 효율은 해당 텍셀 잔여 B에서 계산한다. 전역 광택 증가 경로는 제거한다.
- **표현·검증 계획:** 기존 Coin_Prototype 공유 에셋을 보존하고 전용 머티리얼/URP PBR 셰이더를 실제 씬 Renderer에 에디터 API로 연결한다. 같은 로컬 XZ/bounds·앞뒤 법선을 사용하며 옆면은 B/광택 표시 제외, 양면 U/V 반전 없음. 지역 색·금속성·거칠기와 반사를 확인하고 CPU 한 점/양면/초기B0/효율/30·60·120Hz, 갱신된 HUD·완료2판·수명, GPU 채널/셰이더 매핑/회전·줌과 카메라 화면을 검사한다. 외형·조명 개선·최종 Windows 빌드는 b, 실제 입력/성능/외부PC·f2는 보류한다. a만 별도 로컬 커밋하고 b 전달을 기다린다.
- **구현 저장·부분 검증:** 양면 CPU 지역 광택·시간평균 해당 텍셀 B 효율·면적 평균 집계·RG 채널 업로드·전역 증가 제거를 구현했다. `CoinSurface.shader`의 UniversalForwardOnly/DepthOnly/DepthNormalsOnly/ShadowCaster와 전용 `Coin_Surface.mat`를 추가했으며 Unity API로 CoinBody에 연결/저장/재조회했다. 기존 Coin_Prototype은 변경하지 않았다. 씬 diff는 머티리얼 GUID 교체와 기존 기본 해상도256/패턴0 필드 직렬화뿐이다. CPU 광택61조건(해석값 최대 오차 `2.610784e-8`, 30/60/120Hz 양채널 최대 `4.860759e-5`, 표본18/36)·마스크212·브러시68 통과. 갱신된 집계/GPU/완료 **110조건·2판**, 뷰 복원5·접촉 통합11조건 통과; GPU 양채널 최대 오차 `0.001960747`. 첫 Play의 null Texture PropertyBlock 오류는 공용 blackTexture 대체로 수정한 뒤 새 Play에서 통합 재검사했다. 로그를 수동 삭제하지 않았다.
- **중단 시점·현재 상태:** 인터넷 중단 예정에 따른 사용자 명시 요청으로 추가 구현/검사/커밋을 멈춘다. 렌더링 검사를 작성/임포트한 직후이며 `CoinSurfaceRenderingValidation.Run()`과 갱신된 `CoinSurfaceMaskValidation.BeginLifecycle/VerifyLifecycleCleanup`은 **미실행**이다. Camera Capture/실제 PBR·Deferred/SSAO·ShadowCaster 시각 검증도 미실행이다. 2026-10-07 마지막 MCP 조회에서 해당 프로젝트 CleaningRoom **Edit Mode·IsPaused=false·IsCompiling=false·IsUpdating=false·Dirty=false·Console 오류/경고0**. 직접 시작한 Play는 Stop 완료했다. 코드·에셋·필수 meta·문서는 미커밋 상태로 보존하며 되돌리지 않는다. stage/commit/push·b 착수 없음.
- **재개 지점:** 사용자가 재개를 요청하면 README/AGENTS/현재 diff와 Unity 프로젝트·씬·Console을 먼저 재조회한다. 새 Play→Pause에서 렌더링 검사를 실행해 실제 화면·양면 채널/UV·회전/줌과 일시 ShadowCasting On 출력 차이를 확인하고 Camera Capture/저장된 `.utmp/s203a-visual-*.png`를 시각 검사한다(현재 파일 생성 전). 원래 씬은 CoinBody 투영 Off·수신 On, Directional Light Soft이며 검사 후 원상복원해야 한다. 갱신된 자원 수명 검사 Begin→별도 Step2회→Cleanup를 실행하고 Stop·자원/참조/씬 보존을 재확인한다. 미검증 부분을 완료한 뒤 문서 상태·최종 diff를 정리하고 승인된 a 로컬 한국어 커밋만 수행한다. b·Windows 새 빌드·실제 입력/성능/체험·f2는 이번 재개 완료와 구분하며 b 전달을 기다린다.
- **재개·검사 원인 분리:** 사용자 재개 요청 후 같은 미커밋 작업을 보존했다. 시작 시 해당 저장 씬 Edit Mode·Console 오류0/Unity MCP 실행 파일 서명 조회 경고6을 기록했다. GPU 검사의 첫 실패는 렌더640×360과 target 복원 뒤 Game View pixelRect 높이652.33의 `ViewportPointToRay`가 다른 중심 Ray를 만들었기 때문이다(최대 월드0.003257992·0.1832624픽셀). 제품/Convex 결함이 아니며 검사 Ray를 실제 투영행렬에서 역투영하도록 수정했다. 임시 렌더 삼각형/hull 우회는 제거했고 최종 비교는 실제 Collider→Mapper 접촉을 그대로 기대값으로 쓰며 hull-only 제외0이다. 테스트 원인 확인/변경된 렌더 검사만 재실행하고 기존 통과한 CPU/집계 전체는 반복하지 않았다.
- **화면·GPU 검증:** `CoinSurfaceRenderingValidation.Run()` **19조건·2302표본(Front1182/Back824/Side296)** 통과. 회전0/45/75/180/복합143·21·63/90°×Zoom1.6/3.2의 실제 면·동일 +X/+Z UV·양채널을 검증했다. R/G/면 구분 GPU 오차0, 실제 접촉 재투영 최대 `7.629395e-5`픽셀. 마스크→linear RenderTexture 양자화 오차는 앞서 집계 검사 `0.001960747`이며 서로 다른 지표다. 기본 Deferred+SSAO 카메라의 앞뒤 접촉 전후와 미접촉 반대면을 저장/육안 확인했고 Unity Camera Capture로 기본 씬도 확인했다. B0 고정·임시 조명85°·A 표시 제외 상태에서 광택만의 PBR 변화 평균 RGB 차이 `0.01831606`을 확인했다. API dt300은 화면 대비용 합성 시간이며 실제 청소 시간/사용감 근거가 아니다.
- **그림자·수명·복원:** CoinBody의 원래 설정은 **투영 Off·수신 On**이다. 기존 그림자 투영이 있었다고 주장하지 않는다. 임시 casting On/조명45°·30° 비교에서 실제 ShadowCaster 출력 평균 RGB 차이 `0.002710544`를 확인하고 Off/원래 조명으로 복원했다. 변경된 `BeginLifecycle()`74→별도 Editor Step2회→`VerifyLifecycleCleanup()`25조건 통과(지역 광택0/.4/1 설정/Reset·RG업로드·텍스처18개/임시 소유자6개 실제 해제, 실제 동전2개만 유지). 이후 마스크 양면 바인딩·debug0·기본 회전/Zoom2.4·null camera target·Dirty=false를 확인하고 Stop했다. 컴파일/임포트 완료·최종 Console 오류/경고0, 로그 수동 삭제 없음.
- **재실행·한계·다음:** 새 CleaningRoom Play→Pause에서 렌더 검사 메뉴/API를 실행하고 `.utmp/s203a-visual-{before,local-after,back-untouched,back-local-after,shadow-probe,polish-only-before,polish-only-after}.png`를 확인한다. 수명 검사는 같은 새 판에서 Begin→별도 Step2회→Cleanup 후 Stop한다. 새 진단은 Editor 전용이며 런타임 GPU 읽기/Shader.Find/패키지를 추가하지 않았다. 현재 동적 Cylinder와 URP PC·D3D12 화면 검증이며 임의 새 Mesh·베이크 lightmap/Meta·다른 플랫폼을 인증하지 않는다. 기존 Coin_Prototype·Data·Packages·ProjectSettings는 보존한다. 단순 외형/조명·앞뒤 무늬·실제 플레이 가독성 개선과 새 Windows 빌드는 b; 실제 입력/3~5분/성능/외부PC·f2는 계속 미검증이다. a만 로컬 커밋한 뒤 b 전달을 기다린다.
- **최종 보존 조회:** 법선/캡 허용값을 좌표 계약의 ±.999/높이.001에 맞춘 최종 셰이더로 렌더19조건을 다시 확인했다. Stop 뒤 저장된 CleaningRoom Edit Mode·Dirty=false·컴파일/임포트 없음·객체35·Missing Script0·깨진 청소 참조0·런타임 마스크0·Console 오류/경고0. Coin(0,1.2,0)/회전0·body scale(2,.1,2)·Zoom2.4·투영 Off/수신 On 유지, 전용 머티리얼의 저장된 shader 의존성도 확인했다. `git diff --check` 통과·기존 Coin_Prototype/Data/Packages/ProjectSettings diff 없음·캡처 Git 제외 확인 후 a 코드/씬/전용 에셋/검사/meta·문서만 별도 한국어 로컬 커밋한다. README/ROADMAP에는 현재 기능 상태만 반영하고 세부 기록은 이 문서에 유지한다.

### 2026-10-07 / S2-03b / 통과 (Editor/API·GPU·화면·빌드 기준)

- **기준·범위:** `main ab7a8c0`·작업 트리 깨끗함, 해당 저장 CleaningRoom Edit Mode·Dirty=false·컴파일/임포트 없음·Console 오류/경고0. 기존 Cylinder Mesh/Convex Collider/로컬 좌표/앞뒤 마스크와 A5개 부착을 보존한다. 전용 셰이더의 자체 절차적 태양/물결 무늬·테두리/각인 법선으로 앞뒤를 구분하고, Coin_Surface와 관련 조명만 에디터 API로 조정한다. 외부 에셋·장식 Collider·새 런타임 상태/프레임워크·도구 속도/UI/소리는 추가하지 않는다.
- **검증 계획:** 실제 기본 카메라의 앞뒤·기울임·오염→지역 청소→청결/광택 캡처와 같은 UV의 회전별 반사 차이를 확인한다. a의 엄격한 CPU 마스크→GPU 채널 대조를 보존하고 관련 씬/참조/A 적중·완료/재시작 API를 검사한다. 새 제외 경로 Windows64 비개발 빌드와 BuildReport/기존 패키지·새 셰이더 경고/산출물/preloadedAssets 보존을 확인한다. 실제 입력/3~5분/성능/외부PC·f2 미검증 유지, b만 별도 한국어 로컬 커밋·push 없음.
- **구현·저장:** 자체 절차적 태양(앞)/마름모·물결(뒤), 구슬 테두리·홈·옆면 줄무늬와 시각 법선을 추가했다. 실제 지오메트리는 변위하지 않으며 원판 Mesh/Convex Collider/로컬 +X/+Z·앞뒤 마스크/A 위치를 유지한다. 깊이 법선에는 같은 시각 법선을 쓰고 그림자 실루엣은 원래 원판이다. Unity API로 전용 Coin_Surface의 색(.56,.46,.30)/metallic .88/smoothness .38, owner 오염색(.11,.09,.065)/dirty .08/max .78, 주광 .95·약한 청색 무그림자 보조광 .25를 저장/재조회했다. 기존 Coin_Prototype·렌더 설정·Data·패키지를 변경하지 않았다.
- **검사 오류·보안 대응:** 첫 외형 검사는 A에 Collider가 있다고 가정해 실패했다. 조회 결과 기존 Coin 하위 Collider는 CoinBody 하나이고 A는 최근접 Coin 적중+기존 거리 가시성 API를 사용한다. 제품을 변경하지 않고 검사 조건을 바로잡았다. MCP 명령에서 internal 캡처 helper 접근/Reflection 사용은 컴파일·권한 제한으로 실행되지 않았고 작은 Editor 검사를 사용했다. 인증서 불일치 창은 사용자가 No로 닫았으며 검증 우회/패키지 교체 없이 기존 MCP 정상 조회를 확인했다.
- **확대 그림자 결함·수정:** Zoom1.6의 B0/P1 캡처에서 굵은 검은 다각형 띠를 발견했다. 일시 SSAO Off에도 남고 주광 shadow Off에서 사라졌다. 설치된 URP LitForwardPass와 대조해 cascade 좌표를 모든 정점에서 보간하던 경로를 URP 조건부 vertex/fragment `TransformWorldToShadowCoord(positionWS)` 계약으로 수정했다. 기본 조명/SSAO·패키지 코드는 유지했다. 수정 후 확대 캡처에서 띠 소멸을 육안 확인하고 같은 평면 반경.6의 shadow on/off **48표본·최대 채널 차이 5.289912e-7**로 회귀 통과했다. 임시 SSAO 진단 코드는 제거하고 확대 그림자 회귀만 남겼다. 수정 전 `.utmp/s203b-diagnostic-{ssao-on,ssao-off,shadow-off}.png`와 수정 후 `s203b-diagnostic-current-shadow-{on,off}.png`를 Git 제외로 보존한다.
- **화면·회귀:** `CoinAppearanceValidation.Run()` **7조건**, A 위치 최근접 Coin/가시성 앞5·뒤0, 원판/Collider/배율 보존 통과. 기본 조명에서 B0의 동일UV81점 RGB별 polished-minus-matte 반응 평균 절댓값 .05644434, 0→35° 같은 표본 반응 차이 .06741607로 밝기 증분만이 아닌 거칠기/반사 변화를 확인했다. 정면 표본 HDR 초과0/81·최대.205832이며 전체 픽셀/성능 측정은 아니다. 최종 `CoinSurfaceRenderingValidation.Run()` **19조건·2302표본(1182/824/296)·GPU 채널 오차0**, 접촉 재투영7.629395e-5픽셀·hull 제외0. 일시 ShadowCaster 변화 .001533983·지역 광택만 PBR 차이 .02125493. 집계/GPU/완료110조건·2판·뷰 복원5조건 통과. a 수명74/25는 재실행하지 않았으며 이번에 자원 소유/생성 코드를 변경하지 않았다.
- **캡처·재실행:** 실제 기본 PC 카메라/Deferred/SSAO·조명으로 1280×720의 `.utmp/s203b-visual-{front-dirty,front-local,front-clean-matte,front-clean-polished,back-dirty,back-local,back-clean-polished,tilt35-dirty,tilt35-local,tilt35-clean-matte,tilt35-clean-polished,tilt65-clean-polished,rim90-clean-polished,zoom16-clean-polished,zoom32-clean-polished}.png` 15장을 저장/육안 확인했다. Overlay HUD가 없는 Camera.Render 월드 화면이다. 새 CleaningRoom Play→Pause에서 `CoinAppearanceValidation.Run()` 및 `RunZoomShadowRegression()` 메뉴/API를 실행하고 finally 복원을 확인한 뒤 Stop한다. dt300과 전체 B0/P0/P1 주입은 합성 비교이며 실제 입력/체험 시간 증거가 아니다.
- **새 빌드·보존:** `BuildPipeline.BuildPlayer`, Windows64/Mono·D3D12/D3D11·BuildOptions.None·CleaningRoom만 활성, 새 `Builds/Windows/Step2-S2-03-20261007/SqueakSqueak.exe`. 최신 BuildReport **Succeeded·오류0·기존 경고485·128,729,201바이트·54.2794078초**, 동전 셰이더 메시지0. 경고는 기존 Inference/Sentis(ConvGeneric460·modulus7·divides2·loop1·signed15)이며 Console 오류0/기존 패키지 경고25를 보존했다. MCP 응답은 출력 경로 재사용 방지 오류였으나 동일 새 경로의 실제 최신 성공 보고서/시간·exe/Data/UnityPlayer/Mono 산출물로 성공을 확인했고 재빌드하지 않았다. Input System 임시 preloaded GUID가 디스크에만 남은 것을 확인해 원래 빈 상태를 PlayerSettings API로 복원했으며 ProjectSettings diff 없음.
- **종료·인계:** 저장된 CleaningRoom Edit Mode·Dirty=false·컴파일/임포트 없음·객체36·Missing Script0·깨진 청소 참조0·잔여 마스크0·preload0, Coin(0,1.2,0)/회전0·body(2,.1,2)·Zoom2.4 및 실제 전용 머티리얼/셰이더 의존성을 재조회했다. 새 Editor 검사 meta를 포함해 b만 한국어 로컬 커밋하고 독립 최종 검토를 기다린다. 플레이어 실행·실제 LMB/UI/MMB/F/Wheel/포커스·3~5분·성능·외부PC·s1-06-f2는 미검증 유지. S2-04/UI/소리/도구 수치 작업이나 push는 하지 않았다.

## 스텝별 검토 결과

### 2026-10-07 — S2-01 최종 검토 (오케스트레이션)

- **범위·판정:** a `9c051ee`·b `9ab13a8`을 기준 `d0380ee`와 대조했다. 계약·런타임 매핑·Pointer/커서 연결·Editor 검사·문서를 검토했고 **S2-01 통과, S2-02a 착수 가능**으로 판정한다. 이번 범위에서 수정이 필요한 결함은 발견하지 못했다. 각 세부 단계가 별도 한국어 커밋으로 분리됐고 필요한 `.meta`가 포함되며 씬·Packages·ProjectSettings 변경은 없다.
- **[s2-01-f1] — 좌표·반경·기존 동작 검토 통과:** 로컬 앞뒷면 분리, 전치/역전치 법선, 현재 변환 행렬의 월드 거리, 다른 면·Side·Invalid 거부와 UI/MMB 입력 경로 보존을 확인했다. 기존 A/B 스칼라 처리 변경 없이 hover만 새 매핑·커서에 연결됐다. 초기 Mesh 정점 검사는 임의 토폴로지의 완전한 인증이 아니며, Mesh 교체·런타임 변형은 재검증이 필요하다는 계약 제한을 유지한다.
- **[s2-01-f2] — Unity 독립 재실행 통과:** 프로젝트 경로·CleaningRoom·Edit Mode·Dirty=false·컴파일/임포트 완료를 먼저 조회했다. 새 Play→Pause에서 커밋된 `CoinSurfaceMappingValidation.Run()`을 독립 실행해 **920개 검사·실제 카메라 Raycast 30개 통과**를 확인했다. UV 최대 오차 `6.53616e-7`, 월드 위치 `1.019457e-6`, 링 반경 `1.192093e-7`. 검사 코드의 기대 좌표·경계값·가장 가까운 Raycast·try/finally 복원도 검토했다. 자체 Editor/API 회귀 검사이며 Unity Test Runner 테스트나 실제 마우스 입력 검증으로 표현하지 않는다.
- **[s2-01-f3] — 후속 인계(결함 아님):** 옆면 커서는 숨지만 기존 스칼라 B는 아직 종전 RaycastHit 경로다. S2-02에서 `TryGetToolUseContact` 기반 마스크 쓰기로 이관하고 기존 전체 감소를 제거해야 한다. 마스크 쓰기·실제 Hold/Drag/UI/포커스·Windows 새 빌드는 이번에 검증하지 않았으며 `s1-06-f2` 보류를 유지한다. S2-01 통과를 Step 2 전체 완료로 확대하지 않는다.
- **종료·보존:** Console 조회 전후 오류 0·Account API 연결 경고 1(같은 계열)을 확인했으며 로그를 수동 삭제하지 않았다. Stop 후 비일시정지 Edit Mode·Dirty=false·컴파일/임포트 없음으로 복귀했다. 독립 실행 후 작업 트리 깨끗함과 `git diff --check` 통과를 확인한 뒤 이 검토 기록만 추가했다. 최종 검토에서는 코드·씬을 수정하지 않았다.

### 2026-10-07 — S2-02 최종 검토 (오케스트레이션)

- **판정:** 기준 `526c893` → a `9b0b848` → b `abf9105` → c `facef20`의 코드·검사·문서를 대조했다. **S2-02 범위 통과, S2-03a 착수 가능**이며 수정이 필요한 결함은 발견하지 못했다. 아래 통과는 코드/API·GPU·빌드에 한정한다.
- **[s2-02-f1] — 상태·접촉·집계 검토 통과:** 양면 CPU 원본과 고정 초기 합, 원판 밖 쓰기/집계 제외, 월드 반경·시간 분배, 차단/면·도구 전환 후 이력 단절을 확인했다. 전체 B 감소는 제거됐고 HUD/완료는 같은 마스크 제거율을 읽는다. 실제 변경만 이벤트를 보내며 Reset·파괴 시 소유 자원을 처리한다. `s2-01-f3`의 B 이관 요구도 반영됐다.
- **[s2-02-f2] — 독립 재실행 통과:** 프로젝트 경로·저장된 CleaningRoom·Edit Mode를 확인하고 커밋된 검사를 실행했다. 마스크 CPU212·브러시 CPU68, 새 Play의 좌표920/Raycast30·접촉 통합11·집계/GPU/완료106·뷰 복원5조건 통과. 별도 새 Play에서 자원 생성/Reset74·해제25조건을 통과했고 텍스처18개·임시 소유자6개 해제를 확인했다. 최대 주파수별 텍셀 차이 `8.237362e-5`(0.00824%p), GPU 오차 `0.001960747`; 완료/재시작2판 통과. 검사 기대값·합성 상태 주입·finally 복원도 검토했다. 자체 Editor/API 검사이며 Test Runner·실제 입력 검증은 아니다.
- **[s2-02-f3] — 빌드·보존 확인:** 최신 BuildReport를 독립 조회해 Windows64 Succeeded·오류0·기존 패키지 경고485·128,686,641바이트를 확인했다. `Builds/Windows/Step2-S2-02-20261007/SqueakSqueak.exe`와 Data/DLL 존재·Git 제외를 확인했으며 재빌드/플레이어 실행은 하지 않았다. 검토 후 Edit Mode·Dirty=false·컴파일/임포트 없음, 객체35·청소 컴포넌트18·Missing Script/직렬화 참조 누락0·잔여 마스크0, 1280×720 고정창·제품명·빈 preloadedAssets를 확인했다. 최종 Console 오류/경고0이며 수동 로그 삭제는 하지 않았다. 씬·Data·Materials·Packages·ProjectSettings diff 없음.
- **[s2-02-f4] — 후속 인계(현재 범위의 결함 아님):** 데이터상 B 제거는 지역적이나 화면은 전체색 임시표현이고 광택은 전역이다. S2-03a에서 양면 B 텍스처·지역 광택·통합 셰이더를 연결해야 한다. 실제 LMB/UI/MMB/F/Wheel·포커스·Windows 실행/외부 PC·성능·3~5분 실측은 미검증이며 `s1-06-f2` 보류를 유지한다. 기존 도구 수치의 위치 기반 청소 시간 튜닝도 후속 체험에서 확인한다. 이번 검토에서는 런타임 코드를 변경하지 않고 기록·현재 상태만 갱신한다.

### 2026-10-07 — S2-03 최종 검토 (오케스트레이션)

- **판정:** 기준 `db44b84` → a `ab7a8c0` → b `b7fb263`의 코드·에셋·검사·문서를 검토했다. **S2-03 범위 통과, S2-04a 착수 가능**이다. 발견한 결함은 b에 수정됐으며 미해결 차단 결함은 없다.
- **[s2-03-f1] — 확대 그림자 결함 해결:** B0/P1·Zoom1.6 화면의 검은 다각형 고리를 독립 시각 검토에서 발견했다. SSAO Off에도 남고 주광 shadow Off에서 사라지는 비교와 로컬 URP Lit 코드로 원인을 좁혔다. cascade 그림자 좌표를 fragment 월드 위치에서 계산하도록 수정한 뒤, 그림자/SSAO를 유지한 확대 화면에서 소멸을 확인했다. 독립 회귀 48표본 최대 차이 `5.289912e-7` 통과.
- **[s2-03-f2] — 지역 상태·외형 검토 통과:** 해당 텍셀 잔여 B에 따른 광택 효율, 양면 면적 평균 HUD/완료, 한 점 포화·반대면 분리·초기 B0·Reset/해제와 RG 셰이더 계약을 확인했다. 자체 절차적 앞뒤 문양·테두리·시각 법선은 원판 Mesh/Collider·A 배치를 유지한다. 기본/기울임/확대 캡처에서 국소 변화·앞뒤 구분·반사 변화를 확인했다. Camera.Render 캡처에는 Overlay HUD가 포함되지 않는다.
- **[s2-03-f3] — 독립 재실행 통과:** CPU 광택61·마스크212·브러시68, 새 Play→Pause의 외형7·셰이더19(실제 Collider 2302표본, 채널 오차0)·집계/GPU/완료110(2판)·뷰 복원5조건 통과. 별도 새 Play의 수명74→Step2회→해제25조건에서 텍스처18개 해제를 확인했다. 광택 주파수별 최대 차이 `4.860759e-5`, CPU→GPU 양자화 오차 `0.001960747`. 자체 Editor/API 검사이며 실제 입력·Test Runner 실행은 아니다.
- **[s2-03-f4] — 빌드·보존 확인:** 최신 BuildReport를 독립 조회해 Windows64/Mono·비개발 Succeeded, 오류0·기존 패키지 경고485·128,729,201바이트를 확인했다. `Builds/Windows/Step2-S2-03-20261007/SqueakSqueak.exe`·Data·UnityPlayer 존재와 Git 제외 확인; 재빌드/플레이어 실행은 하지 않았다. 종료 후 Edit Mode·Dirty=false·컴파일/임포트 없음, 객체36·청소 컴포넌트18·Missing Script/참조 누락/잔여 마스크0, 기본 자세/Zoom2.4·1280×720 고정창·preloadedAssets0을 확인했다. Console은 시작 오류0/기존 Sentis 경고25 → 종료 오류/경고0이며 수동 삭제하지 않았다. 검토 실행 후 작업 트리는 깨끗했고 원본 Coin_Prototype·Data·Packages·ProjectSettings를 보존했다.
- **[s2-03-f5] — 후속 인계(차단 결함 아님):** S2-04 도구별 시각·소리 피드백으로 진행 가능하다. 실제 LMB/UI/MMB/F/Wheel·포커스·새 플레이어 실행/외부PC·성능·3~5분 실측 및 도구 시간 튜닝은 남아 있으며 `s1-06-f2` 보류를 유지한다. 이번 독립 검토의 직접 수정은 기록·현재 상태 문서에 한정한다.
