using System;
using UnityEngine;

[Serializable]
public class PlayerProgressData
{
    public int crystals;
    public int highestWave;
    public int endlessHighestWave;
    public int attackResearchLevel;
    public int startGoldResearchLevel;
    public int goldGainResearchLevel;
    public int rareSummonResearchLevel;
    public int bossDamageResearchLevel;
    public bool adsRemoved;
    public long nextRewardAdUtcTicks;

    // ── 유닛 도감 ──
    /// <summary>발견한 유닛 비트마스크. 비트 번호 = 직업 * 6 + 등급 (3직업 × 6등급 = 18비트).</summary>
    public int collectionMask;

    // ── 일일 미션 ──
    /// <summary>미션 진행도가 기준하는 날짜(yyyyMMdd). 이 값이 오늘과 다르면 진행도를 초기화합니다.</summary>
    public int missionDateKey;
    public int[] missionProgress = new int[DailyMissionManager.MissionCount];
    /// <summary>보상을 수령한 미션 비트마스크.</summary>
    public int missionClaimedMask;

    // ── 출석 ──
    /// <summary>연속 출석 일수(1~7 순환). 0이면 아직 한 번도 수령하지 않은 상태입니다.</summary>
    public int attendanceStreak;
    /// <summary>출석 보상을 마지막으로 수령한 날짜(yyyyMMdd).</summary>
    public int attendanceDateKey;
    /// <summary>마지막으로 관측한 날짜(yyyyMMdd). 기기 시계를 과거로 돌리는 조작을 막는 데 씁니다.</summary>
    public int lastSeenDateKey;

    // ── 튜토리얼 ──
    public bool tutorialDone;
}

public class PlayerProgressManager : MonoBehaviour
{
    /// <summary>PlayerPrefs 키. 에디터 툴이 세이브를 직접 손볼 때도 씁니다.</summary>
    public const string SaveKey = "RandomDefense.PlayerProgress.v1";
    PlayerProgressData m_data = new PlayerProgressData();

    public int Crystals => m_data.crystals;
    public int HighestWave => m_data.highestWave;
    public int EndlessHighestWave => m_data.endlessHighestWave;
    public PlayerProgressData Data => m_data;
    public bool AdsRemoved => m_data.adsRemoved;
    public bool TutorialDone => m_data.tutorialDone;
    public DateTime NextRewardAdUtc => m_data.nextRewardAdUtcTicks > 0
        ? new DateTime(m_data.nextRewardAdUtcTicks, DateTimeKind.Utc)
        : DateTime.MinValue;

    public void Initialize() => Load();

    public void AddCrystals(int amount)
    {
        if (amount <= 0) return;
        m_data.crystals += amount;
        Save();
    }

    public bool SpendCrystals(int amount)
    {
        if (amount < 0 || m_data.crystals < amount) return false;
        m_data.crystals -= amount;
        Save();
        return true;
    }

    public void RecordHighestWave(int wave)
    {
        wave = Mathf.Max(0, wave);
        if (wave <= m_data.highestWave) return;
        m_data.highestWave = wave;
        Save();
    }

    /// <summary>무한모드 최고 도달 웨이브 기록.</summary>
    public void RecordEndlessWave(int wave)
    {
        wave = Mathf.Max(0, wave);
        if (wave <= m_data.endlessHighestWave) return;
        m_data.endlessHighestWave = wave;
        Save();
    }

    public void ResetProgress()
    {
        m_data = new PlayerProgressData();
        Save();
    }

    public void SetAdsRemoved()
    {
        m_data.adsRemoved = true;
        Save();
    }

    public void MarkTutorialDone()
    {
        if (m_data.tutorialDone) return;
        m_data.tutorialDone = true;
        Save();
    }

    public bool CanClaimRewardAd => DateTime.UtcNow >= NextRewardAdUtc;

    public void CompleteRewardAd(int crystalReward, TimeSpan cooldown)
    {
        if (!CanClaimRewardAd) return;
        m_data.crystals += Mathf.Max(0, crystalReward);
        m_data.nextRewardAdUtcTicks = DateTime.UtcNow.Add(cooldown).Ticks;
        Save();
    }

    public void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(m_data));
        PlayerPrefs.Save();
    }

    void Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            m_data = new PlayerProgressData();
            return;
        }

        try
        {
            m_data = JsonUtility.FromJson<PlayerProgressData>(json) ?? new PlayerProgressData();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Progress save could not be loaded. A new save will be used. {exception.Message}");
            m_data = new PlayerProgressData();
        }

        Normalize();
    }

    /// <summary>구버전 세이브나 손상된 세이브를 읽어도 배열 길이가 항상 맞도록 보정합니다.</summary>
    void Normalize()
    {
        if (m_data.missionProgress != null && m_data.missionProgress.Length == DailyMissionManager.MissionCount) return;

        int[] restored = new int[DailyMissionManager.MissionCount];
        if (m_data.missionProgress != null)
        {
            int copyCount = Math.Min(m_data.missionProgress.Length, restored.Length);
            Array.Copy(m_data.missionProgress, restored, copyCount);
        }
        m_data.missionProgress = restored;
    }
}
