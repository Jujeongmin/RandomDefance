using UnityEngine;
using UnityEngine.U2D.Animation;
using TMPro;

public class UnitSpawner : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI m_spawnButtonText;
    [SerializeField] TextMeshProUGUI m_goldText;
    int m_spawnCount = 0;
    int m_lastShownCost = -1;

    [Header("Hold Spawn Settings")]
    [SerializeField] float m_holdInitialDelay = 0.5f;
    [SerializeField] float m_holdRepeatInterval = 0.12f;
    bool m_spawnHeld = false;
    float m_holdTimer = 0f;

    private void Start()
    {
        if (GManager.Instance != null) GManager.Instance.RegisterUnitSpawner(this);
        if (m_spawnButtonText != null) m_spawnButtonText.text = GameLanguage.Choose("소환", "Spawn");

        RefreshGoldText();
    }

    void RefreshGoldText()
    {
        if (m_goldText == null) return;
        EconomyManager economy = GManager.Instance != null ? GManager.Instance.IsEconomy : null;
        int cost = economy != null ? economy.SummonCost : 0;
        if (cost == m_lastShownCost) return;
        m_lastShownCost = cost;
        m_goldText.text = cost.ToString();
    }

    private void OnDestroy()
    {
        if (GManager.Instance != null && GManager.Instance.IsSpawner == this)
            GManager.Instance.RegisterUnitSpawner(null);
    }

    void Update()
    {
        RefreshGoldText(); // 비용이 바뀐 프레임에만 텍스트 갱신 (재시작 리셋 포함)

        if (!m_spawnHeld) return;

        m_holdTimer -= Time.deltaTime;
        if (m_holdTimer <= 0f)
        {
            Spawn();
            m_holdTimer = m_holdRepeatInterval;
        }
    }

    public void Spawn()
    {
        GManager game = GManager.Instance;
        if (game == null || game.IsEconomy == null || game.IsUnitData == null) return;
        EconomyManager economy = game.IsEconomy;

        // 클래스 선택
        int classRoll = Random.Range(0, 3);
        EntityType.TYPE spawnType = classRoll switch
        {
            0 => EntityType.TYPE.Wizard,
            1 => EntityType.TYPE.Archer,
            _ => EntityType.TYPE.Warrior,
        };

        // 등급 (비율: 50 일반, 33 고급, 10 정예, 6.5 전설, 0.4 신화, 0.1 태초)
        int rareResearchLevel = game.IsResearch != null
            ? game.IsResearch.GetLevel(ResearchType.RareSummon)
            : 0;
        RarityType.TYPE rarity = game.Balance != null
            ? game.Balance.RollRarity(rareResearchLevel)
            : RarityType.TYPE.Common;

        if (game.IsUnitData.Get(spawnType, (int)rarity) == null) return;

        // 비용 지불 (소환할수록 비용 증가)
        if (!economy.TrySpend(economy.SummonCost)) return;
        economy.RegisterSummon();
        RefreshGoldText();

        ExecuteSpawn(spawnType, rarity, 0f);
        // 도감 등록 · 일일 미션 진행 · 튜토리얼 1단계는 실제 소환에서만 발생합니다
        game.NotifyUnitSummoned(spawnType, rarity);
    }

    public void ForceSpawnWithRarity(RarityType.TYPE forcedRarity)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play Mode에서만 테스트 소환이 가능합니다.");
            return;
        }

        int classRoll = Random.Range(0, 3);
        EntityType.TYPE spawnType = classRoll switch
        {
            0 => EntityType.TYPE.Wizard,
            1 => EntityType.TYPE.Archer,
            _ => EntityType.TYPE.Warrior,
        };

        float mockRar = forcedRarity switch
        {
            RarityType.TYPE.Common => 25f,
            RarityType.TYPE.Rare => 60f,
            RarityType.TYPE.Elite => 88f,
            RarityType.TYPE.Legendary => 95f,
            RarityType.TYPE.Mythic => 99.7f,
            RarityType.TYPE.Eternal => 99.95f,
            _ => 0f
        };

        ExecuteSpawn(spawnType, forcedRarity, mockRar);
    }

    private void ExecuteSpawn(EntityType.TYPE spawnType, RarityType.TYPE rarity, float rar, bool announce = true)
    {
        int index = (int)rarity;

        var unitData = GManager.Instance.IsUnitData;
        var checkData = unitData.Get(spawnType, index);
        if (checkData == null) return;

        // 생성
        GameObject go = null;
        if (GManager.Instance != null && GManager.Instance.IsPool != null)
        {
            go = GManager.Instance.IsPool.GetUnit(spawnType);
        }
        else
        {
            go = new GameObject();
        }
        go.name = $"Unit_{spawnType}_{m_spawnCount}";
        m_spawnCount++;

        SpriteLibrary spriteLibrary = GetOrAddComponent<SpriteLibrary>(go);
        GetOrAddComponent<SpriteResolver>(go);
        GetOrAddComponent<SpriteRenderer>(go);

        var regionMgr = GManager.Instance.IsRegion;
        var region = regionMgr.GetRegionForType(spawnType);
        if (region == null)
        {
            if (GManager.Instance != null && GManager.Instance.IsPool != null)
            {
                GManager.Instance.IsPool.ReturnUnit(go);
            }
            else
            {
                Destroy(go);
            }
            return;
        }

        // 초기화
        if (checkData.IsSpLibAsset != null)
            spriteLibrary.spriteLibraryAsset = checkData.IsSpLibAsset;

        if (go.transform.localScale == Vector3.one) go.transform.localScale = Vector3.one * 0.3f;
        Character character = GetOrAddComponent<Character>(go);

        ParentsController controller = go.GetComponent<ParentsController>();
        if (controller == null)
        {
            controller = spawnType switch
            {
                EntityType.TYPE.Wizard => go.AddComponent<WizardController>(),
                EntityType.TYPE.Archer => go.AddComponent<ArcherController>(),
                EntityType.TYPE.Warrior => go.AddComponent<WarriorController>(),
                _ => go.AddComponent<ParentsController>()
            };
        }

        controller.Setting(spawnType, index);
        controller.IsRarity = rarity;
        character.CacheController();
        region.AddUnit(go);

        if (!announce) return;

        // Show spawn message above the unit using pooled DamageText
        Vector3 spawnPos = go.transform.position + Vector3.up * 0.5f;

        // 실제 RollRarity 확률과 동일하게 표시 (고급소환 연구 반영)
        int rareResearchLevel = GManager.Instance != null && GManager.Instance.IsResearch != null
            ? GManager.Instance.IsResearch.GetLevel(ResearchType.RareSummon)
            : 0;
        float percent = GManager.Instance != null && GManager.Instance.Balance != null
            ? GManager.Instance.Balance.GetRarityPercent(rarity, rareResearchLevel)
            : rar;

        if (GManager.Instance != null)
        {
            GManager.Instance.ShowSpawnText(percent, spawnType, rarity, spawnPos);
        }
        if (rarity >= RarityType.TYPE.Mythic)
            GameAudioManager.Play(GameAudioManager.Sfx.RareSummon);

        // '고급 이하 자동판매' 토글이 켜져 있으면 방금 소환한 저등급 유닛을 즉시 판매
        if (GManager.Instance != null && GManager.Instance.AutoSellLowGradeEnabled && GManager.IsLowGrade(rarity))
            GManager.Instance.SellUnits(spawnType, rarity, 1);
    }

    public void BeginSpawnHold()
    {
        Spawn();
        m_spawnHeld = true;
        m_holdTimer = m_holdInitialDelay;
    }

    public void EndSpawnHold()
    {
        m_spawnHeld = false;
    }

    static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        return target.TryGetComponent(out T component) ? component : target.AddComponent<T>();
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(UnitSpawner))]
public class UnitSpawnerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UnitSpawner spawner = (UnitSpawner)target;

        GUILayout.Space(15);
        GUILayout.Label("Test Spawns (Requires Play Mode)", UnityEditor.EditorStyles.boldLabel);

        if (GUILayout.Button("Spawn Common (일반)"))
        {
            spawner.ForceSpawnWithRarity(RarityType.TYPE.Common);
        }
        if (GUILayout.Button("Spawn Rare (고급)"))
        {
            spawner.ForceSpawnWithRarity(RarityType.TYPE.Rare);
        }
        if (GUILayout.Button("Spawn Elite (정예)"))
        {
            spawner.ForceSpawnWithRarity(RarityType.TYPE.Elite);
        }
        if (GUILayout.Button("Spawn Legendary (전설)"))
        {
            spawner.ForceSpawnWithRarity(RarityType.TYPE.Legendary);
        }
        if (GUILayout.Button("Spawn Mythic (신화)"))
        {
            spawner.ForceSpawnWithRarity(RarityType.TYPE.Mythic);
        }
        if (GUILayout.Button("Spawn Eternal (태초)"))
        {
            spawner.ForceSpawnWithRarity(RarityType.TYPE.Eternal);
        }
    }
}
#endif
