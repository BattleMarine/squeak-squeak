# S3-04a 최종 결과 확정 검증

2026-10-19. 구현 담당 기록. 상위 체크리스트·지시서는 root 소유다.

## 범위와 기준선

- 04a만 승인됨. 기존03b의 목표/확인/저장 소유자를 재사용한다. 결함 증거 없이 제품 기능·등급·보상·해금·04b 통합을 추가하지 않는다.
- main/18414b6. 시작 변경은 root의 지시서·체크리스트와 사용자 `_Recovery`뿐이다. 복구 파일은 조회만 하고 편집·삭제·stage하지 않는다.
- Unity6000.3.23f1, StandaloneWindows64. 원본 CleaningRoom/Edit/Dirty=false/루트10, 컴파일·임포트·빌드 없음. preload0/검증 override 빈 값/background=false. Console 오류0, 기존 MCP 실행파일 서명 수집 경고2.
- 원본 씬 SHA256 `9CC4903988C0CED294B9F42998EE287AFBC6DF73BA5A8E039BB4CA624080A1FB`, Recovery 씬 `5C1611F5CEB209ED53CC178DF3C0B8C00CA0751948353922D6B9D7E3DF55F7C2`, ProjectSettings `CD1C9897971C2FB8F27628F2385CE98DA48C3927D7C629E94078F36C50A4B867`.

## 검증 계약·계획

1. 실제 유효 도포로 작업을 시작하고 합성 커널로 목표 표본을 준비한다. 목표 달성 때 pending/current 결과가 없고 추가 도포·청소와 작업 시간 증가가 가능한지 확인한다.
2. 확인 모달 진입은 입력·타이머를 pause하지만 사본을 만들지 않는다. 실제 프레임 대기와 포커스=true의 결정론적 시계 자극을 모두 사용해 모달 시간이 제외되는지 확인한다. 취소는 동일 판·표면·시간을 보존한다.
3. worker 쓰기 경계에서 대기 후 확정 전 실패를 주입한다. Saving/SaveFailed 동안 시간·결과가 고정되고, 실패 취소 후 작업을 재개하며 다음 확인은 새 ID/최신 표면·시간을 사용하는지 확인한다.
4. 최종 확인 후보를 동결하고 실제 파일 쓰기→동일ID 재조회 경계를 각각 대기시킨다. 파일이 존재해도 재조회 완료 전 Stored/event를 표시하지 않는지 검사한다. 원본 CPU 배열의 시험용 직접 변경이 후보에 번지지 않는지도 별도 검사한다.
5. 성공 결과는 확인 시점의 양면 B/P/풀림·A·시간과 정확히 같고 목표 시점과 달라야 한다. 성공 후 관찰만 허용하고 새 판과 저장 결과가 독립적이어야 한다. 별도 Play/Windows 프로세스에서 같은 파일을 읽어 ID·시간·양면 원본을 비교한다.

자동 검사는 `.utmp/s304a-*` 격리 경로만 사용한다. 제품 persistentDataPath에는 접근하지 않는다. 시계 자극은 검증 define 안의 reflection으로 기존 타이머에 포커스 있는 구간을 제공하는 것이며 실제 마우스/체감 시간 실측으로 보고하지 않는다. 기존03b의 GPU/전체 조명 한계·기존 저장114조건은 증거를 재사용하고 관련 없는 전체 검사를 반복하지 않는다.

## 진행

- 문서·실제 연결·원본 씬·Git 기준선 확인. 제품 코드상 사본 생성은 ConfirmDialog→BeginStore, 모달 진입의 RequestStore는 pause만 수행함을 확인했다.
- 첫 Editor 실행 `.utmp/s304a-editor-1792380980000`은 최종 새 판 검사에서 실패했다. 앞선 목표 후 작업/확인 취소/쓰기 실패 취소/동일ID 재조회 대기/확인 표면·시간 일치는 통과했으나, 마스크 객체 자체도 매번 새로 생성되어야 한다는 검사 조건이 기존 `EnsureSurfaceMasks`의 정상 재사용 정책과 맞지 않았다. 해당 실패 report를 보존한다. 제품 코드를 바꾸지 않고 새 판의 A/B/시간/결과 초기화와 실제 추가 작업 후 저장 사본 불변을 검사하도록 정정한다.
- 최종 재실행·Windows 검사·완료 판정 전이다.
- 두 번째 Editor 실행 `.utmp/s304a-editor-1792381171117`에서 새 판 초기화까지 통과했다. 추가 작업 시험 대상 M2는 미처리 B 제거 계수가0인데 도포 없이 문지른 fixture 오류로 마지막 변경 검사만 실패했다. 이번 시간/사본 경계는 M1 대표 표본으로 한정하고 새 M1 판에서 실제 승인 문지르기 경로의 변경·저장 사본 불변을 검사한다. M2의 도포 필요 계약과 제품 코드는 변경하지 않는다. 두 실패 자료 모두 보존한다.
- 세 번째 `.utmp/s304a-editor-1792381323347`도 동일 마지막 시험에서 실패했다. 실제 조회는 M1/작업허용=true/칫솔이며, 정적 반응표를 다시 대조하니 M1도 **칫솔의 미처리 B 계수는0**이다(M1 천은0.5). M1 선택만으로 해결된다고 추정한 앞선 fixture 정정이 불충분했다. 입력/반응 가드를 완화하지 않고 M1의 미처리 B에 유효한 천으로 합성 승인 문지르기를 변경한다. 제품 결함으로 계상하지 않으며 세 실패 report를 보존한다.

## Editor 최종 결과

- `.utmp/s304a-editor-1792381425416/probe-finalization-seed.txt`: **50조건 통과 + 시간/변경 표본1행**(probe 출력 checks=51). 새 Play의 `probe-finalization-reload.txt`: **13조건 통과**. 검증 조건을 숨기거나 제품 반응/입력/시간 가드를 완화하지 않았다.
- 결과 ID `8e80e58e148f4410bec30247f5c68921`. 목표2초 → 첫 확인 모달5.05929327초 → 취소 후 확정 전 실패 후보7.06701565초 → 추가 작업 후 최종11.072176초. 활성 구간은 결정론적 포커스 시계 자극을 포함한다. 실제 프레임의 소량 활성 시간도 합산하므로 벽시계 플레이 시간/성능 측정치가 아니다.
- 목표 대비 최종 앞면 B **11,046텍셀**, 뒷면 풀림 **3,333텍셀**이 다르다. 확인 시점 양면 B/P/풀림·A·시간이 pending snapshot, 실제 파일 재조회, 새 Play 재로드에서 정확히 일치했다. 최종 확인 이후 시험용 live CPU 변경도 저장 사본으로 전파되지 않았다.
- 모달·쓰기 gate·명확한 실패 오류 화면·동일ID 재조회 gate에서 각각 실제 프레임/0.3초 대기 및 포커스=true 시계20초 자극에도 시간이 증가하지 않았다. 파일이 생겨도 재조회 gate가 닫혀 있는 동안 Saving/목록0/이벤트0/CurrentResult=null이며, 재조회 후 Stored/목록1/이벤트1로 전환했다.
- 확인 취소와 확정 전 실패 취소는 같은 판·표면·시간을 유지한다. 취소 후 유효 도포/천 문지르기·시간 증가, 새 확인의 다른 ID/최신 상태를 확인했다. Stored/Viewing 청소·도포 차단과 시간 고정, 새 판 초기화 뒤 실제 추가 작업에도 저장 사본 불변을 확인했다. 새 판의 CPU 마스크 재사용은 허용하며 저장 사본과의 소유권 분리로 판정한다.
- 두 Play 종료 후 원본 CleaningRoom/Edit/Dirty=false/루트10, override 빈 값/background=false/preload0. Windows64/Mono2x/None 검증 define 전용 빌드 `.utmp/s304a-player-build-1792381519036` 진행 중. 제품 코드 변경0, 사용자 저장 접근0, 씬/복구 파일 저장0.

## Windows 최종 결과

- 검증 산출물 `.utmp/s304a-player-build-1792381519036/SqueakSqueak.exe`: **StandaloneWindows64/Mono2x/BuildOptions.None/SQUEAK_SAVE_VALIDATION 전용**, CleaningRoom1개. 최신 실제 BuildReport **Succeeded/오류0/경고485/129,670,078B/88.5421053초**. 기존 Sentis/ConvGeneric 셰이더 경고군이며 패키지 수정·억제·로그 삭제는 하지 않았다. MCP는 경고 때문에 partial error로 반환했지만 실제 BuildReport와 출력 존재를 별도 조회하여 성공을 확인했다. 같은 출력으로 재빌드하지 않았다.
- 검증 DLL SHA256 `B4469740E5F5FD9B4A6434B4F1E46E8AF901CBA8F08F5159FF9227E4437A451A`. exe는 공통 Unity launcher이므로 해시만으로 제품 DLL 동일을 주장하지 않는다.
- `.utmp/s304a-player-1792381634500`: `finalization-seed` **50조건 + 표본1행 통과**, 첫 프로세스 exit0. 별도 프로세스 `finalization-reload` **13조건 통과**, exit0. 각각 같은 디렉터리에 `probe-finalization-*.txt`; 로그는 디렉터리 뒤 `-seed.log`/`-reload.log`다. 숨김 창으로 `-batchmode -force-d3d11 -saveProbeDirectory <위 절대 경로> -saveProbePhase <phase> -logFile <로그 경로>`를 사용했다.
- Player 결과ID `f85b45ccaf6048c088cf3a9a98ec0224`, 목표2초/첫 모달5.07353735초/실패 후보7.08110762초/**최종11.0818787초**. 목표 대비 앞면 오염11,046·뒷면 풀림3,333텍셀 변화. 최종 파일164,205B. 두 프로세스 사이 ID·시간·A·양면 B/P/풀림의 bit-exact 왕복, 열람 입력/시간 차단, 새 판 상태 초기화와 실제 천 변경 뒤 결과 불변을 확인했다.

## 정식 빌드 재사용·최종 보존

- **제품 런타임 변경0.** 변경 C#은 기존 `SavePlayerProbe.cs`의 partial/phase 라우팅/실패 gate 해제와 새 `SavePlayerProbe.Finalization.cs`뿐이며 두 파일 전체가 `#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION` 안에 있다. 전역 define은 `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY` 그대로다. 제품 저장·타이머·반응·씬 설정은 수정하지 않았다.
- 따라서 기존 `Builds/Windows/Step3-20261017-save/` 정식 산출물을 재사용한다(이번에 새 정식 빌드를 만들었다고 보고하지 않음). 기존 release DLL SHA256 `EBDD023F5097C83F20052295A17AFF1464A8552EEADB920EDBAE7DD219F1A517` 유지. 실제 DLL에서 `SavePlayerProbe`/`RunFinalization`/`PulseFinalizationClock`/`UseValidationFiles`는 검증본에만 있고 정식본에는 없으며 `CoinSaveSession`/`ViewStoredResult`는 양쪽에 있다.
- 빌드 후 메모리 preload0을 확인했다. 빌드 hook가 디스크에 다시 추가한 확인된 InputSystem GUID052faaac586de48259a63d0c4782560b 블록만 제거하여 ProjectSettings SHA256이 시작 기준과 일치한다. URP/Graphics/BuildSettings diff0, 임시 Resources 잔여0이다.
- 최종 Editor: 원본 CleaningRoom/Edit/Play=false/Pause=false/Dirty=false/루트10, 컴파일·임포트·빌드 없음. override 빈 값/background=false/preload0. Console Error 필터 전체 조회0, Warning 조회25개 항목은 기존 Sentis 셰이더 경고군이다(BuildReport의 전체485 경고와 구분). Console을 지우지 않았다. 두 검증 Player 모두 종료했다.
- 원본 씬/사용자 머티리얼/ProjectSettings hash 유지. `_Recovery/0.unity`와 해당 meta/폴더meta의 hash도 모두 시작 기준과 같다. 복구 씬을 열거나 재로드·저장하지 않았다. 이 문서 앞의 과거03b 재로드 승인을 이번 검사에서 재사용하지 않았다.
- 관련 C#/새 meta/본 문서만 구현 담당 변경이다. root의 지시서·체크리스트·수동검사 문서와 사용자 복구 파일은 보존했다. stage/커밋/push 없음.

## 판정·한계

**개발 검증·root 코드/증거 검토 완료(한계 포함).** 체크리스트의 s3-04a-f1~f3을 따른다. 제품 결함이 확인되지 않아 제품 수정 없이 검증 공백만 보완했다. 현재04a 계약의 결과 확정·시간·실제 파일 왕복은 Editor와 Windows에서 통과했다. M1 대표 경계 검사이며 M2 반응/GPU·저장 손상/상한 등 변경 없는03b 증거는 기존 기록을 재사용한다. 합성 승인 문지르기·포커스 시계 자극/실제 도포 API·프레임·IO 검사이지 물리 마우스 체험·벽시계 성능·외부PC 검사가 아니다. 기존 전체 조명 렌더 비결정성·음질/광택 체감·사용자 수동 검사 대기는 유지한다. 04b 통합·보상·등급·해금은 착수하지 않았다.
