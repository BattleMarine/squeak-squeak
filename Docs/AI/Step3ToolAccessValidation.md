# S3-05 도구 접근 정책·제한 완료 조건 대조

2026-10-21. 구현 담당 산출물. 상위 지시서/체크리스트·최종 검토·커밋은 root 소유다.

## 범위·기준선

- main/03a9040(ahead3), 시작 작업 트리는 사용자 `_Recovery` 씬/meta만 untracked다. 기존 일반 모드의 소유∩단계 허용과 별도 실험3도구를 유지한다.05a는 접근 정책 확인이며 상점/구매·새 획득·재화·보상·새 해금을 구현하지 않는다.06/UI 확장도 미착수다.
- AGENTS/README/Step3 지시서·체크리스트/RcDecision과 기존02/Rb/03~04 증거·관련 소유자/선택/UI/청소 코드를 대조했다. 판정은 최신 Rc/사용자 승인 범위가 종전 구매 계획보다 우선한다. 현재 잠긴 도구를 구매/획득할 제품 경로가 없다는 것은 의도된 보류이며 구현 완료라고 바꾸지 않는다.
- 정식 Unity MCP 조회: 프로젝트 경로 일치, Unity6000.3.23f1/Edit/Play=false/Pause=false/컴파일·임포트 없음, 원본 CleaningRoom Dirty=false/루트10. 실제 조회·기존 증거 재사용과 새 동작 검사를 구분한다.
- 시작 hash: CleaningRoom `9CC4903988C0CED294B9F42998EE287AFBC6DF73BA5A8E039BB4CA624080A1FB`, ProjectSettings `CD1C9897971C2FB8F27628F2385CE98DA48C3927D7C629E94078F36C50A4B867`. Recovery 씬 `5C1611F5CEB209ED53CC178DF3C0B8C00CA0751948353922D6B9D7E3DF55F7C2`, 파일meta `670B2E3704FD70D13F346A0B5D44AD528EA52DED18C7862855D6D3935491DE0B`, 폴더meta `7FF77923B325F3813BDB763CF40A3A8210AC982B5272169EC83CBBF165D99A99`. 보존·커밋 제외.

## 조건별 코드·기존 증거 매핑

| 조건 | 현재 코드와 책임 | 재사용하는 실행 증거 | 판정/한계 |
| --- | --- | --- | --- |
| 05a 일반 소유 제한 | `Progression/StageAccessContext.cs`의 Owns/Validate/CanEnter, `CleaningStageController.TryStartStage`가 소유∩해당 단계 허용만 ConfigureAvailableTools에 전달 | `CleaningStageValidation.RunPlay`140조건의 초기1도구·직접 선택 거부·전제 없는 다음 단계 거부/6회 단계 왕복. [03b 기록](Step3SaveValidation.md)의 재통과·자원 확인 | 기존 구현/개발 검증 충족. 개발 접근 fixture는 제품 소유/해금으로 보지 않음 |
| 05a 실험3도구와 적합성 분리 | `CleaningExperimentController.StartCoin`은 실험 정의의3도구만 구성. `CleaningReactionProfile`의 효과/무효 계수는 선택 잠금과 별도 | [Rb 기록](Step3RbValidation.md)의 반응122조건, [04b 기록](Step3ResultIntegrationValidation.md)의 실험3도구 접근/실제 도포·일반 파일/소유 불변 | 선택 가능해도 모든 표면에 유효한 것은 아님. M1 천 미처리 B=.5, M1/M2 칫솔 미처리 B=0 유지 |
| 05a 일반 진행 오염 방지 | `CleaningExperimentPersistence.CaptureProgress`는 legacy StageAccessContext 소유/완료/선택 ID만 캡처. 결과와 일반 진행은 별도 저장소 | 04b Editor/Windows 각각 seed53/reload23에서 일반 파일128B·DTO·메모리 소유/완료·다음 단계 제한 불변 | 충족. 새 획득/상점/보상 정책은 해당 없음(보류) |
| 05b UI와 직접 선택의 동일 제한 | `CleaningToolButton.RefreshVisual`은 IsAvailable로 interactable/사유 표시, SelectTool은 TrySelect 호출. `CleaningToolSelection.TrySelect`는 미허용 참조/ID 거부, CurrentTool은 private setter | `CleaningToolFollowupValidation.Run`64조건: 잠금·미보유/단계 제한 사유·직접 선택 거부·구독/목록 갱신. [문지르기 기록](Step3RubbingValidation.md) 및03b/ Rb 재통과 | UI만 잠그는 구조가 아님. 물리 hit test/마우스 클릭 체험과 API 검사는 구분 |
| 05b 실제 청소가 허용된 선택만 사용 | `ObjectDirtCleaner.ApplyAtPoint`와 `CoinSurfaceContamination.ApplyRubSample`은 동일 selection.CurrentTool의 반경·제거/광택량을 읽음. 청소 API에 별도 도구 인자를 받지 않음. ApplyDroplet도 현재 선택의 IsDropper/반응/ProcessingAllowed 검사 | 기존 문지르기 Scene API171·브러시/단계의 실제 A 적중 검사와 선택 거부 증거를 결합. 아래 빠진 경계4조건 추가 통과 | 실패한 잠금 선택이 다른 도구 효과를 남기지 않는 A 경계를 직접 보강. B/P는 현재 선택 읽기 코드·기존 커널/상태 검사를 대조하며 새 물리 B 드래그라고 보고하지 않음 |
| 05b 유효 기본 선택 | ConfigureAvailableTools는 비어 있지 않은 중복 없는 도구 사본, 첫 허용 도구를 default로 정하고 ResetToDefaultTool. 단계 준비 중 입력 차단 후 구성·초기화 완료 뒤 재개 | Stage RunPlay140의 각 단계 허용 목록/마른 천 기본·시간 초기화, ToolFollowup64의 동일 기본일 때 AvailableToolsChanged·새 판 정상 복귀 | 충족. 예외/빈 목록은 조용히 잠금 우회하지 않음 |
| 05b 모드·단계·로드 후 제한 | 실험 Leave는 원래 일반 Stage 재적용, 저장 로드는 검증된 StageAccessContext로 TryInitializeFromAccess, 열람 Stored/Viewing은 선택·청소/도포 차단 | 단계140/도구64,04a Editor/Windows50+13,04b Editor/Windows53+23의 정상 복귀·재실행 잠긴 도구 API 거부/관찰 차단 | 기존 증거 충족. 그대로 다시 실행하지 않음 |
| Editor 개발 전체 도구 우회가 제품으로 남지 않음 | `Editor/CleaningToolTestMode.cs`만 개발 메뉴를 제공하고 판 변경/Stop 후 만료. 일반 소유 정책과 실험 도구 접근을 영구 변경하지 않음 | ToolFollowup64의 반복 Enable/Restore·새 판 만료·옛 목록 복원 금지.04b의 정식 DLL 검증 probe 제외와 진행 불변 | 개발 검사를 구매/해금 기능으로 간주하지 않음 |

## 추가 검사 범위

기존64/140/171/04a/04b를 다시 돌리지 않는다. 새로운 `.utmp/s305-editor-*` 경로를 Play 전에 override로 설정하고 초기 stage-01/마른 천을 전제로 다음4조건만 검사한다.

1. 잠긴 작은 솔의 실제 Button.onClick을 강제 호출하고 정의/ID 직접 선택을 요청해도 CurrentTool은 허용된 마른 천이다.
2. 잠긴 광택천도 동일하게 거부된다.
3. 실제 Coin MeshCollider.Raycast 적중에 public A 청소 API를 호출하면 감소량은 마른 천 rate×dt와 같으며 잠긴 도구 rate를 쓰지 않는다.
4. 해당 A 호출로 양면 B/광택이 변경되지 않는다.

이는 합성 UI 콜백/실제 물리 raycast/공개 청소 API 검사다. 물리 LMB/연속 드래그·Player 체감을 새로 검사하지 않는다. C#/씬/에셋 변경이 없으면 새 빌드를 만들지 않고 기존 정식 빌드와04b Windows 증거를 재사용한다. 사용자 persistentDataPath에는 접근하지 않는다.

## 추가 검사 결과·최종 상태

- 새 `.utmp/s305-editor-1792542779637`을 **Play 전** Editor override로 지정했다. 실제 로드 완료 SaveReady=true/PersistenceBusy=false/격리 경로 일치를 확인한 후 Pause에서 기존 코드에 대한 MCP 임시 C# 명령만 실행했다. 지속 C#/검증 스크립트·씬·에셋을 추가/편집하지 않았다.
- `tool-access-gap-report.txt`: **S3-05 GAP PASS checks=4**. 잠긴 SmallBrush/PolishingCloth 각각의 Button.onClick 강제 실행·정의 직접 선택·ID 직접 선택이 모두 거부되고 마른 천이 유지됐다. 실제 Coin MeshCollider.Raycast 성공 후 공개 A 청소 API의 감소량은 **0.004000008**, 현재 허용 마른 천 rate×0.01초 기대값 **0.004**와 float 오차1e-6 내 일치했다. 양면 B/광택 합은 그대로다. 잠금 선택 거부 뒤 A 소비자가 잠긴 도구 계수로 바뀌지 않는다는 부족한 실행 증거를 보완했다.
- 정상 마른 천의 A 청소 자체를0으로 막는 것이 요구가 아니다. 거부된 도구 대신 **기존 허용 선택**의 합법 효과만 남는지를 검사했다. 직접 A API·합성 UI 이벤트 검사이며 실제 LMB 드래그/잠긴 도구의 물리 클릭·새 Player 검사가 아니다.
- 기존64/140/171/03b/04a/04b 검사를 재실행하지 않았다. 제품/검증 C#·에셋·패키지·설정 변경0, 새 빌드/새 Player 실행0이다. 기존 정식 `Builds/Windows/Step3-20261017-save/`와04b Windows 검증 증거를 재사용한다. 문서 점검/기존 코드 API 검사만으로 새 제품 산출물을 만들지 않았다.
- 최종 Unity: Edit/Play=false/Pause=false/컴파일·임포트·빌드 없음, 원본 CleaningRoom Dirty=false/루트10. override 빈 값/background=false/preload0으로 복원했다. Console Error0/Warning0(현재 조회), 로그를 명시적으로 지우지 않았으며 과거485개 패키지 셰이더 경고 이력은 그대로 남긴다.
- 원본 CleaningRoom/ProjectSettings/Recovery 씬·meta2개의 hash 모두 시작 기준과 일치한다. `_Recovery` 열기/수정/삭제/저장/커밋0, 사용자 persistentDataPath 접근0. 구현 담당 변경은 본 문서뿐이다. root의 README·ROADMAP·지시서·체크리스트 변경을 보존했고 stage/commit/push하지 않았다.

## 판정·남은 공백

**현재 승인된 S3-05a 접근 정책과05b 제한의 개발 완료 조건 충족(한계 포함), 오케스트레이션 코드·증거 검토 완료.** root가 선택/UI/청소 소비 코드와 원본4조건 report를 대조했으며 추가 차단 결함은 없다. 최종 MCP 직접 조회에서도 Edit/Dirty=false/Console Error0을 확인했다. 기존에 구현·검증된 기능을 새로 구현했다고 보고하지 않는다.05a의 상점/구매·대체 획득 방식은 미채택이므로 구현/검사 대상이 아니며 “새 도구를 획득 가능”하다는 의미가 아니다. 일반 모드 잠금과 실험 접근/반응의 분리가 현재 완료 대상이다.

제한 구성은 Stage/Experiment 소유자가 허용 목록을 만들고 선택 컴포넌트가 거부하는 구조다. 임의 C# 코드의 ConfigureAvailableTools/데이터 직접 변경을 불신 코드 권한 경계까지 막는 보안 기능을 요구하거나 검증한 것은 아니다. B/광택의 잠긴 선택 후 물리 입력을 이번에 새로 검사하지 않았으며, 동일 CurrentTool 소비 코드·기존171/반응122/관찰 차단/04b Windows 선택 제한 증거를 조합한 판정이다. 실제 기기별 입력·체감·사용자16개 상한·외부PC·음질/성능 등은 원래 후속 수동 기록대로 유지한다.06/상점/재화/신규 획득/UI 확장은 착수하지 않았다.
