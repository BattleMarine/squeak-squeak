# S2-01a — 동전 표면 좌표 계약

확정: 2026-10-07. 기준: `d0380ee`, [Step 2 지시서](../Step2-구현지시서.md) §3.1.
이 문서는 S2-01b 구현을 위한 계약이다. **런타임 매핑·커서 이관·마스크는 아직 구현하지 않았다.**

## 범위와 소유자

- 기본안을 채택한다: 앞/뒤 독립 로컬 XZ 평면, 옆면은 외형 전용. Cylinder의 UV·triangleIndex를 사용하지 않는다. Convex Collider의 textureCoord나 삼각형 번호는 Renderer UV 계약으로 간주하지 않는다.
- 좌표 기준은 **`coinCollider.transform` (`Coin/CoinBody`)**이다. 회전 대상 `coinTransform` (`Coin`)이나 월드 XZ를 기준으로 삼지 않는다.
- S2-01b에서 `SqueakSqueak.Cleaning`의 작은 무상태 매핑 코드로 구현한다. 별도 Manager·입력 루프·상태 원본·패키지는 추가하지 않는다. 기존 Pointer의 가장 가까운 적중·UI/MMB 차단 경로를 소비한다.
- 입력: 명시적으로 연결된 대상 MeshCollider, 그 Collider의 실제 RaycastHit, 현재 localToWorldMatrix, 검증된 공유 Mesh의 local bounds. 출력: 면 ID·정규화 좌표·투영 접촉점·표면 월드 법선. 입력 원본과 출력은 읽기 전용이며 매핑 자체는 청소·재질·Transform을 변경하지 않는다.
- 제안 API: `TryMapHit(MeshCollider target, RaycastHit hit, out CoinSurfaceContact contact)`. contact는 읽기 전용 `Face`, `IsCleanable`(Front/Back만 true), `Uv`, `LocalPosition`, `WorldPosition`, `WorldNormal`을 제공한다. false면 contact는 Invalid/default; true라도 Side는 청소 불가다. Side의 Uv/LocalPosition은 default로 두고 WorldPosition/WorldNormal은 실제 적중값을 유지하되, 소비자는 `IsCleanable` 확인 전 쓰기용 좌표를 읽지 않는다. 접촉 생성은 매핑 경로로 제한한다.

## 면·좌표·텍셀

| 면 ID | 로컬 법선/평면 | B·광택 | 마스크 |
| --- | --- | --- | --- |
| Invalid=0 | 입력/설정 실패 | 불가 | 없음; default가 앞면이 되지 않음 |
| Front=1 | +Y / `c.y+h` | 가능 | Front 전용 |
| Back=2 | -Y / `c.y-h` | 가능 | Back 전용 |
| Side=3 | 측면·모서리의 비평면 법선 | 불가 | 없음; 집계 제외 |

- `c = sharedMesh.bounds.center`, `r = bounds.extents.x`, `h = bounds.extents.y`. X/Z 반경이 같은 원판형 평면 캡 Mesh만 지원한다. bounds만으로 임의 Mesh를 원판으로 자동 인정하지 않는다. 현재 builtin Cylinder를 최초 지원 대상으로 검증하며 Mesh 교체 시 다시 확인한다.
- 저장 씬은 Cylinder sharedMesh fileID 10206을 Renderer/Convex MeshCollider가 공유하고 CoinBody scale은 `(2,0.1,2)`이다. local `c=(0,0,0), r=0.5, h=1`은 S2-00 에디터 조회 근거다. **이번 a에서는 에디터 재조회하지 못했다.**
- `pLocal = M.inverse.MultiplyPoint3x4(hit.point)`, `q = ((pLocal.x-c.x)/r, (pLocal.z-c.z)/r)`, `uv = (q+1)/2`. q는 원판 반경 1, uv는 `[0,1]²`이며 중심은 `(0.5,0.5)`다. 양쪽 면 모두 U=+X, V=+Z이다. **Back에서 U/V를 뒤집지 않는다.** 뒷면을 직접 바라볼 때 보이는 좌우 반전은 자세에 따른 자연스러운 결과이며 셰이더·커서도 같은 축을 따른다.
- `q.x²+q.y²<=1`이 유효 원판이다. 대상 밖 입력을 먼저 Clamp01하여 유효 영역으로 옮기지 않는다. 단, 부동소수 허용분 `1 < q² <= 1+1e-5`만 q를 단위원으로 투영한다. `q²>1+1e-5`는 Invalid다.
- Front/Back 선택 후 LocalPosition은 `(c.x+r*q.x, c.y±h, c.z+r*q.y)`로 투영하고 WorldPosition은 `M.MultiplyPoint3x4(LocalPosition)`으로 복원한다. 실제 hit.point는 필요하면 진단용으로 따로 보존한다. Side의 default 좌표를 쓰기용 UV/투영 접촉점으로 해석하지 않는다.
- 후속 마스크 해상도 N에서 텍셀 중심 `uv=((i+0.5)/N,(j+0.5)/N)`, `index=j*N+i` (V=0 행부터). 원판 밖 텍셀은 쓰기·집계에서 제외한다. UV=1의 인덱스만 N-1로 제한하며 wrap/repeat·반대면 인덱스 공유를 하지 않는다. 텍스처 생성·가중 집계는 S2-02 범위다.

## 법선 변환과 면 판별

1. 대상이 null/비활성·Collider 미적중·`hit.collider!=target`·필수 Mesh 누락이면 Invalid다. target sharedMesh와 Renderer sharedMesh 일치는 설정 검증에서 확인한다. 포인터의 가장 가까운 적중을 건너뛰는 Collider 전용 Raycast를 게임플레이 경로에 추가하지 않는다.
2. point/normal/행렬/bounds가 NaN·Infinity이거나 normal.sqrMagnitude가 `<=1e-12`이면 Invalid다. r/h는 유한한 양수이고 X/Z extents 차이는 `<=1e-5*r`여야 한다. 아래 스케일 지원 범위를 검사한 뒤 역행렬을 계산한다. 결과도 유한값인지 검사한다.
3. **world→local 법선:** `nLocal = normalize(M.transpose.MultiplyVector(hit.normal))`. **local→world 법선:** `nWorld = normalize(M.inverse.transpose.MultiplyVector(nLocal))`. normal에 InverseTransformDirection이나 점용 역행렬을 쓰지 않는다. 변위 벡터는 아래 거리식처럼 M을 사용한다.
4. 평면 위치 오차 `epsilonY=1e-3*h`와 `abs(nLocal.y)>=0.999`를 함께 검사한다. +Y 법선 및 `abs(pLocal.y-(c.y+h))<=epsilonY`이면 Front; -Y 법선 및 `abs(pLocal.y-(c.y-h))<=epsilonY`이면 Back. 법선 부호와 평면 위치가 불일치하면 Invalid다. Y 부호만으로 면을 선택하지 않는다.
5. `abs(nLocal.y)<0.999`이고 bounds 높이 범위 및 원판 반경 허용분 안이면 Side다. cap-normal인데 어느 캡에도 닿지 않거나 bounds 밖이면 Invalid다. 캡/옆면 모서리의 hit.normal이 측면이면 청소 불가로 보수적으로 처리한다. 경사/둥근 베벨은 지원 캡이 아니다.
6. 쓰기용 월드 법선은 판별된 `±Vector3.up`의 역전치 변환으로 통일한다. 정규화된 hit.normal은 면 판별에만 사용한다. 커서는 동일 투영점·캡 법선을 사용해 시각 표시와 청소 평면을 맞춘다.

## 스케일·월드 브러시

- M의 변환된 local 단위축 `bx=M.MultiplyVector(right)`, `by=M.MultiplyVector(up)`, `bz=M.MultiplyVector(forward)`를 쓴다. 길이 `sx,sy,sz`는 부모까지 포함한 유효 배율이며 Transform.localScale만 읽지 않는다.
- 지원: 이동·회전 및 축들이 직교하는 비균일 스케일. 각 길이는 `[1e-4,1e4]`, 최대/최소 길이 비율은 `<=1e4`; 정규화한 축의 쌍별 `abs(dot)<=1e-4`, 정규화 삼중곱 `dot(cross(bx/sx,by/sy),bz/sz)>0.9999`다. 실제 `(2,0.1,2)`는 지원한다. X/Z 배율이 달라도 로컬 원판→월드 타원을 지원한다.
- 실패: 0/준0 축, 비유한 값, 위 수치 범위 밖, 반사(음의 handedness), 부모 비균일 스케일과 자식 회전으로 생기는 shear. 음수 localScale을 분해해 추정하지 않으며 **최종 행렬**이 proper rotation+positive scale과 동등한 경우는 지원한다(두 축 음수는 회전과 동등할 수 있음). 실패 시 청소 불가·이력 초기화, 설정 진단은 상태 변화 때만 내고 매 프레임 로그를 쌓지 않는다.
- 유한한 양수 R=`CleaningToolDefinition.BrushRadius`는 **월드 단위**다. 0/음수/NaN/Infinity 반경은 쓰기 불가다. 현재 마른 천 .32·솔 .18·광택천 .28은 유지한다.
- 같은 Face의 후보 q와 접촉 중심 q0에 대해 `deltaLocal=(r*(q.x-q0.x),0,r*(q.y-q0.y))`, `distanceSquared=M.MultiplyVector(deltaLocal).sqrMagnitude`. `distanceSquared<=R²`인 유효 원판 후보만 브러시 안이다. local 3D 거리나 화면 픽셀 거리, 평균 scale로 나눈 반경을 사용하지 않는다.
- 직교 지원 범위에서 동등식은 `(r*sx*deltaQ.x)²+(r*sz*deltaQ.y)²<=R²`. UV 후보 경계 상자는 `halfU=R/(2*r*sx)`, `halfV=R/(2*r*sz)`이며 원판/텍셀 범위로 잘라도 **최종 거리식**을 생략하지 않는다. 현재 halfU/halfV는 .16/.16, .09/.09, .14/.14다.
- 커서 링은 이 월드 접평면에 반경 R의 원으로 그린다. 옆면·Invalid는 최소안으로 중심/링을 모두 숨겨 사용 불가를 표시한다. 이는 b에서 연결할 동작이지 현재 씬의 구현 상태가 아니다. A 청소 규칙은 변경하지 않는다.

## 반대면 차단·통합 경계

- 한 접촉에는 정확히 한 Face만 있다. Front/Back 각각 배열·텍스처를 보유하고 면 ID로 해당 채널을 선택한다. 후보 위치가 가까워도 다른 Face의 후보는 **거리 계산 전** 거부한다. Side/Invalid는 양쪽 B/광택 쓰기와 집계에 참여하지 않는다.
- b에서 Pointer의 hover 적중→매핑→커서 표시 경로를 연결한다. 기존 A 소비자용 RaycastHit 경로와 차단 상태는 보존하며 B용 `TryGetToolUseContact` 같은 최소 경로는 기존 `TryGetToolUseSurfaceHit` 성공을 전제로 한다. hover 가능과 LMB 청소 가능을 구분한다. 매핑 성공 자체가 도구 사용 승인이 아니다.
- b는 좌표·커서만 구현/검증한다. **기존 전체 B 감소는 S2-02에서 교체한다.** 옆면 청소 불가 계약을 b에서 B 런타임 전체에 적용했다고 보고하지 않는다. S2-02의 마스크 접촉 이력은 동전 밖/UI/MMB/포커스/도구·Face 변경/재시작에서 끊고 프레임 시간을 표본별로 나눈다.
- sharedMesh bounds/설정은 초기화·검증 시 읽고 현재 M은 사용 시 갱신한다. 회전 후 오래된 M을 캐시하거나 Editor-only API를 런타임 매핑에 넣지 않는다. 기존 입력·완료·Reset·시리얼라이즈 필드는 b에서 필요한 최소 추가만 하며 저장 참조를 유지한다.

## S2-01b 실행 인계

1. MCP 연결·프로젝트·Edit Mode·Dirty·Console 재확인. 대상 Mesh/Collider bounds·실제 cap/side 법선을 다시 읽고 위 허용값이 맞는지 확인한다. 맞지 않으면 근거를 기록한 뒤 계약을 조정한다.
2. 무상태 매핑 및 Pointer/커서 연결을 구현하고 API 단위 검증. Front/Back 중심·편심점·rim·Side, 다른 Collider·영/비유한 법선·평면 불일치·범위 밖·잘못된 scale·반경을 확인한다.
3. Play Mode 실제 Physics Raycast로 0/45/75/180°·X/Z 복합 회전·Zoom 1.6/3.2를 검사한다. 화면점→적중→로컬 좌표→월드 표시의 오차를 기록하고 마른 천/솔/광택천의 링 반경을 확인한다. 예상 좌표는 주어진 local 점에서 만든 독립 기준과 대조한다. 카메라에서 보이지 않는 면을 직접 Collider Raycast한 결과를 가시성 통과로 쓰지 않는다.
4. 같은 면 거리 경계 안/정확/밖과 반대면 거부, X/Z 배율이 다른 지원 행렬 및 shear/반사 거부를 확인한다. 기본 씬과 Inspector 참조를 저장·재조회하고 Console 검사 후 **b 별도 커밋**한다. 물리 마우스 전체 루프 `s1-06-f2`는 보류 유지한다.

## a 검증 한계

독립 PowerShell 수치 계산 최종 결과: **76개 통과, 0개 실패**, 비교 오차 `1e-10` (double). 초기 임시 검증 스크립트의 배열 산술 괄호 오류를 수정한 뒤 전체를 재실행했다. 검증용 스크립트는 Git 제외 임시 경로에서 사용했으며 런타임 구현물이 아니다.

| 계약 계산 | 검사 수 | 기대·관찰 |
| --- | --- | --- |
| 면·잘못된 입력 | 11 | Front/Back/Side·림 법선 구분, 법선 부호/위치 불일치·내부점 cap-normal·원판 밖·영법선·NaN/Infinity는 Invalid |
| 앞뒤 UV·역변환 | 8 | local `(0.2,±1,-0.15)` → UV `(0.7,0.35)`; 양쪽 같은 축·XZ 복원 |
| 반경·거리 경계 | 39 | R .32/.18/.28, `(sx,sz)=(2,2)/(3,1)`에서 X/Z 경계 거리=R, 경계의 99.9% 안·100.1% 밖 |
| 법선 수식 | 3 | `(sx,sy)=(2,.1)`·혼합 법선에서 역전치/전치 왕복 복원; 점용 역행렬은 불일치 |
| 지원 스케일 | 9 | 현재 배율·상이한 X/Z·두 축 부호 회전 동등은 허용, 0/shear/반사/준0/과도한 비율/NaN은 거부 |
| 쓰기 면 선택 | 6 | 같은 Front 또는 Back만 허용; 반대면/Side/Invalid 후보 거부 |

현재 Git·저장 씬·코드는 파일로 확인했다. Unity MCP `GetProjectRoot`는 미응답으로 읽기 묶음을 취소했고 `Get-Process -Name Unity`도 결과가 없었다. 연결·활성 씬·Console·실제 Collider 좌표를 이번 실행에서 확인했다고 주장하지 않는다. a의 수치 검증은 계약 수식의 독립 계산이며 Unity 구현·Play Mode·빌드를 대체하지 않는다.

설치된 실행 파일은 `C:/Program Files/Unity/Hub/Editor/6000.3.23f1/Editor/Unity.exe`다. Test-Path=true, 파일 ProductVersion=`6000.3.23f1_09d2ecc7fb28`을 읽기로 확인했다. b에서 사용자 작업과 충돌이 없음을 다시 확인한 뒤 필요하면 이 실행 파일과 프로젝트 경로로 에디터를 시작할 수 있다. 이번 a에서는 실행하지 않았다.
