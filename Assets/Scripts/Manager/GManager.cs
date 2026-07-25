using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine;

public class GManager : MonoBehaviour
{
    [Header("Game Balance")]
    [SerializeField] GameBalanceData m_balanceData = null;

    [Header("Managers")]
    [SerializeField] UnitDataManager m_unitDataManager = null;
    [SerializeField] PoolManager m_poolManager = null;
    RegionManager m_regionManager = null;
    MobManager m_mobManager = null;
    EconomyManager m_economyManager = null;
    UpgradeManager m_upgradeManager = null;
    UnitSpawner m_unitSpawner = null;
    SpeedManager m_speedManager = null;
    SpawnTextManager m_spawnTextManager = null;
    ClassRarityDisplay m_classRarityDisplay = null;
    PlayerProgressManager m_playerProgress = null;
    ResearchManager m_researchManager = null;
    CollectionManager m_collectionManager = null;
    DailyMissionManager m_dailyMissionManager = null;

    ResultPanel m_resultPanel = null;
    GameObject m_settingsPanel = null;
    TutorialOverlay m_tutorialOverlay = null;
    Transform m_damageTextParent = null;

    TextMeshProUGUI m_selectedClassNameText = null;
    [SerializeField] Sprite[] m_wizardSp = null;
    [SerializeField] Sprite[] m_archerSp = null;
    [SerializeField] Sprite[] m_warriorSp = null;


    bool m_gameOver = false;
    bool m_runRewardGranted = false;
    readonly UnitSaleService m_unitSaleService = new UnitSaleService();

    [Header("Debug (Inspector)")]
    [SerializeField] bool m_testGameOver = false;
    [SerializeField] bool m_testGameClear = false;

    public static GManager Instance { get; private set; } = null;

    public UnitDataManager IsUnitData => m_unitDataManager;
    public PoolManager IsPool => m_poolManager;
    public RegionManager IsRegion => m_regionManager;
    public MobManager IsMob => m_mobManager;
    public EconomyManager IsEconomy => m_economyManager;
    public UpgradeManager IsUpgrade => m_upgradeManager;
    public UnitSpawner IsSpawner => m_unitSpawner;
    public SpeedManager IsSpeed => m_speedManager;
    public SpawnTextManager IsSpawnText => m_spawnTextManager;
    public Transform DamageTextParent => m_damageTextParent;
    public GameObject IsSettingPanel => m_settingsPanel;
    public ClassRarityDisplay IsClassRarityDisplay => m_classRarityDisplay;
    public ResultPanel IsResultPanel => m_resultPanel;
    public GameBalanceData Balance => m_balanceData;
    public PlayerProgressManager IsProgress => m_playerProgress;
    public ResearchManager IsResearch => m_researchManager;
    public CollectionManager IsCollection => m_collectionManager;
    public DailyMissionManager IsDaily => m_dailyMissionManager;

    // ── 런타임 등록 메서드 (각 게임씬 매니저가 Start에서 자기 자신을 등록) ──
    public void RegisterMobManager(MobManager mgr)
    {
        m_mobManager = mgr;
        if (m_upgradeManager != null) m_upgradeManager.BindMobManager(mgr);
    }

    /// <summary>하위호환성 유지용 — RegisterMobManager 사용 권장</summary>
    public void SetMobManager(MobManager mgr) => RegisterMobManager(mgr);

    public void RegisterRegionManager(RegionManager mgr) { m_regionManager = mgr; }

    public void RegisterEconomyManager(EconomyManager mgr)
    {
        m_economyManager = mgr;
        if (mgr != null) mgr.Initialize();
    }

    public void RegisterUpgradeManager(UpgradeManager mgr)
    {
        m_upgradeManager = mgr;
        if (mgr != null)
        {
            mgr.BindMobManager(m_mobManager);
            mgr.Initialize();
        }
    }

    public void RegisterSpeedManager(SpeedManager mgr)
    {
        m_speedManager = mgr;
        if (mgr != null) mgr.Initialize();
    }

    public void RegisterSpawnTextManager(SpawnTextManager mgr)
    {
        m_spawnTextManager = mgr;
        if (mgr != null) mgr.Initialize();
    }

    public void RegisterUnitSpawner(UnitSpawner mgr) { m_unitSpawner = mgr; }

    public void RegisterClassRarityDisplay(ClassRarityDisplay display) { m_classRarityDisplay = display; }

    public void RegisterResultPanel(ResultPanel panel)
    {
        m_resultPanel = panel;
        if (m_resultPanel != null) m_resultPanel.gameObject.SetActive(false);
    }

    public void RegisterSettingsPanel(GameObject panel)
    {
        m_settingsPanel = panel;
    }

    public void RegisterDamageTextParent(Transform parent)
    {
        m_damageTextParent = parent;
    }

    public void RegisterSelectedClassNameText(TextMeshProUGUI text)
    {
        m_selectedClassNameText = text;
        if (m_selectedClassNameText != null) m_selectedClassNameText.text = GameLanguage.Choose("직업 선택", "SELECT CLASS");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        m_gameOver = false;
        m_runRewardGranted = false;
        m_autoSellLowGrade = false; // 재시작 시 자동판매 OFF
        // Ensure restart sequence runs but always hide the result panel even if an error occurs.
        try
        {
            // Clear regions and mobs first
            if (m_regionManager != null)
            {
                m_regionManager.ClearAllRegions();
                m_regionManager.InitializeRegions();
            }

            if (m_mobManager != null)
            {
                m_mobManager.ClearAllMobs();
            }

            // Cleanup any units that might be outside regions (player-summoned heroes)
            if (m_poolManager != null)
            {
                m_poolManager.CleanupAllUnits();
                m_poolManager.DestroyAllPooledObjects();
                // Recreate pool parents and prewarm
                m_poolManager.Initialize();
            }

            // Reinitialize other managers
            if (m_economyManager != null) m_economyManager.Initialize();
            if (m_upgradeManager != null) m_upgradeManager.Initialize();
            if (m_speedManager != null) m_speedManager.Initialize();
            if (m_spawnTextManager != null) m_spawnTextManager.Initialize();

            if (m_mobManager != null)
            {
                m_mobManager.ResetForRestart();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            if (m_resultPanel != null) m_resultPanel.gameObject.SetActive(false);
        }
    }

    // Initialization via inspector-serialized references. Do not use runtime Find/registration when possible.

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 터치가 없어도 화면이 꺼지지 않도록 (방치형 플레이 대응)
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (m_balanceData == null)
            {
                m_balanceData = ScriptableObject.CreateInstance<GameBalanceData>();
            }

            m_playerProgress = GetComponent<PlayerProgressManager>();
            if (m_playerProgress == null) m_playerProgress = gameObject.AddComponent<PlayerProgressManager>();
            m_playerProgress.Initialize();

            m_researchManager = GetComponent<ResearchManager>();
            if (m_researchManager == null) m_researchManager = gameObject.AddComponent<ResearchManager>();
            m_researchManager.Initialize(m_playerProgress, m_balanceData);

            m_collectionManager = GetComponent<CollectionManager>();
            if (m_collectionManager == null) m_collectionManager = gameObject.AddComponent<CollectionManager>();
            m_collectionManager.Initialize(m_playerProgress, m_balanceData);

            m_dailyMissionManager = GetComponent<DailyMissionManager>();
            if (m_dailyMissionManager == null) m_dailyMissionManager = gameObject.AddComponent<DailyMissionManager>();
            m_dailyMissionManager.Initialize(m_playerProgress, m_balanceData);

            LeaderboardService.Initialize();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private IEnumerator DelayedInitializeManagers()
    {
        yield return null;
        if (m_poolManager != null) m_poolManager.Initialize();
        if (m_economyManager != null) m_economyManager.Initialize();
        if (m_upgradeManager != null) m_upgradeManager.Initialize();
        if (m_speedManager != null) m_speedManager.Initialize();
        if (m_spawnTextManager != null) m_spawnTextManager.Initialize();
        if (m_regionManager != null)
        {
            m_regionManager.ClearAllRegions();
            m_regionManager.InitializeRegions();
        }
        if (m_mobManager != null) m_mobManager.ResetForRestart();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>씬 이름 상수 — 빌드 세팅의 씬 이름과 반드시 일치해야 합니다.</summary>
    public const string SCENE_MAIN = "MainScene";
    public const string SCENE_GAME = "GameScene";

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        m_gameOver = false;
        m_runRewardGranted = false;
        Time.timeScale = 1f;

        if (scene.name == SCENE_MAIN)
        {
            // 메인씬: 게임씬 전용 매니저 참조를 초기화
            OnMainSceneLoaded();
        }
        else if (scene.name == SCENE_GAME)
        {
            ResultPanel sceneResultPanel = FindAnyObjectByType<ResultPanel>(FindObjectsInactive.Include);
            if (sceneResultPanel != null) RegisterResultPanel(sceneResultPanel);

            // 설정 패널이 비활성 상태로 배치돼 있으면 Start()가 실행되지 않아 등록이 누락되므로 직접 찾는다
            SettingPanel sceneSettingPanel = FindAnyObjectByType<SettingPanel>(FindObjectsInactive.Include);
            if (sceneSettingPanel != null)
            {
                RegisterSettingsPanel(sceneSettingPanel.gameObject);
                sceneSettingPanel.gameObject.SetActive(false);
            }

            // 튜토리얼 오버레이도 비활성 상태로 배치돼 있어 직접 찾는다
            m_tutorialOverlay = FindAnyObjectByType<TutorialOverlay>(FindObjectsInactive.Include);

            // 매 판 자동판매는 OFF로 시작
            m_autoSellLowGrade = false;

            // 게임씬: 풀 초기화 후 매니저들은 각자 Start에서 등록됨
            StartCoroutine(DelayedInitializeManagers());
            if (m_resultPanel != null) m_resultPanel.gameObject.SetActive(false);
            if (m_tutorialOverlay != null) m_tutorialOverlay.BeginIfNeeded();
        }
    }

    void OnMainSceneLoaded()
    {
        // 판에서 쌓인 미션 진행도를 저장하고, 자정을 넘겼으면 오늘 자로 갱신한다
        if (m_dailyMissionManager != null)
        {
            m_dailyMissionManager.Flush();
            m_dailyMissionManager.RefreshDay();
        }

        m_tutorialOverlay = null;

        // 게임씬 전용 매니저 참조를 해제 (다음 게임씬 진입 시 다시 등록됨)
        m_mobManager = null;
        m_regionManager = null;
        m_economyManager = null;
        m_upgradeManager = null;
        m_speedManager = null;
        m_spawnTextManager = null;
        m_unitSpawner = null;
        m_classRarityDisplay = null;
        m_resultPanel = null;
        m_settingsPanel = null;
        m_selectedClassNameText = null;
    }

    private void Start()
    {
        if (m_economyManager != null) m_economyManager.Initialize();
        if (m_upgradeManager != null) m_upgradeManager.Initialize();
        if (m_speedManager != null) m_speedManager.Initialize();
        if (m_poolManager != null) m_poolManager.Initialize();
        if (m_selectedClassNameText != null) m_selectedClassNameText.text = GameLanguage.Choose("직업 선택", "SELECT CLASS");

        if (m_spawnTextManager != null) m_spawnTextManager.Initialize();

        if (m_resultPanel != null) m_resultPanel.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    // Allow triggering game over/clear from inspector at runtime
    void Update()
    {
        if (m_testGameOver)
        {
            m_testGameOver = false;
            ForceDefeat();
        }
        if (m_testGameClear)
        {
            m_testGameClear = false;
            ForceVictory();
        }
    }
#endif

    public void HandleDefeat()
    {
        if (m_gameOver) return;
        GameAudioManager.Play(GameAudioManager.Sfx.Defeat);
        m_gameOver = true;
        Time.timeScale = 0f;
        if (m_tutorialOverlay != null) m_tutorialOverlay.Abort(); // 결과 패널을 가리지 않도록
        int currentWave = m_mobManager != null ? m_mobManager.CurrentWave : 1;
        RecordRunResult(currentWave);
        if (m_resultPanel != null)
        {
            m_resultPanel.Setup(false, currentWave);
        }
    }

    // ── 도감 · 일일 미션 · 튜토리얼 진행 알림 ──

    /// <summary>플레이어가 유닛을 실제로 소환했을 때 호출합니다 (에디터 테스트 소환은 제외).</summary>
    public void NotifyUnitSummoned(EntityType.TYPE classType, RarityType.TYPE rarity)
    {
        if (m_collectionManager != null) m_collectionManager.TryDiscover(classType, rarity);
        if (m_dailyMissionManager != null) m_dailyMissionManager.NotifySummon();
        if (m_tutorialOverlay != null) m_tutorialOverlay.NotifySummonPerformed();
    }

    /// <summary>새 웨이브에 진입했을 때 MobManager가 호출합니다.</summary>
    public void NotifyWaveReached(int wave)
    {
        if (m_dailyMissionManager != null) m_dailyMissionManager.NotifyWaveReached(wave);
    }

    /// <summary>보스를 제한시간 안에 처치했을 때 MobManager가 호출합니다.</summary>
    public void NotifyBossDefeated()
    {
        if (m_dailyMissionManager != null) m_dailyMissionManager.NotifyBossDefeated();
    }

    // 도달 웨이브 기록 + 무한모드면 리더보드에 제출
    void RecordRunResult(int wave)
    {
        if (m_dailyMissionManager != null)
        {
            m_dailyMissionManager.NotifyWaveReached(wave);
            m_dailyMissionManager.Flush(); // 판이 끝났으니 모아 둔 진행도를 디스크에 쓴다
        }
        if (m_playerProgress != null) m_playerProgress.RecordHighestWave(wave);
        if (GameModeSettings.IsEndless)
        {
            if (m_playerProgress != null) m_playerProgress.RecordEndlessWave(wave);
            LeaderboardService.SubmitEndlessWave(wave);
        }
    }

    public void HandleVictory()
    {
        if (m_gameOver) return;
        GameAudioManager.Play(GameAudioManager.Sfx.Victory);
        m_gameOver = true;
        Time.timeScale = 0f;
        if (m_tutorialOverlay != null) m_tutorialOverlay.Abort(); // 결과 패널을 가리지 않도록
        int currentWave = m_mobManager != null ? m_mobManager.CurrentWave : 1;
        RecordRunResult(currentWave);
        if (m_resultPanel != null)
        {
            m_resultPanel.Setup(true, currentWave);
        }
    }

    public void ForceDefeat()
    {
        m_gameOver = false;
        HandleDefeat();
    }

    public void ForceVictory()
    {
        m_gameOver = false;
        HandleVictory();
    }

    public int GetPendingRunCrystalReward()
    {
        int reachedWave = m_mobManager != null ? m_mobManager.CurrentWave : 1;
        return m_balanceData != null ? m_balanceData.GetCrystalReward(reachedWave) : 0;
    }

    public bool ClaimRunCrystalReward(int multiplier)
    {
        if (m_runRewardGranted || m_playerProgress == null) return false;

        int reward = GetPendingRunCrystalReward() * Mathf.Max(1, multiplier);
        m_playerProgress.AddCrystals(reward);
        m_runRewardGranted = true;
        return true;
    }

    public void ClickSettingBtn()
    {
        if (m_settingsPanel == null || m_gameOver) return;

        m_settingsPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ShowSpawnText(float percent, EntityType.TYPE classType, RarityType.TYPE rarity, Vector3 worldPos)
    {
        string className = classType switch
        {
            EntityType.TYPE.Wizard => GameLanguage.Choose("마법사", "WIZARD"),
            EntityType.TYPE.Archer => GameLanguage.Choose("궁수", "ARCHER"),
            EntityType.TYPE.Warrior => GameLanguage.Choose("전사", "WARRIOR"),
            _ => GameLanguage.Choose("영웅", "HERO")
        };
        string rarityName = rarity switch
        {
            RarityType.TYPE.Common => GameLanguage.Choose("일반", "COMMON"),
            RarityType.TYPE.Rare => GameLanguage.Choose("고급", "RARE"),
            RarityType.TYPE.Elite => GameLanguage.Choose("정예", "ELITE"),
            RarityType.TYPE.Legendary => GameLanguage.Choose("전설", "LEGENDARY"),
            RarityType.TYPE.Mythic => GameLanguage.Choose("신화", "MYTHIC"),
            RarityType.TYPE.Eternal => GameLanguage.Choose("태초", "ETERNAL"),
            _ => rarity.ToString()
        };

        string msg = GameLanguage.IsEnglish
            ? string.Format("{0}% {1} {2}", percent.ToString("F1"), rarityName, className)
            : string.Format("{0}% {1} {2}등급 소환", percent.ToString("F1"), className, rarityName);

        if (rarity >= RarityType.TYPE.Mythic)
        {
            if (IsSpawnText != null)
            {
                IsSpawnText.ShowMythicNotice(msg);
                return;
            }
        }

        if (IsSpawnText != null)
        {
            IsSpawnText.Show(msg, worldPos, 10f);
            return;
        }

        if (IsPool != null)
        {
            var go = IsPool.GetDamageText(m_damageTextParent);
            if (go != null)
            {
                go.transform.position = worldPos;
                var dt = go.GetComponent<DamageText>();
                if (dt != null) dt.SetupMessage(msg, 10f, 0.6f, Color.white);
            }
            return;
        }

        var tmp = new GameObject("SpawnMessage");
        var tmpDt = tmp.AddComponent<DamageText>();
        tmp.transform.position = worldPos;
        tmpDt.SetupMessage(msg, 10f, 0.6f, Color.white);
    }

    public void SetClassToSell(int classType)
    {
        m_selectedClassToSell = classType;

        if (m_selectedClassNameText != null)
        {
            string className = (EntityType.TYPE)classType switch
            {
                EntityType.TYPE.Wizard => GameLanguage.Choose("마법사", "WIZARD"),
                EntityType.TYPE.Archer => GameLanguage.Choose("궁수", "ARCHER"),
                EntityType.TYPE.Warrior => GameLanguage.Choose("전사", "WARRIOR"),
                _ => GameLanguage.Choose("알 수 없음", "UNKNOWN")
            };
            m_selectedClassNameText.text = className;
        }

        if (m_classRarityDisplay != null)
        {
            m_classRarityDisplay.UpdateRarityImages((EntityType.TYPE)m_selectedClassToSell);
        }
    }

    int m_selectedClassToSell = 0;
    public void SellCurrentClassByRarity(int rarityType)
    {
        EntityType.TYPE type = (EntityType.TYPE)m_selectedClassToSell;
        RarityType.TYPE rarity = (RarityType.TYPE)rarityType;

        SellUnits(type, rarity, 1);
    }

    public int GetUnitCount(EntityType.TYPE type, RarityType.TYPE rarity)
    {
        return m_regionManager != null ? m_regionManager.GetCountByClassAndRarity(type, rarity) : 0;
    }

    public int GetUnitSellPrice(RarityType.TYPE rarity) => GetSellPrice(rarity);

    public int SellUnits(EntityType.TYPE type, RarityType.TYPE rarity, int requestedAmount)
    {
        int sold = m_unitSaleService.Sell(type, rarity, requestedAmount,
            m_regionManager, m_economyManager, m_poolManager, GetSellPrice(rarity));
        if (sold > 0) GameAudioManager.Play(GameAudioManager.Sfx.Sell);
        if (m_classRarityDisplay != null) m_classRarityDisplay.UpdateRarityImages(type);
        return sold;
    }

    public Sprite GetSprite(EntityType.TYPE classType, RarityType.TYPE rarity)
    {
        Sprite[] arr = null;
        switch (classType)
        {
            case EntityType.TYPE.Wizard:
                arr = m_wizardSp;
                break;
            case EntityType.TYPE.Archer:
                arr = m_archerSp;
                break;
            case EntityType.TYPE.Warrior:
                arr = m_warriorSp;
                break;
            default:
                arr = null;
                break;
        }

        if (arr == null) return null;

        int idx = rarity switch
        {
            RarityType.TYPE.Common => 0,
            RarityType.TYPE.Rare => 1,
            RarityType.TYPE.Elite => 2,
            RarityType.TYPE.Legendary => 3,
            RarityType.TYPE.Mythic => 4,
            RarityType.TYPE.Eternal => 5,
            _ => -1
        };

        if (idx < 0 || idx >= arr.Length) return null;
        return arr[idx];
    }

    int GetSellPrice(RarityType.TYPE rarity)
    {
        return m_balanceData != null ? m_balanceData.GetSellPrice(rarity) : 0;
    }

    // ── 고급 이하 자동 판매 토글 (게임 시작 시 항상 OFF) ──
    bool m_autoSellLowGrade = false;

    /// <summary>'고급 이하 자동 판매' 토글이 켜져 있는지 여부.</summary>
    public bool AutoSellLowGradeEnabled => m_autoSellLowGrade;

    /// <summary>고급(Rare) 이하 등급인지 판단합니다.</summary>
    public static bool IsLowGrade(RarityType.TYPE rarity) => rarity <= RarityType.TYPE.Rare;

    /// <summary>자동 판매 토글 상태를 설정합니다. 켜면 현재 보유한 고급 이하 유닛도 즉시 정리합니다.</summary>
    public void SetAutoSellLowGrade(bool enabled)
    {
        m_autoSellLowGrade = enabled;
        if (enabled) SweepLowGradeUnits();
    }

    /// <summary>현재 필드 위의 모든 고급 이하 유닛을 판매합니다.</summary>
    void SweepLowGradeUnits()
    {
        EntityType.TYPE[] classes = { EntityType.TYPE.Wizard, EntityType.TYPE.Archer, EntityType.TYPE.Warrior };
        foreach (EntityType.TYPE classType in classes)
        {
            SellUnits(classType, RarityType.TYPE.Common, 0);
            SellUnits(classType, RarityType.TYPE.Rare, 0);
        }
    }
}

sealed class UnitSaleService
{
    public int Sell(EntityType.TYPE type, RarityType.TYPE rarity, int requestedAmount,
        RegionManager regions, EconomyManager economy, PoolManager pool, int unitPrice)
    {
        if (regions == null || economy == null) return 0;

        int owned = regions.GetCountByClassAndRarity(type, rarity);
        int targetAmount = requestedAmount <= 0 ? owned : Mathf.Min(requestedAmount, owned);
        int sold = 0;

        foreach (Region region in regions.GetRegions())
        {
            if (region == null || region.OccupiedType != type) continue;
            while (sold < targetAmount)
            {
                GameObject unit = region.FindAndRemoveUnitByRarity(rarity);
                if (unit == null) break;
                if (pool != null) pool.ReturnUnit(unit);
                else Object.Destroy(unit);
                sold++;
            }
            if (sold >= targetAmount) break;
        }

        if (sold > 0) economy.AddGold(unitPrice * sold);
        return sold;
    }
}
