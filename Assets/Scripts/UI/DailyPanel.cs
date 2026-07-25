using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일일 미션 3종과 7일 순환 출석 보상 화면.
/// UI는 씬에 미리 배치돼 있고, 직렬화 참조로 연결됩니다.
/// </summary>
public class DailyPanel : MonoBehaviour
{
    static readonly Color CreamText = new Color(0.96f, 0.90f, 0.78f, 1f);
    static readonly Color DoneText = new Color(0.55f, 0.58f, 0.66f, 1f);
    static readonly Color ReadyText = new Color(1.00f, 0.83f, 0.28f, 1f);
    static readonly Color TodayTint = new Color(1.00f, 0.83f, 0.28f, 1f);
    static readonly Color ClaimedTint = new Color(0.24f, 0.30f, 0.24f, 1f);
    static readonly Color UpcomingTint = new Color(0.10f, 0.09f, 0.20f, 1f);

    [Header("References (씬에서 할당)")]
    [SerializeField] Button m_bgButton;
    [SerializeField] Button m_closeButton;
    [SerializeField] TextMeshProUGUI m_titleText;
    [SerializeField] TextMeshProUGUI m_closeText;

    [Header("Attendance")]
    [SerializeField] TextMeshProUGUI m_attendanceTitle;
    [Tooltip("1~7일차 칸 배경")]
    [SerializeField] Image[] m_attendanceCells = new Image[DailyMissionManager.AttendanceCycle];
    [SerializeField] TextMeshProUGUI[] m_attendanceLabels = new TextMeshProUGUI[DailyMissionManager.AttendanceCycle];
    [SerializeField] Button m_attendanceClaimButton;
    [SerializeField] TextMeshProUGUI m_attendanceClaimText;

    [Header("Missions")]
    [SerializeField] TextMeshProUGUI m_missionTitle;
    [SerializeField] TextMeshProUGUI[] m_missionTitles = new TextMeshProUGUI[DailyMissionManager.MissionCount];
    [SerializeField] TextMeshProUGUI[] m_missionProgressTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];
    [SerializeField] Button[] m_missionClaimButtons = new Button[DailyMissionManager.MissionCount];
    [SerializeField] TextMeshProUGUI[] m_missionClaimTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];

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
        if (m_attendanceClaimButton != null) m_attendanceClaimButton.onClick.AddListener(ClaimAttendance);

        for (int i = 0; i < m_missionClaimButtons.Length; i++)
        {
            int index = i; // 클로저가 반복 변수를 잡지 않도록 복사
            if (m_missionClaimButtons[i] != null)
                m_missionClaimButtons[i].onClick.AddListener(() => ClaimMission(index));
        }
    }

    void ClaimAttendance()
    {
        DailyMissionManager daily = Daily;
        if (daily == null) return;

        int reward = daily.ClaimAttendance();
        ShowStatus(reward);
        Populate();
    }

    void ClaimMission(int index)
    {
        DailyMissionManager daily = Daily;
        if (daily == null) return;

        int reward = daily.ClaimMission(index);
        ShowStatus(reward);
        Populate();
    }

    void ShowStatus(int reward)
    {
        if (m_statusText == null) return;
        if (reward <= 0)
        {
            m_statusText.text = string.Empty;
            return;
        }

        GameAudioManager.Play(GameAudioManager.Sfx.Upgrade);
        m_statusText.text = GameLanguage.Choose($"크리스탈 +{reward:N0}", $"+{reward:N0} crystals");
        m_statusText.color = ReadyText;
    }

    void Populate()
    {
        bool english = GameLanguage.IsEnglish;
        DailyMissionManager daily = Daily;

        if (m_titleText != null) m_titleText.text = english ? "DAILY" : "일일 보상";
        if (m_closeText != null) m_closeText.text = english ? "CLOSE" : "닫기";
        if (m_attendanceTitle != null)
        {
            m_attendanceTitle.text = english ? "ATTENDANCE" : "출석 보상";
            m_attendanceTitle.color = CreamText;
        }
        if (m_missionTitle != null)
        {
            m_missionTitle.text = english ? "DAILY MISSIONS" : "일일 미션";
            m_missionTitle.color = CreamText;
        }

        PopulateAttendance(daily, english);
        PopulateMissions(daily, english);
    }

    void PopulateAttendance(DailyMissionManager daily, bool english)
    {
        int claimedDay = daily != null ? daily.ClaimedAttendanceDay : 0;
        int pendingDay = daily != null ? daily.PendingAttendanceDay : 1;
        bool canClaim = daily != null && daily.CanClaimAttendance;

        for (int day = 1; day <= DailyMissionManager.AttendanceCycle; day++)
        {
            int slot = day - 1;
            // 오늘 받을 칸은 금색, 이미 받은 칸은 어두운 초록, 남은 칸은 기본 네이비.
            bool isToday = canClaim && day == pendingDay;
            bool isClaimed = !canClaim ? day <= claimedDay : day < pendingDay;

            if (slot < m_attendanceCells.Length && m_attendanceCells[slot] != null)
                m_attendanceCells[slot].color = isToday ? TodayTint : isClaimed ? ClaimedTint : UpcomingTint;

            if (slot < m_attendanceLabels.Length && m_attendanceLabels[slot] != null)
            {
                int reward = daily != null ? daily.GetAttendanceReward(day) : 0;
                m_attendanceLabels[slot].text = english
                    ? $"DAY {day}\n{reward:N0}"
                    : $"{day}일차\n{reward:N0}";
                m_attendanceLabels[slot].color = isToday ? new Color(0.11f, 0.08f, 0.30f) : CreamText;
            }
        }

        if (m_attendanceClaimButton != null) m_attendanceClaimButton.interactable = canClaim;
        if (m_attendanceClaimText != null)
        {
            m_attendanceClaimText.text = canClaim
                ? (english ? "CLAIM" : "받기")
                : (english ? "COME BACK TOMORROW" : "내일 다시 오세요");
            m_attendanceClaimText.color = canClaim ? ReadyText : DoneText;
        }
    }

    void PopulateMissions(DailyMissionManager daily, bool english)
    {
        for (int i = 0; i < DailyMissionManager.MissionCount; i++)
        {
            bool canClaim = daily != null && daily.CanClaim(i);
            bool claimed = daily != null && daily.IsClaimed(i);

            if (i < m_missionTitles.Length && m_missionTitles[i] != null)
            {
                m_missionTitles[i].text = daily != null ? daily.GetTitle(i) : string.Empty;
                m_missionTitles[i].color = claimed ? DoneText : CreamText;
            }

            if (i < m_missionProgressTexts.Length && m_missionProgressTexts[i] != null)
            {
                int progress = daily != null ? daily.GetProgress(i) : 0;
                int goal = daily != null ? daily.GetGoal(i) : 0;
                int reward = daily != null ? daily.GetReward(i) : 0;
                m_missionProgressTexts[i].text = $"{progress:N0} / {goal:N0}   <color=#FFD447>+{reward:N0}</color>";
                m_missionProgressTexts[i].color = claimed ? DoneText : CreamText;
            }

            if (i < m_missionClaimButtons.Length && m_missionClaimButtons[i] != null)
                m_missionClaimButtons[i].interactable = canClaim;

            if (i < m_missionClaimTexts.Length && m_missionClaimTexts[i] != null)
            {
                m_missionClaimTexts[i].text = claimed
                    ? (english ? "DONE" : "완료")
                    : canClaim ? (english ? "CLAIM" : "받기") : (english ? "IN PROGRESS" : "진행 중");
                m_missionClaimTexts[i].color = claimed ? DoneText : canClaim ? ReadyText : CreamText;
            }
        }
    }
}
