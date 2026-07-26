# 리텐션 UI 개편 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 출석과 퀘스트를 별도 패널로 나누고, 출석은 메인화면 진입 시 자동으로 띄우며, 메뉴 버튼 5개를 아이콘화해 메인 페이지 안으로 옮긴다.

**Architecture:** 런타임 쪽은 `DailyPanel` 하나가 하던 일을 `AttendancePanel`·`QuestPanel` 둘로 쪼갠다. 둘 다 기존 `DailyMissionManager`를 읽기만 하므로 저장 형식과 보상 로직은 건드리지 않는다. 씬 구성은 전부 에디터 툴 `RetentionUIBuilder`가 만든다 — 런타임에 UI를 생성하지 않는 기존 방침을 따른다.

**Tech Stack:** Unity 6000.4.0f1, Built-in Render Pipeline, uGUI + TextMeshPro, C# 9.

## Global Constraints

- 렌더 파이프라인은 Built-in이다. URP 셰이더나 머티리얼로 바꾸지 않는다.
- 씬 UI는 기본적으로 hands-off다. 이 계획이 명시적으로 지정한 오브젝트·좌표만 건드린다.
- 기준 해상도는 720×1280이다. 좌표는 전부 이 기준이다.
- 모든 사용자 노출 문자열은 `GameLanguage.Choose(한국어, English)` 또는 `GameLanguage.IsEnglish` 분기를 거친다.
- 에디터 툴은 여러 번 실행해도 안전해야 한다. 이름이 같은 오브젝트는 재사용하고 참조를 끊지 않는다.
- 아이콘 아틀라스는 `Assets/Down/Universal Stylized UI/Atlases/Complete_Stylized_UI_elements_icons_2.png`다. `Assets/Down`은 gitignore 대상이라 클론 환경에는 없을 수 있다.
- **이 프로젝트에는 테스트 프레임워크가 없다.** 각 태스크의 검증은 `Tools/compile-check.ps1`(Unity 번들 Roslyn으로 컴파일)과 마지막 태스크의 수동 플레이 확인이다. 컴파일 검증은 Unity 콘솔을 대체하지 않는다.

**컴파일 검증 명령** (모든 코드 태스크에서 사용):

```bash
powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1
```

```bash
powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1 -Project Assembly-CSharp-Editor.csproj
```

성공 시 마지막 줄이 `OK - no compile errors`, 실패 시 `FAILED - N error(s)`이며 종료 코드가 1이다.

## File Structure

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/UI/AttendancePanel.cs` (신규) | 출석 7칸 표시와 하루 1회 수령 |
| `Assets/Scripts/UI/QuestPanel.cs` (신규) | 퀘스트 3줄 표시와 개별 수령 |
| `Assets/Scripts/UI/DailyPanel.cs` (삭제) | 위 둘로 대체 |
| `Assets/Scripts/Manager/DailyMissionManager.cs` | 퀘스트 전용 수령 가능 여부 질의 추가 |
| `Assets/Scripts/Manager/MainMenuManager.cs` | 두 패널 참조, 자동 팝업, 배지 2개 |
| `Assets/Scripts/Editor/BrandUI.cs` | 아이콘 아틀라스 로더와 인덱스 상수 |
| `Assets/Scripts/Editor/RetentionUIBuilder.cs` | 위 전부를 씬에 반영 |

---

### Task 1: 출석·퀘스트 패널 스크립트

`DailyPanel`이 하던 일을 두 클래스로 나눈다. 아직 아무도 이 둘을 참조하지 않으므로 `DailyPanel`은 그대로 두고, 이 태스크만으로도 컴파일이 통과한다.

**Files:**
- Create: `Assets/Scripts/UI/AttendancePanel.cs`
- Create: `Assets/Scripts/UI/QuestPanel.cs`
- Reference: `Assets/Scripts/UI/DailyPanel.cs` (옮겨올 원본, 이 태스크에서는 수정하지 않음)

**Interfaces:**
- Consumes: `DailyMissionManager`의 기존 공개 멤버 — `RefreshDay()`, `ClaimAttendance()`, `ClaimMission(int)`, `CanClaimAttendance`, `PendingAttendanceDay`, `ClaimedAttendanceDay`, `GetAttendanceReward(int)`, `CanClaim(int)`, `IsClaimed(int)`, `GetTitle(int)`, `GetProgress(int)`, `GetGoal(int)`, `GetReward(int)`, 상수 `AttendanceCycle`(7)·`MissionCount`(3). `GameLanguage.IsEnglish`, `GameLanguage.Choose(string,string)`, `GameAudioManager.Play(GameAudioManager.Sfx.Upgrade)`.
- Produces: `AttendancePanel.Open()`, `AttendancePanel.Close()`, `QuestPanel.Open()`, `QuestPanel.Close()` — 전부 `public void`, 인자 없음. Task 2의 `MainMenuManager`와 Task 4의 빌더가 쓴다. 빌더가 채워야 할 직렬화 필드 이름은 Task 4에 그대로 나온다.

- [ ] **Step 1: `AttendancePanel.cs` 생성**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 7일 순환 출석 보상 화면. 오늘 받을 칸만 금색으로 켜지고 하루에 한 번 수령합니다.
/// 오늘 아직 받지 않았다면 MainMenuManager가 메인화면에 들어올 때 자동으로 띄웁니다.
/// UI는 씬에 미리 배치돼 있고, 직렬화 참조로 연결됩니다.
/// </summary>
public class AttendancePanel : MonoBehaviour
{
    static readonly Color CreamText = new Color(0.96f, 0.90f, 0.78f, 1f);
    static readonly Color DoneText = new Color(0.55f, 0.58f, 0.66f, 1f);
    static readonly Color ReadyText = new Color(1.00f, 0.83f, 0.28f, 1f);
    static readonly Color TodayTint = new Color(1.00f, 0.83f, 0.28f, 1f);
    static readonly Color ClaimedTint = new Color(0.24f, 0.30f, 0.24f, 1f);
    static readonly Color UpcomingTint = new Color(0.10f, 0.09f, 0.20f, 1f);
    static readonly Color TodayLabel = new Color(0.11f, 0.08f, 0.30f, 1f);

    [Header("References (씬에서 할당)")]
    [SerializeField] Button m_bgButton;
    [SerializeField] Button m_closeButton;
    [SerializeField] TextMeshProUGUI m_titleText;
    [SerializeField] TextMeshProUGUI m_closeText;

    [Header("Attendance")]
    [Tooltip("1~7일차 칸 배경")]
    [SerializeField] Image[] m_cells = new Image[DailyMissionManager.AttendanceCycle];
    [SerializeField] TextMeshProUGUI[] m_labels = new TextMeshProUGUI[DailyMissionManager.AttendanceCycle];
    [SerializeField] Button m_claimButton;
    [SerializeField] TextMeshProUGUI m_claimText;

    [Header("Feedback")]
    [SerializeField] TextMeshProUGUI m_statusText;

    bool m_wired;

    static DailyMissionManager Daily => GManager.Instance != null ? GManager.Instance.IsDaily : null;

    public void Open()
    {
        EnsureWired();
        Daily?.RefreshDay();
        if (m_statusText != null) m_statusText.text = string.Empty;
        Populate();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close() => gameObject.SetActive(false);

    void EnsureWired()
    {
        if (m_wired) return;
        m_wired = true;
        if (m_bgButton != null) m_bgButton.onClick.AddListener(Close);
        if (m_closeButton != null) m_closeButton.onClick.AddListener(Close);
        if (m_claimButton != null) m_claimButton.onClick.AddListener(Claim);
    }

    void Claim()
    {
        DailyMissionManager daily = Daily;
        if (daily == null) return;

        int reward = daily.ClaimAttendance();
        if (reward > 0)
        {
            GameAudioManager.Play(GameAudioManager.Sfx.Upgrade);
            if (m_statusText != null)
            {
                m_statusText.text = GameLanguage.Choose($"크리스탈 +{reward:N0}", $"+{reward:N0} crystals");
                m_statusText.color = ReadyText;
            }
        }
        Populate();
    }

    void Populate()
    {
        bool english = GameLanguage.IsEnglish;
        DailyMissionManager daily = Daily;

        if (m_titleText != null) m_titleText.text = english ? "ATTENDANCE" : "출석 보상";
        if (m_closeText != null) m_closeText.text = english ? "CLOSE" : "닫기";

        int claimedDay = daily != null ? daily.ClaimedAttendanceDay : 0;
        int pendingDay = daily != null ? daily.PendingAttendanceDay : 1;
        bool canClaim = daily != null && daily.CanClaimAttendance;

        for (int day = 1; day <= DailyMissionManager.AttendanceCycle; day++)
        {
            int slot = day - 1;
            // 오늘 받을 칸은 금색, 이미 받은 칸은 어두운 초록, 남은 칸은 기본 네이비.
            bool isToday = canClaim && day == pendingDay;
            bool isClaimed = !canClaim ? day <= claimedDay : day < pendingDay;

            if (slot < m_cells.Length && m_cells[slot] != null)
                m_cells[slot].color = isToday ? TodayTint : isClaimed ? ClaimedTint : UpcomingTint;

            if (slot < m_labels.Length && m_labels[slot] != null)
            {
                int reward = daily != null ? daily.GetAttendanceReward(day) : 0;
                m_labels[slot].text = english ? $"DAY {day}\n{reward:N0}" : $"{day}일차\n{reward:N0}";
                m_labels[slot].color = isToday ? TodayLabel : CreamText;
            }
        }

        if (m_claimButton != null) m_claimButton.interactable = canClaim;
        if (m_claimText != null)
        {
            m_claimText.text = canClaim
                ? (english ? "CLAIM" : "받기")
                : (english ? "COME BACK TOMORROW" : "내일 다시 오세요");
            m_claimText.color = canClaim ? ReadyText : DoneText;
        }
    }
}
```

- [ ] **Step 2: `QuestPanel.cs` 생성**

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 퀘스트 3종(소환·웨이브·보스) 화면. 진행도는 게임을 하는 동안 쌓이고,
/// 완료한 퀘스트만 개별로 수령합니다. 날짜가 바뀌면 진행도가 초기화됩니다.
/// UI는 씬에 미리 배치돼 있고, 직렬화 참조로 연결됩니다.
/// </summary>
public class QuestPanel : MonoBehaviour
{
    static readonly Color CreamText = new Color(0.96f, 0.90f, 0.78f, 1f);
    static readonly Color DoneText = new Color(0.55f, 0.58f, 0.66f, 1f);
    static readonly Color ReadyText = new Color(1.00f, 0.83f, 0.28f, 1f);

    [Header("References (씬에서 할당)")]
    [SerializeField] Button m_bgButton;
    [SerializeField] Button m_closeButton;
    [SerializeField] TextMeshProUGUI m_titleText;
    [SerializeField] TextMeshProUGUI m_closeText;

    [Header("Quests")]
    [SerializeField] TextMeshProUGUI[] m_questTitles = new TextMeshProUGUI[DailyMissionManager.MissionCount];
    [SerializeField] TextMeshProUGUI[] m_progressTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];
    [SerializeField] Button[] m_claimButtons = new Button[DailyMissionManager.MissionCount];
    [SerializeField] TextMeshProUGUI[] m_claimTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];

    [Header("Feedback")]
    [SerializeField] TextMeshProUGUI m_statusText;

    bool m_wired;

    static DailyMissionManager Daily => GManager.Instance != null ? GManager.Instance.IsDaily : null;

    public void Open()
    {
        EnsureWired();
        Daily?.RefreshDay();
        if (m_statusText != null) m_statusText.text = string.Empty;
        Populate();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Close() => gameObject.SetActive(false);

    void EnsureWired()
    {
        if (m_wired) return;
        m_wired = true;

        if (m_bgButton != null) m_bgButton.onClick.AddListener(Close);
        if (m_closeButton != null) m_closeButton.onClick.AddListener(Close);

        for (int i = 0; i < m_claimButtons.Length; i++)
        {
            int index = i; // 클로저가 반복 변수를 잡지 않도록 복사
            if (m_claimButtons[i] != null) m_claimButtons[i].onClick.AddListener(() => Claim(index));
        }
    }

    void Claim(int index)
    {
        DailyMissionManager daily = Daily;
        if (daily == null) return;

        int reward = daily.ClaimMission(index);
        if (reward > 0)
        {
            GameAudioManager.Play(GameAudioManager.Sfx.Upgrade);
            if (m_statusText != null)
            {
                m_statusText.text = GameLanguage.Choose($"크리스탈 +{reward:N0}", $"+{reward:N0} crystals");
                m_statusText.color = ReadyText;
            }
        }
        Populate();
    }

    void Populate()
    {
        bool english = GameLanguage.IsEnglish;
        DailyMissionManager daily = Daily;

        if (m_titleText != null) m_titleText.text = english ? "DAILY QUESTS" : "일일 퀘스트";
        if (m_closeText != null) m_closeText.text = english ? "CLOSE" : "닫기";

        for (int i = 0; i < DailyMissionManager.MissionCount; i++)
        {
            bool canClaim = daily != null && daily.CanClaim(i);
            bool claimed = daily != null && daily.IsClaimed(i);

            if (i < m_questTitles.Length && m_questTitles[i] != null)
            {
                m_questTitles[i].text = daily != null ? daily.GetTitle(i) : string.Empty;
                m_questTitles[i].color = claimed ? DoneText : CreamText;
            }

            if (i < m_progressTexts.Length && m_progressTexts[i] != null)
            {
                int progress = daily != null ? daily.GetProgress(i) : 0;
                int goal = daily != null ? daily.GetGoal(i) : 0;
                int reward = daily != null ? daily.GetReward(i) : 0;
                m_progressTexts[i].text = $"{progress:N0} / {goal:N0}   <color=#FFD447>+{reward:N0}</color>";
                m_progressTexts[i].color = claimed ? DoneText : CreamText;
            }

            if (i < m_claimButtons.Length && m_claimButtons[i] != null)
                m_claimButtons[i].interactable = canClaim;

            if (i < m_claimTexts.Length && m_claimTexts[i] != null)
            {
                m_claimTexts[i].text = claimed
                    ? (english ? "DONE" : "완료")
                    : canClaim ? (english ? "CLAIM" : "받기") : (english ? "IN PROGRESS" : "진행 중");
                m_claimTexts[i].color = claimed ? DoneText : canClaim ? ReadyText : CreamText;
            }
        }
    }
}
```

- [ ] **Step 3: 컴파일 검증**

Run:
```bash
powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1
```
Expected: `OK - no compile errors`

- [ ] **Step 4: `.meta` 파일 생성**

Unity를 아직 열지 않았으므로 `.meta`가 없다. 없는 채로 커밋하면 클론 환경에서 GUID가 새로 생겨 씬 참조가 끊긴다. 다른 `.meta`와 같은 최소 형식으로 직접 만든다.

Run:
```bash
python -c "
import uuid
for n in ['AttendancePanel','QuestPanel']:
    p=f'Assets/Scripts/UI/{n}.cs.meta'
    open(p,'w',newline='').write('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex)
    print(p)
"
```
Expected: 두 경로가 출력된다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/UI/AttendancePanel.cs Assets/Scripts/UI/AttendancePanel.cs.meta Assets/Scripts/UI/QuestPanel.cs Assets/Scripts/UI/QuestPanel.cs.meta
git commit -m "Split daily panel into attendance and quest panels

Attendance and quests shared one panel, which made neither read as its
own thing. Each half moves to its own file with the same behaviour;
nothing references them yet."
```

---

### Task 2: 매니저 전환과 자동 팝업

`MainMenuManager`가 새 패널 둘을 쓰도록 바꾸고, 출석 자동 팝업과 배지 2개를 붙인다. 마지막으로 `DailyPanel`을 지운다. 이 세 가지는 함께 있어야 컴파일이 통과하므로 한 태스크다.

**Files:**
- Modify: `Assets/Scripts/Manager/DailyMissionManager.cs:169-179`
- Modify: `Assets/Scripts/Manager/MainMenuManager.cs`
- Delete: `Assets/Scripts/UI/DailyPanel.cs`, `Assets/Scripts/UI/DailyPanel.cs.meta`

**Interfaces:**
- Consumes: Task 1의 `AttendancePanel.Open()`, `QuestPanel.Open()`. 기존 `TitleScreenPanel`(컴포넌트, 페이드가 끝나면 스스로 `SetActive(false)`), `DailyMissionManager.Changed` 이벤트, `DailyMissionManager.CanClaimAttendance`.
- Produces: `DailyMissionManager.HasClaimableMission` (`public bool`, getter 전용). `MainMenuManager`의 직렬화 필드 이름 — `m_attendanceButton`, `m_attendancePanel`, `m_attendanceBadge`, `m_questButton`, `m_questPanel`, `m_questBadge`, `m_titleScreen`. Task 4의 빌더가 이 이름으로 값을 넣는다.

- [ ] **Step 1: `DailyMissionManager`의 배지 질의를 퀘스트 전용으로 교체**

`Assets/Scripts/Manager/DailyMissionManager.cs`에서 아래 블록을 찾는다.

```csharp
    /// <summary>수령 대기 중인 보상이 하나라도 있는지 (메인화면 배지용).</summary>
    public bool HasClaimable
    {
        get
        {
            if (CanClaimAttendance) return true;
            for (int i = 0; i < MissionCount; i++)
                if (CanClaim(i)) return true;
            return false;
        }
    }
```

아래로 바꾼다. 출석 배지는 이미 있는 `CanClaimAttendance`를 그대로 쓰므로 합쳐진 질의는 쓸 곳이 없어졌다.

```csharp
    /// <summary>완료했지만 아직 수령하지 않은 퀘스트가 있는지 (퀘스트 버튼 배지용).</summary>
    public bool HasClaimableMission
    {
        get
        {
            for (int i = 0; i < MissionCount; i++)
                if (CanClaim(i)) return true;
            return false;
        }
    }
```

- [ ] **Step 2: `MainMenuManager`에 `System.Collections` using 추가**

파일 맨 위 using 블록을 아래로 바꾼다. 자동 팝업이 코루틴이라 필요하다.

```csharp
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
```

- [ ] **Step 3: 직렬화 필드 교체**

`Assets/Scripts/Manager/MainMenuManager.cs`에서 아래 블록을 찾는다.

```csharp
    [Tooltip("일일 보상(출석·미션) 버튼")]
    [SerializeField] Button m_dailyButton;
    [Tooltip("일일 보상 패널")]
    [SerializeField] DailyPanel m_dailyPanel;
    [Tooltip("수령 대기 중인 일일 보상이 있을 때 켜지는 빨간 점")]
    [SerializeField] GameObject m_dailyBadge;
```

아래로 바꾼다.

```csharp
    [Tooltip("출석 보상 버튼")]
    [SerializeField] Button m_attendanceButton;
    [Tooltip("출석 보상 패널 — 오늘 안 받았으면 메인화면에 들어올 때 자동으로 열립니다")]
    [SerializeField] AttendancePanel m_attendancePanel;
    [Tooltip("오늘 출석을 아직 안 받았을 때 켜지는 빨간 점")]
    [SerializeField] GameObject m_attendanceBadge;
    [Tooltip("일일 퀘스트 버튼")]
    [SerializeField] Button m_questButton;
    [Tooltip("일일 퀘스트 패널")]
    [SerializeField] QuestPanel m_questPanel;
    [Tooltip("수령할 퀘스트 보상이 있을 때 켜지는 빨간 점")]
    [SerializeField] GameObject m_questBadge;
    [Tooltip("타이틀 화면 — 이게 사라진 뒤에 출석 팝업을 띄웁니다")]
    [SerializeField] TitleScreenPanel m_titleScreen;
```

- [ ] **Step 4: `Start()` 교체**

아래 블록을 찾는다.

```csharp
        m_rankingButton?.onClick.AddListener(() => LeaderboardService.ShowLeaderboard());
        m_collectionButton?.onClick.AddListener(() => { if (m_collectionPanel != null) m_collectionPanel.Open(); });
        m_dailyButton?.onClick.AddListener(OnDailyButtonClicked);
```

아래로 바꾼다.

```csharp
        m_rankingButton?.onClick.AddListener(() => LeaderboardService.ShowLeaderboard());
        m_collectionButton?.onClick.AddListener(() => { if (m_collectionPanel != null) m_collectionPanel.Open(); });
        m_attendanceButton?.onClick.AddListener(() => { if (m_attendancePanel != null) m_attendancePanel.Open(); });
        m_questButton?.onClick.AddListener(() => { if (m_questPanel != null) m_questPanel.Open(); });
```

이어서 아래 블록을 찾는다.

```csharp
        if (m_settingPanel != null) m_settingPanel.SetActive(false);
        if (m_collectionPanel != null) m_collectionPanel.gameObject.SetActive(false);
        if (m_dailyPanel != null) m_dailyPanel.gameObject.SetActive(false);
        BindDaily();
        ApplyLanguage();
        Time.timeScale = 1f;
    }
```

아래로 바꾼다.

```csharp
        if (m_settingPanel != null) m_settingPanel.SetActive(false);
        if (m_collectionPanel != null) m_collectionPanel.gameObject.SetActive(false);
        if (m_attendancePanel != null) m_attendancePanel.gameObject.SetActive(false);
        if (m_questPanel != null) m_questPanel.gameObject.SetActive(false);
        BindDaily();
        ApplyLanguage();
        Time.timeScale = 1f;

        if (m_attendancePanel != null && m_daily != null && m_daily.CanClaimAttendance)
            StartCoroutine(OpenAttendanceWhenTitleGone());
    }

    /// <summary>
    /// 타이틀 화면이 사라진 뒤에 출석 팝업을 띄웁니다.
    /// AttendancePanel.Open()이 자기를 맨 앞으로 보내므로, 타이틀이 떠 있는 동안 열면
    /// 팝업이 타이틀 위로 올라옵니다. 타이틀은 앱 실행당 한 번만 뜨기 때문에,
    /// 게임에서 메인으로 돌아온 경우에는 이미 꺼져 있어 곧바로 열립니다.
    /// </summary>
    IEnumerator OpenAttendanceWhenTitleGone()
    {
        while (m_titleScreen != null && m_titleScreen.gameObject.activeSelf) yield return null;
        m_attendancePanel.Open();
    }
```

- [ ] **Step 5: `OnDailyButtonClicked` 제거**

아래 블록을 찾아 통째로 지운다. Step 4에서 리스너를 인라인 람다로 바꿔 호출부가 사라졌다.

```csharp
    void OnDailyButtonClicked()
    {
        if (m_dailyPanel != null) m_dailyPanel.Open();
    }

```

- [ ] **Step 6: 배지 갱신을 둘로 분리**

아래 블록을 찾는다.

```csharp
    /// <summary>보상을 수령하면 배지가 바로 꺼지도록 일일 미션 매니저의 변경 이벤트를 구독합니다.</summary>
    void BindDaily()
    {
        m_daily = GManager.Instance != null ? GManager.Instance.IsDaily : null;
        if (m_daily != null) m_daily.Changed += RefreshDailyBadge;
        RefreshDailyBadge();
    }

    void OnDestroy()
    {
        if (m_daily != null) m_daily.Changed -= RefreshDailyBadge;
    }

    void RefreshDailyBadge()
    {
        if (m_dailyBadge == null) return;
        m_dailyBadge.SetActive(m_daily != null && m_daily.HasClaimable);
    }
```

아래로 바꾼다.

```csharp
    /// <summary>보상을 수령하면 배지가 바로 꺼지도록 일일 미션 매니저의 변경 이벤트를 구독합니다.</summary>
    void BindDaily()
    {
        m_daily = GManager.Instance != null ? GManager.Instance.IsDaily : null;
        if (m_daily != null) m_daily.Changed += RefreshBadges;
        RefreshBadges();
    }

    void OnDestroy()
    {
        if (m_daily != null) m_daily.Changed -= RefreshBadges;
    }

    void RefreshBadges()
    {
        if (m_attendanceBadge != null)
            m_attendanceBadge.SetActive(m_daily != null && m_daily.CanClaimAttendance);
        if (m_questBadge != null)
            m_questBadge.SetActive(m_daily != null && m_daily.HasClaimableMission);
    }
```

- [ ] **Step 7: 버튼 라벨 교체**

`ApplyLanguage()` 안에서 아래 줄을 찾는다.

```csharp
        if (m_dailyButton != null) SetButtonText(m_dailyButton, english ? "DAILY" : "일일 보상");
```

아래 두 줄로 바꾼다. 아이콘 버튼은 좁으므로 라벨을 짧게 둔다.

```csharp
        if (m_attendanceButton != null) SetButtonText(m_attendanceButton, english ? "CHECK-IN" : "출석");
        if (m_questButton != null) SetButtonText(m_questButton, english ? "QUESTS" : "퀘스트");
```

- [ ] **Step 8: `DailyPanel` 삭제**

Run:
```bash
git rm Assets/Scripts/UI/DailyPanel.cs Assets/Scripts/UI/DailyPanel.cs.meta
```
Expected: 두 파일이 삭제로 스테이징된다.

- [ ] **Step 9: 컴파일 검증**

Run:
```bash
powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1
```
Expected: `OK - no compile errors`

`DailyPanel`을 참조하던 곳이 남아 있으면 여기서 잡힌다. `RetentionUIBuilder`는 에디터 어셈블리라 이 명령에 포함되지 않는다 — Task 4에서 고친다.

- [ ] **Step 10: 커밋**

```bash
git add Assets/Scripts/Manager/MainMenuManager.cs Assets/Scripts/Manager/DailyMissionManager.cs
git commit -m "Pop the attendance panel on entering the main scene

The attendance reward sat behind a button, so a player who never
pressed it never collected. It now opens by itself when today's reward
is unclaimed, and waits for the title screen to fade first - Open()
sends the panel to the front, so opening underneath the title would
have put the popup on top of it.

Badges split in two, one per button, which leaves the combined
HasClaimable query with no caller."
```

---

### Task 3: 아이콘 아틀라스 로더

`BrandUI`에 아이콘 아틀라스 경로, 인덱스 상수, 로더를 추가한다. 기존 `LoadAtlasSprite`는 그대로 둔다 — 호출부가 전부 버튼 아틀라스를 쓰고 있어 일반화할 이유가 없다.

**Files:**
- Modify: `Assets/Scripts/Editor/BrandUI.cs:16-24` (상수 블록), `:40-47` (로더 아래)

**Interfaces:**
- Consumes: `AssetDatabase.LoadAllAssetsAtPath` (이미 사용 중).
- Produces: `BrandUI.LoadIconSprite(int index)` → `Sprite` (못 찾으면 `InvalidOperationException`). 상수 `BrandUI.IconCheck`(38), `IconList`(11), `IconCards`(23), `IconDiamond`(40), `IconCrown`(33), `BrandUI.IconAtlasPath`. Task 4가 쓴다.

- [ ] **Step 1: 아틀라스 경로와 인덱스 상수 추가**

`Assets/Scripts/Editor/BrandUI.cs`에서 아래 줄을 찾는다.

```csharp
    public const string ButtonAtlasPath = "Assets/Down/Universal Stylized UI/Atlases/Complete_Stylized_UI_elements_buttons.png";
```

바로 아래에 이 줄을 넣는다.

```csharp
    public const string IconAtlasPath = "Assets/Down/Universal Stylized UI/Atlases/Complete_Stylized_UI_elements_icons_2.png";
```

이어서 아래 줄을 찾는다.

```csharp
    public const string GoldFlat = "Complete_Stylized_UI_elements_buttons_53";  // 평평한 골드 — 작은 버튼용
```

바로 아래에 이 블록을 넣는다.

```csharp

    // 아이콘 아틀라스는 9열 × 5행이고 인덱스는 행*9 + 열이다.
    // 달력·책·두루마리·트로피는 이 아틀라스에 없어 뜻이 가장 가까운 것을 골랐다.
    public const int IconCheck = 38;    // 초록 체크 — 출석
    public const int IconList = 11;     // 3줄 목록 — 퀘스트
    public const int IconCards = 23;    // 카드 더미 — 도감
    public const int IconDiamond = 40;  // 다이아 — 확률
    public const int IconCrown = 33;    // 금관 — 랭킹
```

- [ ] **Step 2: 로더 추가**

`LoadAtlasSprite` 메서드 바로 아래, `LoadFont` 위에 넣는다.

```csharp

    /// <summary>아이콘 아틀라스에서 인덱스로 스프라이트를 꺼냅니다.</summary>
    public static Sprite LoadIconSprite(int index)
    {
        string spriteName = $"Complete_Stylized_UI_elements_icons_2_{index}";
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(IconAtlasPath).OfType<Sprite>()
            .FirstOrDefault(s => s.name == spriteName);
        if (sprite == null)
            throw new System.InvalidOperationException($"아이콘 아틀라스에서 '{spriteName}'를 찾지 못했습니다: {IconAtlasPath}");
        return sprite;
    }
```

- [ ] **Step 3: 컴파일 검증**

Run:
```bash
powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1 -Project Assembly-CSharp-Editor.csproj
```
Expected: `OK - no compile errors`

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Editor/BrandUI.cs
git commit -m "Add icon atlas loader to BrandUI

Menu buttons are about to carry icons. Indices are named here so the
builder reads as intent rather than magic numbers."
```

---

### Task 4: 빌더 개편

에디터 툴이 새 패널 둘을 만들고, 구 `DailyPanel` 오브젝트를 지우고, 메뉴 버튼 5개를 아이콘화해 `MainPage`로 옮기게 한다.

**Files:**
- Modify: `Assets/Scripts/Editor/RetentionUIBuilder.cs:27-63` (진입점), `:168-282` (`BuildDailyPanel`), `:284-323` (`CloneMenuButton`)

**Interfaces:**
- Consumes: Task 1의 `AttendancePanel`·`QuestPanel` 직렬화 필드 이름, Task 2의 `MainMenuManager` 필드 이름, Task 3의 `BrandUI.LoadIconSprite`와 인덱스 상수.
- Produces: 없음. 이 태스크가 마지막 코드 변경이다.

- [ ] **Step 1: 진입점 교체**

`BuildMainSceneUI` 메서드 전체(`[MenuItem("Tools/Random Defense/Build Collection and Daily UI")]` 어트리뷰트부터 그 메서드의 닫는 중괄호까지)를 아래로 바꾼다.

```csharp
    // 메뉴 버튼 크기 — 아이콘형은 정사각, 기존 확률·랭킹은 가로형 그대로.
    static readonly Vector2 IconButtonSize = new Vector2(88f, 88f);
    static readonly Vector2 WideButtonSize = new Vector2(128f, 48f);

    [MenuItem("Tools/Random Defense/Build Collection and Daily UI")]
    public static void BuildMainSceneUI()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        MainMenuManager manager = Object.FindAnyObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (manager == null) throw new System.InvalidOperationException("MainScene에서 MainMenuManager를 찾지 못했습니다.");
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("MainScene에서 Canvas를 찾지 못했습니다.");

        // 메뉴 버튼은 캐러셀의 가운데 페이지 안에 들어가야 상점·연구소에서 따라다니지 않는다.
        RectTransform mainPage = BrandUI.FindDeep(canvas.transform, "MainPage");
        if (mainPage == null)
            throw new System.InvalidOperationException("MainScene에서 MainPage를 찾지 못했습니다. 메뉴 버튼을 붙일 곳입니다.");

        SerializedObject managerSo = new SerializedObject(manager);
        Button odds = managerSo.FindProperty("m_oddsButton").objectReferenceValue as Button;
        if (odds == null)
            throw new System.InvalidOperationException("MainMenuManager의 확률 버튼이 비어 있습니다. 새 버튼을 복제할 원본으로 필요합니다.");
        Button ranking = managerSo.FindProperty("m_rankingButton").objectReferenceValue as Button;
        if (ranking == null)
            throw new System.InvalidOperationException("MainMenuManager의 랭킹 버튼이 비어 있습니다.");

        CollectionPanel collectionPanel = BuildCollectionPanel(canvas);
        AttendancePanel attendancePanel = BuildAttendancePanel(canvas);
        QuestPanel questPanel = BuildQuestPanel(canvas);

        // 출석과 퀘스트로 갈라지기 전의 통합 패널이 남아 있으면 지운다.
        BrandUI.RemoveChild(canvas.transform, "DailyPanel");

        Button collectionButton = EnsureMenuButton(canvas, odds, "CollectionButton");
        Button attendanceButton = EnsureMenuButton(canvas, odds, "AttendanceButton", "DailyButton");
        Button questButton = EnsureMenuButton(canvas, odds, "QuestButton");

        StyleIconButton(attendanceButton, BrandUI.IconCheck);
        StyleIconButton(questButton, BrandUI.IconList);
        StyleIconButton(collectionButton, BrandUI.IconCards);
        StyleWideButton(odds, BrandUI.IconDiamond);
        StyleWideButton(ranking, BrandUI.IconCrown);

        // 출석은 좌상단에서 설정 기어와 대칭을 이루고, 퀘스트·도감은 기어 아래로 쌓인다.
        // 기어 하단이 -80이므로 8px 띄워 -88부터 시작한다.
        PlaceMenuButton(attendanceButton, mainPage, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), IconButtonSize);
        PlaceMenuButton(questButton, mainPage, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -88f), IconButtonSize);
        PlaceMenuButton(collectionButton, mainPage, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -184f), IconButtonSize);
        PlaceMenuButton(odds, mainPage, Vector2.zero, Vector2.zero, new Vector2(16f, 230f), WideButtonSize);
        PlaceMenuButton(ranking, mainPage, Vector2.zero, Vector2.zero, new Vector2(16f, 286f), WideButtonSize);

        GameObject attendanceBadge = BuildBadge(attendanceButton);
        GameObject questBadge = BuildBadge(questButton);

        TitleScreenPanel titleScreen = Object.FindAnyObjectByType<TitleScreenPanel>(FindObjectsInactive.Include);
        if (titleScreen == null)
            throw new System.InvalidOperationException("MainScene에서 TitleScreenPanel을 찾지 못했습니다. 출석 팝업이 타이틀을 기다리려면 필요합니다.");

        BrandUI.SetRef(managerSo, "m_collectionButton", collectionButton);
        BrandUI.SetRef(managerSo, "m_collectionPanel", collectionPanel);
        BrandUI.SetRef(managerSo, "m_attendanceButton", attendanceButton);
        BrandUI.SetRef(managerSo, "m_attendancePanel", attendancePanel);
        BrandUI.SetRef(managerSo, "m_attendanceBadge", attendanceBadge);
        BrandUI.SetRef(managerSo, "m_questButton", questButton);
        BrandUI.SetRef(managerSo, "m_questPanel", questPanel);
        BrandUI.SetRef(managerSo, "m_questBadge", questBadge);
        BrandUI.SetRef(managerSo, "m_titleScreen", titleScreen);
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        collectionPanel.gameObject.SetActive(false);
        attendancePanel.gameObject.SetActive(false);
        questPanel.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RetentionUIBuilder] 메인화면 완료 — 도감 / 출석 / 퀘스트 패널, 메뉴 버튼 5개를 MainPage로 이동");
    }
```

- [ ] **Step 2: `BuildDailyPanel`을 두 빌더로 교체**

`static DailyPanel BuildDailyPanel(Canvas canvas)` 메서드 전체를 아래 두 메서드로 바꾼다. `// ---- 일일 보상 ----` 주석은 `// ---- 출석 보상 ----`으로 고친다.

```csharp
    static AttendancePanel BuildAttendancePanel(Canvas canvas)
    {
        Sprite whitePill = BrandUI.LoadAtlasSprite(BrandUI.WhitePill);
        Sprite goldFlat = BrandUI.LoadAtlasSprite(BrandUI.GoldFlat);

        RectTransform root = BrandUI.EnsureChild(canvas.transform, "AttendancePanel");
        root.SetAsLastSibling();
        BrandUI.Stretch(root);
        Image dim = BrandUI.Ensure<Image>(root.gameObject);
        dim.sprite = null;
        dim.type = Image.Type.Simple;
        dim.color = BrandUI.ModalDim;
        dim.raycastTarget = true;
        Button background = BrandUI.Ensure<Button>(root.gameObject);
        background.transition = Selectable.Transition.None;
        background.targetGraphic = dim;

        // 출석만 담으므로 카드가 화면 가운데 절반이면 충분하다.
        RectTransform card = BrandUI.EnsureChild(root, "Card");
        BrandUI.Anchor(card, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.72f));
        BrandUI.StylePanel(card, whitePill, BrandUI.CardNavy, 0.5f).raycastTarget = true;
        BrandUI.MakeColumn(card, new RectOffset(20, 20, 22, 22), 12f);

        TextMeshProUGUI title = BrandUI.MakeText(card, "Title", 44f, BrandUI.CreamText);
        BrandUI.SetHeight(title, 62f);

        RectTransform row = BrandUI.EnsureChild(card, "Row");
        BrandUI.MakeRow(row, new RectOffset(0, 0, 0, 0), 6f);
        BrandUI.SetHeight(row, 110f);

        var cells = new Image[DailyMissionManager.AttendanceCycle];
        var labels = new TextMeshProUGUI[DailyMissionManager.AttendanceCycle];
        for (int day = 1; day <= DailyMissionManager.AttendanceCycle; day++)
        {
            RectTransform cell = BrandUI.EnsureChild(row, $"Day_{day}");
            Image cellImage = BrandUI.StylePanel(cell, whitePill, BrandUI.SlotNavy, 1.6f);
            cellImage.raycastTarget = false;
            TextMeshProUGUI label = BrandUI.MakeText(cell, "Label", 18f, BrandUI.CreamText);
            BrandUI.Stretch((RectTransform)label.transform);
            cells[day - 1] = cellImage;
            labels[day - 1] = label;
        }

        Button claim = BrandUI.MakeButton(card, "Claim", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 28f);
        BrandUI.SetHeight(claim, 76f);

        TextMeshProUGUI status = BrandUI.MakeText(card, "Status", 24f, BrandUI.CreamText);
        BrandUI.SetHeight(status, 34f);

        Button close = BrandUI.MakeButton(card, "Close", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 28f);
        BrandUI.SetHeight(close, 68f);

        AttendancePanel panel = BrandUI.Ensure<AttendancePanel>(root.gameObject);
        SerializedObject so = new SerializedObject(panel);
        BrandUI.SetRef(so, "m_bgButton", background);
        BrandUI.SetRef(so, "m_closeButton", close);
        BrandUI.SetRef(so, "m_titleText", title);
        BrandUI.SetRef(so, "m_closeText", close.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRefArray(so, "m_cells", cells);
        BrandUI.SetRefArray(so, "m_labels", labels);
        BrandUI.SetRef(so, "m_claimButton", claim);
        BrandUI.SetRef(so, "m_claimText", claim.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRef(so, "m_statusText", status);
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    // ---- 일일 퀘스트 ----

    static QuestPanel BuildQuestPanel(Canvas canvas)
    {
        Sprite whitePill = BrandUI.LoadAtlasSprite(BrandUI.WhitePill);
        Sprite navyPill = BrandUI.LoadAtlasSprite(BrandUI.NavyPill);
        Sprite goldFlat = BrandUI.LoadAtlasSprite(BrandUI.GoldFlat);

        RectTransform root = BrandUI.EnsureChild(canvas.transform, "QuestPanel");
        root.SetAsLastSibling();
        BrandUI.Stretch(root);
        Image dim = BrandUI.Ensure<Image>(root.gameObject);
        dim.sprite = null;
        dim.type = Image.Type.Simple;
        dim.color = BrandUI.ModalDim;
        dim.raycastTarget = true;
        Button background = BrandUI.Ensure<Button>(root.gameObject);
        background.transition = Selectable.Transition.None;
        background.targetGraphic = dim;

        RectTransform card = BrandUI.EnsureChild(root, "Card");
        BrandUI.Anchor(card, new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.76f));
        BrandUI.StylePanel(card, whitePill, BrandUI.CardNavy, 0.5f).raycastTarget = true;
        BrandUI.MakeColumn(card, new RectOffset(20, 20, 22, 22), 12f);

        TextMeshProUGUI title = BrandUI.MakeText(card, "Title", 44f, BrandUI.CreamText);
        BrandUI.SetHeight(title, 62f);

        var questTitles = new TextMeshProUGUI[DailyMissionManager.MissionCount];
        var progressTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];
        var claimButtons = new Button[DailyMissionManager.MissionCount];
        var claimTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];

        for (int i = 0; i < DailyMissionManager.MissionCount; i++)
        {
            RectTransform questRow = BrandUI.EnsureChild(card, $"Quest_{i}");
            BrandUI.StylePanel(questRow, whitePill, BrandUI.SlotNavy, 1.2f).raycastTarget = false;
            BrandUI.MakeRow(questRow, new RectOffset(16, 12, 8, 8), 10f);
            BrandUI.SetHeight(questRow, 96f);

            RectTransform info = BrandUI.EnsureChild(questRow, "Info");
            BrandUI.MakeColumn(info, new RectOffset(0, 0, 0, 0), 2f);
            LayoutElement infoElement = BrandUI.Ensure<LayoutElement>(info.gameObject);
            infoElement.flexibleWidth = 1f;

            TextMeshProUGUI rowTitle = BrandUI.MakeText(info, "Title", 24f, BrandUI.CreamText, TextAlignmentOptions.Left);
            BrandUI.SetHeight(rowTitle, 34f);
            TextMeshProUGUI rowProgress = BrandUI.MakeText(info, "Progress", 22f, BrandUI.CreamText, TextAlignmentOptions.Left);
            BrandUI.SetHeight(rowProgress, 30f);

            Button claim = BrandUI.MakeButton(questRow, "Claim", navyPill, 1f, BrandUI.NavyButtonLabel, 22f);
            LayoutElement claimElement = BrandUI.Ensure<LayoutElement>(claim.gameObject);
            claimElement.preferredWidth = 170f;
            claimElement.flexibleWidth = 0f;

            questTitles[i] = rowTitle;
            progressTexts[i] = rowProgress;
            claimButtons[i] = claim;
            claimTexts[i] = claim.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        TextMeshProUGUI status = BrandUI.MakeText(card, "Status", 24f, BrandUI.CreamText);
        BrandUI.SetHeight(status, 34f);

        Button close = BrandUI.MakeButton(card, "Close", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 28f);
        BrandUI.SetHeight(close, 68f);

        QuestPanel panel = BrandUI.Ensure<QuestPanel>(root.gameObject);
        SerializedObject so = new SerializedObject(panel);
        BrandUI.SetRef(so, "m_bgButton", background);
        BrandUI.SetRef(so, "m_closeButton", close);
        BrandUI.SetRef(so, "m_titleText", title);
        BrandUI.SetRef(so, "m_closeText", close.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRefArray(so, "m_questTitles", questTitles);
        BrandUI.SetRefArray(so, "m_progressTexts", progressTexts);
        BrandUI.SetRefArray(so, "m_claimButtons", claimButtons);
        BrandUI.SetRefArray(so, "m_claimTexts", claimTexts);
        BrandUI.SetRef(so, "m_statusText", status);
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }
```

- [ ] **Step 3: `CloneMenuButton`을 새 버튼 헬퍼 세 개로 교체**

`CloneMenuButton` 메서드 전체(그 위의 `/// <summary>` 주석 블록 포함)를 아래로 바꾼다. 호출부가 Step 1에서 전부 사라졌으므로 이 메서드는 남겨두면 죽은 코드다.

```csharp
    /// <summary>
    /// 메뉴 버튼을 확보합니다. 이미 있으면 그대로 쓰고, 없으면 확률 버튼을 복제해 만듭니다.
    /// legacyName은 이전 버전이 쓰던 이름으로, 그 오브젝트가 남아 있으면 새 이름으로 바꿔 재사용합니다.
    /// </summary>
    static Button EnsureMenuButton(Canvas canvas, Button template, string name, string legacyName = null)
    {
        RectTransform existing = BrandUI.FindDeep(canvas.transform, name);
        if (existing == null && !string.IsNullOrEmpty(legacyName))
        {
            existing = BrandUI.FindDeep(canvas.transform, legacyName);
            if (existing != null) existing.gameObject.name = name;
        }

        if (existing != null)
        {
            Button found = existing.GetComponent<Button>();
            if (found == null)
                throw new System.InvalidOperationException($"'{name}' 오브젝트에 Button 컴포넌트가 없습니다.");
            return found;
        }

        GameObject clone = Object.Instantiate(template.gameObject, template.transform.parent);
        clone.name = name;
        Button button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent(); // 복제된 리스너 제거 — MainMenuManager가 다시 연결합니다
        return button;
    }

    /// <summary>
    /// 메뉴 버튼을 MainPage 안으로 옮기고 좌표를 잡습니다.
    /// 캐러셀 밖에 두면 상점·연구소 페이지에서도 따라다니므로 가운데 페이지의 자식으로 만듭니다.
    /// </summary>
    static void PlaceMenuButton(Button button, RectTransform mainPage, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.SetParent(mainPage, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    /// <summary>아이콘을 위, 라벨을 아래에 둔 정사각 메뉴 버튼으로 만듭니다.</summary>
    static void StyleIconButton(Button button, int iconIndex)
    {
        Image icon = BrandUI.MakeImage(button.transform, "Icon", Color.white);
        icon.sprite = BrandUI.LoadIconSprite(iconIndex);
        icon.preserveAspect = true;
        BrandUI.Anchor((RectTransform)icon.transform, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.92f));

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) return;
        label.fontSize = 18f;
        label.alignment = TextAlignmentOptions.Center;
        BrandUI.Anchor((RectTransform)label.transform, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.30f));
    }

    /// <summary>아이콘을 왼쪽, 라벨을 오른쪽에 둔 가로 메뉴 버튼으로 만듭니다.</summary>
    static void StyleWideButton(Button button, int iconIndex)
    {
        Image icon = BrandUI.MakeImage(button.transform, "Icon", Color.white);
        icon.sprite = BrandUI.LoadIconSprite(iconIndex);
        icon.preserveAspect = true;
        BrandUI.Anchor((RectTransform)icon.transform, new Vector2(0.05f, 0.16f), new Vector2(0.28f, 0.84f));

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) return;
        label.alignment = TextAlignmentOptions.Left;
        BrandUI.Anchor((RectTransform)label.transform, new Vector2(0.33f, 0.05f), new Vector2(0.96f, 0.95f));
    }
```

- [ ] **Step 4: 컴파일 검증**

Run:
```bash
powershell -ExecutionPolicy Bypass -File Tools/compile-check.ps1 -Project Assembly-CSharp-Editor.csproj
```
Expected: `OK - no compile errors`

`DailyPanel` 참조가 남아 있었다면 여기서 잡힌다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Editor/RetentionUIBuilder.cs
git commit -m "Build the split panels and move menu buttons into MainPage

The five menu buttons hung off UICanvas, so they stayed on screen while
the carousel swiped to the shop and lab pages. They now live inside
MainPage and slide away with it. Each carries an icon from the icons_2
atlas, and the builder renames a leftover DailyButton rather than
stranding it."
```

---

### Task 5: Unity 실행과 플레이 확인

여기부터는 사람이 Unity에서 해야 한다. 에디터 툴 실행과 플레이 모드 확인은 자동화할 수 없다.

**Files:**
- Modify: `Assets/Scenes/MainScene.unity` (툴 실행 결과)
- Create: 신규 오브젝트에 대한 `.meta` (Unity가 생성)

- [ ] **Step 1: Unity에서 프로젝트를 열고 콘솔 확인**

Expected: 컴파일 에러 0. 에러가 있으면 여기서 멈추고 원인을 고친다.

- [ ] **Step 2: 에디터 툴 실행**

메뉴에서 `Tools > Random Defense > Build Collection and Daily UI` 실행.

Expected: 콘솔에 `[RetentionUIBuilder] 메인화면 완료 — 도감 / 출석 / 퀘스트 패널, 메뉴 버튼 5개를 MainPage로 이동`

Hierarchy에서 확인할 것:
- `UICanvas/MainCarousel/Pages/MainPage` 아래에 `AttendanceButton`, `QuestButton`, `CollectionButton`, `OddsButton`, `RankingButton` 다섯 개가 있다.
- `UICanvas` 직속에 `DailyPanel`과 `DailyButton`이 **없다**.
- `UICanvas` 직속에 `AttendancePanel`, `QuestPanel`, `CollectionPanel`이 있고 전부 비활성이다.
- `MainMenuManager` 인스펙터의 새 필드 9개가 전부 채워져 있다.

- [ ] **Step 3: 튜토리얼 오버레이 툴 실행**

메뉴에서 `Tools > Random Defense > Build Tutorial Overlay` 실행. 앞선 커밋에서 딤 오브젝트를 다시 만들도록 바꿨으므로, 깨진 `TutorialDimClick` 중복을 정리하려면 한 번 돌려야 한다.

Expected: 콘솔에 `[RetentionUIBuilder] 튜토리얼 오버레이 완료 — 딤 4장 / 말풍선 / 건너뛰기 / 단계 대상 3곳`. `GameScene`의 `TutorialOverlay` 아래 각 `Dim_*` 오브젝트에 `TutorialDimClick`이 **하나씩만** 붙어 있고 Missing Script 경고가 없다.

- [ ] **Step 4: 플레이 모드 확인**

`MainScene`에서 Play. 아래를 순서대로 확인한다.

1. 오늘 출석 미수령 상태에서 타이틀을 탭하면, 타이틀이 사라진 뒤 출석 팝업이 뜬다. 타이틀 위로 겹쳐 뜨지 않는다.
2. 받기 → 크리스탈이 늘고, 출석 배지가 꺼지고, 버튼이 "내일 다시 오세요"로 잠긴다.
3. 팝업을 닫고 출석 버튼을 누르면 다시 열린다.
4. 퀘스트 패널에 퀘스트 3줄만 있고 출석 칸이 없다.
5. 상점·연구소로 스와이프하면 메뉴 버튼 5개가 페이지와 함께 사라진다.
6. 아이콘 5개가 `HighWave` 텍스트·설정 기어와 겹치지 않는다.
7. 도감을 열면 격자가 스크롤되고 닫기 버튼이 항상 보인다.
8. `GameScene`에 처음 들어가면 튜토리얼이 뜨고, 딤을 탭하면 다음 단계로 넘어간다.

어긋나는 게 있으면 인스펙터에서 좌표·크기를 조정한다. 코드 기본값은 720×1280 계산값이라 실제 화면에서 미세 조정이 필요할 수 있다.

- [ ] **Step 5: 씬과 신규 meta 커밋**

```bash
git add Assets/Scenes/MainScene.unity Assets/Scenes/GameScene.unity
git add -A Assets
git status
```

`git status`로 스테이징된 목록을 확인한다. 의도치 않은 파일이 섞였으면 빼낸다.

```bash
git commit -m "Rebuild main scene UI for the split panels and icon menu"
```

---

## Self-Review

**스펙 커버리지**

| 스펙 절 | 태스크 |
|---|---|
| 4.1 패널 분리 | Task 1, Task 2 Step 1 |
| 4.2 출석 자동 팝업 | Task 2 Step 4 |
| 4.3 메뉴 버튼 재배치 | Task 4 Step 1, Step 3 |
| 4.4 아이콘 | Task 3, Task 4 Step 3 |
| 4.5 배지 | Task 2 Step 6, Task 4 Step 1 |
| 4.6 에디터 툴 | Task 4 |
| 6 검증 | Task 5 |

**이름 일관성 확인**

- `AttendancePanel`의 필드 `m_cells`·`m_labels`·`m_claimButton`·`m_claimText`·`m_statusText` (Task 1) ↔ 빌더의 `SetRef`/`SetRefArray` 인자 (Task 4 Step 2). 일치.
- `QuestPanel`의 필드 `m_questTitles`·`m_progressTexts`·`m_claimButtons`·`m_claimTexts` (Task 1) ↔ 빌더 (Task 4 Step 2). 일치.
- `MainMenuManager`의 필드 9개 (Task 2 Step 3) ↔ 빌더의 `SetRef` 인자 (Task 4 Step 1). 일치.
- `HasClaimableMission` (Task 2 Step 1 정의) ↔ `RefreshBadges` 사용처 (Task 2 Step 6). 일치.
- `BrandUI.IconCheck`/`IconList`/`IconCards`/`IconDiamond`/`IconCrown` (Task 3) ↔ 빌더 호출 (Task 4 Step 1). 일치.

**남은 위험**

- `Assets/Down`이 gitignore 대상이라 아틀라스가 없는 환경에서는 `LoadAtlasSprite`/`LoadIconSprite`가 예외를 던진다. 에디터 툴 전용이라 런타임에는 영향이 없다.
- 아이콘 버튼 안의 라벨을 `GetComponentInChildren<TextMeshProUGUI>`로 찾는다. 버튼 아래 텍스트가 하나뿐이라는 전제이고, 확률 버튼을 복제해 만들므로 현재는 성립한다. Task 5 Step 2의 육안 확인에서 잡힌다.
