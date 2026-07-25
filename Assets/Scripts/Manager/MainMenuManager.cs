using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Main UI")]
    [SerializeField] Button m_startButton;
    [SerializeField] Button m_settingButton;
    [SerializeField] TextMeshProUGUI m_highWaveText;
    [Tooltip("모드(일반/무한) 선택 버튼")]
    [SerializeField] Button m_modeButton;
    [Tooltip("확률 표기 화면을 여는 버튼")]
    [SerializeField] Button m_oddsButton;
    [Tooltip("확률 표기 패널")]
    [SerializeField] RarityOddsPanel m_oddsPanel;
    [Tooltip("무한모드 랭킹(리더보드) 버튼")]
    [SerializeField] Button m_rankingButton;
    [Tooltip("유닛 도감 버튼")]
    [SerializeField] Button m_collectionButton;
    [Tooltip("유닛 도감 패널")]
    [SerializeField] CollectionPanel m_collectionPanel;
    [Tooltip("일일 보상(출석·미션) 버튼")]
    [SerializeField] Button m_dailyButton;
    [Tooltip("일일 보상 패널")]
    [SerializeField] DailyPanel m_dailyPanel;
    [Tooltip("수령 대기 중인 일일 보상이 있을 때 켜지는 빨간 점")]
    [SerializeField] GameObject m_dailyBadge;

    [Header("Settings UI")]
    [SerializeField] GameObject m_settingPanel;
    [SerializeField] TextMeshProUGUI m_settingsTitleText;
    [FormerlySerializedAs("m_soundButton"), SerializeField] Button m_bgmButton;
    [FormerlySerializedAs("m_soundButtonText"), SerializeField] TextMeshProUGUI m_bgmButtonText;
    [SerializeField] Button m_sfxButton;
    [SerializeField] TextMeshProUGUI m_sfxButtonText;
    [SerializeField] Button m_koreanButton;
    [SerializeField] Button m_englishButton;
    [SerializeField] Button m_resetButton;
    [SerializeField] TextMeshProUGUI m_resetButtonText;
    [SerializeField] Button m_closeButton;
    [SerializeField] TextMeshProUGUI m_closeButtonText;

    bool m_resetConfirmationPending;

    bool IsEnglish => GameLanguage.IsEnglish;

    void Start()
    {
        m_startButton?.onClick.AddListener(OnStartButtonClicked);
        m_settingButton?.onClick.AddListener(OpenSettings);

        m_modeButton?.onClick.AddListener(OnModeButtonClicked);
        m_oddsButton?.onClick.AddListener(() => { if (m_oddsPanel != null) m_oddsPanel.Open(); });
        m_rankingButton?.onClick.AddListener(() => LeaderboardService.ShowLeaderboard());
        m_collectionButton?.onClick.AddListener(() => { if (m_collectionPanel != null) m_collectionPanel.Open(); });
        m_dailyButton?.onClick.AddListener(OnDailyButtonClicked);
        m_bgmButton?.onClick.AddListener(ToggleBgm);
        m_sfxButton?.onClick.AddListener(ToggleSfx);
        m_koreanButton?.onClick.AddListener(() => SetLanguage(false));
        m_englishButton?.onClick.AddListener(() => SetLanguage(true));
        m_resetButton?.onClick.AddListener(OnResetButtonClicked);
        m_closeButton?.onClick.AddListener(CloseSettings);

        if (m_settingPanel != null) m_settingPanel.SetActive(false);
        if (m_collectionPanel != null) m_collectionPanel.gameObject.SetActive(false);
        if (m_dailyPanel != null) m_dailyPanel.gameObject.SetActive(false);
        BindDaily();
        ApplyLanguage();
        Time.timeScale = 1f;
    }

    void OnDailyButtonClicked()
    {
        if (m_dailyPanel != null) m_dailyPanel.Open();
    }

    DailyMissionManager m_daily;

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

    void OpenSettings()
    {
        if (m_settingPanel == null) return;
        m_resetConfirmationPending = false;
        m_settingPanel.SetActive(true);
        m_settingPanel.transform.SetAsLastSibling();
        ApplyLanguage();
    }

    void CloseSettings()
    {
        m_resetConfirmationPending = false;
        if (m_settingPanel != null) m_settingPanel.SetActive(false);
    }

    void ToggleBgm()
    {
        GameAudioSettings.ToggleBgm();
        ApplyLanguage();
    }

    void ToggleSfx()
    {
        GameAudioSettings.ToggleSfx();
        ApplyLanguage();
    }

    void SetLanguage(bool english)
    {
        PlayerPrefs.SetInt(GameLanguage.PreferenceKey, english ? 1 : 0);
        PlayerPrefs.Save();
        m_resetConfirmationPending = false;
        ApplyLanguage();
    }

    void OnResetButtonClicked()
    {
        if (!m_resetConfirmationPending)
        {
            m_resetConfirmationPending = true;
            RefreshResetLabel();
            return;
        }

        if (GManager.Instance != null && GManager.Instance.IsProgress != null)
            GManager.Instance.IsProgress.ResetProgress();
        SceneManager.LoadScene(GManager.SCENE_MAIN);
    }

    void ApplyLanguage()
    {
        bool english = IsEnglish;
        int highestWave = GManager.Instance != null && GManager.Instance.IsProgress != null
            ? GManager.Instance.IsProgress.HighestWave
            : 0;

        if (m_highWaveText != null)
            m_highWaveText.text = english
                ? $"HIGHEST WAVE\n{highestWave:N0} Wave"
                : $"최고 도달 웨이브\n{highestWave:N0} Wave";
        SetButtonText(m_startButton, english ? "START" : "게임시작");
        if (m_settingsTitleText != null) m_settingsTitleText.text = english ? "SETTINGS" : "설정";
        if (m_bgmButtonText != null)
            m_bgmButtonText.text = $"BGM  {(GameAudioSettings.BgmEnabled ? "ON" : "OFF")}";
        if (m_sfxButtonText != null)
            m_sfxButtonText.text = $"SFX  {(GameAudioSettings.SfxEnabled ? "ON" : "OFF")}";
        if (m_closeButtonText != null) m_closeButtonText.text = english ? "CLOSE" : "닫기";
        RefreshResetLabel();

        SetButtonSelected(m_koreanButton, !english);
        SetButtonSelected(m_englishButton, english);

        RefreshModeButtons();
        if (m_oddsButton != null) SetButtonText(m_oddsButton, english ? "ODDS" : "확률 정보");
        if (m_rankingButton != null) SetButtonText(m_rankingButton, english ? "RANKING" : "랭킹");
        if (m_collectionButton != null) SetButtonText(m_collectionButton, english ? "COLLECTION" : "도감");
        if (m_dailyButton != null) SetButtonText(m_dailyButton, english ? "DAILY" : "일일 보상");
    }

    void RefreshResetLabel()
    {
        if (m_resetButtonText == null) return;
        m_resetButtonText.text = IsEnglish
            ? (m_resetConfirmationPending ? "TAP AGAIN TO CONFIRM" : "RESET DATA")
            : (m_resetConfirmationPending ? "한 번 더 눌러 초기화" : "데이터 초기화");
    }

    static void SetButtonText(Button button, string value)
    {
        if (button == null) return;
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = value;
    }

    static void SetButtonSelected(Button button, bool selected)
    {
        if (button == null || button.targetGraphic == null) return;
        button.targetGraphic.color = selected
            ? new Color(0.2f, 0.55f, 0.8f, 1f)
            : new Color(0.16f, 0.24f, 0.38f, 1f);
    }

    void OnStartButtonClicked() => SceneManager.LoadScene(GManager.SCENE_GAME);

    void OnModeButtonClicked()
    {
        GameModeSettings.Mode = GameModeSettings.Mode == GameMode.Normal ? GameMode.Endless : GameMode.Normal;
        ApplyLanguage();
    }

    void RefreshModeButtons()
    {
        bool english = IsEnglish;

        if (m_modeButton != null)
        {
            string mode = GameModeSettings.Mode == GameMode.Endless
                ? (english ? "ENDLESS" : "무한")
                : (english ? "NORMAL" : "일반");
            SetButtonText(m_modeButton, (english ? "MODE: " : "모드: ") + mode);
        }
    }
}
