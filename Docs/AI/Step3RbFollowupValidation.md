# Rb 수동 테스트 후속 수정·검증

2026-10-17. 원본 `Docs/Step3Rb-수동테스트_2610171034.md`와 기존 기록/사용자 변경 보존. 사용자 요청으로 후속 수정·검토 결과를 로컬 커밋에 포함한다. push는 하지 않는다. 문지르기 속도 로직·수치 변경은 범위 밖이다.

## 최종 결과

- 스캐너 반대면 원 노출과 UI 밖 MMB 시작을 수정했다. 제품 변경은 CoinSurfacePointer/CleaningExperimentController 두 파일이며 새 직렬화 참조·씬·머티리얼·도구/속도 자산 변경은 없다.
- 실제 카메라32조건, 격리된 가상 입력→실제 Play 프레임37조건, 기존 단계140·리소스·문지름 씬171·도구64·Rb30·데이터122·CPU7238 회귀 통과. 실패한 하네스 조건과 정정 근거는 아래 보존했다.
- 새 빌드: `Builds/Windows/Step3Rb-20261017-scanner-rotation/SqueakSqueak.exe`. Windows64/Mono/BuildOptions.None, **Succeeded·오류0/경고485·31.7108초·129,583,057바이트**. exe/UnityPlayer.dll/MonoBleedingEdge/Data 존재 확인. 출력 폴더 전체를 유지한다.
- 새 exe 실행·물리 마우스/트랙패드·실제 AltTab·청취/성능은 미실행. Editor GameView 포커스 이탈/가상 장치 검사를 OS 창 입력의 통과로 확대하지 않는다. 기존10/17 사용자 검사는 수정 전 exe의 결과다.
- 최종 Edit/CleaningRoom Dirty=false/루트10. 가상 장치·device filter·probe·runInBackground 복원. 수정본 사용자 재검사는 대기다.

## 기준선·재현

- main/4a22ce3, 시작 Git 변경은 사용자 수동 기록 신규 파일뿐. tracked Assets/Packages/ProjectSettings261개 SHA-256 기준선을 보관했다.
- Unity6000.3.23f1/URP17.3.0/InputSystem1.20.0, 프로젝트 경로 일치. 시작 Edit·컴파일/임포트 없음·CleaningRoom Dirty=false/루트10. Console 기존 MCP 실행파일 서명 수집 경고를 제품 오류와 구분하며 지우지 않았다.
- 새 Play에서 M1/스캐너, 동전 각도0/70/110/180도를 공개 Camera.Render로 캡처했다(`.utmp/step3-rb-followup/before-*.png`). 70/110도에서 반대면의 원이 동전 실루엣 밖으로 드러남을 확인했다. LineRenderer는 깊이 검사/ZWrite1인 Opaque 재질이지만, 반대면 자체의 표시 허용을 판정하지 않는다. 모든 원을 ScannerEnabled만으로 켜는 것이 관통처럼 보이는 원인이다.
- MMB 시작은 CoinSurfacePointer.TryBeginRotation이 화면 위치에서 HasSurfaceHit를 요구한다. UI 밖 배경 시작을 거부하는 직접 원인이다.

## 수정·검증 진행

- 스캐너 원은 렌더링 카메라별로 실제 동전 면 법선·카메라 방향을 검사한다. 보이는 면의 미제거 A만 켜며 grazing은 숨긴다. 기존 깊이 검사/크기 축소/부모 비활성 해제를 유지한다. beginCameraRendering 구독은 OnDestroy에서 해제한다. 새 씬/에셋 참조는 없다.
- MMB 시작은 동전 적중 대신 UI 밖 Camera.pixelRect 안에서 허용한다. 포커스·활성 상태·새 MMB 눌림 및 확인창 입력 차단은 유지한다. 문지르기 승인/예산/속도 수치는 변경하지 않았다.
- `RunScanner()` 실제 카메라32조건 통과: 0/70/90/110/180도 면 표시, 반대면 원의 존재 여부가 픽셀에 영향0, 제거/남은 A 일치, 소유자 비활성/재활성, 스캐너 Off, B/P/타이머 불변. before/after70·110도 이미지를 직접 비교해 잘못 보이던 원이 사라지고 앞면 원은 유지됨을 확인했다. `.utmp/step3-rb-followup/after-*.png`.
- 가상 Mouse/Keyboard → 실제 Play 프레임의 첫 MMB 검사33조건 통과: 배경 시작/이동, UI 시작 거부, UI에서 누른 채 배경 진입 거부, 확인창 중단/취소 뒤 Hold 재시작 거부, F, 화면 밖 거부, LMB 동시 청소0/타이머0. OS 마우스 검사는 아니다. Editor 배경에서 프레임이1에 정지해 Play의 runInBackground만 임시 true로 허용했고 false 복원했다. 검증 코드는 이후 자체 복원·8초 timeout·실제 GameView 포커스 이탈 검사도 추가했다.
- Rb 데이터122/기존 문지름 CPU7238 회귀 통과.
- 포커스 추가 검사 중 두 차례 하네스 실패도 보존한다: UI 시작 조건 실패 후 Canvas.ForceUpdateCanvases로 실제 레이아웃 좌표를 확정했다. 이후 화면 밖 조건이 실패했으며, 실제/가상 마우스를 동시에 허용한 Value 액션의 magnitude 경쟁 가능성을 확인했다(현재 InputSystem 소스의 conflict-resolution 계약). 검사 동안만 InputActionAsset.devices를 가상 Mouse/Keyboard로 격리하고 원래 필터를 복원했다. 제품 가드·바인딩·포커스 판정은 우회/변경하지 않았다.
- 최종 격리 검사 **37조건 통과**, 실제 GameView 포커스 이탈의 회전 취소 포함. 완료 직후 paused=true/background=false/원래 Mouse/deviceFilter 없음/probe 없음 확인. 실제 OS 마우스·새 exe 검사는 별도다.
- 기존 단계140·프레임 flush 리소스 일치·문지름 씬171·도구64 통과. 이어진 Rb30은 재활성 타이머 비교에서 실패했다. GameView 포커스가 실제 있는 경우 SetTimingPaused가 pause 경계까지 정상 합산하므로, ‘pause 전’ 경과 시간과 정확히 같아야 한다는 기존 검증 기준이 잘못됐다. 기준을 pause 직후로 옮겨 재개 시 보존을 검사했고 **새 Play30조건 통과**했다. 제품 타이머는 수정하지 않았다.

## 빌드·보존 확인

RunCommand의 최종 응답은 ‘Saved Edit CleaningRoom/fresh output required’ 경계 실패였으나, 동일 출력 경로에 새 파일이 생성됐으며 독립적인 최신 BuildReport에서 위 성공 결과를 확인했다. 출력 존재 후 명령 재진입 가능성은 있으나 원인을 확정하지 않는다. 덮어쓰기/빌드 재시도는 하지 않았다. 경고485는 기존 AI Inference/Sentis 패키지 셰이더 경고이며 패키지·검사를 우회하지 않았다.

InputSystem 빌드 hook이 preload1을 디스크에만 자동 추가(메모리0)해 그 블록만 기준선0으로 복원했다. 빌드 전261개 파일 해시와 최종 비교한 부수 변경은0이다. 공유 Coin_Surface Dirty=false/사용자 Front 썸네일 참조 유지. 글로벌 SaveAssets·씬 저장·사용자 원본 수정 없음. 생성된 새 검증 파일의 .meta도 함께 인계한다.

## 기존 속도 커널 통제 측정(변경 없음)

`MeasureExistingSpeed()`: 60Hz, 실제 M1 미처리 천 자산(반경.32/속도4.8/계수.5), Uniform B1/res128, 동일1m 경로1회와 동일4초 왕복을 구분했다. 물리 마우스/트랙패드 입력 측정이 아니다.

| world/s | 1회 유효초 | 1회 중심 제거 | 1회 앞면 제거율 | 4초 왕복 유효초 | 4초 앞면 제거율 |
| --- | --- | --- | --- | --- | --- |
| .3 | 1.666667 | 1.000000 | .125053 | 2.000000 | .139086 |
| .6 | 1.666665 | 1.000000 | .125052 | 3.999996 | .176152 |
| 1.2 | .833333 | .681650 | .068140 | 4.000000 | .174786 |
| 2.4 | .416667 | .340824 | .034070 | 4.000000 | .176028 |

4초 왕복 중심 제거는 모두1이었다. 같은 경로 한 번의 빠른 통과는 체류 시간/상한 예산이 줄지만, 같은 시간 반복의 결과는 다르다. 이 값으로 트랙패드 우위나 가속도 모델 필요성을 확정하지 않는다. root의 별도 속도 조사/설계 판단 범위다.

## 2026-10-17 재검사·테스트 종료

사용자가 수정본 재검사에서 스캐너(1)·UI 밖 MMB 회전(2)은 정상이라고 보고했다. MMB 중 LMB 청소 차단, LMB 청소 중 MMB 진입 시 청소 중단·회전 전환도 확인했다. 이는 회전 우선 입력 계약에 맞는다. 사용자의 ‘테스트는 마무리하고 다음 스텝’ 지시에 따라 이번 Rb 수동 검사를 종료한다.

새 빌드의 Alt+Tab 복귀·F 초기화·스포이드 개별 동시 입력·16개 초과 보관은 별도 확인 보고가 없어 사용자 통과로 추가하지 않는다. 기존 개발 검사 증거는 유지하고 잔여 항목은 후속 통합 검사로 이월한다. 속도 튜닝·트랙패드 원인·외부 체험·성능은 이월이며 이번 종료를 전체 품질/재미 검증 완료로 해석하지 않는다.
