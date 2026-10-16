# Step 3 문지르기·단계 실행 검증 안내

2026-10-13. 현재 입력 규칙은 LMB를 누르고 **실제로 이동할 때만** 청소한다. 과거 Step1/2 Hold 검증 기록은 당시의 규칙으로 보존한다.

## 현재 설정·검사

- CleaningRoom의 CoinSurfacePointer: `minimumRubDistance=.001` 월드 단위, `referenceRubSpeed=.6` 월드 단위/초. 이동량/기준 속도와 이동 프레임 시간 중 작은 예산을 A/B/광택이 공유한다. 느린 이동은 누적하되 정지 시간은 제외한다.
- 최초 클릭·정지·정지 재클릭은0. UI·표면 이탈·포커스·도구/판 교체·MMB/Wheel/F에서 기준을 끊고 재진입 첫 표본은0이다. 읽기는 비소모성, A/B 적용은 소유자별 같은 프레임1회다. 기존 도구 선호 속도·반경은 그대로다.
- Edit: `CleaningRubValidation.RunData()`(7238 프레임/조건 검사), 브러시86·광택69·실제 도구 Hz12조건 통과. 최대 텍셀0.300904%p, A0.086806%p, 적용 예산0.00069444초 차이. CPU 수치는 실제 플레이 완료 시간/성능을 인증하지 않는다.
- 새 Play/Pause: `CleaningStageValidation.RunPlay()`134조건→Unity 실제1프레임→`VerifyResources()`. `CleaningRubValidation.RunPlay()`171조건은 승인 프레임 패킷을 합성해 실제 A/B/광택·타이머 소비 경로를 검사한다. 포인터의 raw 입력 gate 검사는 아니다.
- 새 Play/Pause: `CleaningRubInputValidation.Begin()`은 별도 가상 Mouse/Keyboard와 실제 프레임으로 실제 Input Action/포인터/UI/MMB/Wheel/F 경로를 검사한다. `Result`를 읽는다. 이번 실행은 GameView 포커스 상실로 **BLOCKED**이며 통과로 표시하지 않는다. 포커스 값을 우회하지 않았고 가상 장치/임시 probe/구독은 정리됐다. 중단은 `Cancel()`, Play 종료·assembly reload에서도 정리한다.

## 사용자 수동 확인

### Editor 전체 도구 검사 (이번 Play/판만)

일반 시작은 stage-01·마른 천만 허용한다. 작은 솔/광택천은 비활성화되고 `잠김 · 미보유 도구`를 표시한다. 소유했더라도 현재 단계가 허용하지 않으면 `잠김 · 이 단계 사용 불가`다. 상점/구매 기능을 추가했다는 뜻은 아니다.

- CleaningRoom Play의 청소 상태에서 메뉴 `Squeak Squeak > Testing > Tools > Enable All Tools (This Play Only)`를 실행하면 현재 판의 세 도구를 검사할 수 있다. 현재 청소 수치·판·단계 목표는 그대로이고 정식 데이터·소유권·저장은 변경하지 않는다.
- `Restore Normal Tool Limits`로 원래 목록/잠금 사유를 복원한다. 단계/새 판 전환 또는 Stop→새 Play에서는 정상 제한이 다시 적용된다. Editor 폴더의 코드만 제공하므로 Player에는 이 메뉴가 없다.
- 검사: 새 Play/Pause에서 `CleaningToolFollowupValidation.Run()`64조건. 1280×720 Canvas 렌더는 `CaptureUi(".utmp/s302-followup-ui.png")`; Overlay 설정을 임시 전환·복원한다. 이 검사 이후 단계 자원 검사를 이어갈 때는 먼저 실제 프레임을 진행해 지연 Destroy를 비워야 한다. 또는 별도 새 Play에서 `CleaningStageValidation.RunPlay()`140조건→실제1프레임→`VerifyResources()`를 실행한다.
- 신규 단계 먼지는 기존 FixedDirt와 동일하게 Cast Shadows=Off로 생성된다. 실제 메인 카메라 전후 비교에서 동전 좌상단의 떠다니는 검은 원2개가 사라졌으며 동전/A 자체는 유지된다. 이 후속 변경은 기존 빌드에 포함되지 않았다(이번에는 빌드하지 않음).

새 빌드는 `Builds/Windows/Step3-20261013-stage-rub/SqueakSqueak.exe`이며 출력 폴더 전체를 유지한다. 초기 stage-01만 출시 경로로 연결됐다. stage-02/03 진입은 후속 진행/구매/UI 작업 전에는 개발 검사 API만 사용한다. 보상·저장·상점 기능은 아직 없다.

1. 새 판의 동전 위에서 좌클릭을 누른 채10초 정지한다. A/B/광택이 변하지 않고 청소 타이머도 시작되지 않아야 한다. 같은 곳을 재클릭해도 동일하다.
2. 좌클릭을 누르고 큰 먼지/얼룩 위를 문지른다. 실제 변화와 타이머 시작을 확인한다. 멈춘 뒤 누르고 있어도 수치가 더 변하지 않아야 한다. 아주 느린 문지르기도 누적해 반응해야 한다.
3. UI 위/동전 밖으로 나갔다가 돌아오거나 MMB/Wheel/F를 사용한다. 이동 조작 자체로 청소되지 않고, 다시 실제로 문지를 때 재개돼야 한다. F는 기존 오염·시간을 초기화하지 않는다.
4. 완료 후 `새 청소 시작`을 눌러 초기 A2개·B/광택·목표·마른 천·시간·자세·줌이 복원되는지 확인한다. 이번 초기 단계에서 잠긴 도구 버튼을 눌러도 실제 선택이 바뀌면 안 된다.

이 수동 확인·실제 소리·시각 만족감·Player 성능은 아직 미검증이다. 결과는 Step3 체크리스트에 별도 기록하며 CPU/API 통과로 대체하지 않는다.
