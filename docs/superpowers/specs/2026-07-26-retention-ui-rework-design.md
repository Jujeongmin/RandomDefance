# 리텐션 UI 개편 — 출석 팝업 · 퀘스트 분리 · 아이콘 메뉴

> 작성일 2026-07-26 · 브랜치 `dev`
> 선행 작업: 커밋 `4437b0a` (도감 · 일일 미션 · 튜토리얼 추가) 및 그 이후의 버그 수정

## 1. 배경

리텐션 3종을 붙인 뒤 실기기 확인에서 나온 요구사항이다.

- 출석 보상을 사용자가 버튼을 눌러 찾아가야 한다. 접속했을 때 알아서 떠야 한다.
- 출석과 일일 미션이 한 패널에 섞여 있어 목적이 흐리다.
- 메뉴 버튼 4개가 `UICanvas` 직속이라 상점·연구소 페이지에서도 따라다닌다.
- 버튼이 글자뿐이라 한눈에 구분되지 않는다.

## 2. 범위

포함: 출석/퀘스트 패널 분리, 출석 자동 팝업, 메뉴 버튼 5개의 위치·아이콘 개편.
제외: 연구소 페이지 리디자인(별도 작업), 도감 내부 레이아웃, 튜토리얼.

## 3. 현재 구조

```
UICanvas
├── MainCarousel/Pages/{ShopPage, MainPage, ResearchPage}   ← 스와이프 3페이지
│   └── MainPage
│       ├── Top      (HighWave 전폭 텍스트, Setting 기어 우상단)
│       ├── Mid      (UnitShowcase)
│       └── Under    (StartBtn, ModeButton)
├── OddsButton / RankingButton / CollectionButton / DailyButton   ← 캐러셀 밖
├── CollectionPanel / DailyPanel / RarityOddsPanel
└── TitleScreen
```

`DailyPanel`이 출석 7칸과 미션 3줄을 함께 담고 있다.

## 4. 설계

### 4.1 패널 분리

`DailyPanel`을 삭제하고 두 개로 나눈다. 둘 다 같은 `DailyMissionManager`를 읽는다.

| 새 파일 | 담당 |
|---|---|
| `Assets/Scripts/UI/AttendancePanel.cs` | 출석 7칸 + 받기 버튼 + 닫기 + 상태 텍스트 |
| `Assets/Scripts/UI/QuestPanel.cs` | 미션 3줄(제목·진행도·개별 받기) + 닫기 + 상태 텍스트 |

각 패널은 기존 `DailyPanel`에서 해당 절반을 그대로 옮긴다. 동작 변경은 없다.

`DailyMissionManager`에 미션 전용 조회를 추가한다. 기존 `HasClaimable`은 배지가 둘로 갈라지면서 쓸 곳이 없어지므로 제거한다.

```csharp
public bool HasClaimableMission { get; }   // 완료했는데 아직 안 받은 미션이 있는가
// CanClaimAttendance 는 이미 있음 — 출석 배지가 그대로 쓴다
```

### 4.2 출석 자동 팝업

`MainMenuManager.Start()`에서 `CanClaimAttendance`가 참이면 출석 패널을 연다.

단, 즉시 열면 안 된다. `TitleScreenPanel`이 캔버스 최상단을 덮고 있고 `AttendancePanel.Open()`이 `SetAsLastSibling()`을 호출하므로, 팝업이 타이틀 화면 **위로** 튀어나온다. 타이틀이 사라진 뒤에 열어야 한다.

```csharp
IEnumerator OpenAttendanceWhenReady()
{
    while (m_titleScreen != null && m_titleScreen.gameObject.activeSelf) yield return null;
    m_attendancePanel.Open();
}
```

`TitleScreenPanel`은 페이드가 끝나면 스스로 `SetActive(false)` 하므로 추가 API가 필요 없다. 앱 실행당 한 번만 뜨는 패널이라, 게임에서 메인으로 복귀했을 때는 이미 비활성이므로 루프가 즉시 빠져나간다.

동작 정리:
- 오늘 출석을 안 받았으면 메인 진입 때마다 뜬다(첫 실행은 타이틀 이후).
- 이미 받았으면 안 뜬다.
- 닫아도 출석 버튼으로 다시 열 수 있다.

### 4.3 메뉴 버튼 재배치

버튼 5개를 전부 `MainCarousel/Pages/MainPage` 자식으로 옮긴다. 페이지와 함께 밀려나므로 상점·연구소에서는 보이지 않는다.

`MainScreenDecorator`는 확률·랭킹 버튼의 **스타일만** 건드리고 부모·좌표는 손대지 않으므로, 그 툴을 다시 돌려도 배치가 깨지지 않는다.

좌표 (기준 해상도 720×1280, `MainPage` 로컬):

| 버튼 | anchor(min=max) | pivot | anchoredPosition | size |
|---|---|---|---|---|
| 출석 | (0, 1) | (0, 1) | (10, −10) | 88 × 88 |
| 퀘스트 | (1, 1) | (1, 1) | (−10, −88) | 88 × 88 |
| 도감 | (1, 1) | (1, 1) | (−10, −184) | 88 × 88 |
| 확률 | (0, 0) | (0, 0) | (16, 230) | 128 × 48 |
| 랭킹 | (0, 0) | (0, 0) | (16, 286) | 128 × 48 |

- 출석은 좌상단 코너에서 설정 기어(우상단 `(−10,−10)`, 70×70)와 대칭을 이룬다.
- 퀘스트·도감은 기어 아래로 세로 스택. 기어 하단이 −80이므로 8px 띄워 −88부터 시작한다.
- 확률·랭킹은 기존 좌표 그대로 둔다. 도감·출석이 위로 올라가면서 좌하단에는 이 둘만 남는다.
- `HighWave`가 상단 전폭(−20 ~ −120)을 차지하지만 텍스트가 가운데 정렬이라 좌우 끝 아이콘과 겹치지 않는다.

### 4.4 아이콘

`Assets/Down/Universal Stylized UI/Atlases/Complete_Stylized_UI_elements_icons_2.png`를 쓴다. 네이비 외곽선이 있어 기존 브랜드와 맞는다. 45칸 9열×5행이고 스프라이트 이름은 `Complete_Stylized_UI_elements_icons_2_{row*9+col}`이다.

| 버튼 | 아이콘 | 스프라이트 |
|---|---|---|
| 출석 | 초록 체크 | `..._icons_2_38` |
| 퀘스트 | 3줄 목록 | `..._icons_2_11` |
| 도감 | 카드 더미 | `..._icons_2_23` |
| 확률 | 다이아 | `..._icons_2_40` |
| 랭킹 | 금관 | `..._icons_2_33` |

달력·책·두루마리·트로피는 이 아틀라스에 없다. 위가 최선의 대응이다.

구성:
- 정사각 버튼(출석·퀘스트·도감): 아이콘 52×52 위, 라벨 18px 아래.
- 가로 버튼(확률·랭킹): 아이콘 28×28 왼쪽, 라벨 오른쪽. 크기·위치는 그대로.

`BrandUI`에 아이콘 아틀라스 경로 상수, 위 5개 인덱스 상수, 그리고 인덱스로 스프라이트를 꺼내는 `LoadIconSprite(int)`를 추가한다. 기존 `LoadAtlasSprite`는 버튼 아틀라스 전용으로 그대로 둔다 — 호출부가 전부 버튼 아틀라스를 쓰고 있어 경로를 받도록 일반화할 이유가 없다.

### 4.5 배지

기존에는 일일 버튼 하나에 배지 하나였다. 이제 둘로 나눈다.

| 배지 | 조건 |
|---|---|
| 출석 배지 | `CanClaimAttendance` |
| 퀘스트 배지 | `HasClaimableMission` |

각 버튼 우상단에 붙는 빨간 점. `DailyMissionManager.Changed`를 구독해 갱신하는 기존 방식을 그대로 쓴다.

### 4.6 에디터 툴

`Tools/Random Defense/Build Collection and Daily UI`가 하는 일:

1. 씬에 남은 구 `DailyPanel` 오브젝트를 제거한다.
2. `AttendancePanel`·`QuestPanel`·`CollectionPanel`을 만든다.
3. 버튼 5개를 `MainPage`로 옮기고 4.3 좌표로 배치한다. 퀘스트 버튼은 새로 만든다.
4. 아이콘·라벨·배지를 붙이고 `MainMenuManager` 참조를 연결한다.

여러 번 실행해도 안전해야 한다는 기존 원칙을 유지한다. 이름이 같은 오브젝트는 재사용하고, 참조가 끊기지 않게 한다.

## 5. 영향 받는 파일

신규
- `Assets/Scripts/UI/AttendancePanel.cs`
- `Assets/Scripts/UI/QuestPanel.cs`

삭제
- `Assets/Scripts/UI/DailyPanel.cs`

수정
- `Assets/Scripts/Manager/MainMenuManager.cs` — 참조 분리, 자동 팝업, 배지 2개, 라벨
- `Assets/Scripts/Manager/DailyMissionManager.cs` — `HasClaimableMission` 추가, `HasClaimable` 제거
- `Assets/Scripts/Editor/BrandUI.cs` — 아이콘 아틀라스 로더
- `Assets/Scripts/Editor/RetentionUIBuilder.cs` — 위 전부를 씬에 반영
- `Assets/Scenes/MainScene.unity` — 툴 실행 결과

## 6. 검증

에디터 툴은 사람이 Unity에서 실행해야 하므로, 확인도 실기기/플레이 모드에서 한다.

- 오늘 출석 미수령 상태로 앱 실행 → 타이틀 탭 → 출석 팝업이 뜬다.
- 받기 → 크리스탈 증가, 배지 사라짐, 닫기 동작.
- 다시 메인 진입 → 팝업 안 뜸. 출석 버튼으로는 열림.
- 퀘스트 패널에 미션 3줄만 보이고 출석 칸이 없다.
- 미션 완료 시 퀘스트 배지만 켜지고 출석 배지는 안 켜진다.
- 상점·연구소로 스와이프 → 버튼 5개가 페이지와 함께 사라진다.
- 720×1280에서 아이콘이 `HighWave` 텍스트나 설정 기어와 겹치지 않는다.

## 7. 미결 사항

- 연구소 페이지 리디자인은 다음 작업으로 미뤘다. 현재는 연구 5줄이 아이콘·진행도 바 없이 텍스트만으로 나열돼 있다.
