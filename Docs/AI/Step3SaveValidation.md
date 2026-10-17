# S3-03 저장 기반·제품 연결 검증

2026-10-17. 정본: `Step3RcDecision.md`/Step3 지시서. 03a 구현·검증 및 오케스트레이션 검토를 마쳤다. 03b의 제품 보관 연결·열람/표면 복원·persistentDataPath 사용은 아직 하지 않는다. 상위 문서는 root 소유, 이 문서는 구현 계약/증거를 기록한다.

## 03a 최종 결과

- **구현·격리 검사·오케스트레이션 검토 완료.** 기존 제품의 보관은 여전히 시험용 메모리다. 저장 기반 추가를 영구 보관 제품 연결 완료로 보고하지 않는다.
- 최종 `CoinSaveValidation.Run()` **114조건 통과**. 새 정의 카탈로그로 이전 도메인에서 쓴 사본3개 재읽기 통과, 기존 Rb 데이터122/단계 Edit·Preview111 통과. Unity 컴파일/임포트 완료·Console 오류0·CleaningRoom Edit/Dirty=false/루트10. Play·씬 저장·새 Windows 빌드/Player 실행은 이번03a에서 하지 않았다.
- 최종 증거: `.utmp/s303a-a6567f3d2bfd42eb9f04b6903b844173/report.txt` 및 하위 실제 저장/손상/실패 자료. 이전 도메인 읽기 대상은 `.utmp/s303a-477849adee5a4a6da147720c3da09ea2/normal/`이다. 사용자 persistentDataPath 접근 없음.

| 256² 실측 후보 | 1개 표본 크기 | 검증 |
| --- | --- | --- |
| JSON float 배열 | 1,967,302B | 이 표본의 float/bool/metadata bit-exact 왕복 |
| GZip JSON | 97,663B | 비교 후보, 제품 형식으로 채택하지 않음 |
| **GZip 이진 envelope** | **39,174B** | **형식1 채택. float32/float64·bool·ID 무손실** |

16개(같은 표면 표본·각각 다른 결과ID) 이진 envelope637,251B, 최종 인코딩1125.61ms·압축 해제/검증340.19ms. 앞선110조건 실행은 인코딩618.38ms/로드332.84ms였다. Editor/Mono 표본이며 최악 용량·Player 성능 보증이 아니다. **03b에서는 메인 스레드에 동기 IO를 연결하지 말고** DTO/정의 캡처 후 worker에서 실행한다.

## 파일·API 인계

- `Assets/Scripts/Saving/CoinSaveData.cs`: 결과/일반 진행 DTO·읽기/쓰기 상태·상한. mutable DTO는 전송 사본이며 기존 runtime 상태 소유자를 대체하지 않는다.
- `CoinSaveDefinitionCatalog.cs`: 결과 정의·실제 렌더 오버라이드 값 계약/fingerprint 동결. `CoinSaveValidator.cs`: 완성 결과/수치/배열 검증.
- `GeneralProgressSaveDefinition.cs`: 기존 카탈로그의 도구/완료/입장 제약을 순수 값으로 동결. 실험 도구 접근/경제를 저장하지 않는다.
- `CoinSaveCodec.cs`: 버전1 bounded 이진/GZip/SHA-256, 결과/일반 진행 문서 종류 분리.
- `SafeSaveFileStore.cs`: 경로 명시·배타 writer lock·임시 닫기/재검증·원자 교체/백업·복구/오류. `CompletedCoinStore.cs`: 결과ID 중복/충돌/16개 상한 및 별도 GeneralProgressStore의 기대 revision 검증.
- `Assets/Scripts/Editor/CoinSaveValidation.cs`: 격리 검사·형식 실측·이전 실행 읽기. 생성된 모든 meta 포함. 기존 tracked Assets/Packages/ProjectSettings diff0이다.

03b 호출 순서: 메인 스레드에서 `CoinSaveRenderContract`에 실제 씬 dirtColor/dirtySmoothness/maximumSmoothness와 명시 revision(`cleaning-room-rb-v1`)을 넣고 결과/일반 진행 계약을 동결한다. `SavedCoinDto.FromSnapshot`으로 **같은 pending snapshot의 ID**를 유지한다. worker에서 저장/조회하고 `Saved`/`AlreadySaved` 확인 후에만 메인 스레드 메모리·UI/다음 판을 확정한다. `Indeterminate`는 취소/새ID로 우회하지 않고 같은 ID 재조회/재시도로 해소한다. 일반 진행은 읽은 revision으로만 쓰며 충돌 시 재조회한다.

`SaveReadResult.Source==Backup` 복구 안내와 Unsupported/UnknownDefinition/DefinitionChanged/IoError/Corrupt 차단을 구분한다. `.Data`가 없다고 새 진행을 생성해 덮어쓰지 않는다. `Issue==Missing`만 신규 확정 저장의 시작 조건이다. 결과 열람은 로드 DTO의 양면 원본으로 별도 마스크/인스턴스를 만들며 live 청소나 저장 DTO 배열을 공유하지 않는다.

## 구현 전 설계

- **책임:** 완성 `ExperimentSnapshot` → 별도 DTO/검증 → 결과 저장소. 일반 진행은 독립 DTO·형식/파일·검증·저장소이며 기존 소유/단계 ID만 다룬다. 재화/가격/등급/보상·미완료 판·카메라·입력/대화상자 상태를 새로 저장하지 않는다. 기존 메모리 보관 코드에는 연결하지 않는다.
- **데이터:** 결과 GUID·판 ID·DTO/정의/렌더 계약 버전·coin/reaction/layout ID·정의 fingerprint·해상도·경과시간·초기 B 합·A 및 양면 B/P/풀림 전체 값을 보존한다. 정의 fingerprint는 현재 정의의 색/반응/배치/메시/머티리얼 파라미터를 검증하며 표면 payload를 대체하지 않는다. 셰이더 의미 변경은 명시적인 렌더 계약 버전 갱신이 필요하다. 알려진 버전/정의와 다르면 읽기/덮어쓰기를 중지하며 자동 migration은 없다.
- **형식 후보:** JSON float 배열 / GZip JSON / GZip 이진(float32 그대로·bool bitpack)을 실제 현재256² 표본에서 비교한다. 형식1의 이진 envelope는 magic/버전/정확한 원문 크기/압축 크기/SHA-256으로 무결성을 검사한다. 최대16개·해상도16~256(2의 제곱)·원문24MiB/파일32MiB를 선검사하고 압축 해제·문자열·배열 할당을 제한한다. 양면 길이/디스크 유효 텍셀/유한 수치/0~1·풀림은 남은 B에만/완성 A전부·B95%를 확인한다. 양자화·손실 압축 없음.
- **저장:** caller가 지정한 전용 디렉터리, 결과/진행의 별도 고정 파일명. .lock의 OS 배타 핸들 아래 재조회·검증 → 새 고유 .tmp 파일 durable 쓰기/닫기 → File.Replace(이전 정상본 백업) 또는 최초 File.Move → 성공 반환. 본 파일 선삭제/비우기·비원자 복사 대체는 없다. 동일 결과 ID+동일 payload는 중복 없음, 다른 payload는 충돌. 일반 진행은 기대 revision 일치 때만 교체한다.
- **복구:** 본 파일 우선, 손상본+유효 백업은 복구 상태로 읽고 손상본을 보존한 교체만 허용한다. 양쪽 손상/미래 버전/불명 ID·정의 버전은 자동 초기화/덮어쓰기하지 않는다. 유일한 정상 백업을 손상본으로 교체하지 않는다. 임시 파일은 미확정 자료로 보존하며 완료 결과로 자동 승격하지 않는다. 교체 시도 뒤 오류는 결과 불명 상태로 알려 같은 ID 재조회/재시도를 요구한다.
- **예정 파일:** `Assets/Scripts/Saving/`의 DTO·정의 계약 카탈로그·검증/codec·안전 파일 저장소와 결과/진행 facade, `Assets/Scripts/Editor/CoinSaveValidation.cs`. Unity가 생성한 meta 포함. 패키지/씬/기존 에셋 변경 없음.
- **검사:** `.utmp/s303a-<고유ID>/`만 사용. 실제 IO 왕복·최대16개·정밀도/용량·ID 재시도/충돌·미래/불명 정의/배열/수치·손상/백업·읽기전용/쓰기·닫기/교체 전후 실패·중단 임시본을 검사한다. 사용자의 persistentDataPath는 조회/쓰기 테스트하지 않는다. 제품 연결은 03b 인계 사항이다.

## 기준선

- main/365881f, 시작 Git 작업 트리 clean. Unity6000.3.23f1/URP17.3.0/Input1.20.0, 실제 프로젝트 경로 일치. Edit·컴파일/임포트 없음·CleaningRoom Dirty=false/루트10. 기존 Sentis 셰이더 경고를 보존한다.

## 03b 구현 전 연결 설계 (2026-10-17)

- 기준: 03a root 검토·7f159eb 커밋. 현재 Edit/Dirty=false/루트10/컴파일·임포트 없음/Console0. root의 상위 문서 변경 보존. 이번 구현은03b 전체이며 커밋/push는 root가 담당한다.
- 기존 CleaningExperimentController가 판/확인/보관/관찰 상태와 UI를 계속 소유한다. 별도 순수 `CoinSaveSession`이 동결 계약·DTO·worker IO를 처리한다. main에서 정의/실제 렌더 값 캡처 → worker 로드/쓰기 → main Update에서 결과 적용. 미완료 판은 파일로 복원하지 않는다.
- 시작 로드 동안 입력 차단. 완성 결과와 일반 진행 모두 안전하게 읽은 뒤 기존 StageAccessContext/선택 단계로 새 일반 판을 시작한다. 실험 가용 도구를 소유로 내보내지 않으며 기존 접근 상태만 저장한다. 새 획득·보상/경제 없음.
- 보관 확인에서 snapshot/DTO를 한 번만 동결하고 Saving 동안 pause/입력 차단·중복 클릭 거부. 저장 후 같은 ID 재조회까지 성공해야 Stored·메모리 목록·이벤트를 확정한다. 실패는 판/시간 유지. 교체 결과 불명은 같은 ID 재시도만, 명확한 실패 취소 뒤 추가 청소는 새 확정 ID를 만든다.
- 로드 오류/미래·정의불일치·양쪽손상은 원본 보존+재시도/종료 안내, 자동 초기화 없음. 보관 목록 최대16개와 간단 열람을 제공한다. 열람은 검증된 실제 coin 정의와 저장 양면 원본을 별도 마스크로 복원하고 MMB/줌/F만 관찰 허용, 청소/도포 차단한다.
- 판 ID/owner 생명주기와 진행 중 Task를 확인해 비활성·씬 종료 뒤 늦은 완료가 새 판/UI를 덮지 않게 한다. IO는 중간 취소하지 않으며 종료는 worker의 닫기/교체가 끝나기 전 승인하지 않는다.
- 제품 기본 경로는 persistentDataPath 아래 전용 SaveData/v1. Editor 검증은 SessionState의 Editor 전용 override를 Play 전에 설정한다. 검증 Player는 별도 compile define/출력으로만 격리경로 probe를 포함하며 **release에는 경로 override·치트 진입점을 포함하지 않는다**.
- 검증: 격리된 제품 API/실제 프레임·재로드/저장 실패·복구/동일ID/취소·관찰 gate·양면 렌더·기존 회귀. 가능하면 별도 검증 Player를 두 번 실행해 같은 코드/파일 왕복 및 fingerprint/readability를 확인한다. 최종 일반 빌드는 `Builds/Windows/Step3-20261017-save/SqueakSqueak.exe`로 별도 생성한다.

## 진행 결과

- 첫 코드 검사는 Editor 테스트의 CompressionLevel 모호성 컴파일 오류를 발견해 전체 네임스페이스로 정정했다. 이후95조건 통과, 계약 강화 후110조건 통과. 최종 추가 상한/완성 경계 검사는 아래 후속 결과에 기록한다. 제품 가드/저장 실패를 숨기기 위한 검사 제외는 없다.
- `CoinSaveRenderContract`는 명시적 렌더 revision과 실제 씬의 dirtColor/dirtySmoothness/maximumSmoothness를 값으로 받는다. factory는 메인 스레드에서 현재 정의·메시·머티리얼과 이 오버라이드를 동결한다. 같은 이름의 다른 머티리얼 수치/메시 내용·렌더 오버라이드 변경 검출, 로캘 독립 fingerprint를 검사했다. shader 의미·렌더 알고리즘/씬 계약 변경은 revision을 수동 갱신해야 한다. 과거 renderer 보관/마이그레이션은 아직 없으므로 불일치는 차단한다.
- 결과와 일반 진행 모두 메인 스레드에서 Unity 정의를 값 계약으로 동결한다. codec/store worker는 Unity 객체·AssetDatabase·씬에 접근하지 않는다. Task.Run에서 두 저장소 쓰기/읽기 왕복을 통과했다. DTO는 mutable 전송 자료이며 runtime 상태 원본이 아니다. pending snapshot/resultId 유지와 UI 확정은03b 책임이다.
- 형식1은 현재 Rb 완성 결과만 지원하므로 P==0을 검사한다. 미래 일반 광택/후처리 결과까지 지원한다고 주장하지 않는다. bool bitpack/float32 무손실, SHA-256은 무결성용이며 인증/보안 저장 기능은 아니다.
- 본문이 정상이면 물리 손상 백업보다 우선한다. 단, 어느 사본이든 미래/정의 불일치/불명ID/읽기 실패/지원 파일 크기 초과이면 정상 본문이 있더라도 전체 저장을 차단하는 보수 정책이다. 본문 우선의 자동 복구는 물리 손상에만 적용한다. 미래 헤더는 최소12바이트 단계에서 먼저 판별한다.
- 임시/손상 보존 파일은 미확정/진단 자료로 남기며 자동 삭제하지 않는다. 파일별 상한과 확정 결과16개는 제한하지만 진단 파일 총량은 자동 정리하지 않는다. 제품에서 용량 정리/파일 폐기는 별도 사용자 승인 절차가 필요하다.

## 검증 범위·남은 위험

### 03b 연결 진행 (2026-10-17, 최종 인계 전)

- 기존 실험 소유자에 비동기 load/store/readback·오류/재시도·최대16개 목록·관찰 전용 열람 연결. 일반 진행은 기존 StageAccessContext의 소유/완료 ID와 선택 단계만 별도 저장하며 새 보상·해금 없음. 로드 동안 Stage.Start 전에 입력을 차단한다.
- 제품 경로는 `persistentDataPath/SaveData/v1`. 아래 모든 검사는 Play 전에 SessionState override를 설정한 `.utmp` 하위에서만 수행했다. 정식 Player에는 경로 override·합성 청소·fault 주입 probe가 컴파일되지 않는다. 파일 작업은 씬 수명과 독립적으로 끝까지 실행하며 정상 종료 요청은 완료까지 보류한다(강제 종료/Editor 강제 Stop의 완료 보장은 아님).
- `.utmp/s303b-editor-1792206383977`: 제품 seed34조건 / 새 Play 재로드38조건 통과. 실제 lock 실패→취소·추가 청소→새 ID, 교체 직후 예외→동일 ID 재시도, 중복확인/비활성 소유자, 양면 수치/풀림·ID·시간의 정확한 복원, 앞뒤 렌더4장 픽셀 일치, 열람의 청소/도포/타이머 차단, 새 판 소리/효과 복원을 확인했다. 합성 커널/제품 API와 실제 프레임이며 물리 마우스 체험을 대체하지 않는다.
- 첫 reload 실행은 Play 직후 소스 재컴파일/도메인 재로드가 겹쳐 `isolated product path` 가드에서 실패했다. 가드를 완화하지 않았으며 컴파일 종료 확인→새 Play→동일 가드38조건 통과로 재검증했다. 실패 기록을 남기고 최종 report는 성공 실행으로 갱신했다. 이후 검사는 Edit 컴파일 완료를 먼저 확인한다.
- `.utmp/s303b-general-1792207351642`: 일반 진행 실제 쓰기 실패/입력 차단→종료 취소 시 재시도 UI 복원→재시도 성공 후 모달·소리/효과 복원11조건 통과. 검증 fixture로 기존 접근 API에 합법 상태를 주입했을 뿐 제품 보상 규칙을 추가하지 않았다.
- 저장114/단계 데이터111/문지르기 CPU7238/실험 데이터122조건 재통과. 기존 paused Play 단계140/문지르기171/도구64조건 통과. 이후 자원 확인은 다른 회귀가 판을 바꾼 뒤 호출하여 RunPlay의 기대 판/프레임 가드에서 실패했으며 자원 검사 통과로 계상하지 않는다. 단독 단계 회귀→프레임→자원 확인을 다시 수행한다.
- 미래 버전 `.utmp/s303b-future-1792207414376`과 양쪽 손상 `.utmp/s303b-corrupt-1792207564447`의 제품 로드 차단·종료 취소→오류 UI 복원·재조회 원본 보존·fixture 명시 이동 후 재시도 정상 복귀가 각각8조건 통과했다. 검증 fixture만 별도 보존 이름으로 이동했으며 제품 자동 초기화/삭제 동작은 없다.
- `.utmp/s303b-capacity-1792207799707`: 최대16개 실제 목록/버튼/상한 거부·취소11조건 통과. `library16-1280.png`를 직접 검사하여 제목과16항목/닫기 버튼의 배치를 확인했다. 저장 해상도16의 CPU/양면 텍스처 복원→새 판 기본256 복원도 별도 API로 통과했다.
- Windows x64 Mono/BuildOptions.None 검증 전용 Player 1차 빌드 성공(129,647,902B/38.591초/오류0/경고0, shader cache 상태). 실제 제품 경로 seed37조건 통과. 두 번째 프로세스에서 ID/시간/양면 원본·열람 차단·소유 분리까지 통과했으나 조명 렌더 픽셀 완전일치는 실패했다. 실패 원본/로그를 `.utmp/s303b-player-1792207497150`에 보존했다.
- 조명 캡처 진단 빌드(129,649,438B/13.253초/오류0/경고0), `.utmp/s303b-player-diagnostic-1792207754409`: 합성 추가 청소 뒤 실제 제품처럼 UploadSurfaceMasks를 호출하도록 검증 경로를 정정한 후 seed42조건 통과. 재시작 양면 수치/ID는 정확하지만 전체 조명 렌더4장은 여전히 다르다. 동일 세션·동일 프레임의 연속 Camera.Render도4장 모두 달랐다. M1앞면 연속 seed 차이4,260픽셀/max33/sum(maxRGB차)10,794; 연속 reload4,318/max36/sum11,045; 재시작4,277/max45/sum11,046. coin/camera/projection·재질 블록 수치·환경광 SH는 비교 결과 동일했다. 임의 오차나 가드 삭제로 통과 처리하지 않았다.
- root 검토에 따라 렌더러 일반 비결정성 수정은 범위 밖으로 둔다. CPU bit-exact와 GPU 양면 마스크 원본/셰이더 샘플·표현 파라미터 동일·실제 Player 캡처 검토를 별도 검사하고, 전체 조명 렌더 픽셀 완전일치는 미보장 한계로 인계한다. 해당 분리 검사와 씬 종료 중 worker 완료 검증 빌드가 현재 진행 중이다.
- 분리 검증 Player 빌드 `.utmp/s303b-player-mask-build-1792207960574`는 182.106초 후 `Build was canceled`로 실패(오류1/경고0/출력0B). 사용자 보고상 Unity에 다른 확인 창이 있으며 root가 문구/버튼 확인을 요청 중이다. 원인 확인 전 중복 빌드/에디터 변경을 하지 않는다. 컴파일·저장 기능 실패로 단정하지 않으며 이 빌드는 성공 산출물로 계상하지 않는다. 해당 검사·최종 release·빌드 hook 임시 산출물/설정 복원은 아직 남아 있다.

114조건은 정상/백업 재읽기·동일ID 재시도/다른payload 충돌·16개 상한·worker IO·writer lock·부분 쓰기/닫기 뒤 실패·교체 전후 불명 결과·파일/압축/배열/수치 상한·짧은 미래 헤더·정의/미정의 ID·양쪽 손상·일반 진행 제약·씬 오버라이드·같은 이름의 다른 mesh/material 내용·로캘 독립성을 포함한다. 손상 main 복구 뒤 기존 정상 backup가 손상본으로 대체되지 않는 것과 손상 원본 별도 보존을 실제 파일로 검사했다.

Player/실제 프로세스 재시작 간 fingerprint·mesh readability·파일 지원/재실행/외관 복원은 **03b 필수 검증**이며 Editor 도메인 재로드 읽기로 대체하지 않는다. shader 의미·메시 생성/렌더 알고리즘 변경은 명시 revision 갱신과 호환 정책이 필요하다. 자동 migration·과거 렌더러 보관·클라우드·전원 차단 실측·사용자 파일 삭제/수리 UI는 없다. File.Replace 미지원/교체 불명은 비원자 덮어쓰기로 우회하지 않는다. 현재 형식은 광택 작업 없는 Rb 완성 표본(P0)에 한정한다.

## 03b 안전 중단 체크포인트 (2026-10-17)

사용자가 잠시 중단을 요청했다. 새 수정/검증/빌드/커밋을 시작하지 않고 읽기 전용 상태 확인과 이 기록만 남겼다. 커밋/push 없음. 다음은 **중단 시 실제 상태**이며 완료 선언이 아니다.

- 프로젝트 경로 일치. Edit/Play=false/Pause=false/컴파일=false/임포트=false/BuildPipeline.isBuildingPlayer=false. 실행 중 SqueakSqueak 검증 프로세스 조회 결과 없음. 진행 중 파일 쓰기/빌드를 강제 종료하지 않았다.
- 유일한 열린 씬 `Assets/Scenes/CleaningRoom.unity`, 루트10, **Dirty=true**. 사용자는 빌드의 변경 씬 저장 팝업을 Cancel로 닫았다. 씬 디스크 Git diff는 없다. 메모리 씬은 저장/폐기/리로드/dirty 해제하지 않았다.
- Dirty 유력 경로는 16개 UI 임시 캡처에서의 `RegisterObjectModification(canvas/camera)`이다. 실제 MCP 구현은 `Undo.RegisterCompleteObjectUndo`를 호출한다. 임시 값은 finally에서 복원했지만 메모리/디스크 전체 동일은 아직 입증하지 못했으므로 확정 원인으로 처리하지 않는다. 읽기 전용 직렬화 비교 시도는 도구 동적 컴파일의 Regex 해석 오류로 **실행되지 않았다**(Preview씬도 열리지 않음). 사용자 변경 가능성을 남기고 그대로 보존했다.
- 최신 빌드는 `s303b-player-mask-build-1792207960574` **Failed/취소**,182.106초/오류1/출력0B. 직전 성공 검증 빌드 두 개는 위 기록에 보존. 최종 `Builds/Windows/Step3-20261017-save/SqueakSqueak.exe` 일반 release는 **미생성**이다.
- 빌드 hook 잔여: `ProjectSettings/ProjectSettings.asset`의 preload가 빈 목록에서 InputSystem 참조1개(GUID052faaac586de48259a63d0c4782560b)로 바뀌었다. **현재 메모리도 preload1개**. 시작 시 메모리/디스크0개였으며 기준 파일 SHA256은 `CD1C9897971C2FB8F27628F2385CE98DA48C3927D7C629E94078F36C50A4B867`. 중단 요청 후 원복을 새로 수행하지 않았다.
- 취소된 빌드 hook가 만든 `Assets/Resources/PerformanceTestRunInfo.json`(3042B), `PerformanceTestRunSettings.json`(23B), 각 meta와 새 Resources 폴더 meta가 untracked로 남아 있다. 사용자 파일과 섞어 삭제/커밋하지 않았다. 재개 시 정확한 생성물 확인 후 Editor API로 정리한다.
- Editor SessionState override는 `.utmp/s303b-capacity-1792207799707`이며 이전 값은 빈 문자열. 제품 사용자 저장에 쓰는 검증은 수행하지 않았다. `Application.runInBackground=false`로 원래 값 복원됨. `SQUEAK_SAVE_VALIDATION`은 검증 BuildPlayerOptions.extraScriptingDefines에만 사용했으며 전역 Player 설정 define을 바꾸지 않았다.
- 사용자 머티리얼 `Coin_Surface.mat` SHA256 `7F41BE547FB29617B555757A1815C3EBCF030A3113004F882C77563EE6111D32` 유지. 씬/머티리얼을 저장하지 않았다. root가 관리하는 상위 문서 수정과 코드/meta를 모두 미커밋 상태로 보존했다.
- 재개 우선순위: Dirty 메모리/디스크 차이를 안전하게 확정 → 검사 변경만 정리 → preload/hook 잔여와 override 복원 계획 확인 → GPU 양면 텍스처/조명 없는 셰이더 샘플 검사 및 씬 수명 probe → 단독 단계 자원 회귀 → 검증 define 없는 release 빌드·산출물 확인 → 최종 상태/문서 인계. 원인 미확인 씬을 일괄 저장/폐기하지 않는다.

현재 CPU/파일/주요 제품 API의 통과와 **미완료 GPU 분리 검사·씬 수명 검사·release**를 구분한다. 일반 조명 렌더의 무변경 연속 캡처도 달라 픽셀 완전일치 가드는 실패로 보존했다. 해당 renderer 비결정성의 일반 수정은 이번 범위에 포함하지 않는다.

## 03b 재개 기록 (2026-10-17)

- 사용자 재개 요청 후 프로젝트/Edit/Dirty=true/루트10/빌드 없음 재확인. `SaveScene(saveAsCopy:true)`로 `.utmp/s303b-dirty-preserved.unity` 보존(원본 Dirty 유지). 전체 YAML 차이는 기존 Horizontal/VerticalLayoutGroup이 구동하는 자식7개 RectTransform의 anchor/position/size28줄뿐이었다. 실제 구동 값은 디스크와 일치했고 디스크 Preview에도 동일 레이아웃 계산을 적용하자 전체263개 객체·컴포넌트 직렬화 비교가 차이0이었다. 전체 YAML의 다른 참조/RenderSettings/씬 설정은 변하지 않았다. Unity uGUI의 DrivenRectTransformTracker가 해당 값을 자동 구동하는 코드도 확인했다.
- 초기 Regex 도구 컴파일 실패 이후 단순 문자열 참조 정규화로 비교 성공. PreviewScene 저장은 지원하지 않아 사본 저장 시도1회가 실패했으며 finally로 닫았다. 현재 씬이나 사용자 원본은 그 시도에서 저장하지 않았다.
- 재로드 요청이 자동 검토에서 차단되어 실행되지 않았다. 사용자에게 위험/사본을 설명한 뒤 **‘보존 사본을 남기고 재로드 승인’** 직접 승인을 받고 Editor API로 재로드했다. 이후 CleaningRoom Dirty=false/루트10, 디스크 씬 변경 없음. 보존 사본은 유지한다.
- 취소 빌드의 PerformanceTestRunInfo/Settings JSON은 `.utmp/s303b-cancelled-*.json`으로 복사 후 Unity 에셋 도구로 각 JSON/meta와 빈 Resources 폴더를 제거했다(복사본으로 복구 가능). Editor SessionState override는 이전 빈 값으로 복원했다. preload는 확인된 `Assets/InputSystem_Actions.inputactions`만 API로0개 복원하고, 파일의 동일 자동 추가 블록을 제거하여 기준 SHA256과 완전히 일치시켰다.
- URP runtime 목록16개→빈 목록의 원인은 패키지 `RenderPipelineGraphicsSettingsContainer.OnBeforeSerialize/OnAfterDeserialize`이다. Editor에서는 무조건 비우고 Player 빌드 직렬화 때만 stripping 결과를 채운다. 기존34개 정의 참조는 모두 유지된다. SerializedObject로 기존16개 참조만 재연결하고 SaveAssetIfDirty를 시도했으나 같은 콜백이 다시 비웠으므로 반복하지 않았다. 최종 빌드 후 확인된16개 rid 블록만 파일 기준대로 복원하고 임포트·diff를 확인한다. 이는 콘텐츠/렌더러 변경이 아니다.
- 재개 검증 Player `s303b-resumed-validation-build-1792213387310`: Windows64 Mono/None 성공(129,651,486B/35.410초/오류0/경고0). 기존 제품 seed42조건 통과, 씬 종료 중 worker를 취소하지 않고 새 씬에서 동일 ID를 읽으며 파괴된 소유자의 메모리 이벤트를 적용하지 않는 검사 통과. 해당 scene 보고의10830조건 중 대부분은 대기 프레임에서 같은 무오류 조건을 반복 센 것이므로 독립10830시나리오로 주장하지 않는다. 이후 probe의 중복 성공 카운트만 제거했다.
- 단독 단계140조건→실제 프레임→자원 확인을 다른 검사 없이 순서대로 재실행하여 통과했다: objects137/dirt7/StageDirt roots1/masks2/audio3/lines4/particles1. Stop 후 Dirty=false/루트10, background=false/override 이전값 복원 확인.
- 재개 후 shader debug 전체 화면 캡처는 0/0에서 실패했고 그 뒤에 있던 texture byte 검사는 실행되지 않았으므로 GPU 통과로 계상하지 않았다. 제품 로직을 바꾸지 않고 검증만 분리했다: texture bytes 검사를 먼저 실행, `_SurfaceDebug`의 기존 동일 샘플 경로에 대해 동전만 별도 임시 layer로 렌더하고 검정 배경·후처리 없음으로 비교한다. 카메라/layer/머티리얼 블록은 finally에서 복원하며 정식 Player에는 probe 자체가 없다. 기존 full-lit 완전일치 실패 가드와 결과는 유지한다.
- GPU 전용 Player `s303b-gpu-validation-build-1792213654424` 성공(129,653,534B/37.507초/오류0/경고0). 기존 `.utmp/s303b-resumed-player-1792213387310`의 제품 보관 파일은 바꾸지 않고 별도 프로세스 `capture`/`gpu` 두 번 실행했다. 각각 exit0, report27/31개 기록이며 독립 GPU 대상 검사가 통과했다. ID/시간·A/양면 B/P/풀림의 원본 정확 일치, 기존 seed GPU와 **M1/M2 앞뒤4개 RGBAHalf texture bytes 정확 일치**, 두 프로세스의 **동전만 렌더한 동일 셰이더 샘플4장 픽셀 완전 일치**, 재질·카메라·표현 파라미터 동일을 확인했다. Player에서 원본 Mesh/Material 계약 동결과 fingerprint 재로드도 성공했으므로 Mesh readability/정의 계약이 프로세스 사이에서 유효했다.
- `gpu-0-0/0-1/1-0/1-1.png`의 실제 Player 금색M1·은색M2 양면 문양/색/표면을 직접 검사했다. 전체 조명 렌더는 여전히 같은 세션 연속 캡처부터 달라 별도 `FAIL pixel restart` 진단과 기존 실패 report를 보존했다. 독립 GPU 통과를 full-lit 픽셀 통과로 바꾸어 쓰지 않았다. 허용오차/셰이더·조명 수정은 없다.

## 03b 최종 개발 인계 (2026-10-17)

상태: **구현·개발 검증 완료, root 코드 검토·커밋 대기(한계 포함)**. S3-04 보상/상점/등급/해금은 추가하지 않았다. 상위 문서는 root 소유이며 여기서는 변경하지 않았다.

| 대상 | 최종 증거 | 판정 |
| --- | --- | --- |
| 실제 보관/재실행 | 제품 API로 실제 파일 IO·별도 Windows 프로세스에서 동일 ID/시간/양면 float32·풀림 원본 재로드 | 통과 |
| GPU 복원/외관 | GPU 양면 RGBAHalf 원본4개 bit-exact, 동전 단독 셰이더 샘플4장 일치, 실제 Player M1/M2 양면 캡처 직접 검사 | 통과 |
| 입력/보관 UX | 미보관 목록 진입 제한, 최대16개·가득 참 거부, 안전 실패 취소/추가 청소 새 ID, 교체 불명 동일 ID 재시도, 오류→종료 취소 UI 복원 | 통과 |
| 일반 진행 분리 | 기존 소유/완료/선택 ID만 저장·재시도. 실험 도구 접근을 정식 소유로 저장하지 않음 | 통과 |
| 비동기/수명 | worker 중 실제 프레임 진행, 비활성 소유자 지연 확정 차단, 정상 종료 요청 보류, 씬 파괴 뒤 worker 완료·새 씬 읽기 | 통과 |
| 기존 회귀 | 저장114/단계 데이터111/문지르기7238/실험122, Play140/171/64, 단독 단계 자원 동일 | 통과 |
| 일반 조명 렌더 전체 픽셀 | 같은 상태 연속 캡처부터 비결정적. 실패 원본·측정 보존, 허용오차로 통과 처리 없음 | 미보장 한계 |
| 실제 마우스/성능·청취 | 이번 검사는 합성 커널/제품 API/실제 프레임·카메라. 새 release의 사용자 체험/외부PC/성능 실측·음질은 대체하지 않음 | 수동 확인 대기 |

### 정식 산출물

- `Builds/Windows/Step3-20261017-save/SqueakSqueak.exe` 및 같은 폴더의 Data/UnityPlayer/Mono 런타임을 유지한다. Windows x86_64(PE machine8664), Mono, BuildOptions.None, 검증 extra define 없음. BuildReport **Succeeded/오류0/경고0/129,627,257B/48.334초**. 앞선 취소 빌드와 구분한다. 경고0은 현재 shader cache 상태의 결과이며 과거 패키지 경고485 이력을 지우지 않는다.
- exe SHA256 `82EFD5A8C297A5046F6BB72C240DAB8972E64003851935EBA0A46AD121CE0305`, Assembly-CSharp.dll SHA256 `EBDD023F5097C83F20052295A17AFF1464A8552EEADB920EDBAE7DD219F1A517`.
- 실제 release DLL에는 SavePlayerProbe/UseValidationFiles/ApproveValidationQuit/ValidateIsolatedPath/`-saveProbeDirectory` 문자열/심볼이 없고 제품 CoinSaveSession/ViewStoredResult는 존재함을 확인했다. 전역 define은 기존 `SENTIS_ANALYTICS_ENABLED;APP_UI_EDITOR_ONLY` 유지.
- 정식 exe는 사용자 persistentDataPath를 실제로 사용하므로 자동 검사 목적으로 실행하지 않았다. 별도 검증 define Player가 같은 제품 저장 경로 코드를 `.utmp` override로 실행했다. 정식 exe 직접 체험은 사용자 확인으로 남긴다. 강제 종료/전원 차단 실측과 전체 renderer 비결정성 해결은 미포함.

### 최종 정리 상태

- Editor 프로젝트 경로 일치, Edit/컴파일·임포트·빌드 없음, CleaningRoom Dirty=false/루트10, Console 오류0·경고0. 로그를 명시적으로 지우지 않았다. 과거 취소/지원하지 않는 Preview 저장/검증 실패 기록은 이 문서와 `.utmp`에 유지한다.
- 최종 preload 메모리0개. 빌드가 디스크에 남긴 동일 InputSystem 블록만 원복하여 ProjectSettings SHA256이 중단 전 기준 `CD1C9897971C2FB8F27628F2385CE98DA48C3927D7C629E94078F36C50A4B867`와 일치한다. URP/Graphics/BuildSettings/씬은 최종 Git 내용 diff가 없다(정상 최종 빌드가 기존 runtime16개 목록을 동일하게 재생성). `Coin_Surface.mat` 기준 hash 유지.
- SessionState override 빈 값/background=false, 검증 Player 프로세스 없음. 취소 빌드 Resources 생성물은 제거했고 정상 빌드 hook 생성물도 자동 정리되어 해당 untracked 잔여가 없다. 보존 씬/취소 JSON/검증 파일·이미지·exe는 `.utmp`/Builds의 Git 제외 산출물로 유지한다.
- 작업 관련 C#/meta와 기록만 미커밋이다. 상위 README/지시서/체크리스트/로드맵의 root 변경을 보존했다. stage/커밋/push는 수행하지 않았다.
