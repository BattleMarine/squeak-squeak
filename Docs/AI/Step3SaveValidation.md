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

## 진행 결과

- 첫 코드 검사는 Editor 테스트의 CompressionLevel 모호성 컴파일 오류를 발견해 전체 네임스페이스로 정정했다. 이후95조건 통과, 계약 강화 후110조건 통과. 최종 추가 상한/완성 경계 검사는 아래 후속 결과에 기록한다. 제품 가드/저장 실패를 숨기기 위한 검사 제외는 없다.
- `CoinSaveRenderContract`는 명시적 렌더 revision과 실제 씬의 dirtColor/dirtySmoothness/maximumSmoothness를 값으로 받는다. factory는 메인 스레드에서 현재 정의·메시·머티리얼과 이 오버라이드를 동결한다. 같은 이름의 다른 머티리얼 수치/메시 내용·렌더 오버라이드 변경 검출, 로캘 독립 fingerprint를 검사했다. shader 의미·렌더 알고리즘/씬 계약 변경은 revision을 수동 갱신해야 한다. 과거 renderer 보관/마이그레이션은 아직 없으므로 불일치는 차단한다.
- 결과와 일반 진행 모두 메인 스레드에서 Unity 정의를 값 계약으로 동결한다. codec/store worker는 Unity 객체·AssetDatabase·씬에 접근하지 않는다. Task.Run에서 두 저장소 쓰기/읽기 왕복을 통과했다. DTO는 mutable 전송 자료이며 runtime 상태 원본이 아니다. pending snapshot/resultId 유지와 UI 확정은03b 책임이다.
- 형식1은 현재 Rb 완성 결과만 지원하므로 P==0을 검사한다. 미래 일반 광택/후처리 결과까지 지원한다고 주장하지 않는다. bool bitpack/float32 무손실, SHA-256은 무결성용이며 인증/보안 저장 기능은 아니다.
- 본문이 정상이면 물리 손상 백업보다 우선한다. 단, 어느 사본이든 미래/정의 불일치/불명ID/읽기 실패/지원 파일 크기 초과이면 정상 본문이 있더라도 전체 저장을 차단하는 보수 정책이다. 본문 우선의 자동 복구는 물리 손상에만 적용한다. 미래 헤더는 최소12바이트 단계에서 먼저 판별한다.
- 임시/손상 보존 파일은 미확정/진단 자료로 남기며 자동 삭제하지 않는다. 파일별 상한과 확정 결과16개는 제한하지만 진단 파일 총량은 자동 정리하지 않는다. 제품에서 용량 정리/파일 폐기는 별도 사용자 승인 절차가 필요하다.

## 검증 범위·남은 위험

114조건은 정상/백업 재읽기·동일ID 재시도/다른payload 충돌·16개 상한·worker IO·writer lock·부분 쓰기/닫기 뒤 실패·교체 전후 불명 결과·파일/압축/배열/수치 상한·짧은 미래 헤더·정의/미정의 ID·양쪽 손상·일반 진행 제약·씬 오버라이드·같은 이름의 다른 mesh/material 내용·로캘 독립성을 포함한다. 손상 main 복구 뒤 기존 정상 backup가 손상본으로 대체되지 않는 것과 손상 원본 별도 보존을 실제 파일로 검사했다.

Player/실제 프로세스 재시작 간 fingerprint·mesh readability·파일 지원/재실행/외관 복원은 **03b 필수 검증**이며 Editor 도메인 재로드 읽기로 대체하지 않는다. shader 의미·메시 생성/렌더 알고리즘 변경은 명시 revision 갱신과 호환 정책이 필요하다. 자동 migration·과거 렌더러 보관·클라우드·전원 차단 실측·사용자 파일 삭제/수리 UI는 없다. File.Replace 미지원/교체 불명은 비원자 덮어쓰기로 우회하지 않는다. 현재 형식은 광택 작업 없는 Rb 완성 표본(P0)에 한정한다.
