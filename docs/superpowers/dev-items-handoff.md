# 인수인계 — 리텐션 3종 (도감 · 일일보상 · 튜토리얼)

> 작성일 2026-07-25 · 브랜치 `claude/development-items-review-2338f1` (워크트리)
> 목표: 출시 이후 리텐션 장치 3종 추가 — **유닛 도감**, **일일 미션 + 출석**, **튜토리얼 코치마크**
> 승급/합성 시스템은 **범위에서 제외** (사용자 결정).

---

## 1. 지금 상태 한 줄 요약

C# 로직/UI 스크립트는 **전부 작성 완료**. 런타임 어셈블리는 **수동 컴파일 통과 확인**.
남은 일은 **① 에디터 어셈블리 컴파일 최종 확인 → ② Unity에서 에디터 툴 2개 실행해 씬에 UI 생성 → ③ 인게임 검증 → ④ 커밋 & 푸시**.

작업 요청 시 사용자는 **작업 완료 후 커밋 + 푸시까지** 요청했음. 아직 커밋/푸시 안 함.

---

## 2. 무엇을 만들었나 (파일별)

### 신규 파일 (7개, 전부 untracked)

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Manager/CollectionManager.cs` | 유닛 도감. 3직업×6등급=18칸을 `collectionMask` 비트마스크로 기록. 최초 발견 시 등급별 크리스탈 보너스 지급, `Discovered` 이벤트 발생 |
| `Assets/Scripts/Manager/DailyMissionManager.cs` | 일일 미션 3종(소환30/웨이브10/보스1) + 7일 순환 출석. 로컬 날짜(yyyyMMdd) 기준. **시계 되돌리기 방어** 포함. `Changed` 이벤트, `HasClaimable`(배지용), `Flush()`(진행도 배치 저장) |
| `Assets/Scripts/UI/CollectionPanel.cs` | 도감 화면. 3×6 그리드, 미발견은 실루엣+`?`, 상단 `n/18`. 스프라이트는 `m_unitSprites`(에디터 툴이 채움) → 없으면 `GManager.GetSprite()` |
| `Assets/Scripts/UI/DailyPanel.cs` | 출석 7칸 + 미션 3줄 + 수령 버튼들. `Daily.RefreshDay()` 후 populate |
| `Assets/Scripts/UI/TutorialOverlay.cs` | 최초 1회 코치마크. **셰이더 없이** 대상 사각형 상하좌우 딤 4장으로 구멍. 5단계(소환→배치→상성%→강화→판매). 1단계는 실제 소환해야 진행, 나머지 탭. 건너뛰기 상시. `TutorialDimClick`(딤 탭 전달) 동거 |
| `Assets/Scripts/Editor/BrandUI.cs` | 에디터 툴 공용 디자인 토큰/헬퍼(색·스프라이트·폰트·레이아웃 생성). `MainScreenDecorator`도 이 상수를 재사용하도록 리팩터 |
| `Assets/Scripts/Editor/RetentionUIBuilder.cs` | UI를 씬에 생성하는 에디터 툴. 메뉴 2개: `Tools/Random Defense/Build Collection and Daily UI`(MainScene), `Tools/Random Defense/Build Tutorial Overlay`(GameScene) |

### 수정 파일 (7개, 전부 M)

| 파일 | 변경 내용 |
|---|---|
| `Assets/Scripts/Manager/PlayerProgressManager.cs` | `PlayerProgressData`에 필드 추가: `collectionMask`, `missionDateKey`, `missionProgress[3]`, `missionClaimedMask`, `attendanceStreak`, `attendanceDateKey`, `lastSeenDateKey`, `tutorialDone`. `TutorialDone`/`MarkTutorialDone()` 추가. `Load()`에 `Normalize()`(배열 길이 보정) 추가. **기존 세이브 호환**(JsonUtility가 신규 필드 기본값 채움) |
| `Assets/Scripts/Data/GameBalanceData.cs` | 인스펙터 튜닝 필드: `m_collectionRewards[6]`, 미션 목표 3개 + `m_missionRewards[3]`, `m_attendanceRewards[7]`. getter: `GetCollectionReward`, `GetMissionReward`, `GetAttendanceReward`, 미션 목표 프로퍼티 |
| `Assets/Scripts/Manager/GManager.cs` | `CollectionManager`/`DailyMissionManager`를 `Awake`에서 `AddComponent`+`Initialize`. `IsCollection`/`IsDaily` 프로퍼티. 게임씬 로드 시 `TutorialOverlay` 찾아 `BeginIfNeeded()`. 메인씬 복귀 시 `Flush()`+`RefreshDay()`. `NotifyUnitSummoned`/`NotifyWaveReached`/`NotifyBossDefeated` 진행 알림. `HandleDefeat`/`HandleVictory`에서 `tutorialOverlay.Abort()`(결과 패널 안 가리게) |
| `Assets/Scripts/Manager/UnitSpawner.cs` | `Spawn()` 실제 소환 뒤 `game.NotifyUnitSummoned(...)` (에디터 테스트 소환 `ForceSpawnWithRarity`는 **제외** — 의도적) |
| `Assets/Scripts/Manager/MobManager.cs` | 웨이브 진입 시 `NotifyWaveReached`, 보스 처치 시 `NotifyBossDefeated` |
| `Assets/Scripts/Manager/MainMenuManager.cs` | 도감/일일 버튼·패널·배지 직렬화 필드 + 리스너. `BindDaily()`로 `Changed` 구독해 배지 갱신. 언어 적용에 신규 버튼 텍스트 추가 |
| `Assets/Scripts/Editor/MainScreenDecorator.cs` | 디자인 상수를 `BrandUI.*` 재사용으로 치환(동작 동일) |

---

## 3. 다음 세션이 할 일 (순서대로)

### ⓪ 워크트리 위치
```
C:\Users\anshd\RandomDefense-Unity\.claude\worktrees\development-items-review-2338f1
```
브랜치: `claude/development-items-review-2338f1`

### ① 컴파일 최종 확인
- **런타임 어셈블리는 수동 csc 컴파일 통과 확인 완료** (에러 0).
- **에디터 어셈블리는 미확인**. 수동 컴파일 시 `CS0433 PluginVersion` 에러가 났는데, 이는 **내 코드 문제가 아님** — GooglePlayGames 에셋이 asmdef 없이 있어 flat 참조 목록에서 `PluginVersion`이 Assembly-CSharp과 Google.Play.Games 양쪽에 중복된 것. 실제 Unity 빌드에선 asmdef가 분리하므로 발생 안 함. **Unity 에디터를 열어 콘솔에서 실제 컴파일 에러 유무를 확인**하면 됨.
  - 참고 수동 컴파일 방법(원하면): `C:/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Data/DotNetSdkRoslyn/csc.dll` + `Assembly-CSharp(-Editor).csproj`의 HintPath/Define 추출. 단 GooglePlayGames flat 중복 때문에 에디터쪽은 위양성이 남음. **Unity 콘솔이 정답.**

### ② 에디터 툴 실행 (Unity 필수 — 나는 실행 불가)
Unity로 이 워크트리를 열고:
1. `Tools > Random Defense > Build Collection and Daily UI` 실행
   → MainScene에 `CollectionPanel`/`DailyPanel` 생성, `MainMenuManager`에 버튼·패널·배지 자동 연결, 확률 버튼 아래로 버튼 2개 복제 배치.
2. `Tools > Random Defense > Build Tutorial Overlay` 실행
   → GameScene에 `TutorialOverlay`(딤4·말풍선·건너뛰기) 생성, 단계 대상(`SpawnBtn`/`JobBtns`/`SellBtn`) 자동 연결.
- 두 툴 모두 **여러 번 실행해도 안전**(이름 재사용). 단 재실행은 앵커/크기를 기본값으로 되돌리므로, **손으로 배치 잡은 뒤엔 재실행 금지**.
- 의존 에셋: `Assets/Down/Universal Stylized UI/...` 아틀라스. **`Assets/Down`은 gitignored** — 클론 환경엔 없을 수 있음(에셋스토어 캐시서 임포트 필요). 아틀라스 없으면 `LoadAtlasSprite`가 예외 던짐.

### ③ 인게임 검증 (Play 모드)
- 소환하면 도감 등록 + 최초 발견 크리스탈 지급, 태초(0.1%)는 500 크리스탈.
- 메인화면 도감/일일 버튼, 일일 배지(수령 대기 시 빨간 점) 표시.
- 출석 오늘 1회 수령, 미션 진행/수령, **기기 시계 과거로 돌려도 재수령 안 되는지** 확인.
- 첫 게임씬 진입 시 튜토리얼 뜨고, 1단계는 소환해야 넘어가고, 건너뛰기 동작, 튜토 중 패배해도 결과 패널 안 가림.
- 배치는 720×1280 기준으로 코드 계산 — 실제로 보고 **인스펙터에서 미세 조정**(특히 도감 그리드 셀 높이, 말풍선 위치).

### ④ 커밋 & 푸시 (사용자 명시 요청)
- 신규 7 + 수정 7 = 14개 파일 + **Unity가 생성한 .meta + 씬 변경**(MainScene/GameScene .unity)까지 스테이징.
- 커밋 메시지 끝에 반드시:
  ```
  Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
  ```
- 푸시.

---

## 4. 설계 결정 · 주의점 (재논의 방지)

- **UI 생성 방식**: 에디터 툴로 씬에 생성(기존 `MainScreenDecorator` 패턴). 런타임 생성 아님 — 커밋 `5b3d7fc`에서 런타임 생성 코드를 제거한 이력 있어 방향 반대. **Scene UI는 hands-off 메모리** 있음: 배치/SellPanel 텍스트를 코드로 덮지 말 것(이번 신규 패널은 새로 만드는 것이라 예외).
- **렌더 파이프라인**: Built-in RP. 튜토 딤은 마스킹 셰이더 대신 Image 4장 — **URP/셰이더 도입 금지** 메모리 준수.
- **밸런스는 전부 `GameBalanceData.asset` 인스펙터**에서 조정 가능하게 뺌(보상 수치 하드코딩 아님). 단 `ResearchManager`의 배율은 기존부터 하드코딩(이번 범위 아님).
- **에디터 테스트 소환 제외**: `UnitSpawner.ForceSpawnWithRarity`는 도감/미션에 반영 안 함(실제 소환만).
- **시계 되돌리기 방어**: `lastSeenDateKey`보다 과거 날짜면 미션 리셋·출석·수령 전부 무시(`ClockRolledBack`).
- **세이브 호환**: 기존 유저 세이브 그대로 로드됨. `Normalize()`가 `missionProgress` 배열 길이 보정.

## 5. 알려진 리스크 / 확인 필요
- 도감 아이콘 스프라이트 경로 `Assets/GData/Image/Character/{Class}_{tier}.png`의 `{Class}_{tier}_1` 프레임 가정 — 메인 쇼케이스와 동일 규칙이나 **실제 존재 확인** 필요.
- `RetentionUIBuilder.CloneMenuButton`은 부모에 LayoutGroup 없으면 확률 버튼 아래로 수동 오프셋 — MainScene 버튼 부모 구조에 따라 겹칠 수 있으니 **육안 확인**.
- 스펙 문서(brainstorming의 정식 spec)는 별도로 안 씀 — 사용자가 바로 구현 지시. 필요하면 이 문서가 사실상의 스펙.
