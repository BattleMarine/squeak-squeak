# S3-06 UI·포기/교체/종료 검증

2026-10-21. 구현 담당 기록. 상위 문서·검토·커밋은 root 담당이며 이 작업에서는 Git 변경을 게시하지 않는다.

## 범위·기준선

- 최신 재개 지시로 S3-06a/b를 진행한다. 과거 중단/재실행 기록은 현재 상태 판정에 재사용하지 않는다.
- main/07018b6(ahead4), root의 지시서·체크리스트 수정2개와 사용자 `_Recovery`3개를 보존한다. 정식3D 도구함·방/전시장 이동·경제·07은 범위 밖이다.
- MCP 실제 조회: 프로젝트 경로 일치, Unity6000.3.23f1, Edit/컴파일·임포트 없음, 원본 CleaningRoom Dirty=false/루트10, Console 오류0·경고0. override 빈 값/background=false/preload0. 씬·머티리얼·설정·Recovery hash는05 종료 기준과 동일하다.
- 검사는 `.utmp/s306-*`에 격리하며 사용자 persistentDataPath와 정식 exe는 자동 검사에 사용하지 않는다.

## 증거 재사용·새 공백

03b 파일 복원/손상/16개 상한·목록,04a 최종 표면/시간과 취소/실패 복귀,04b 중복 저장·종료 취소 후 재시도·명시 폐기,05 도구 제한 증거를 재사용한다. 이 전체 로직 검사를 반복하지 않는다. 사용자 경량 수동 입력 보고는 실제 보고 범위로만 유지한다.

| 항목 | 새 검사 |
| --- | --- |
| 06a 화면 | 1280×720 단일 게이지·스캐너·목표 후 더 닦기/보관/관찰 전용 문구, 확인/저장 대기/오류/상한과16개 목록의 실제 캡처·본문/버튼 배치 |
| 06a 입력 | 실제 프레임 가상 InputSystem 장치로 UI/모달 진입 전부터 누른 입력 차단. 물리 OS 드래그와 구분하고 실제 focus 가드는 우회하지 않음 |
| 06b 전환 | 교체/실험 종료의 폐기 고지·취소 복귀, 미보관 종료 고지·저장 중/실패 후 종료 취소·재시도 화면, F의 진행 보존 공백 |

## 가설·진행

- H1(미검증): 고정 본문600×120/font21에 실제 긴 파일 오류를 표시하면 안내 뒷부분이 잘린다. 실제 격리 파일 잠금 오류의 preferredHeight/렌더와 버튼 위치로 판별한다.
- H2(미검증): ShowNotice는 owner 상태를 유지하고, 이미 시작된 회전 분기는 UI 재검사를 하지 않아 모달 뒤 회전을 계속할 수 있다. 실제 가상 장치→Play 프레임으로 판별한다. 일반 확인 창에서 pointer disable하는 기존 통과 증거와 구분한다.
- 수정 전에 재현부터 진행한다. 결함 확인 시 해당 경계만 수정하고 같은 재현·인접 상태·Unity/Windows 빌드로 검증한다.

### 재현 결과

- `.utmp/s306-editor-1792545482393/long-path-save-validation-errors-remain-readable-for-the-player`의 실제 배타 잠금으로 SaveFailed를 재현했다. 본문249자/preferredHeight143/영역120, `04-long-error-before.png`에서 마지막 “취소한 뒤 더 닦으면…” 안내가 잘림을 직접 확인했다. H1 확인.
- 같은 격리 Play의 `held-notice.txt`: 실제 focus=true/가상 Mouse→실제 Play 프레임, MMB 회전 중 RequestLibrary 제한 안내 뒤 overUi=true/rotating=true/약7.999956도 변화. H2 확인. 물리 마우스 검사 아님.
- 최초 검사 표본이 원래0인 B 텍셀을 증가시켜 `Invalid/increased dirt`를 발생시켰다. 검사 오류를 보존하고 기존 값의 배율 감소 방식으로 정정했다. 제품 데이터 검증을 약화하지 않았다.
- 수정: 기존 폭640/짧은 창280은 유지하고 본문 높이에 따라 최대560까지 늘리며 그 이상은 ScrollRect로 표시한다. 버튼은 본문 아래에 고정한다. CoinSurfacePointer는 Update 초기에 UI 진입 시 기존 회전·접촉을 중단한다. 확인 전 검증 완료로 표시하지 않는다.
- 회전 차단 수정 후 실제 프레임으로 Notice 시간을 추가 검사했다. `timer-before.txt`: 회전0도/중단은 통과했지만 paused=false/안내 중 약0.1041216초 누적을 확인했다. Notice도 소유자가 SetWork(false)로 입력/효과/시간을 정지하고 닫을 때 RestoreInput하도록 보완했다. 활성 모달이 있으면 OnEnable도 작업을 재개하지 않는다.
- 일반 UI 위 기존 MMB 드래그도 중단하고 새 MMB press를 요구한다. UI 위 F 역시 실행하지 않으며 UI 밖 F는 기존 자세/줌만 복원하는 계약을 유지한다. 최초 긴 문구의 폭 측정이600으로 시작하도록 content x sizeDelta=0을 생성 시점에 설정했다.
- Editor 입력 회귀 `s306-ui-final-1792546052157/.../held-notice.txt`9조건 통과: 실제 focus/프레임에서 Notice 정지·시간 증분0·닫기 복원, 모달/일반 UI 이탈 후 held MMB 재개 없음·새 press 재개를 확인했다.
- UI 검사 좌표 오류2개(월드 Y 및 자식까지 포함한 bounds)를 카드 로컬의 실제 viewport 모서리로 정정했다. `s306-ui-final-1792546116510/.../probe-ui-flow.txt`341조건 통과했으나 직접 캡처 검토에서 배율 전환 시 스크롤 맨 아래가 유지되지 않아 마지막 줄 일부가 잘림을 추가 발견했다. 높이 재계산 시 스크롤 비율 보존을 추가했고 다시 검증한다. 수치 검사만으로 시각 통과를 단정하지 않는다.

## 최종 Editor 증거

- `.utmp/s306-ui-final-1792546274738/long-path-save-validation-errors-remain-readable-for-the-player/probe-ui-flow.txt`: **PASS341 세부 조건**. 이는341개 서로 독립된 기능이 아니라 상태/본문별 높이·버튼 배치 반복 검사 포함 수치다. 일반/실험 Notice 정지·복귀, 스캐너/게이지/목표 후 작업/보관/열람 문구, 교체/폐기/종료 취소, 실제 OS 잠금 오류·오류→종료 취소 후 재시도/복귀, 실제 worker 대기/종료 대기→완료 후 취소, 관찰 전용과 새M2를 확인했다.
- 같은 경로 `09-error.png`는 실제 긴 경로 오류의 마지막 안내까지 보이며, `11-overflow-top.png`/`12-overflow-bottom.png`는48줄 합성 스트레스 본문의 스크롤 시작/끝과 독립된 확인/취소 버튼이다. 마지막 줄을 직접 이미지로 확인했다. ScrollRect.OnScroll에 전달한 합성 휠 UI 이벤트이며 물리 휠 검사가 아니다. 캡처 시에만 Canvas/Camera를1280×720으로 전환하고 finally 복원한다. 씬 저장하지 않는다.
- `.utmp/s306-capacity-1792546317261/probe-ui-capacity.txt`: **PASS59 세부 조건**. 기존03b의16개 유효 파일을 새 경로에 복사해 UI만 재검사했다. `16-library.png`/`17-capacity.png`:16개 행·상한 안내/취소 후 같은 작업 복귀. 기존 저장 codec/안전성 전체 검사는 재실행하지 않았다.
- `s306-ui-final-1792546052157/.../held-notice.txt`: **PASS9**, 실제 focus/Play 프레임의 가상 Mouse. MMB 정상 시작→Notice에서 회전/시간 정지→닫기 복원→held 재개 차단→새 press 재개, 일반 UI 진입/이탈에서도 동일 경계를 확인했다.
- 최종 UI 경로의 `rub-ui-F.txt`: **PASS6**, 실제 focus/Play 프레임의 가상 Mouse+Keyboard. 정상 LMB 천의 B 감소를 양성 대조로 확인한 뒤 Notice 중 A/B·시간 변화0, UI 위 F 차단/UI 밖 F 자세·줌 복원과 진행·타이머·도구 보존을 확인했다. 최초 F 실패는 CoinBody 자식에 검사 회전을 준 오류였고 실제 pointer.coinTransform(Coin 루트)로 정정했다. 중간 재시도1회는 실제 focus 상실로 BLOCKED이며 가드를 우회하지 않았다.
- 제품 변경은 UI 동적 본문/스크롤·Notice 생명주기·UI 진입 시 기존 입력 중단으로 한정한다. 렌더 직전 콜백은 Dispose 해제와 먼저 파괴된 Canvas 자식 null 가드를 갖춘다. 저장 형식·반응 계수·경제·씬/머티리얼 변경 없음. Windows 검증/정식 빌드는 다음 기록을 따른다.

## Windows·배포 산출물

- 검증 Player `.utmp/s306-player-build-1792546524176/SqueakSqueak.exe`: Windows64/Mono/비개발/검증 define, **Succeeded/오류0/경고0/129694654B/20.9207883초**.
- 실제 Windows 실행: `.utmp/s306-player-ui-1792546573812/long-path-save-validation-errors-remain-readable-for-the-player`의 `probe-ui-flow.txt` **PASS341/exit0**, `.utmp/s306-player-capacity-1792546573812`의 `probe-ui-capacity.txt` **PASS59/exit0**. 실제 파일 IO/OS 잠금·프레임·uGUI 콜백/합성 휠 이벤트 검사이며 물리 마우스·키·휠 검사가 아니다. Player 로그에서 Exception/Error/Warning/FAIL 추가 항목은 없고 PASS 보고를 확인했다.
- 최초 batchmode의 `12-overflow-bottom.png`에서 일부 글자 누락처럼 보이는 캡처 차이를 의심해 보존했다. 정확한 대상은 위 Windows UI 경로의 합성48줄 본문이며 실제249자 오류 `09-error.png`의 마지막 안내는 정상이다. 추가 제품 변경 없이 동일 코드의 일반 창 모드 Player로 교차 확인했다.
- 교차 검증 빌드 `.utmp/s306-overlay-build-1792546737110/`: Succeeded/오류0/경고0/129695166B/20.1873856초. 추가 검사는 검증 define 내부 ScreenCapture 진단뿐이며 정식 제품 코드 차이는 없다.
- batchmode Overlay 캡처는5초 안에 파일이 생성되지 않아 exit1로 보존했다. 같은 검증 Player의 일반 창 모드 `.utmp/s306-player-overlay-window-1792546794324/long-path-save-validation-errors-remain-readable-for-the-player`는 **PASS342/exit0**이며, 마지막1조건은 캡처 파일 존재 확인일 뿐 시각 통과가 아니다. `12-overflow-bottom.png`에서 모든 마지막 안내/버튼을 직접 확인했다. `12-overflow-native-overlay.png`는 숨김 창의 검은 화면이므로 시각 증거에서 **제외**한다. 정식 exe의 실제 Overlay/물리 휠은 수동 검사로 남긴다. 이 진단 한계를 제품 렌더링 결함 또는 해결로 단정하지 않는다.
- 새 정식 빌드 `Builds/Windows/Step3-20261021-ui/SqueakSqueak.exe`: Windows64/Mono/`BuildOptions.None`, CleaningRoom1씬, **Succeeded/오류0/경고0/129631001B/18.006632초**. 사용자 저장 보호를 위해 정식 exe를 자동 실행하지 않았다. 실행 시 폴더 전체가 필요하다.
- 정식 exe SHA256 `82EFD5A8C297A5046F6BB72C240DAB8972E64003851935EBA0A46AD121CE0305`(Unity 공용 런처이므로 이전과 동일), 실제 변경 코드 DLL SHA256 `60A555E4F38A372E9D774F341454995D647E0EF2F96C2564758EDB19E4122CF1`. 최초 검증 DLL은 `2EA5A7E7B2D02F0DB694A17A0643708268E488D4E1EA3D5182649E4828019C3B`. 정식 DLL의 SavePlayerProbe/UseValidationFiles 이름이 없고 검증 DLL에는 probe가 있음을 확인했다. 전역 define은 변경하지 않았다.

## 정리·한계

- Build hook이 추가한 알려진 InputSystem preload1개만 디스크에서 원래 `[]`로 복원했다. 메모리 preload0, override 빈 값, background=false. 원본 씬/머티리얼/ProjectSettings/Recovery 씬·meta2개의 SHA256은 모두 시작 기준과 일치한다. Recovery는 열기/편집/저장/삭제/커밋하지 않았다.
- 일반 UI를 가로지르는 기존 MMB 드래그는 이제 중단하며 UI 밖에서 새 press가 필요하다. F는 UI 위에서 차단하고 UI 밖에서는 기존 자세/줌 전용 동작이다. 단일 게이지·보관함·스캐너의 버튼 UI는 임시 UI를 유지한다. 정식3D 도구함·방/전시장·경제/07은 구현하지 않았다.
- 검사는 실제 focus와 입력 가드를 유지한 가상 장치/프레임 검사다. 제품 수동 조작·정식 exe 실행·외부 PC·음질/체감/성능·실제 오류/상한 사용자 검사는 root의 `Step3SaveManualTest.md`를 따른다. 전체03~05 로직 검사를 다시 통과했다고 보고하지 않는다.
- 구현 담당의 제품4파일·검증 코드/메타·이 기록만 검토 대상으로 남긴다. root가 수정한 상위 문서/수동 검사와 사용자 변경을 보존하며 stage/commit/push하지 않는다. 개발 검증 결과는 위와 같고 최종06 완료 판정·상위 문서 갱신·커밋은 root 검토 대상이다.
- 최종 MCP 재조회: Edit/Play=false/Pause=false/컴파일·임포트·빌드 없음, 원본 CleaningRoom Dirty=false/루트10, Console Error0/Warning0. override 빈 값/background=false/preload0/전역 define 불변이다. ProjectSettings/Assets/Settings diff0, `git diff --check` 통과. README/ROADMAP/상위2문서/수동 검사에 대한 root의 진행 중 변경은 그대로 보존했다.
