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
