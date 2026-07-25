using UnityEngine;

[CreateAssetMenu(fileName = "GameBalanceData", menuName = "Random Defense/Game Balance")]
public class GameBalanceData : ScriptableObject
{
    [Header("Economy")]
    [SerializeField] int m_startGold = 100;
    [SerializeField] int m_summonCost = 20;
    [SerializeField] int m_normalKillGold = 2;
    [SerializeField] int m_bossKillGold = 100;
    [SerializeField] int m_upgradeBaseCost = 20;
    [SerializeField] int m_upgradeCostStep = 20;

    [Header("Summon Scaling")]
    [Tooltip("소환 비용이 몇 마리마다 오르는지 (예: 10 = 10마리 소환당)")]
    [SerializeField] int m_summonCostStepEvery = 10;
    [Tooltip("한 단계마다 추가되는 소환 비용")]
    [SerializeField] int m_summonCostIncrement = 1;

    [Header("Progression")]
    [SerializeField] int m_crystalPerReachedWave = 2;
    [SerializeField] int m_researchBaseCost = 50;
    [SerializeField] float m_rareSummonBonusPerLevel = 0.1f;

    [Header("Collection")]
    [Tooltip("도감에 유닛을 처음 등록할 때 주는 크리스탈. 순서: 일반/고급/정예/전설/신화/태초")]
    [SerializeField] int[] m_collectionRewards = { 0, 5, 15, 40, 150, 500 };

    [Header("Daily Missions")]
    [SerializeField] int m_missionSummonGoal = 30;
    [SerializeField] int m_missionWaveGoal = 10;
    [SerializeField] int m_missionBossGoal = 1;
    [Tooltip("미션 보상 크리스탈. 순서: 소환/웨이브/보스")]
    [SerializeField] int[] m_missionRewards = { 20, 30, 50 };

    [Header("Attendance")]
    [Tooltip("출석 1~7일차 보상 크리스탈")]
    [SerializeField] int[] m_attendanceRewards = { 10, 10, 20, 20, 30, 30, 100 };

    [Header("Combat")]
    [SerializeField] float m_attackInterval = 1f;

    [Header("Waves")]
    [SerializeField] int m_maxWave = 50;
    [SerializeField] int m_mobsPerWave = 40;
    [SerializeField] float m_mobSpawnInterval = 1f;
    [SerializeField] float m_waveInterval = 5f;
    [SerializeField] float m_bossTimeLimit = 120f;

    [Header("Difficulty - Mob HP")]
    [Tooltip("몹 체력 = 기본HP × (1 + (웨이브-1) × 이 값). 클수록 웨이브당 급격히 단단해짐")]
    [SerializeField] float m_mobHpGrowthPerWave = 0.45f;

    [Header("Difficulty - Boss HP")]
    [Tooltip("보스 기본 체력 배수 (기본HP × 이 값)")]
    [SerializeField] float m_bossHpBaseMultiplier = 40f;
    [Tooltip("보스 체력 웨이브당 추가 배수 (기본HP × 이 값 × (웨이브-1))")]
    [SerializeField] float m_bossHpGrowthPerWave = 12f;

    public int StartGold => m_startGold;
    public int SummonCost => m_summonCost;
    public int NormalKillGold => m_normalKillGold;
    public int BossKillGold => m_bossKillGold;
    public int UpgradeBaseCost => m_upgradeBaseCost;
    public int UpgradeCostStep => m_upgradeCostStep;
    public int SummonCostStepEvery => m_summonCostStepEvery;
    public int SummonCostIncrement => m_summonCostIncrement;

    /// <summary>이번 판 소환 횟수에 따른 소환 비용을 계산합니다.</summary>
    public int GetSummonCost(int summonCount)
    {
        int steps = m_summonCostStepEvery > 0 ? Mathf.Max(0, summonCount) / m_summonCostStepEvery : 0;
        return m_summonCost + steps * m_summonCostIncrement;
    }
    public float AttackInterval => m_attackInterval;
    public int MaxWave => m_maxWave;
    public int MobsPerWave => m_mobsPerWave;
    public float MobSpawnInterval => m_mobSpawnInterval;
    public float WaveInterval => m_waveInterval;
    public float BossTimeLimit => m_bossTimeLimit;

    /// <summary>웨이브에 따른 일반 몹 최대 체력.</summary>
    public int GetMobHp(int baseHp, int wave)
    {
        float scaled = baseHp * (1f + Mathf.Max(0, wave - 1) * m_mobHpGrowthPerWave);
        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }

    /// <summary>웨이브에 따른 보스 최대 체력.</summary>
    public int GetBossHp(int baseHp, int wave)
    {
        float scaled = baseHp * (m_bossHpBaseMultiplier + Mathf.Max(0, wave - 1) * m_bossHpGrowthPerWave);
        return Mathf.Max(1, Mathf.RoundToInt(scaled));
    }

    public int MissionSummonGoal => Mathf.Max(1, m_missionSummonGoal);
    public int MissionWaveGoal => Mathf.Max(1, m_missionWaveGoal);
    public int MissionBossGoal => Mathf.Max(1, m_missionBossGoal);

    /// <summary>도감에 처음 등록할 때 주는 크리스탈. 배열이 비어 있으면 0입니다.</summary>
    public int GetCollectionReward(RarityType.TYPE rarity) => ReadReward(m_collectionRewards, (int)rarity);

    public int GetMissionReward(int missionIndex) => ReadReward(m_missionRewards, missionIndex);

    /// <summary>출석 일차(1~7)의 보상 크리스탈.</summary>
    public int GetAttendanceReward(int day) => ReadReward(m_attendanceRewards, day - 1);

    static int ReadReward(int[] table, int index)
    {
        if (table == null || index < 0 || index >= table.Length) return 0;
        return Mathf.Max(0, table[index]);
    }

    public int GetCrystalReward(int reachedWave) => Mathf.Max(0, reachedWave) * m_crystalPerReachedWave;
    public int GetResearchCost(int currentLevel) => m_researchBaseCost * (currentLevel + 1);
    public float GetRareSummonBonus(int level) => Mathf.Max(0, level) * m_rareSummonBonusPerLevel;

    public int GetSellPrice(RarityType.TYPE rarity)
    {
        return rarity switch
        {
            RarityType.TYPE.Common => 10,
            RarityType.TYPE.Rare => 25,
            RarityType.TYPE.Elite => 60,
            RarityType.TYPE.Legendary => 150,
            RarityType.TYPE.Mythic => 400,
            RarityType.TYPE.Eternal => 1000,
            _ => 0
        };
    }

    public float GetDamageMultiplier(EntityType.TYPE attacker, SpeciesType.TYPE target)
    {
        if (target == SpeciesType.TYPE.None) return 1f;

        return attacker switch
        {
            EntityType.TYPE.Archer when target == SpeciesType.TYPE.Orc => 1f,
            EntityType.TYPE.Archer => 0.8f,
            EntityType.TYPE.Wizard when target == SpeciesType.TYPE.Undead => 1f,
            EntityType.TYPE.Wizard when target == SpeciesType.TYPE.Orc => 0.8f,
            EntityType.TYPE.Wizard => 0.6f,
            EntityType.TYPE.Warrior when target == SpeciesType.TYPE.Troll => 1f,
            EntityType.TYPE.Warrior when target == SpeciesType.TYPE.Orc => 0.8f,
            EntityType.TYPE.Warrior => 0.6f,
            _ => 1f
        };
    }

    // 각 등급의 누적 상한 경계값(0~100). RollRarity와 GetRarityPercent가 이 값을 공유하여
    // 실제 뽑기 확률과 표시 확률이 절대 어긋나지 않도록 합니다.
    float[] GetRarityThresholds(int rareResearchLevel)
    {
        // 고급소환 연구는 '일반'에서 빠진 확률을 '고급'으로만 옮깁니다.
        // 따라서 일반 상한만 낮추고, 나머지 경계(정예/전설/신화/태초)는 고정합니다.
        float bonus = Mathf.Min(49f, GetRareSummonBonus(rareResearchLevel));
        return new float[]
        {
            50f - bonus,   // Common 상한 (연구 시 감소 → 그만큼 고급 확률로 이동)
            83f,           // Rare 상한 (고급 확률이 bonus만큼 증가)
            93f,           // Elite 상한 (고정)
            99.5f,         // Legendary 상한 (고정)
            99.9f,         // Mythic 상한 (고정)
            100f           // Eternal 상한 (고정)
        };
    }

    public RarityType.TYPE RollRarity(int rareResearchLevel)
    {
        float[] thresholds = GetRarityThresholds(rareResearchLevel);
        float roll = Random.Range(0f, 100f);

        if (roll < thresholds[0]) return RarityType.TYPE.Common;
        if (roll < thresholds[1]) return RarityType.TYPE.Rare;
        if (roll < thresholds[2]) return RarityType.TYPE.Elite;
        if (roll < thresholds[3]) return RarityType.TYPE.Legendary;
        if (roll < thresholds[4]) return RarityType.TYPE.Mythic;
        return RarityType.TYPE.Eternal;
    }

    /// <summary>해당 등급이 실제로 소환될 확률(%)을 반환합니다. 고급소환 연구 레벨이 반영됩니다.</summary>
    public float GetRarityPercent(RarityType.TYPE rarity, int rareResearchLevel)
    {
        float[] thresholds = GetRarityThresholds(rareResearchLevel);
        int index = (int)rarity;
        if (index < 0 || index >= thresholds.Length) return 0f;

        float lower = index == 0 ? 0f : thresholds[index - 1];
        float upper = thresholds[index];
        return Mathf.Max(0f, upper - lower);
    }
}
