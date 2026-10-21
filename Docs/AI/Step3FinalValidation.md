# S3-07a/b 최종 개발 검증·빌드 인계

2026-10-21. 구현 담당 기록. 승인 범위는07a 통합 회귀와07b 별도 Windows 빌드/인계다. **07c 사용자 수동 검사·Step3 전체 최종 판정은 대기**이며 이 문서의 자동 통과와 구분한다. root가 독립 검토·상위 문서·수동 인계·커밋을 담당한다.

## 기준선

- main/12eba24(ahead5), 착수 시 사용자 `_Recovery`와 meta만 untracked. 사용자 파일·저장·복구본 보존, Git stage/commit/push 금지. 검사 경로는 새 `.utmp/s307-*`만 사용한다. 정식 exe 자동 실행하지 않는다.
- README/AGENTS/Step3 지시서·체크리스트와03b/04a/04b/05/06 증거를 대조했다. 기존03b 파일 형식·손상/백업/상한114조건·양면 GPU,04a 확정50+복원13,04b 중복/일반 진행53+복원23,05 도구 접근4,06 UI341/상한59·가상 MMB9/LMB/F6 통과는 해당 범위의 기존 증거로 재사용한다. 전체 조명 픽셀 비결정성, 숨김 창 Overlay 검은 캡처, 물리 입력/청취/성능 미검증을 통과로 바꾸지 않는다.
- 실제 MCP: 올바른 프로젝트, Unity6000.3.23f1/Edit/Play=false/Pause=false/컴파일·임포트 없음, CleaningRoom Dirty=false/루트10. Console Error0/Warning0(지우지 않음). 필수 controller 직렬화 ObjectReference22개/null0, override 빈 값/background=false/preload0, Windows64/Mono, 전역 define `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY`.
- Build Settings는 CleaningRoom enabled/index0, SampleScene disabled다. URP17.3.0/InputSystem1.20.0/uGUI2.0.0/TestFramework1.6.0, Assets asmdef 없음. 패키지/씬/에셋 변경 계획 없음.
- SHA256: CleaningRoom `9CC4903988C0CED294B9F42998EE287AFBC6DF73BA5A8E039BB4CA624080A1FB`, Coin_Surface `7F41BE547FB29617B555757A1815C3EBCF030A3113004F882C77563EE6111D32`, ProjectSettings `CD1C9897971C2FB8F27628F2385CE98DA48C3927D7C629E94078F36C50A4B867`. Recovery 씬 `5C1611F5CEB209ED53CC178DF3C0B8C00CA0751948353922D6B9D7E3DF55F7C2`, 파일meta `670B2E3704FD70D13F346A0B5D44AD528EA52DED18C7862855D6D3935491DE0B`, 폴더meta `7FF77923B325F3813BDB763CF40A3A8210AC982B5272169EC83CBBF165D99A99`.

## 검사 계획·새 공백

| 기준 | 기존 근거/이번 추가 | 현재 상태 |
| --- | --- | --- |
| 최신06에서 대표 M1/M2 연속 청소→목표→추가 작업→보관→관찰/새 판 | 기존 개별 반응/확정/중복 증거를 재사용하고 작은 final-seed 통합 경로 추가. 실제 도포/기존 승인 문지르기 커널·합성 완료/시계·UI 콜백·실제 IO | 통과(Editor/Windows) |
| 미보관 작업에서 제품 종료 확인→새 Play/프로세스 복원 | 기존04b 종료 시점이 열람이었던 공백. 완성2개만 저장된 상태에서 미완료 판을 남기고 제품 종료 확인을 실제 실행, final-reload에서 ID/시간/양면 값/일반 진행 및 중간 작업 미복원 확인 | 통과(Editor/Windows), 물리 조작 아님 |
| 반복 작업 자원/참조/Console | 한 번 준비 후6회 M1/M2 진입·작업·안내·취소/폐기·정상 복귀를 실제 프레임으로 반복. 같은 정상 상태의 장면 객체·오염·마스크·스캐너·오디오/효과 수와 핵심 참조를 비교. FPS/메모리 예산 인증 아님 | 통과(표본6회 한정) |
| 채택한 거래/실패/손상·접근 제한 | 별도 경제/구매/보상 거래 미채택. 결과 저장의 동일 ID/실패/손상/상한·일반 잠금은03~06 증거 재사용, 새 통합에서도 파일2개/이벤트2회/일반 파일 불변 확인 | 기존 근거+통합 통과, 경제 거래 해당 없음 |
| Windows x64/Mono 비개발 산출물 | 새 검증 Player의 seed/reload 실행 후 `Builds/Windows/Step3-20261021-final/` 별도 정식 빌드. 검증 define/override 제외, hash/ignore·preload 부작용 복원 | 통과, 정식 exe 수동 실행은07c |
| 07c 실제 사용자 조작·최종 판정 | root 수동 인계 문서와 사용자 후속 검사. 이번 자동 실행과 구분 | 대기(이번 범위 아님) |

## 진행

- 파일·Editor 기준선 및 기존 증거 대조 완료. 검사는 기존 probe를 확장한 검증 define 내부 코드로만 추가한다. 제품 결함이 확인되지 않으면 제품 코드는 바꾸지 않는다.
- 첫 Editor `.utmp/s307-editor-1792551280572`에서 seed144조건·제품 확인 후 실제 Edit 복귀를 확인했다. 정상 동일 상태 자원은 objects142/dirt7/lines4/particles1/audio4/masks2/scannerMaterials1/dropClips1로6회 모두 동일했다. 이름 필터의 항상0 통과를 막기 위해 SqueakSqueak.BMask.Front/Back=2, RB.ScannerOutline=1, RB.Droplet=1의 명시 양수 기준을 검사에 추가해 최종 버전으로 재검사한다. 이는 이벤트 횟수/자원 수 검사이지 delegate 구독 목록 자체 계측이나 메모리/FPS 성능 인증이 아니다.
- 최종 Editor `.utmp/s307-editor-final-1792551406482`: **final-seed145조건·새 Play final-reload27조건 통과**. 제품 종료 확인 후 GetState에서 실제 Play=false/Edit 복귀를 재확인했다. seed는 Finish(automaticQuit:false)로 선행 검사 결과만 기록한 뒤 실제 UI 확인→ConfirmDialog→ConfirmAppQuit를 호출한다. probe ApproveValidationQuit 경로를 쓰지 않는다. reload 검증 종료 정리는 기존 probe 승인 경로와 수동 Stop을 사용하며 제품 종료 검사로 주장하지 않는다.
- 최종 seed의 명시적 앞면/뒷면 마스크 각각1개·스캐너 머티리얼1·도포 클립1 기준과6회 동일 상태 수량 비교가 통과했다. 저장 성공 이벤트는 M1/M2 각1회/총2회, 반복·열람에서 증가0이며 일반 진행 파일/소유/완료/다음 단계 잠금은 불변이다. 누락 스크립트0과 핵심 참조·마스크2개 유효성을 매 반복 확인했다. Console Error0/Warning0이며 제품 코드·씬·에셋 수정0이다.

## Windows 실행·07b 빌드

- 검증 Player `.utmp/s307-player-build-1792551500973/SqueakSqueak.exe`: Windows64/Mono/None+`SQUEAK_SAVE_VALIDATION`, CleaningRoom1씬. **Succeeded/오류0/경고0/129707966B/14.9611045초**.
- `.utmp/s307-player-1792551541225`: **seed145/exit0 → 별도 새 프로세스 reload27/exit0**. `probe-final-seed.txt`, `probe-final-reload.txt`, `seed-player.log`, `reload-player.log`, `final-identities.txt`에 원본을 보존했다. 실행 인자는 `-batchmode -force-d3d11 -saveProbeDirectory <격리경로> -saveProbePhase final-seed|final-reload -logFile <격리로그>`이며 그래픽 비활성화 옵션은 쓰지 않았다.
- seed의 PASS는 제품 확인 직전까지의 검사 결과로 기록하고 **Finish(false)→실제 확인 Button.onClick→ConfirmDialog→ConfirmAppQuit→Application.Quit()**를 실행했다. 외부 프로세스 대기에서 종료/exit0을 확인했으며 seed에서 probe ApproveValidationQuit를 호출하지 않았다. reload 후 검사 도구 종료는 자동 승인 경로이므로 제품 종료 검사로 중복 계산하지 않는다.
- 복원은 완성 M1/M2 각1개/총2개의 ID·최종 작업시간·A/양면 B·P·풀림 배열 bit-exact 비교다. 미완료 판·확인창·시간·pending 후보는 복원되지 않았다. 본 파일360088B/직전 유효 백업179542B/일반 진행128B다. 일반 소유/완료/선택/다음 단계 잠금은 그대로다.
- Windows 정상 동일 상태 자원: **objects141/dirt7/lines4/particles1/audio4/masks2/scannerMaterials1/dropClips1**,6회 모두 동일. Editor는 objects142이며 Editor probe가 Scene에, Player probe는 DontDestroyOnLoad에 위치하는 코드 차이와 구분한다. 앞/뒷면 mask 실제 이름 각1·스캐너1·도포 클립1을 명시 검사했다. 단순 명칭 필터0 비교가 아니다. 구독 목록 자체·장기 메모리 누수·성능을 인증한 것은 아니다.
- Player 로그에는 게임 검사 FAIL/managed Exception은 없지만 **Unity 서비스 호스트의 Curl6 DNS 오류가 seed3건/reload2건** 있다(`config.uca.cloud.unity3d.com`, `cdp.cloud.unity3d.com`). 환경 통신 실패로 별도 분류하고 원본을 보존한다. 전체 Player 로그 오류0이라고 보고하지 않는다. 이를 없애기 위해 패키지/네트워크 설정을 바꾸지 않았다.
- 별도 정식 산출물 **`Builds/Windows/Step3-20261021-final/SqueakSqueak.exe`**: x64(PE Machine0x8664)/Mono/비개발`BuildOptions.None`, CleaningRoom1씬, **Succeeded/오류0/경고0/129631001B/18.6341139초**. 출력 폴더 전체가 필요하다. 이전06 빌드는 그대로 보존했다. 사용자 저장 보호를 위해 정식 exe를 자동 실행하지 않았다.
- SHA256: 정식 exe `82EFD5A8C297A5046F6BB72C240DAB8972E64003851935EBA0A46AD121CE0305`(공용 런처), 정식 `Assembly-CSharp.dll` **`1EB35737CFF5850C871ABB32AF9CE21B1C36480E7DA76549D7E9972E23659A07`**, 검증 DLL `DAABFC0966844DF7AA594533DCCE65758D97D7B6B61696ABA37FDDEE60816137`.
- 정식 DLL에는 SavePlayerProbe/RunFinalRegression/UseValidationFiles/ValidateIsolatedPath 메타데이터와 `-saveProbeDirectory` UTF16 CLI 문자열이 없고, 검증 DLL에는 모두 있음을 확인했다. 전역 define은 바꾸지 않았고 extra define은 검증 BuildPlayerOptions에만 지정했다. 공개 const 키의 존재 여부와 실행 가능한 override 경로의 제외를 혼동하지 않는다.

## 정리·판정·남은 작업

- 제품 코드/저장 형식/씬/머티리얼/패키지 수정0. 변경은 검증 define 내부 SavePlayerProbe.cs의07 라우팅·도구 자동 종료 분리, 새 FinalRegression.cs/meta와 이 기록이다. 최종 probe의 Finish(false)는07 seed에서만 사용하고 기존 phase 기본 동작은 유지한다.
- 빌드 hook의 알려진 InputSystem preload1개만 디스크에서 원래 `[]`로 복원했다. 메모리 preload0, override 빈 값/background=false, 전역 define 불변. 원본 CleaningRoom·Coin_Surface·ProjectSettings·Recovery 씬/meta2개 hash는 모두 시작 기준과 같다. 복구 씬 열기/수정/저장/삭제0, 사용자 persistentDataPath 접근0이다.
- 최종 MCP 조회는 Edit/Play=false/Pause=false/컴파일·임포트 없음, 원본 CleaningRoom Dirty=false/루트10, Console Error0/Warning0. Assets/Settings·ProjectSettings·Packages diff0, `git diff --check` 통과. `.utmp`/Builds의 exe·보고서에 `git check-ignore`가 적용된다. 임시 Resources 잔여 없이 검증 Player2개가 exit0으로 종료했다.
- **07a/b 개발 검증 결과: Ready with limitations, root 독립 검토 완료.** 지정한 핵심 통합·새 프로세스 복원·표본 반복 자원·빌드 기준은 통과했으며 제품 결함이 확인되지 않아 수정하지 않았다. root가 검증 코드·원본 report·Player 로그·정식 DLL hash·보존 hash를 대조하고 최종 MCP에서 Edit/Dirty=false/Console 오류·경고0을 확인했다. 기존03~06의 전체 검사를 재실행한 것이 아니다. 승인 청소 커널/합성 완료/포커스 시계·UI 콜백·실제 프레임·IO 검사를 물리 드래그/실측 작업시간으로 주장하지 않는다.
- **07c/Step3 전체 최종 판정은 대기.** 새 정식 exe의 실제 조작·Overlay/물리 휠·오류/16개 상한 사용자 확인, 체감/음질/외부 PC/성능·광택 등 이월 검사는 root의 `Step3FinalManualTest.md`와 `Step3SaveManualTest.md`를 따른다. 상점/재화/신규 획득/정식3D 도구함/방·전시실/고급 손상은 이번에 구현·검증하지 않았다.
- root 상위 문서·수동 인계 변경과 사용자 Recovery를 그대로 보존하며 구현 담당 stage/commit/push는0이다.
