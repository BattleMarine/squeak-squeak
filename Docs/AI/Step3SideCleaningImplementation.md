# S3-C06 동전 옆면 청소 구현·검증

2026-10-21. 요구사항 커밋 d209189 이후 구현 담당 기록. root는 상위 문서·독립 검토·커밋 담당, 이 작업에서는 stage/commit/push하지 않는다.07c 사용자 최종 판정은 계속 대기다.

## 구현 계약·영향

- 기존 표면 소유자/입력 승인/반응/저장 경로를 확장한다. 새 일반 단계는 기존 허용 도구로 옆면 B 청소·광택/완료를 지원하고, M1/M2는 기존 칫솔/천/스포이드·스캐너의 옆면 작업·도포 풀림·최종 결과 보관/재열람을 지원한다. 일반 모드에 종전에 없던 스포이드·스캐너 UI나 완성 결과 파일 보관 UI를 추가하지 않는다(일반 진행은 기존 별도 저장). A 덩어리 배치는 기존 앞/뒷면만 유지하며 옆면용 새 미술/경제/손상 시스템은 만들지 않는다. 초기 B가0인 단계는 옆면도0이다.
- 옆면 좌표는 원통의 U=둘레 각도(0..1 반복), V=높이(0..1)다. 옆면 직사각형 마스크는 N×max(4,N/16), 앞뒤의 원판 N×N은 유지한다. UV 이음부는 짧은 둘레 경로로 이어지고 높이 끝은 clamp한다. 앞/뒤↔옆면 접촉 변경은 기존 단절 계약대로 이전 stroke를 끊으며 인접 chart를 무조건 함께 칠하지 않는다.
- 원판은 기존 월드 원형 footprint, 옆면은 둘레 호 길이+높이를 펼친 월드 거리의 원형 footprint를 쓴다. 현재 지원하는 Transform 범위를 보존하고 둘레/높이 실제 월드 크기와 회전을 반영한다. 새 Scale 유형 지원을 목표로 확대하지 않는다. 정지/최초 클릭 청소 금지·MMB/UI/focus/F/휠 차단·시간 예산은 기존 공통 승인 경로를 유지한다. 약품은 한 클릭 한 방울, 재질 반응 계수는 그대로다.
- 단일 게이지는 유지한다. B 합계는 각 영역의 텍셀 밀도 차이를 정규화해 앞/뒤/옆면을 집계하고, **실험 완료는 기존 A 전부/B95%에 더해 옆면 자체도95% 제거**를 요구한다. 일반 완료에도 기존 단계 B 허용치를 옆면 자체에 동일 적용한다. 옆면이 작다는 이유로 오염된 채 완료되지 않는다. GPU는 제3 텍스처/옆면 UV를 샘플하고 스캐너가 남은 옆면 B/풀림을 표시한다.
- 주 영향: CoinSurfaceMapper/MaskData/RubMotion/BrushStroke/Contamination·셰이더, 진행/완료, ExperimentSnapshot/저장 codec/검증/복원. 기존 serialized 필드·GUID·씬 참조와 앞뒤 셰이더 속성은 유지한다. 필요 없는 씬 저장/새 패키지 추가는 하지 않는다.

## 저장 호환안(구현 기준)

- **공통 envelope·일반 진행·보관함 outer version1을 그대로 유지**하고 완성 결과 레코드만 v1/v2를 구분한다. v2는 옆면 B/P/풀림 배열을 추가하며 혼합 보관함에서 v1 결과도 그대로 읽고 재기록한다. 미래/손상 데이터는 기존 차단·백업 정책을 유지한다. 사용자 저장은 직접 읽거나 바꾸지 않고 기존 `.utmp` 유효본 복사로 검증한다.
- v1 양면 결과는 HasSide=false/빈 옆면 데이터로 복원한다. 앞뒤 배열·ID·시간을 보존하고 새 옆면 오염을 붙이지 않으며 옆면 외관은 종전대로 둔다. 관찰 화면에 이전 결과의 옆면 데이터 없음 표시를 추가한다. 동일 ID 재시도 시 v1 payload를 v2로 바꿔 충돌시키지 않는다.
- 기존 정의 fingerprint 계산과 rb-v1 렌더 계약은 v1 검증용으로 유지한다. v2에는 기존 fingerprint에서 파생한 명시적인 옆면 계약 fingerprint/렌더 revision을 사용한다. 일반 진행 형식과 과거 결과를 전역 버전/셰이더 변경만으로 DefinitionChanged 처리하지 않는다. 새 쓰기는 HasSide에 따라 v2를 선택한다.

## 검사 계획·기준선

- 실제 MCP 조회: 올바른 프로젝트/Unity6000.3.23f1/Edit/컴파일·임포트 없음, CleaningRoom Dirty=false/루트10, Console 오류0·경고0. main/d209189(ahead7), 사용자 Recovery만 untracked.07 종료의 씬/머티리얼/설정/Recovery 보존 기준을 유지한다.
- CPU: U 이음부·높이/앞뒤 경계·현재 지원 Transform의 월드 footprint·정지/중복 frame·반응/도포·진행 분모·옆면만 남은 완료 거부·리셋/복원·v1/v2 혼합/손상/미래/동일ID.
- Unity: 실제 MeshCollider 회전 적중→옆면 좌표·GPU 마스크/셰이더·스캐너 캡처, 입력 차단/관찰 전용, 양면 회귀·자원/참조/Console.
- Windows: 격리 검증 Player의 옆면 작업→최종 보관→새 프로세스 복원/구버전 혼합, 별도 정식 빌드·검증 define 제외·부작용 정리. 물리 드래그·체감/실제 Overlay·07c는 수동 검사로 구분한다.

## 진행

- 요구/기존 확장점·저장 호환 위험을 확인하고 위 계약으로 착수했다. 구현·통과 판정 전이다.
- 옆면 U/V·호 길이 footprint·마스크/셰이더·진행/완료·v1/v2 혼합 codec/불변 snapshot을 연결했고 Unity 컴파일/Console 오류·경고0을 확인했다. 기존 저장 envelope/일반 진행 버전과 v1 fingerprint 계산은 그대로다. A 배치는 캡 전용이므로 옆면 stroke는 캡 A를 지우지 않게 하고 옆면 효과선의 법선을 보완했다.
- `.utmp/side-cpu-1792558125021`: 옆면 CPU/실제v1→혼합v2 호환79조건 통과. 앞뒤 기존 CPU 회귀 데이터212/브러시86/광택69도 통과했다. 종전 옆면 hover 거부 검사는 새 요구대로 옆면 hover/커서 허용·입력 없는 청소 거부로 갱신했다.
- `.utmp/side-editor-1792558470549`: seed99조건·새 Play reload38조건 통과. 실제 회전 MeshCollider/셰이더의 옆면 샘플과 텍스처를 대조하고 M1/M2 작업/보관·앞뒤만 청소한 완료 거부·v1/v2 혼합·2/3개 텍스처 전환을 확인했다. `dirty-M1.png`/`scanner-M1.png`/`legacy-label.png`를 직접 확인했다. 카메라 진단 이미지는 명시적 Linear RT이며 실제 Overlay 색감/조도 품질 보증은 아니다. 기존 결과 안내의 “옆면 없음(이전 결과)”는 잘림 없이 표시됐다.
- 같은 경로 `side-rub-ui-F.txt`7조건: 실제 focus/Play 프레임에 가상 Mouse+Keyboard를 전달해 실제 옆면 적중/LMB 제거가 옆면만 바꾸는 양성 대조, 모달 뒤 청소·시간 차단, UI 위 F 차단/밖 F 자세·줌 복원과 진행·도구 보존을 확인했다. 물리 OS 드래그 검사가 아니다. 일반 단계·Windows·도포 입력의 남은 검증을 계속한다.
- `side-drop-input.txt`7조건도 실제 focus/가상 Mouse 프레임에서 통과: MMB+LMB 도포0, MMB 해제 뒤 held 재개 차단, fresh click1회·hold 반복0, UI 클릭/held 복귀 차단, release 뒤 새 클릭2회째·캡 불변.
- 일반 단계 검사는 검사 코드의 없는 AvailableTools API 참조를 GetToolAt로 정정했고, 완료 연출 이동 후 옛 접촉/새 행렬을 혼합한 광택 표본 오류를 고정 workMatrix로 정정했다. `.utmp/side-normal-final-1792558871757/side-normal.txt`10조건 통과: 실제 일반 stage-01의 옆면 생성/허용 도구 사용, 전체 평균은 목표 이상인데 옆면 자체는 미달일 때 완료 거부, 옆면까지 청소하면 완료, 옆면만 광택 증가. 별도 격리 접근 fixture이며 실제 획득/보상 검사로 주장하지 않는다. 실패 로그는 지우지 않았다.
- 옆면 커서 범위 링도 원통 chart에 배치하고 높이 끝에서 clamp하여 접평면 밖으로 떠 있는 원만 표시하지 않도록 보완했다. 새 Scene/Inspector 참조 없이 기존 pointer가 보유한 행렬/Bounds를 전달한다. Windows·최종 정리는 계속 진행한다.

## 최종 검증·산출물

| 기준 | 근거 | 판정/제한 |
| --- | --- | --- |
| 옆면 U 이음부/회전/월드 footprint·정지/단절·반응 | `.utmp/side-cpu-1792558125021/side-cpu.txt`79조건 | 통과. 현재 원통/지원 Transform 범위의 검사이며 새 형상 지원 아님 |
| 기존 원판·저장 회귀 | 기존 CPU 마스크212/브러시86/광택69, 안전 저장114 재실행 | 통과. 미래 결과 fixture는 이제 지원하는2가 아닌3으로 갱신.114 원본 `.utmp/s303a-3242c965e56f4b51b227c01cc6d77f2d` |
| 일반 단계 | `.utmp/side-normal-final-1792558871757/side-normal.txt`10조건 | 통과. 접근 fixture/승인 커널 검사, 실제 해금·물리 입력 아님 |
| 옆면 청소·커서·UI/F | `.utmp/side-editor-1792558470549/side-rub-ui-F.txt`최종8조건 | 실제 focus/Play 프레임의 가상 Mouse+Keyboard 통과. 곡면 링48점의 월드 반경/높이 범위도 확인 |
| 방울·MMB/UI release gate | 같은 경로 `side-drop-input.txt`7조건 | 실제 focus/가상 Mouse 프레임 통과. 한 클릭1회/hold 반복 없음/차단 뒤 release 필요 |
| 통합·저장·GPU·구버전 | Editor seed99/새 Play reload38, 아래 Windows seed99/새 프로세스 reload38 | 통과. 실제 MeshCollider/텍스처/파일 IO/UI·합성 승인 rub/완료/시계 자극이며 물리 조작 아님 |
| 표시·스캐너·기존 결과 안내 | dirty/scanner/stored/reload-mask 및 legacy-label 캡처 직접 확인 | 옆면 오염/스캐너·mask 샘플과 이전 결과 옆면 없음 문구 확인. Linear 카메라 진단과 Overlay 캡처를 구분 |

- 최초 검증 빌드는 오류0·경고486(기존 Sentis485+새 CoinSurface1)이었다. 암시적 derivative 제거만으로는 경고가 남아, 모든 분기의 반환 mask를0으로 초기화하고 mip 없는 Point 마스크를 LOD0으로 읽도록 보완했다. 후속 빌드는485개 Sentis 계열만 남았고 자체 셰이더 경고0, 최종 캐시 빌드는 경고0이다. 경고 억제·패키지 변경·Console 지우기 없음.
- **최종 검증 Player** `.utmp/side-release-test-build-1792559695540/SqueakSqueak.exe`: Windows x64/Mono/비개발+검증 define, CleaningRoom1씬, **Succeeded/오류0/경고0/129727118B/18.211842초**. 중간 빌드·실패/경고 기록은 `.utmp`에 보존했다.
- **최종 Windows 실행** `.utmp/side-handoff-player-1792559735912`: seed99/exit0 → 별도 새 프로세스 reload38/exit0. 원본 `probe-side-seed.txt`/`probe-side-reload.txt`/`side-identities.txt`/`gpu-<ID>.txt`를 보존한다. 구버전 M1/M2 두 개+새 옆면 M1/M2 두 개의 ID/최종시간/A·세 영역 B/P/풀림 배열과 새 옆면 RGBAHalf 바이트를 재실행 전후 비교했다. 기존2개의 앞뒤/시간은 그대로, 옆면을 새로 붙이지 않았다.
- 본 파일366603B/직전 백업363377B/일반 진행128B. v1 동일 ID 재시도는 같은 v1 payload로 AlreadySaved이며 파일 바이트가 바뀌지 않는다. 새 v2 추가 후에도 일반 진행·소유권을 바꾸지 않는다. 미완료 옆면 작업은 종료 고지 뒤 실제 제품 확인→ConfirmAppQuit로 종료하고 재실행에서 복원하지 않았다. seed 선행 검사 기록에 Finish(false)를 사용하며 종료는 probe 승인 우회가 아니다. reload 검사 종료만 기존 도구 승인 경로다.
- Player 로그에는 managed Exception/FAIL이 없지만 Unity 서비스 DNS 오류 Curl6가 seed3/reload3건 있다. 기존 환경 통신 한계로 구분하며 전체 Player 로그 오류0이라고 보고하지 않는다. 네트워크 설정/패키지를 변경하지 않았다.
- **새 정식 빌드** `Builds/Windows/Step3-20261021-side/SqueakSqueak.exe`: Windows x64(PE0x8664)/Mono/`BuildOptions.None`, CleaningRoom1씬, **Succeeded/오류0/경고0/129638889B/15.6335276초**. 출력 폴더 전체가 필요하다. 사용자 저장 보호를 위해 정식 exe는 자동 실행하지 않았다.
- SHA256: 정식 exe `82EFD5A8C297A5046F6BB72C240DAB8972E64003851935EBA0A46AD121CE0305`(Unity 공용 런처), 정식 `Assembly-CSharp.dll` **`E9946F4F3DB1055A7D4D992C97042C2B035A7FFE2A2476219A4B70CD563FCAE8`**, 검증 DLL `572DC4DF8F4CFFAFEB6EE8631B36FC1C0EB0D1657F47F736425F274DE1CCDEB7`. 정식 DLL에 SavePlayerProbe/RunSideCleaning/UseValidationFiles와 격리 CLI 문자열이 없고 검증본에는 있음을 확인했다.

## 인계·제한

- 일반 HUD의 종전 “옆면은 닦지 않아요”를 옆면 청소 안내로 고치고, 평균 B는 목표여도 옆면 자체가 미달이면 다음 청소 안내를 유지한다. 실험 조작 안내에도 앞·뒤·옆면 회전을 표시했다. 새 Inspector 참조/Scene 저장·패키지·경제·A옆면 미술 추가 없이 기존 구조를 확장했다.
- CPU 데이터 기본 생성자의 includeSide=false는 기존 순수 양면 fixture/구버전 재구성을 위한 호환 API다. 실제 새 판 Surface 소유자는 명시적으로 true를 사용한다. 구버전 결과는 관찰 전용/옆면 없음이고 다음 새 판부터 옆면 오염을 생성한다. 공통 envelope·일반 진행·보관함 outer는1, 결과 레코드만1/2 혼합이다. 옛 프로그램은 v2 결과를 미래 형식으로 차단하므로 새 파일을 옛 빌드로 편집하지 않는다.
- 성능/FPS·실사용 장기 누수·체감/음질·OS 물리 드래그·정식 exe 실제 Overlay/다른 PC·07c 사용자 최종 판정은 이번 자동 통과와 별개다. 현재 원통/단일 재질의 옆면 B/풀림·일반 광택 확장이며 옆면 A 덩어리/새 형상/고급 손상·경제/방 이동은 추가하지 않았다.
- **개발 구현·검증 통과(위 한계 포함), root 독립 검토 대기.07c는 계속 대기.** 최종 보존/Editor/Git 확인은 아래 정리 기록을 따른다. root의 상위/수동 문서 변경과 Recovery를 보존하며 구현 담당 stage/commit/push는0이다.

## 최종 정리

- 원본 CleaningRoom/사용자 Coin_Surface/ProjectSettings/Recovery 씬·meta2개의 SHA256이07 종료·이번 착수 기준과 모두 일치한다. Scene 편집/저장·Recovery 열기/수정/삭제·사용자 저장 직접 접근0이다.
- 실제 build hook가 남긴 확인된 InputSystem preload1개만 디스크에서 원래 `[]`로 복원했다. 최종 메모리 preload0/override 빈 값/background=false, 전역 define 불변이다. Assets/Settings·ProjectSettings·Packages 내용 diff0, `git diff --check` 통과. `.utmp`/Builds 산출물 ignore 확인, 임시 Resources 잔여 없음. 정상/실험 검증 fixture는 모두 Git 제외 경로에 보존했다.
- 최종 MCP: Edit/Play=false/Pause=false/컴파일·임포트·빌드 없음, 원본 CleaningRoom Dirty=false/루트10, Console Error0/Warning0, Coin Surface shaderMessages0. 필수 controller 직렬화 참조22개/null0, 추가 Inspector 연결 없이 기존 참조를 유지한다.
- 구현 담당 변경은 옆면 관련 런타임/셰이더·필요한 기존 검증 helper 갱신·신규 검증 코드/meta·이 문서다. root가 진행 중인 ROADMAP/Step3 지시서·체크리스트·수동 검사 갱신은 별도 소유로 보존한다. 커밋·push·07c 완료 처리는 하지 않았다.

## 독립 검토 결과

2026-10-21 root 핵심 코드·검증 근거 검토 통과. CPU/호환79조건 독립 재실행 PASS, Windows99/38 원본과 정식 DLL hash 일치 확인. 상세 태그 s3-c06-f1~f3은 Step3ImplementationChecklist.md 하단을 따른다. 사용자 물리 조작·07c는 대기다.
