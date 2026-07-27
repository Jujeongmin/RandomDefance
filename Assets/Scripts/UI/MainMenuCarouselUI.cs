using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public interface IStorePurchaseService
{
    void Purchase(string productId, Action<bool> completed);
    // 스토어(구글 플레이)에서 받은 현지 통화 가격 문자열. 아직 준비 안 됐으면 null.
    string GetLocalizedPrice(string productId);
}

public interface IRewardedAdService
{
    bool IsReady { get; }
    void Show(Action<bool> completed);
}

public sealed class MockCommerceService : IStorePurchaseService, IRewardedAdService
{
    public bool IsReady => true;

    public void Purchase(string productId, Action<bool> completed)
    {
#if UNITY_EDITOR
        completed?.Invoke(true);
#else
        completed?.Invoke(false);
#endif
    }

    public string GetLocalizedPrice(string productId) => null;

    public void Show(Action<bool> completed)
    {
#if UNITY_EDITOR
        completed?.Invoke(true);
#else
        completed?.Invoke(false);
#endif
    }
}

public class MainMenuCarouselUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Serializable]
    public class ProductBinding
    {
        public Button button;
        public string productId;
        public int crystals;
        public bool removesAds;
        [Tooltip("스토어 가격을 못 받아올 때 표시할 가격 (예: ₩1,100). 실제 구글 플레이 가격이 오면 그걸 우선 표시")]
        public string fallbackPrice;
    }

    [Serializable]
    public class ResearchBinding
    {
        public ResearchType type;
        public Button button;
        public TextMeshProUGUI nameText;
        [Tooltip("현재 효과 요약. 이름 옆의 작은 글씨")]
        public TextMeshProUGUI effectText;
        [Tooltip("게이지 위에 얹는 'Lv 3 / 20'")]
        public TextMeshProUGUI levelText;
        [Tooltip("레벨 비율만큼 차오르는 금색 바. anchorMax.x를 비율로 움직여 채운다")]
        public Image gaugeFill;
        [Tooltip("버튼 안 크리스탈 그림. 최대 레벨이면 숨긴다")]
        public Image costIcon;
        public TextMeshProUGUI costText;
    }

    const int ShopPage = 0;
    const int MainPage = 1;
    const int ResearchPage = 2;
    const int RewardAdCrystals = 50;

    [Header("Carousel")]
    [SerializeField] RectTransform m_content;
    [SerializeField] RectTransform m_viewport;
    [SerializeField, Range(0.1f, 0.9f)] float m_pageChangeRatio = 0.5f;
    [SerializeField, Min(0.1f)] float m_fastSwipePagesPerSecond = 1.5f;

    [Header("Shared UI")]
    [SerializeField] TextMeshProUGUI m_crystalText;

    [Header("Shop UI")]
    [SerializeField] ProductBinding[] m_products;
    [SerializeField] Button m_rewardAdButton;
    [SerializeField] TextMeshProUGUI m_rewardAdText;
    [SerializeField] TextMeshProUGUI m_shopStatus;

    [Header("Research UI")]
    [SerializeField] ResearchBinding[] m_researchRows;

    Coroutine m_snapRoutine;
    float m_dragStartX;
    float m_dragStartTime;
    float m_pageWidth;
    int m_page = MainPage;
    int m_lastCrystalCount = -1;
    float m_uiRefreshTimer;
    IStorePurchaseService m_store;
    IRewardedAdService m_ads;
    bool? m_lastEnglish;

    public void Configure(
        RectTransform viewport,
        RectTransform content,
        TextMeshProUGUI crystalText,
        ProductBinding[] products,
        Button rewardAdButton,
        TextMeshProUGUI rewardAdText,
        TextMeshProUGUI shopStatus,
        ResearchBinding[] researchRows)
    {
        m_viewport = viewport;
        m_content = content;
        m_crystalText = crystalText;
        m_products = products;
        m_rewardAdButton = rewardAdButton;
        m_rewardAdText = rewardAdText;
        m_shopStatus = shopStatus;
        m_researchRows = researchRows;
    }

    void Start()
    {
#if UNITY_EDITOR
        m_store = new MockCommerceService();
        m_ads = new MockCommerceService();
#elif UNITY_ANDROID
        m_store = CreateGooglePlayStore();
        m_ads = AdMobRewardedAdService.Shared;
#else
        m_store = new MockCommerceService();
        m_ads = AdMobRewardedAdService.Shared;
#endif
        m_pageWidth = m_viewport != null ? m_viewport.rect.width : 720f;
        if (m_pageWidth <= 1f) m_pageWidth = 720f;

        if (m_products != null)
        {
            foreach (ProductBinding product in m_products)
            {
                ProductBinding captured = product;
                if (captured?.button != null) captured.button.onClick.AddListener(() => Purchase(captured));
            }
        }

        if (m_rewardAdButton != null) m_rewardAdButton.onClick.AddListener(TryRewardAd);
        if (m_researchRows != null)
        {
            foreach (ResearchBinding row in m_researchRows)
            {
                ResearchBinding captured = row;
                if (captured?.button != null) captured.button.onClick.AddListener(() => UpgradeResearch(captured.type));
            }
        }

        SetPageImmediate(MainPage);
        RefreshAll();
    }

    void Purchase(ProductBinding product)
    {
        m_store.Purchase(product.productId, success =>
        {
            if (!success)
            {
                SetShopStatus("결제 서비스가 아직 연결되지 않았습니다.");
                return;
            }

            if (product.removesAds) GManager.Instance.IsProgress.SetAdsRemoved();
            else GManager.Instance.IsProgress.AddCrystals(product.crystals);
            SetShopStatus("테스트 구매가 완료되었습니다.");
            RefreshAll();
        });
    }

#if !UNITY_EDITOR && UNITY_ANDROID
    IStorePurchaseService CreateGooglePlayStore()
    {
        List<GooglePlayStoreService.ProductInfo> infos = new List<GooglePlayStoreService.ProductInfo>();
        if (m_products != null)
        {
            foreach (ProductBinding product in m_products)
            {
                if (product == null || string.IsNullOrEmpty(product.productId)) continue;
                infos.Add(new GooglePlayStoreService.ProductInfo
                {
                    id = product.productId,
                    nonConsumable = product.removesAds
                });
            }
        }
        return new GooglePlayStoreService(infos, OnRestoreOwned);
    }

    // 앱 시작 시 Google Play가 복원한 비소모성 상품(광고 제거)을 반영
    void OnRestoreOwned(string productId)
    {
        ProductBinding match = FindProduct(productId);
        if (match == null || GManager.Instance == null || GManager.Instance.IsProgress == null) return;
        if (match.removesAds) GManager.Instance.IsProgress.SetAdsRemoved();
        RefreshAll();
    }

    ProductBinding FindProduct(string productId)
    {
        if (m_products == null) return null;
        foreach (ProductBinding product in m_products)
            if (product != null && product.productId == productId) return product;
        return null;
    }
#endif

    void TryRewardAd()
    {
        PlayerProgressManager progress = GManager.Instance != null ? GManager.Instance.IsProgress : null;
        if (progress == null || progress.AdsRemoved || !progress.CanClaimRewardAd || !m_ads.IsReady) return;
        m_ads.Show(success =>
        {
            if (!success)
            {
                SetShopStatus("보상형 광고 서비스가 아직 연결되지 않았습니다.");
                return;
            }
            progress.CompleteRewardAd(RewardAdCrystals, TimeSpan.FromMinutes(30));
            SetShopStatus("테스트 광고 보상을 받았습니다.");
            RefreshAll();
        });
    }

    void UpgradeResearch(ResearchType type)
    {
        if (GManager.Instance != null && GManager.Instance.IsResearch.TryUpgrade(type)) RefreshAll();
    }

    void Update()
    {
        m_uiRefreshTimer -= Time.unscaledDeltaTime;
        if (m_uiRefreshTimer > 0f) return;
        m_uiRefreshTimer = 1f;
        RefreshAll();
    }

    void RefreshAll()
    {
        if (GManager.Instance == null || GManager.Instance.IsProgress == null) return;
        if (m_lastEnglish != GameLanguage.IsEnglish)
        {
            m_lastEnglish = GameLanguage.IsEnglish;
            ApplyStaticLanguage();
        }
        PlayerProgressManager progress = GManager.Instance.IsProgress;
        if (m_crystalText != null && m_lastCrystalCount != progress.Crystals)
        {
            m_lastCrystalCount = progress.Crystals;
            m_crystalText.text = $"{progress.Crystals:N0}";
        }

        // 스토어 가격이 비동기로 로딩되면 라벨에 반영 (구매 완료로 문구가 바뀐 뒤에는 유지)
        RefreshProductLabels();

        ResearchManager research = GManager.Instance.IsResearch;
        if (m_researchRows != null)
        {
            foreach (ResearchBinding row in m_researchRows)
            {
                if (row == null || row.button == null) continue;
                int level = research.GetLevel(row.type);
                int max = research.GetMaxLevel(row.type);
                bool maxed = level >= max;

                if (row.nameText != null) row.nameText.text = GetResearchName(row.type);
                if (row.effectText != null) row.effectText.text = GetCurrentResearchEffect(row.type, level);
                if (row.levelText != null) row.levelText.text = $"Lv {level} / {max}";
                if (row.gaugeFill != null)
                {
                    // 스프라이트 없는 사각 채움이라 fillAmount 대신 앵커 폭으로 채웁니다.
                    float ratio = max > 0 ? (float)level / max : 0f;
                    RectTransform fill = row.gaugeFill.rectTransform;
                    fill.anchorMax = new Vector2(ratio, 1f);
                    // 0에 가까우면 안쪽 여백 때문에 폭이 음수가 되어 이상하게 그려집니다. 그냥 끕니다.
                    row.gaugeFill.enabled = ratio > 0.02f;
                }

                // 최대 레벨이면 가격 대신 MAX를 보여주고 크리스탈 그림은 치운다.
                if (row.costText != null)
                    row.costText.text = maxed ? GameLanguage.Choose("최대", "MAX") : $"{research.GetCost(row.type):N0}";
                if (row.costIcon != null) row.costIcon.gameObject.SetActive(!maxed);

                row.button.interactable = !maxed && progress.Crystals >= research.GetCost(row.type);
            }
        }

        if (m_rewardAdButton != null && m_rewardAdText != null)
        {
            bool adsAllowed = !progress.AdsRemoved;
            m_rewardAdButton.gameObject.SetActive(adsAllowed);
            if (!adsAllowed) return;

            TimeSpan remaining = progress.NextRewardAdUtc - DateTime.UtcNow;
            bool ready = remaining <= TimeSpan.Zero;
            m_rewardAdButton.interactable = ready && m_ads != null && m_ads.IsReady;
            m_rewardAdText.text = ready
                ? GameLanguage.Choose("광고 시청  +50 크리스탈", "WATCH AD  +50 CRYSTALS")
                : GameLanguage.Choose($"다음 보상  {remaining.Minutes:00}:{remaining.Seconds:00}", $"NEXT REWARD  {remaining.Minutes:00}:{remaining.Seconds:00}");
        }
    }

    void ApplyStaticLanguage()
    {
        if (m_content != null && m_content.childCount >= 3)
        {
            SetFirstText(m_content.GetChild(ShopPage), GameLanguage.Choose("상점", "SHOP"));
            SetFirstText(m_content.GetChild(ResearchPage), GameLanguage.Choose("연구소", "LABORATORY"));

            TextMeshProUGUI[] researchTexts = m_content.GetChild(ResearchPage).GetComponentsInChildren<TextMeshProUGUI>(true);
            if (researchTexts.Length > 0)
                researchTexts[researchTexts.Length - 1].text = GameLanguage.Choose(
                    "왼쪽으로 밀어 메인 화면으로 이동", "SWIPE LEFT TO RETURN TO MAIN");
        }

        RefreshProductLabels();

        if (m_shopStatus != null)
            m_shopStatus.text = GameLanguage.Choose(
                "오른쪽으로 밀어 메인 화면으로 이동", "SWIPE RIGHT TO RETURN TO MAIN");
    }

    static void SetFirstText(Transform root, string value)
    {
        TextMeshProUGUI text = root != null ? root.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (text != null) text.text = value;
    }

    static void SetButtonText(Button button, string value)
    {
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null) text.text = value;
    }

    void RefreshProductLabels()
    {
        if (m_products == null) return;
        foreach (ProductBinding product in m_products)
            if (product?.button != null) SetButtonText(product.button, GetProductLabel(product));
    }

    string GetProductLabel(ProductBinding product)
    {
        string storePrice = m_store != null ? m_store.GetLocalizedPrice(product.productId) : null;
        string price = !string.IsNullOrEmpty(storePrice) ? storePrice : product.fallbackPrice;
        string priceLine = string.IsNullOrEmpty(price) ? string.Empty : $"\n<size=85%>{price}</size>";

        if (product.removesAds)
            return GameLanguage.Choose("광고 제거 + 3배속", "REMOVE ADS + 3X SPEED") + priceLine;
        return GameLanguage.Choose(
            $"크리스탈 {product.crystals:N0}개", $"{product.crystals:N0} CRYSTALS") + priceLine;
    }

    static string GetResearchName(ResearchType type) => type switch
    {
        ResearchType.Attack => GameLanguage.Choose("공격력 연구", "ATTACK RESEARCH"),
        ResearchType.StartGold => GameLanguage.Choose("시작 골드 연구", "START GOLD RESEARCH"),
        ResearchType.GoldGain => GameLanguage.Choose("골드 획득 연구", "GOLD GAIN RESEARCH"),
        ResearchType.RareSummon => GameLanguage.Choose("희귀 소환 연구", "RARE SUMMON RESEARCH"),
        ResearchType.BossDamage => GameLanguage.Choose("보스 피해 연구", "BOSS DAMAGE RESEARCH"),
        _ => type.ToString()
    };

    static string GetCurrentResearchEffect(ResearchType type, int level) => type switch
    {
        ResearchType.Attack => GameLanguage.Choose($"공격력 +{level * 5}%", $"ATTACK +{level * 5}%"),
        ResearchType.StartGold => GameLanguage.Choose($"시작 골드 +{level * 10}", $"START GOLD +{level * 10}"),
        ResearchType.GoldGain => GameLanguage.Choose($"처치 골드 +{level * 3}%", $"KILL GOLD +{level * 3}%"),
        ResearchType.RareSummon => GameLanguage.Choose($"고급 확률 +{level * 1.5f:0.0}%p", $"RARE +{level * 1.5f:0.0}%p"),
        ResearchType.BossDamage => GameLanguage.Choose($"보스 피해 +{level * 5}%", $"BOSS DAMAGE +{level * 5}%"),
        _ => string.Empty
    };

    void SetShopStatus(string message)
    {
        if (m_shopStatus != null) m_shopStatus.text = message;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (m_snapRoutine != null) StopCoroutine(m_snapRoutine);
        m_dragStartX = m_content.anchoredPosition.x;
        m_dragStartTime = Time.unscaledTime;
    }

    public void OnDrag(PointerEventData eventData)
    {
        float minX = -m_pageWidth * 2f;
        float x = m_dragStartX + eventData.position.x - eventData.pressPosition.x;
        m_content.anchoredPosition = new Vector2(Mathf.Clamp(x, minX, 0f), 0f);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float delta = eventData.position.x - eventData.pressPosition.x;
        float dragDuration = Mathf.Max(0.01f, Time.unscaledTime - m_dragStartTime);
        float pagesPerSecond = Mathf.Abs(delta) / m_pageWidth / dragDuration;
        bool passedHalf = Mathf.Abs(delta) >= m_pageWidth * m_pageChangeRatio;
        bool fastSwipe = pagesPerSecond >= m_fastSwipePagesPerSecond;

        if (passedHalf || fastSwipe) m_page += delta < 0f ? 1 : -1;
        else m_page = Mathf.RoundToInt(-m_content.anchoredPosition.x / m_pageWidth);
        m_page = Mathf.Clamp(m_page, ShopPage, ResearchPage);
        m_snapRoutine = StartCoroutine(SnapToPage());
    }

    IEnumerator SnapToPage()
    {
        Vector2 start = m_content.anchoredPosition;
        Vector2 target = new Vector2(-m_page * m_pageWidth, 0f);
        float timer = 0f;
        while (timer < 0.25f)
        {
            timer += Time.unscaledDeltaTime;
            float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(timer / 0.25f), 3f);
            m_content.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
            yield return null;
        }
        m_content.anchoredPosition = target;
        m_snapRoutine = null;
    }

    void SetPageImmediate(int page)
    {
        m_page = page;
        if (m_content != null) m_content.anchoredPosition = new Vector2(-page * m_pageWidth, 0f);
    }
}
