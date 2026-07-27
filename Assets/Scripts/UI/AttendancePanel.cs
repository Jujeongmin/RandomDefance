using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 7일 순환 출석 보상 화면. 오늘 받을 칸만 금색으로 켜지고, 그 칸을 직접 눌러 하루에 한 번 수령합니다.
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
    static readonly Color ClaimedIcon = new Color(1f, 1f, 1f, 0.35f);

    [Header("References (씬에서 할당)")]
    [SerializeField] Button m_bgButton;
    [SerializeField] Button m_closeButton;
    [SerializeField] TextMeshProUGUI m_titleText;
    [SerializeField] TextMeshProUGUI m_closeText;

    [Header("Attendance")]
    [Tooltip("1~7일차 칸 배경")]
    [SerializeField] Image[] m_cells = new Image[DailyMissionManager.AttendanceCycle];
    [Tooltip("칸 위쪽 '1일차' 표시")]
    [SerializeField] TextMeshProUGUI[] m_labels = new TextMeshProUGUI[DailyMissionManager.AttendanceCycle];
    [Tooltip("칸 자체가 수령 버튼입니다. 오늘 받을 칸만 눌립니다.")]
    [SerializeField] Button[] m_cellButtons = new Button[DailyMissionManager.AttendanceCycle];
    [Tooltip("보상이 크리스탈이라는 걸 보여주는 아이콘")]
    [SerializeField] Image[] m_rewardIcons = new Image[DailyMissionManager.AttendanceCycle];
    [Tooltip("아이콘 옆 보상 수량")]
    [SerializeField] TextMeshProUGUI[] m_rewardTexts = new TextMeshProUGUI[DailyMissionManager.AttendanceCycle];
    [Tooltip("칸 아래 '받기' / '완료' 표시")]
    [SerializeField] TextMeshProUGUI[] m_stateTexts = new TextMeshProUGUI[DailyMissionManager.AttendanceCycle];

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

        // 어느 칸을 누르든 수령 대상은 오늘 칸 하나뿐이라 인자를 넘길 필요가 없습니다.
        // 오늘이 아닌 칸은 Populate가 interactable을 꺼 둡니다.
        foreach (Button cell in m_cellButtons)
            if (cell != null) cell.onClick.AddListener(Claim);
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

            // 오늘 칸은 금색 바탕이라 글자를 어둡게 뒤집어야 읽힙니다.
            Color textColor = isToday ? TodayLabel : isClaimed ? DoneText : CreamText;

            if (slot < m_labels.Length && m_labels[slot] != null)
            {
                m_labels[slot].text = english ? $"DAY {day}" : $"{day}일차";
                m_labels[slot].color = textColor;
            }

            int reward = daily != null ? daily.GetAttendanceReward(day) : 0;

            if (slot < m_rewardTexts.Length && m_rewardTexts[slot] != null)
            {
                m_rewardTexts[slot].text = $"{reward:N0}";
                m_rewardTexts[slot].color = textColor;
            }

            // 받은 칸은 크리스탈을 흐리게 해 이미 지나간 날임을 아이콘만 봐도 알게 합니다.
            if (slot < m_rewardIcons.Length && m_rewardIcons[slot] != null)
                m_rewardIcons[slot].color = isClaimed ? ClaimedIcon : Color.white;

            if (slot < m_stateTexts.Length && m_stateTexts[slot] != null)
            {
                m_stateTexts[slot].text = isToday ? (english ? "TAP" : "받기")
                    : isClaimed ? (english ? "DONE" : "완료")
                    : string.Empty;
                m_stateTexts[slot].color = textColor;
            }

            if (slot < m_cellButtons.Length && m_cellButtons[slot] != null)
                m_cellButtons[slot].interactable = isToday;
        }
    }
}
