using System;
using UnityEngine;

/// <summary>
/// 유닛 도감. 실제로 소환해 본 직업×등급 조합을 영구 기록하고,
/// 처음 발견한 조합에는 등급별 크리스탈 보너스를 지급합니다.
/// 저장은 PlayerProgressData.collectionMask 비트마스크 한 개로 끝냅니다.
/// </summary>
public class CollectionManager : MonoBehaviour
{
    public const int ClassCount = 3;   // 마법사 / 궁수 / 전사
    public const int RarityCount = 6;  // 일반 ~ 태초
    public const int TotalEntries = ClassCount * RarityCount;

    static readonly EntityType.TYPE[] Classes =
    {
        EntityType.TYPE.Wizard, EntityType.TYPE.Archer, EntityType.TYPE.Warrior
    };

    PlayerProgressManager m_progress;
    GameBalanceData m_balance;

    /// <summary>새 유닛을 처음 발견했을 때 발생합니다. 인자는 (직업, 등급, 지급된 크리스탈).</summary>
    public event Action<EntityType.TYPE, RarityType.TYPE, int> Discovered;

    public void Initialize(PlayerProgressManager progress, GameBalanceData balance)
    {
        m_progress = progress;
        m_balance = balance;
    }

    /// <summary>도감 순서상 index(0~17)에 해당하는 직업.</summary>
    public static EntityType.TYPE ClassAt(int index) => Classes[Mathf.Clamp(index / RarityCount, 0, ClassCount - 1)];

    /// <summary>도감 순서상 index(0~17)에 해당하는 등급.</summary>
    public static RarityType.TYPE RarityAt(int index) => (RarityType.TYPE)Mathf.Clamp(index % RarityCount, 0, RarityCount - 1);

    /// <summary>직업×등급을 도감 index(0~17)로 바꿉니다. 도감 대상이 아니면 -1.</summary>
    public static int IndexOf(EntityType.TYPE classType, RarityType.TYPE rarity)
    {
        int classSlot = Array.IndexOf(Classes, classType);
        int raritySlot = (int)rarity;
        if (classSlot < 0 || raritySlot < 0 || raritySlot >= RarityCount) return -1;
        return classSlot * RarityCount + raritySlot;
    }

    public bool IsDiscovered(int index)
    {
        if (m_progress == null || index < 0 || index >= TotalEntries) return false;
        return (m_progress.Data.collectionMask & (1 << index)) != 0;
    }

    public bool IsDiscovered(EntityType.TYPE classType, RarityType.TYPE rarity) => IsDiscovered(IndexOf(classType, rarity));

    public int DiscoveredCount
    {
        get
        {
            if (m_progress == null) return 0;
            int mask = m_progress.Data.collectionMask;
            int count = 0;
            for (int i = 0; i < TotalEntries; i++)
                if ((mask & (1 << i)) != 0) count++;
            return count;
        }
    }

    /// <summary>
    /// 유닛을 도감에 기록합니다. 이번에 처음 발견했다면 지급된 크리스탈을, 이미 있던 유닛이면 0을 반환합니다.
    /// </summary>
    public int TryDiscover(EntityType.TYPE classType, RarityType.TYPE rarity)
    {
        int index = IndexOf(classType, rarity);
        if (m_progress == null || index < 0 || IsDiscovered(index)) return 0;

        m_progress.Data.collectionMask |= 1 << index;

        int reward = m_balance != null ? m_balance.GetCollectionReward(rarity) : 0;
        if (reward > 0) m_progress.AddCrystals(reward); // AddCrystals가 저장까지 처리
        else m_progress.Save();

        Discovered?.Invoke(classType, rarity, reward);
        return reward;
    }
}
