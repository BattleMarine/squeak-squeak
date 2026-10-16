# Step3 Rb 구현·검증 결과

2026-10-16 · 정본: [Ra 설계](../Design/Step3Ra-CleaningExperiment.md). **Rb-0~3 구현과 데이터/씬 API·카메라·Windows 빌드 검증을 마쳤다. 수동 테스트는 아직 미실행이며 실제 마우스·Player 실행·청취·성능·Rc 체험은 미검증**이다. 전체 체험 통과로 판정하지 않는다. 사용자 요청으로 구현·검토 결과를 로컬 커밋에 포함하며 push는 하지 않는다. 상위 문서는 오케스트레이션이 관리한다.

## 현재 결과·실행

- Windows x64/Mono 비개발 빌드: `Builds/Windows/Step3Rb-20261016-experiment/SqueakSqueak.exe`. 출력 폴더 전체를 유지한다.
- BuildReport: **Succeeded, 오류0/경고485, 40.289초, 129,582,545바이트**. 기존 AI Inference/Sentis 패키지 셰이더 경고다. RunCommand는 경고를 실패로 포장했으나 최신 BuildReport 재조회와 exe/UnityPlayer.dll/MonoBleedingEdge/Data 존재로 성공을 확인했다. 패키지·설정을 바꿔 경고를 숨기지 않았다.
- Editor: `Assets/Scenes/CleaningRoom.unity` → Play → 화면의 `실험 M1/M2` 버튼. `Squeak Squeak > Experiment > Start M1/M2 (Play Only)`도 같은 경로다. 작업 중이면 폐기 확인이 먼저 나온다. 정의 생성 메뉴는 재실행하지 않는다.
- 기존 모드는 stage-01/마른 천으로 시작하며 실험이 자동 적용되지 않는다. 실험 종료 후 기존 단계·도구 제한으로 복귀한다.
- 최종 Unity6000.3.23f1: Edit/컴파일·임포트 없음, CleaningRoom Dirty=false/루트10. 마지막 Play 종료 뒤 제품 오류·경고0. 빌드 경고 및 기존 Relay/계정 오류는 별도다.
- 인계 직전 읽기 재확인: Missing Script0, 실험 참조21/null0, 보관 상한16, 메모리 preload0, Console 오류0. 빌드 전225개 파일 해시와 비교해 복원되지 않은 빌드 부수 변경0이다.
- [수동 확인·Rc 기록](Step3RbManualTest.md)은 미실행으로 유지한다. 새 Player를 자동 실행하거나 기존 게임 창을 조작하지 않았다.

## 구현 경계

실험 정의8개와 독립 반응 프로필을 추가했다. 기존 enum0/1/2·3도구 자산·선호 속도·3단계·소유권은 보존했다. M1/M2는 같은 원판/양면 A4·고정 B 배치이며 반응과 색만 다르다. 풀림은 별도 bool 원본이고 B/광택을 재해석하지 않는다. 국소 도포는 제거·광택·게이지를 올리지 않으며 첫 유효 풀림만 작업 시간을 시작한다. B0/새 판은 풀림을 해제한다.

한 눌림 한 방울 gate·실제 잔여 B/A 외곽 스캐너·목표 정규화 게이지를 연결했다. 목표1회 알림 후 추가 청소·시간을 허용하고, 보관 확인 취소는 작업을 유지한다. 확정만 최신 상태의 불변 사본을 만든다. 보관은 최대16개의 **시험용 메모리**이며 종료 시 사라짐을 표시한다. 상한 초과는 기존 결과를 버리지 않는다. 디스크 저장·보상·경제·영구 전시·새 광택 기능은 없다.

마스크256²×양면 RGBAHalf의 GPU payload는 약1MiB다. 사본의 float4배열+bool2배열 payload는 약1.125MiB/개, 16개 약18MiB다(관리 객체·원본·업로드·렌더링 비용 제외, 실측 프로파일 아님).

## 실행한 검사

| 검사 | 결과·범위 |
| --- | --- |
| Rb 데이터 | 122조건. 반응12셀·커널·국소 풀림/중복0·클릭 gate·진행 경계/불량 값·불변 사본 |
| 기존 단계 데이터/Preview | 111조건 |
| 기존 마스크/솔/광택 | 각각212/86/69조건 |
| 기존 문지름 CPU/선호 속도 | 7238/12조건. 실제 입력 아님 |
| 기존 단계 Play API | 140조건. 6회 왕복·완료6·감상 중 재도전2 |
| 기존 문지름 씬/도구 잠금 | 171/64조건. 합성 공통 패킷·선택/잠금/복원/구독 |
| Rb Play API | 30조건. 진입·양면·스캐너·풀림·비활성 재개·목표1회·추가 청소·확인/취소·중복 거부·사본·M2 초기화·일반 복귀 |
| 게이지 geometry | 6조건. 0/50/100% 실제 Rect/CanvasRenderer 제출 Mesh 폭 |
| 기존 결과 중 실험 진입 | Showing/Moving/Stored 각4조건. 연출 취소·UI 비노출/복원 |
| 보관 상한/반복 | 47조건. Play 상한16→2로만 줄여 초과 거부·기존 사본/취소 상태 유지 후16 복원. M1/M2 전환20회 |

순수 검사는 `CleaningExperimentValidation.RunData()` 등 Editor 검증 클래스, 씬 검사는 새 CleaningRoom Play/Pause의 `RunPlay()`로 수행했다. 합성 상태·공개 커널 검사를 물리 입력 통과로 확대하지 않는다. 도구 지연으로 첫 프레임이 Showing→Stored를 건너뛰어 Moving 관찰 때만 Play 연출 시간을 .1/300초로 늘렸다. 실제 Update에서 Moving을 관찰한 뒤1.4/1.1초 복원했으며 제품 연출 소요 시간의 실측 검사는 아니다.

실제 프레임 Destroy flush 뒤 기존 리소스는 기준선과 일치했다(objects99/dirt7/StageDirt1/masks2/audio3/lines4/particles1). 실험20회 전환 뒤에도 실험 오염 루트1/A4·마스크2·AudioSource4(기존3+방울1)·LineRenderer8(기존4+외곽4)·outline material1·방울 clip1이었다. MPB 실제 양면 마스크 참조도 일치했다. 생성물 누적은 관찰되지 않았다.

## 카메라 증거

공개 Camera.Render API로1280×720 PNG8개를 `.utmp/step3-rb/`에 남기고 직접 열람했다.

- `m1-untreated.png`, `m1-softened.png`, `m1-scanner.png`
- `m1-progress50.png`, `m1-progress100.png`
- `m2-front.png`, `m2-back.png`, `m2-back-scanner.png`

한글·버튼 배치, M1/M2 색, 양면 A/B, 국소 풀림·도포 시0%, 잔여 스캐너, 게이지0/반폭/전폭을 확인했다. Canvas는 캡처 동안만 ScreenSpaceCamera로 바꾼 뒤 finally에서 Overlay/카메라/RenderTexture 참조를 복원했다. Reflection·네이티브 창 캡처·권한 거부 우회 없음. 실제 Overlay 창·클릭·청취 검사는 아니다.

## 기준선·해결 이력·보존

- 시작 main/30d2f84, 기존 Ra/상위 문서 변경 보존. tracked Assets/Packages/ProjectSettings225개 SHA-256을 비교했다. 기존 카탈로그·3도구 자산·머티리얼·패키지는 불변이다.
- 시작 Dirty=false/루트10. 실험 컴포넌트와21개 non-null 참조 연결 뒤 Dirty=true. 첫 저장은 실행 전 거부(failed/duration0)됐다. `This actio…` 일반 텍스트의 JSON 파싱 실패로 원문을 보존하지 못해 사용자/자동 승인 중 거부 사유는 **복구 불가**다. 재시도·우회 없이 조율했고 이후 사용자의 **‘CleaningRoom 저장과 후속 검증 허용’** 승인을 전달받았다.
- 저장 전 live/디스크 Preview JSON 참조를 경로/GUID로 정규화 비교: 기존262개+신규 컴포넌트1, 삭제0. 차이7개는 기존 LayoutGroup 배치와 일치하는 버튼3·텍스트4의 계산된 RectTransform뿐이었다(drivenByObject는 none). 다른 직렬화 값 일치. 해당 씬만 저장해 Dirty=false 확인. 파일 diff에는 그 레이아웃 캐시와 빈 값 공백 정규화도 포함된다.
- Play 중 코드 리로드 뒤 UI 잔존/RequestCoin 거부 세션이 있었다. 새 Play30조건은 반복 통과했다. 비직렬화 상태 소실 가설이 유력하나 확정하지 않았다. 당시 InputActionMap 오류(15:58:32)는 새 Play에서 재발하지 않았다. **코드는 Edit에서 갱신하고 새 Play로 검사한다.** Hot reload 안전성은 보장하지 않는다.
- Canvas 선파괴 때 View.SetSuspended가 내던 MissingReferenceException은 파괴된 루트를 종료 처리에서 건너뛰도록 최소 수정했다. 이후 Play 종료에서 재발하지 않았다. 신규 geometry 검사의 Unity6 GetMesh 시그니처 컴파일 오류도 수정 후6조건 통과했다.
- 빌드는 InputSystem preload1을 디스크에만 자동 추가했다(메모리0). SetPreloadedAssets(empty)는 디스크를 복원하지 않아 해당 블록만 최소 패치했다. ProjectSettings.asset SHA-256은 기준선 `CD1C9897…A4B867`과 다시 일치한다. 글로벌 SaveAssets/다른 설정 저장 없음.
- 공유 Coin_Surface Dirty=false/Front=`2DSpriteLitTemplateThumbnail`, SHA-256 `7F41BE54…111D32` 기준선·빌드 전후 동일. 사용자 썸네일을 보존했다. 빌드 전후225개 중 자동 변경은 위 preload뿐이며 복원했다. 기존 광택 체감(S3-C02)은 해결로 간주하지 않는다.

## 남은 확인

실제 LMB 드래그/정지·한 클릭·UI/포커스/MMB/Wheel/F 차단·재개, 원래 Overlay/새 Windows Player, 도구별 청취·중첩·음질, 완료 시간·성능·외부PC·외부 비개발자 체험은 미실행이다. Rc의 재미/수치 채택, 영구 저장(S3-03)과 기존 광택 체감은 별도다. [수동 확인 문서](Step3RbManualTest.md)에 실제 결과를 기록한 뒤 판정한다.
