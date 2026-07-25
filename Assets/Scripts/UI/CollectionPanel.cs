using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유닛 도감 화면. 3직업 × 6등급 = 18칸을 보여주고, 아직 뽑지 못한 칸은 검은 실루엣으로 가립니다.
/// UI는 씬에 미리 배치돼 있고, 직렬화 참조로 연결됩니다.
/// </summary>
public class CollectionPanel : MonoBehaviour
{
    static readonly Color[] RarityColors =
    {
        new Color(0.72f, 0.72f, 0.72f), // 일반
        new Color(0.36f, 0.84f, 0.36f), // 고급
        new Color(0.30f, 0.65f, 1.00f), // 정예
        new Color(0.78f, 0.49f, 1.00f), // 전설
        new Color(1.00f, 0.36f, 0.36f), // 신화
        new Color(1.00f, 0.83f, 0.28f), // 태초
    };

    static readonly Color LockedTint = new Color(0.06f, 0.06f, 0.10f, 1f);
    static readonly Color LockedLabel = new Color(0.45f, 0.45f, 0.52f, 1f);
    static readonly Color CreamText = new Color(0.96f, 0.90f, 0.78f, 1f);

    [Header("References (씬에서 할당)")]
    [SerializeField] Button m_bgButton;
    [SerializeField] Button m_closeButton;
    [SerializeField] TextMeshProUGUI m_titleText;
    [SerializeField] TextMeshProUGUI m_countText;
    [SerializeField] TextMeshProUGUI m_closeText;
    [SerializeField] TextMeshProUGUI m_hintText;

    [Header("Cells — 18칸, 순서: 직업(마법사·궁수·전사) × 등급(일반~태초)")]
    [SerializeField] Image[] m_icons = new Image[CollectionManager.TotalEntries];
    [SerializeField] TextMeshProUGUI[] m_labels = new TextMeshProUGUI[CollectionManager.TotalEntries];
    [Tooltip("에디터 툴이 채워 넣는 유닛 스프라이트. 비어 있으면 GManager의 스프라이트를 씁니다.")]
    [SerializeField] Sprite[] m_unitSprites = new Sprite[CollectionManager.TotalEntries];

    bool m_wired;

    public void Open()
    {
        EnsureWired();
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
    }

    void Populate()
    {
        bool english = GameLanguage.IsEnglish;
        CollectionManager collection = GManager.Instance != null ? GManager.Instance.IsCollection : null;

        if (m_titleText != null) m_titleText.text = english ? "COLLECTION" : "유닛 도감";
        if (m_closeText != null) m_closeText.text = english ? "CLOSE" : "닫기";
        if (m_hintText != null)
        {
            m_hintText.text = english
                ? "Summon a unit for the first time to add it here and earn crystals."
                : "처음 소환한 유닛이 도감에 등록되고, 등급에 따라 크리스탈을 받습니다.";
            m_hintText.color = CreamText;
        }

        int discovered = collection != null ? collection.DiscoveredCount : 0;
        if (m_countText != null)
        {
            m_countText.text = $"{discovered} / {CollectionManager.TotalEntries}";
            m_countText.color = discovered >= CollectionManager.TotalEntries ? RarityColors[5] : CreamText;
        }

        for (int i = 0; i < CollectionManager.TotalEntries; i++)
        {
            bool unlocked = collection != null && collection.IsDiscovered(i);
            EntityType.TYPE classType = CollectionManager.ClassAt(i);
            RarityType.TYPE rarity = CollectionManager.RarityAt(i);

            if (i < m_icons.Length && m_icons[i] != null)
            {
                Image icon = m_icons[i];
                icon.sprite = GetSprite(i, classType, rarity);
                icon.enabled = icon.sprite != null;
                // 잠금 상태는 같은 스프라이트를 거의 검게 눌러 실루엣으로 보여줍니다.
                icon.color = unlocked ? Color.white : LockedTint;
            }

            if (i < m_labels.Length && m_labels[i] != null)
            {
                m_labels[i].text = unlocked ? BuildName(classType, rarity, english) : "?";
                m_labels[i].color = unlocked ? RarityColors[(int)rarity] : LockedLabel;
            }
        }
    }

    Sprite GetSprite(int index, EntityType.TYPE classType, RarityType.TYPE rarity)
    {
        if (index < m_unitSprites.Length && m_unitSprites[index] != null) return m_unitSprites[index];
        return GManager.Instance != null ? GManager.Instance.GetSprite(classType, rarity) : null;
    }

    static string BuildName(EntityType.TYPE classType, RarityType.TYPE rarity, bool english)
    {
        string className = classType switch
        {
            EntityType.TYPE.Wizard => english ? "Wizard" : "마법사",
            EntityType.TYPE.Archer => english ? "Archer" : "궁수",
            _ => english ? "Warrior" : "전사"
        };
        string rarityName = rarity switch
        {
            RarityType.TYPE.Common => english ? "Common" : "일반",
            RarityType.TYPE.Rare => english ? "Rare" : "고급",
            RarityType.TYPE.Elite => english ? "Elite" : "정예",
            RarityType.TYPE.Legendary => english ? "Legendary" : "전설",
            RarityType.TYPE.Mythic => english ? "Mythic" : "신화",
            _ => english ? "Eternal" : "태초"
        };
        return $"{rarityName}\n{className}";
    }
}
