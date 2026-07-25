using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;
using UnityEngine.UI;
using TMPro;

public class MobManager : MonoBehaviour
{
    static readonly SpeciesType.TYPE[] SpeciesPool =
    {
        SpeciesType.TYPE.Orc,
        SpeciesType.TYPE.Troll,
        SpeciesType.TYPE.Undead
    };

    public SpeciesType.TYPE CurrentSpecies { get; private set; } = SpeciesType.TYPE.None;
    public event System.Action<SpeciesType.TYPE> SpeciesChanged;
    float m_waveDeadline;
    public float WaveTimeRemaining => Mathf.Max(0f, m_waveDeadline - Time.time);
    [Header("Wave Settings")]
    [SerializeField] int m_currentWave = 1;
    [SerializeField] int m_maxWave = 50;
    [SerializeField] int m_mobsPerWave = 40;
    [SerializeField] float m_mobSpawnInterval = 1.0f;
    [SerializeField] float m_waveInterval = 5.0f;

    int m_spawnCount = 0;
    int m_spawnedInWave = 0;
    bool m_gameOver = false;
    const int m_maxMobCount = 100;

    [Header("Boss Settings")]
    [SerializeField] float m_bossTimeLimit = 120.0f;
    [SerializeField] TextMeshProUGUI m_bossTimerText = null;

    MobController m_activeBoss = null;

    [Header("Active Mobs")]
    [SerializeField] List<MobController> m_activeMobs = new List<MobController>();
    public List<MobController> ActiveMobs => m_activeMobs;

    [Header("UI Settings")]
    [SerializeField] TextMeshProUGUI m_mobCountText;
    [SerializeField] TextMeshProUGUI m_waveText;
    [SerializeField] TextMeshProUGUI m_waveSpeciesText;
    [SerializeField] Image m_waveMobImage;
    [SerializeField] Slider m_mobCountSlider = null;

    [Header("Mob Path Settings")]
    [SerializeField] List<Vector3> m_mobWaypoints = new List<Vector3>();

    [Header("Runtime Parents")]
    [SerializeField] Transform m_mobParent = null;

    [Header("Mob Visuals")]
    [SerializeField] float m_mobWorldScale = 0.3f;

    public List<Vector3> MobWaypoints => m_mobWaypoints;
    public float MobWorldScale => m_mobWorldScale;
    public int MobCount => m_spawnCount;
    public int MaxMobCount => m_maxMobCount;
    public int CurrentWave => m_currentWave;

    void Start()
    {
        if (GManager.Instance != null) GManager.Instance.RegisterMobManager(this);

        // Ensure there is a parent container for spawned mobs to keep hierarchy tidy
        if (m_mobParent == null)
        {
            var go = new GameObject("Mobs");
            go.transform.SetParent(this.transform, false);
            m_mobParent = go.transform;
        }

        UpdateMobCountText();
        UpdateWaveText();
        m_mobCountSlider.value = 0f;
    }

    private void OnDestroy()
    {
        // GManager 참조 해제: 씬 전환 시 null 참조 방지
        if (GManager.Instance != null && GManager.Instance.IsMob == this)
        {
            GManager.Instance.RegisterMobManager(null);
        }
    }

    private void OnDisable()
    {
        if (m_waveCoroutine != null)
        {
            try { StopCoroutine(m_waveCoroutine); } catch { }
            m_waveCoroutine = null;
        }
    }

    public void ResetForRestart() => StartWaveLoopAt(1);

    void StartWaveLoopAt(int wave)
    {
        if (m_waveCoroutine != null)
        {
            try { StopCoroutine(m_waveCoroutine); } catch { }
            m_waveCoroutine = null;
        }

        m_spawnCount = 0;
        m_spawnedInWave = 0;
        m_currentWave = wave;
        m_gameOver = false;

        UpdateMobCountText();
        UpdateWaveText();

        m_waveCoroutine = StartCoroutine(WaveLoop());
    }

    private Coroutine m_waveCoroutine = null;

    void UpdateMobCountText()
    {
        if (m_mobCountText != null) m_mobCountText.text = $"{m_spawnCount} / {m_maxMobCount}";
        if (m_mobCountSlider != null)
        {
            m_mobCountSlider.maxValue = m_maxMobCount;
            m_mobCountSlider.value = m_spawnCount;
        }
    }

    // Called by GManager after scene reload to ensure spawning resumes
    public void Resume()
    {
        if (m_gameOver) m_gameOver = false;

        if (m_waveCoroutine == null) m_waveCoroutine = StartCoroutine(WaveLoop());
    }

    void UpdateWaveText()
    {
        if (m_waveText != null)
        {
            if (m_currentWave % 10 == 0)
            {
                m_waveText.text = GameLanguage.Choose($"웨이브 {m_currentWave} (보스)", $"WAVE {m_currentWave} (BOSS)");
            }
            else
            {
                m_waveText.text = GameLanguage.Choose($"웨이브 {m_currentWave}", $"WAVE {m_currentWave}");
            }
        }
    }

    void UpdateWaveInfoUI(EntityData data)
    {
        if (data != null)
        {
            SetCurrentSpecies(data.IsSpeciesType);
            if (m_waveSpeciesText != null) 
            { 
                switch(data.IsSpeciesType)
                {
                    case SpeciesType.TYPE.Orc:
                        m_waveSpeciesText.text = GameLanguage.Choose("오크", "ORC");
                        break;
                    case SpeciesType.TYPE.Troll:
                        m_waveSpeciesText.text = GameLanguage.Choose("트롤", "TROLL");
                        break;
                    case SpeciesType.TYPE.Undead:
                        m_waveSpeciesText.text = GameLanguage.Choose("언데드", "UNDEAD");
                        break;
                    default:
                        m_waveSpeciesText.text = data.IsSpeciesType.ToString();
                        break;
                }
            }
            if (m_waveMobImage != null)
            {
                m_waveMobImage.sprite = data.IsIcon;
                m_waveMobImage.gameObject.SetActive(data.IsIcon != null);
            }
        }
        else
        {
            SetCurrentSpecies(SpeciesType.TYPE.None);
            if (m_waveSpeciesText != null) m_waveSpeciesText.text = string.Empty;
            if (m_waveMobImage != null) m_waveMobImage.gameObject.SetActive(false);
        }
    }

    IEnumerator WaveLoop()
    {
        GameBalanceData balance = GManager.Instance != null ? GManager.Instance.Balance : null;
        int maxWave = balance != null ? balance.MaxWave : m_maxWave;
        int mobsPerWave = balance != null ? balance.MobsPerWave : m_mobsPerWave;
        float spawnInterval = balance != null ? balance.MobSpawnInterval : m_mobSpawnInterval;
        float waveInterval = balance != null ? balance.WaveInterval : m_waveInterval;
        float bossTimeLimit = balance != null ? balance.BossTimeLimit : m_bossTimeLimit;

        while (!m_gameOver)
        {

            m_spawnedInWave = 0;

            bool isBossWave = (m_currentWave % 10 == 0);
            UpdateWaveText();
            if (GManager.Instance != null) GManager.Instance.NotifyWaveReached(m_currentWave);

            if (isBossWave)
            {
                SetWaveCountdown(bossTimeLimit);
                if (m_bossTimerText != null)
                {
                    m_bossTimerText.gameObject.SetActive(true);
                }
                if (m_waveSpeciesText != null)
                {
                    m_waveSpeciesText.text = GameLanguage.Choose("보스 웨이브", "BOSS WAVE");
                }

                SpawnBoss(); // 보스 아이콘은 SpawnBoss 안에서 m_waveMobImage에 설정됨

                float timer = bossTimeLimit;
                while (timer > 0 && m_activeBoss != null)
                {
                    timer -= Time.deltaTime;
                    if (m_bossTimerText != null)
                    {
                        m_bossTimerText.text = GameLanguage.Choose($"보스 시간: {timer:F1}초", $"BOSS TIME: {timer:F1}s");
                    }
                    yield return null;
                }

                if (m_bossTimerText != null)
                {
                    m_bossTimerText.gameObject.SetActive(false);
                }

                if (m_activeBoss != null)
                {
                    // Boss not killed in time -> Game Over!
                    Debug.LogError("Boss not defeated in time! Game Over.");
                    HandleDefeat();
                    yield break;
                }


                Debug.Log($"Boss Wave {m_currentWave} cleared! Waiting for next wave...");
                if (GManager.Instance != null) GManager.Instance.NotifyBossDefeated();

                // 무한 모드에서는 클리어 없이 계속 진행합니다.
                if (!GameModeSettings.IsEndless && m_currentWave >= maxWave)
                {
                    Debug.Log("Final wave boss defeated! Game Clear.");
                    if (GManager.Instance != null)
                    {
                        GManager.Instance.HandleVictory();
                    }
                    m_gameOver = true;
                    yield break;
                }
            }
            else
            {
                SetWaveCountdown(mobsPerWave * spawnInterval + waveInterval);
                SpeciesType.TYPE chosenSpecies = SpeciesPool[Random.Range(0, SpeciesPool.Length)];

                var unitDataMgr = GManager.Instance != null ? GManager.Instance.IsUnitData : null;
                EntityData chosenData = unitDataMgr != null
                    ? unitDataMgr.GetRandomForSpecies(EntityType.TYPE.Mob, chosenSpecies)
                    : null;

                UpdateWaveInfoUI(chosenData);

                while (m_spawnedInWave < mobsPerWave)
                {
                    SpawnMob(chosenData);
                    m_spawnedInWave++;
                    yield return new WaitForSeconds(spawnInterval);
                }
            }

            SetWaveCountdown(waveInterval);
            yield return new WaitForSeconds(waveInterval);
            m_currentWave++;
        }
    }

    void SpawnMob(EntityData data)
    {
        if (!HasSpawnRegion()) return;

        var mobCtrl = CreateMobController($"Mob_W{m_currentWave}_{m_spawnedInWave}");

        if (data != null)
        {
            mobCtrl.Setting(data);

            GameBalanceData balance = GManager.Instance != null ? GManager.Instance.Balance : null;
            int scaledHp = balance != null
                ? balance.GetMobHp(data.IsHp, m_currentWave)
                : data.IsHp + (m_currentWave - 1) * (data.IsHp / 5);
            mobCtrl.SetMaxHp(scaledHp);
        }

        UpdateMobCountText();
    }

    bool HasSpawnRegion()
    {
        var regionMgr = GManager.Instance != null ? GManager.Instance.IsRegion : null;
        if (regionMgr == null) return false;
        if (regionMgr.RegionCount == 0) regionMgr.InitializeRegions();
        var regions = regionMgr.GetRegions();
        return regions != null && regions.Count > 0;
    }

    MobController CreateMobController(string objectName)
    {
        GameObject go = null;
        if (GManager.Instance != null && GManager.Instance.IsPool != null)
        {
            go = GManager.Instance.IsPool.GetMob(m_mobParent);
        }
        else
        {
            go = new GameObject();
            if (m_mobParent != null) go.transform.SetParent(m_mobParent);
        }

        go.name = objectName;
        m_spawnCount++;

        if (m_spawnCount >= m_maxMobCount)
        {
            HandleDefeat();
        }

        if (go.GetComponent<SpriteLibrary>() == null) go.AddComponent<SpriteLibrary>();
        if (go.GetComponent<SpriteResolver>() == null) go.AddComponent<SpriteResolver>();
        if (go.GetComponent<SpriteRenderer>() == null) go.AddComponent<SpriteRenderer>();
        if (go.GetComponent<Character>() == null) go.AddComponent<Character>();

        var mobCtrl = go.GetComponent<MobController>();
        if (mobCtrl == null) mobCtrl = go.AddComponent<MobController>();
        return mobCtrl;
    }

    public void HandleDefeat()
    {
        if (m_gameOver) return;
        m_gameOver = true;
        Time.timeScale = 0f;
        if (GManager.Instance != null)
        {
            GManager.Instance.HandleDefeat();
        }
    }

    public void OnMobDestroyed(bool giveReward, bool isBoss = false)
    {
        GameAudioManager.Play(isBoss ? GameAudioManager.Sfx.BossDeath : GameAudioManager.Sfx.MobDeath);
        m_spawnCount = Mathf.Max(0, m_spawnCount - 1);
        UpdateMobCountText();

        if (giveReward && GManager.Instance != null && GManager.Instance.IsEconomy != null)
        {
            var balance = GManager.Instance.Balance;
            int reward = balance != null
                ? (isBoss ? balance.BossKillGold : balance.NormalKillGold)
                : (isBoss ? 100 : 2);
            GManager.Instance.IsEconomy.AddKillGold(reward);
        }
    }

    public void RegisterMob(MobController mob)
    {
        if (mob == null) return;
        m_activeMobs.RemoveAll(item => item == null);
        if (!m_activeMobs.Contains(mob)) m_activeMobs.Add(mob);
    }

    public void UnregisterMob(MobController mob)
    {
        if (mob == null) return;
        if (m_activeMobs.Contains(mob)) m_activeMobs.Remove(mob);
        m_activeMobs.RemoveAll(item => item == null);
    }

    // Remove and return all active mobs to the pool (or destroy if no pool)
    public void ClearAllMobs()
    {
        var toClear = new List<MobController>(m_activeMobs);
        foreach (var m in toClear)
        {
            if (m == null) continue;
            var go = m.gameObject;
            // attempt to return to pool
            if (GManager.Instance != null && GManager.Instance.IsPool != null)
            {
                GManager.Instance.IsPool.ReturnMob(go);
            }
            else
            {
                Destroy(go);
            }
        }
        m_activeMobs.Clear();
    }

    void SpawnBoss()
    {
        if (!HasSpawnRegion()) return;
        GameAudioManager.EnterBoss();

        var mobCtrl = CreateMobController($"Boss_W{m_currentWave}");

        var unitDataMgr = GManager.Instance != null ? GManager.Instance.IsUnitData : null;

        SpeciesType.TYPE bossSpecies = SpeciesPool[Random.Range(0, SpeciesPool.Length)];
        var data = unitDataMgr != null
            ? unitDataMgr.GetRandomForSpecies(EntityType.TYPE.Boss, bossSpecies)
            : null;
        if (data == null && unitDataMgr != null)
        {
            data = unitDataMgr.GetRandomForSpecies(EntityType.TYPE.Mob, bossSpecies);
            Debug.LogWarning($"Boss data for {bossSpecies} was not found. Falling back to a matching normal mob.");
        }

        if (data != null)
        {
            SetCurrentSpecies(data.IsSpeciesType);
            mobCtrl.Setting(data);

            GameBalanceData balance = GManager.Instance != null ? GManager.Instance.Balance : null;
            int scaledHp = balance != null
                ? balance.GetBossHp(data.IsHp, m_currentWave)
                : data.IsHp * 15 + (m_currentWave - 1) * (data.IsHp * 3);
            mobCtrl.SetMaxHp(scaledHp);

            mobCtrl.SetWorldScale(Vector3.one * (m_mobWorldScale * 2f));

            // 보스 아이콘을 웨이브 정보 이미지에 표시
            if (m_waveMobImage != null)
            {
                m_waveMobImage.sprite = data.IsIcon;
                m_waveMobImage.gameObject.SetActive(data.IsIcon != null);
            }
        }

        m_activeBoss = mobCtrl;
        UpdateMobCountText();
    }

    void SetCurrentSpecies(SpeciesType.TYPE species)
    {
        if (CurrentSpecies == species) return;
        CurrentSpecies = species;
        SpeciesChanged?.Invoke(species);
    }

    void SetWaveCountdown(float duration)
    {
        m_waveDeadline = Time.time + Mathf.Max(0f, duration);
    }

    // Called by other systems when the active boss has been defeated (clears reference)
    public void NotifyBossDefeated(MobController boss)
    {
        if (boss == null) return;
        if (m_activeBoss == boss)
        {
            m_activeBoss = null;
            GameAudioManager.ExitBoss();
            Debug.Log("MobManager: active boss cleared after defeat.");
        }
    }
}
