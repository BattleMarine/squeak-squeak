# S3-04b 중복 확정·진행 분리 통합 검증

2026-10-21. 구현 담당 기록. root는 상위 문서·최종 검토·커밋을 담당한다.

## 범위와 기준선

- main/939e924(ahead2). 04a aa2e58b·사용자 수동 결과 문서 커밋 완료. 착수 당시 사용자 `_Recovery`만 untracked이며 보존·커밋 제외다. 제품 코드 변경 없이 검증 공백을 우선 보강한다. 등급·재화·보상·상점·새 해금·05는 범위 밖이다.
- README/AGENTS/Step3 지시서·체크리스트/RcDecision/03b·04a 검증과 현재 반응표·마스크 재사용 정책을 대조했다. 대표 M1 천은 미처리 B에 유효(0.5), M1/M2 칫솔 미처리 B는0이다. CPU 마스크 인스턴스 재사용 자체를 결함으로 판정하지 않는다.
- 처음에는 네이티브 Unity 도구0개여서 파일 조회만 했다. 사용자 요청으로 Editor 실행을 기다렸고 이 대기 동안 수정·조작하지 않았다. 실행 후에도 네이티브 도구0/리소스 템플릿0이었으나 기존 Codex 설정의 `relay_win.exe --mcp`를 동일 프로젝트 필터로 실행해 MCP initialize/tools/list를 성공했다. 반환된 실제 도구 스키마를 읽고 GetProjectRoot/GetState/GetActive/Console을 호출했다. 패키지·MCP 설정·승인 정책 변경 없음. 읽기 호출은 서버가 승인 불필요로 반환했으며 승인 창을 보냈다고 보고하지 않는다.
- 실제 경로 일치, Unity6000.3.23f1/Edit/Play=false/Pause=false/컴파일·임포트 없음, 원본 CleaningRoom/Dirty=false/루트10. Console 표본5개는 기존 Codex Windows 서명 수집 경고이며 전체 오류0 판정으로 확대하지 않는다. 기존485개 패키지 셰이더 경고 이력은03b/04a 기록에 있다.
- 시작 hash: CleaningRoom `9CC4903988C0CED294B9F42998EE287AFBC6DF73BA5A8E039BB4CA624080A1FB`, Coin_Surface `7F41BE547FB29617B555757A1815C3EBCF030A3113004F882C77563EE6111D32`, ProjectSettings `CD1C9897971C2FB8F27628F2385CE98DA48C3927D7C629E94078F36C50A4B867`.
- 사용자 보존본: `_Recovery/0.unity` `5C1611F5CEB209ED53CC178DF3C0B8C00CA0751948353922D6B9D7E3DF55F7C2`, 해당 meta `670B2E3704FD70D13F346A0B5D44AD528EA52DED18C7862855D6D3935491DE0B`, 폴더meta `7FF77923B325F3813BDB763CF40A3A8210AC982B5272169EC83CBBF165D99A99`.

## 재사용 증거와 추가 검사

03b의 codec/안전저장114조건·손상/상한·소유자 파괴 뒤 worker 완료/새 씬 읽기,04a의 최종 확인 표면·시간/확정 전 취소/관찰 전용·새 판 독립성은 변경 없는 증거로 재사용한다. 사용자 경량8항목/입력3항목 보고를 자동 검사나 미확인16개 상한·오류 안내의 통과로 바꾸지 않는다.

1. 새 격리 경로에서 결과 A를 실제 UI 보관/확인 이벤트와 중복 API로 저장하고 메모리/파일/이벤트가1회인지 확인한다.
2. 결과 B의 **실제 File.Replace 직후**를 gate로 대기시킨다. primary는 A+B/backup은 A인데 메모리·이벤트는 A만 유지해야 한다. 중복 UI/API·새 판 요청을 거부하고 비활성 소유자에게 늦은 worker 완료가 즉시 적용되지 않는지 확인한다.
3. 교체 직후 IOException으로 결과 불명 상태를 만들고 취소/새ID 우회를 차단한다. 종료 확인 취소 뒤 같은 ID 재시도 UI 복원을 확인한다. 재조회 대기 중 중복 재시도, 성공 후 같은 ID/목록2/이벤트2, primary/backup 바이트 불변을 확인한다.
4. 보관→새 판 실제 도포/천 작업→이전 결과 열람 거부→판 폐기 확인 취소/명시 폐기→정상 모드 보관함의 이전 결과 열람→새 Play/Windows 프로세스 재실행을 연결한다. 미완료 판은 복원하지 않는다.
5. 전 과정 일반 진행 파일 바이트·소유/완료 ID·선택 단계·정상 모드의 잠긴 도구/단계 접근 제한이 유지되는지 확인한다. 실험 도구 접근·사용·목표·보관은 정식 소유/완료/해금으로 쓰지 않는다.

검사는 `.utmp/s304b-*`만 사용한다. 사용자 persistentDataPath 읽기/쓰기·정식 exe 자동 실행은 하지 않는다. 실제 프레임·IO·uGUI 이벤트/합성 커널을 사용하며 물리 마우스·재미·성능 검사로 보고하지 않는다. 새 검사만 검증 define에 추가하며 제품 결함 증거가 없으면 제품 코드를 바꾸지 않는다.

## 진행

- 문서·코드·파일 hash와 실제 MCP 읽기 기준선 확보. 아직04b 새 검사 실행·Windows 빌드·통과 판정 전이다.

## 사용자 요청 중단·재개 지점 (2026-10-21)

- 사용자가 Codex 재실행을 위해 작업 중단을 요청했다. 새 검사/Play/빌드/커밋은 시작하지 않는다. 실행 중인 제품 IO·검증 Player·빌드는 없다(이번04b에서는 실행 자체를 아직 하지 않음).
- 추가한 코드는 **검증 define 내부**의 `SavePlayerProbe.Integration.cs`/Unity 생성 meta, 기존 `SavePlayerProbe.cs`의 integration-seed/reload 라우팅·gate 해제, `SavePlayerProbe.Finalization.cs`의 OnDestroy gate 해제뿐이다. 제품 코드/씬/설정 변경 없음. 통합 시나리오는 준비했으나 **실행 전·미검증**이며 통과로 계상하지 않는다.
- 임포트 요청 뒤 정식 Unity MCP 도구가 다시 제공됐다. 임시 stdio 클라이언트(session54487, 이름 Codex S3-04b connection)는 SIGINT로 종료했다. 이는 해당 클라이언트만 종료한 것이며 Unity Editor·기존 MCP 서버 설정을 종료/변경하지 않았다. 이후 정식 Unity MCP GetProjectRoot/GetState/GetActive/GetConsoleLogs(Error) 조회가 성공했다.
- 중단 시 정식 조회: 올바른 프로젝트, Edit/Play=false/Pause=false/컴파일=false/임포트=false, 원본 CleaningRoom Dirty=false/루트10. 마지막 Error 전체 필터 조회0. Console을 지우지 않았다. 이번04b Play/저장 override/background 변경을 하지 않았으므로 최초 조회의 override 빈 값/background=false/preload0을 유지하며, 재개 시 다시 읽어 확인한다.
- 일반 진행/결과 격리 fixture와 검사 report는 아직 생성하지 않았다. `.utmp/s304b-mcp-connection-20261021`은 연결 로그만 담으며 테스트 성공 증거가 아니다. 사용자 persistentDataPath 접근0, `_Recovery` 열기/저장/삭제/커밋0. stage/commit/push 없음.
- root의 지시서·체크리스트 변경은 별도 소유로 보존한다. 재개 시 Git/현재 Unity/Dirty/Console/override·background·preload와 사용자 복구 hash를 다시 확인한다. 새로운 `.utmp/s304b-editor-<ID>`를 Play **전에** override로 설정하고 컴파일 완료 뒤 `SavePlayerProbe.BeginEditor(path,"integration-seed")` → 새 Play의 `integration-reload`를 실행한다. 결과를 확인한 뒤 별도 Windows64/Mono/None + extra define SQUEAK_SAVE_VALIDATION 검증 Player를 새 `.utmp` 출력에 빌드하고 같은 두 phase를 서로 다른 프로세스로 실행한다. override/background/preload·빌드 hook 잔여를 기준선으로 복원한다. 불합격 fixture는 제품 결함과 구분하고 임의로 가드를 완화하지 않는다.
- 검증 코드만 변경된 상태가 유지되면 기존03b 정식 빌드를 재사용할 수 있으나 **이번04b 검증 완료 선언은 아직 불가**다. 제품 결함을 확인해 제품 코드가 바뀌면 새 정식 빌드가 필요하다. 05/보상/상점/등급/해금 신규 구현은 계속 보류한다.

## 재개·Editor 통합 결과 (2026-10-21)

- root 재개 지시 후 정식 Unity MCP 조회 성공: 프로젝트 일치/Unity6000.3.23f1/Edit/컴파일·임포트 없음/원본 CleaningRoom Dirty=false/루트10. preload0/override 빈 값/background=false/빌드 없음. Error 전체 필터0. Recovery3개·ProjectSettings hash는 시작 기준과 같다. 임시 stdio 클라이언트를 새로 기동하지 않고 정식 도구만 사용했다.
- `.utmp/s304b-editor-1792540412201/probe-integration-seed.txt` **53조건 통과**, 새 Play의 `probe-integration-reload.txt` **23조건 통과**. 이번04b 검사 실행은 첫 실행부터 통과했으며 가드 완화/제품 수정/실패 숨김은 없다. 03b/04a 전체 검사를 무의미하게 반복하지 않았다.
- 실제 File.Replace 후 primary=A+B, backup=A인 상태에서 worker를 대기시켰다. 메모리/이벤트는 A만 유지되고 중복 UI/API/새 판/열람을 거부했다. 소유자 비활성 동안 worker를 완료시켜도 즉시 확정하지 않았으며 다시 활성화한 뒤 결과 불명 오류를 표시했다.
- 오류의 취소/새 ID 우회 거부, 종료 확인 취소 뒤 같은 결과 재시도 UI 복원을 확인했다. 중복 재시도 중 동일 ID 유지·재조회 대기 동안 조기 확정 없음·성공 후 정확히2개/이벤트2회를 확인했다. AlreadySaved 재시도 전후 primary/backup 바이트가 모두 그대로다.
- 결과 B 보관→A 열람→새 M1 실제 승인 천 문지르기→미보관 열람 차단→폐기 확인 취소/현재 표면·시간 유지→명시 폐기→일반 모드 보관함 UI의 A 열람까지 연결했다. 각 경계의 일반 진행 파일 바이트/DTO/메모리 소유·완료 ID·선택 단계가 같고, 일반 모드의 잠긴 도구 선택/다음 단계 입장 제한이 유지됐다.
- 새 Play는 결과2개만 복원하며 미완료 판/대화상자는 복원하지 않았다. 두 ID/시간/양면 배열 정확 일치, 두 결과 관찰 전용·재확정 이벤트0 및 일반 모드로 돌아온 후에도 일반 진행·잠금 유지까지 통과했다.
- Play 종료 후 Edit/Dirty=false/preload0/override 빈 값/background=false, Console Error0. Windows 검증 출력 `.utmp/s304b-player-build-1792540487425` 빌드 중이다. 아직 이 빌드/Player 실행을 통과로 계상하지 않는다.

## Windows 통합 결과

- `.utmp/s304b-player-build-1792540487425/SqueakSqueak.exe` 및 동일 폴더의 Data/Mono/UnityPlayer를 함께 유지한다. StandaloneWindows64/Mono2x/BuildOptions.None, CleaningRoom1개, **extra define SQUEAK_SAVE_VALIDATION 전용**. 실제 최신 BuildReport **Succeeded/오류0/경고485/129,682,878B/28.0211845초**. MCP는 기존 Sentis/ConvGeneric 경고 때문에 partial error로 반환했으나 실제 BuildReport와 파일을 별도 조회해 성공 확인했다. 오류를 숨기거나 같은 출력에 재빌드하지 않았다.
- 검증 DLL SHA256 `7532AD03FD91AFCAE469748CA94AC3DBBFCDF623DD9FC5713D99E7F3C56694C7`. exe SHA256은 기존 공통 Unity launcher와 같은 `82EFD5A8C297A5046F6BB72C240DAB8972E64003851935EBA0A46AD121CE0305`이며 exe만으로 제품 코드 동일을 판단하지 않는다.
- `.utmp/s304b-player-1792540571280`: **integration-seed53조건 통과/exit0**, 별도 프로세스 **integration-reload23조건 통과/exit0**. report는 해당 폴더의 `probe-integration-seed.txt`/`probe-integration-reload.txt`, 로그는 폴더 경로 뒤 `-seed.log`/`-reload.log`다. 숨김 창, `-batchmode -force-d3d11 -saveProbeDirectory <위 절대 경로> -saveProbePhase <phase> -logFile <로그 경로>`로 실행했다.
- 결과 A `b78256c049264717baadc167992de1c0`, B `998615c711e74f23857f5993f51cc111`. primary67,688B/이전 A backup33,706B, 일반 진행128B. 실제 교체 후 예외→같은 B ID 재시도에도 정확히2개이며 primary/backup 바이트는 재시도 전후 동일하다. 재실행 뒤 두 결과의 ID·시간·A/양면 B/P/풀림 원본이 manifest digest와 bit-exact 일치했다. 일반 진행 파일128B도 시작 사본과 정확히 같다.
- Editor와 Windows에서 각각 같은53/23조건을 실행한 것이다. 서로 다른152개 시나리오로 부풀리지 않는다. 실제 `Button.onClick.Invoke()`를 반복했고 비활성/비상호작용 두 번째 콜백까지 강제 호출해 owner API 가드를 검사했다. 물리 더블클릭·EventSystem의 실제 hit test 검사가 아니다.

## 최종 보존·정식 산출물

- **제품 런타임 변경0.** 변경은 검증 define 내부의 probe3개와 본 문서다. 신규 Integration 파일도 전체 `#if UNITY_EDITOR || SQUEAK_SAVE_VALIDATION`이며 전역 define은 `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY` 그대로다. 실제 검증 DLL에만 SavePlayerProbe/RunIntegration/IntegrationFiles/ReleaseIntegrationGate/UseValidationFiles가 있고 기존 정식 DLL에는 없다. 제품 CoinSaveSession/ViewStoredResult는 양쪽에 있다.
- 기존 `Builds/Windows/Step3-20261017-save/` 정식 산출물을 재사용한다. 이번에 새 정식 빌드를 만들거나 사용자 persistentDataPath를 사용하는 정식 exe를 자동 실행하지 않았다. 기존 정식 DLL SHA256 `EBDD023F5097C83F20052295A17AFF1464A8552EEADB920EDBAE7DD219F1A517` 유지. 제품 코드가 변경되지 않았으므로 검증 코드 추가만으로 새 정식 배포본을 요구하지 않는다.
- 빌드 후 메모리 preload0을 확인했고, 디스크의 빌드 hook 자동 추가 InputSystem GUID052faaac586de48259a63d0c4782560b 블록만 기준선으로 복원했다. ProjectSettings SHA256이 시작 값과 완전히 같다. 씬/머티리얼/URP/Graphics/BuildSettings/패키지 diff0, 임시 Resources 생성물 잔여0이다.
- 최종 정식 MCP 조회: 올바른 프로젝트/Unity6000.3.23f1/Edit/Play=false/Pause=false/컴파일=false/임포트=false/빌드=false, 원본 CleaningRoom Dirty=false/루트10. override 빈 값/background=false/preload0. Console Error 전체 필터0, Warning 조회25항목은 기존 Sentis 셰이더 경고군이며 BuildReport의 전체485경고와 구분한다. 로그 삭제/경고 억제 없음.
- `_Recovery` 씬과 meta2개, 원본 CleaningRoom·Coin_Surface·ProjectSettings의 최종 hash가 모두 시작 기준과 같다. 복구 파일은 열거나 수정·삭제·저장·커밋하지 않았다. 임시 연결 클라이언트는 중단 전에 이미 종료했고 두 검증 Player도 모두 exit0으로 종료했다.
- root의 지시서·체크리스트 변경은 별도 소유로 보존한다. 이 작업에서 stage/commit/push하지 않았다.

## 판정·남은 제한

**S3-04b 개발 검증·오케스트레이션 코드/증거 검토 완료(한계 포함).** root가 검사 코드·제품 저장 경계와 Editor/Windows 원본 report를 대조했고 추가 차단 결함을 발견하지 않았다. 최종 MCP 직접 조회도 Edit/컴파일·임포트 없음/CleaningRoom Dirty=false/Console Error0이다. 이번 실행에서 제품 결함이 확인되지 않아 제품 수정 없이 부족한 통합 검사만 추가했다. 기존03b의 손상/상한·씬 파괴 worker 수명/GPU 검사와04a의 최종 시점·시간 경계 증거를 재사용한다. 이번 대표 표본은 M1이며 M2 전체 반응을 새로 검사했다고 주장하지 않는다.

보관→새 작업/열람 거부→폐기 취소·명시 폐기→이전 결과 열람→재실행을 검증했다. seed 마지막 상태는 열람이며 **미완료 판을 남긴 채 실제 종료하는 별도 수동 경로**와 종료 고지/물리 입력·사용자16개 상한·외부PC·성능/청취/전체 조명 픽셀 비결정성은 기존 수동/03b 기록의 구분을 유지한다. 사용자 경량 정상 보고를 미확인 항목 전체 통과로 바꾸지 않았다. 05와 보상·등급·상점·새 해금은 미착수다.
