#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 상점 페이지를 씬에 구성하는 에디터 툴. 텍스트 버튼 6개이던 페이지를
/// 연구소와 같은 언어(리본 헤더 · 원형 홀더 · 카드 행)로 다시 만듭니다.
///
/// 크리스탈 팩 4종은 홀더 안 크리스탈 개수(1~4개)로 크기 차이를 보여줍니다.
/// 상품 라벨과 가격은 MainMenuCarouselUI가 런타임에 계속 다시 쓰므로 여기서는 건드리지 않습니다.
///
/// 여러 번 실행해도 안전합니다. 행 오브젝트는 이름으로 재사용합니다.
/// </summary>
public static class ShopPageBuilder
{
    const string MainScenePath = "Assets/Scenes/MainScene.unity";

    // 크리스탈 팩 행 이름(씬 순서대로)과 홀더에 담을 크리스탈 개수.
    static readonly (string name, int crystals)[] Packs =
    {
        ("Small Crystal", 1),
        ("Medium Crystal", 2),
        ("Large Crystal", 3),
        ("Extra Large Crystal", 4),
    };

    // 홀더(96×96) 안에서 크리스탈 n개를 놓는 자리. 개수별 프리셋.
    static readonly Vector2[][] ClusterOffsets =
    {
        new[] { Vector2.zero },
        new[] { new Vector2(-12f, 8f), new Vector2(12f, -8f) },
        new[] { new Vector2(0f, 11f), new Vector2(-13f, -9f), new Vector2(13f, -9f) },
        new[] { new Vector2(0f, 14f), new Vector2(-14f, 0f), new Vector2(14f, 0f), new Vector2(0f, -14f) },
    };

    [MenuItem("Tools/Random Defense/Build Shop Page")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("MainScene에서 Canvas를 찾지 못했습니다.");
        RectTransform panel = BrandUI.FindDeep(canvas.transform, "SHOPPanel");
        if (panel == null) throw new System.InvalidOperationException("ShopPage의 SHOPPanel을 찾지 못했습니다.");

        Sprite navySquare = BrandUI.LoadAtlasSprite(BrandUI.NavySquare);
        Sprite goldSquare = BrandUI.LoadAtlasSprite(BrandUI.GoldSquare);
        Sprite circle = BrandUI.LoadCompetitiveSprite(BrandUI.CompCircleFilled);
        Sprite ribbon = BrandUI.LoadCompetitiveSprite(BrandUI.CompRibbonGold);
        Sprite crystal = BrandUI.LoadSprite(BrandUI.CrystalIconPath);
        Sprite play = BrandUI.LoadIconSprite(BrandUI.IconPlay);
        Sprite noAds = BrandUI.LoadIconSprite(BrandUI.IconNoAds);

        BrandUI.MakeColumn(panel, new RectOffset(16, 16, 14, 14), 10f);

        // 헤더: 금 리본. ApplyStaticLanguage가 페이지의 첫 TMP에 "상점"을 쓰므로 첫 자식이어야 합니다.
        RectTransform header = BrandUI.EnsureChild(panel, "Header");
        BrandUI.SetHeight(header, 96f);
        Image headerImage = BrandUI.Ensure<Image>(header.gameObject);
        headerImage.sprite = ribbon;
        headerImage.type = Image.Type.Simple;
        headerImage.preserveAspect = true;
        headerImage.raycastTarget = false;
        headerImage.color = Color.white;

        Transform oldTitle = panel.Find("Title");
        if (oldTitle != null) oldTitle.SetParent(header, false);
        TextMeshProUGUI title = BrandUI.MakeText(header, "Title", 30f, Color.white);
        BrandUI.Anchor((RectTransform)title.transform, new Vector2(0.28f, 0.25f), new Vector2(0.72f, 0.80f));

        // 크리스탈 팩 4종 — 통째로 눌리는 구매 카드.
        foreach ((string rowName, int count) in Packs)
        {
            RectTransform row = StyleRow(panel, rowName, navySquare, 1.1f, BrandUI.NavyButtonLabel);
            RectTransform holder = EnsureHolder(row, circle);

            // 이전 실행이 다른 개수를 만들었을 수 있으니 여분부터 정리합니다.
            for (int i = count; i < 4; i++) BrandUI.RemoveChild(holder, $"Crystal_{i}");
            Vector2[] offsets = ClusterOffsets[count - 1];
            for (int i = 0; i < count; i++)
            {
                Image gem = BrandUI.MakeImage(holder, $"Crystal_{i}", Color.white);
                gem.sprite = crystal;
                gem.preserveAspect = true;
                RectTransform gemRect = (RectTransform)gem.transform;
                gemRect.anchorMin = gemRect.anchorMax = new Vector2(0.5f, 0.5f);
                gemRect.pivot = new Vector2(0.5f, 0.5f);
                gemRect.sizeDelta = new Vector2(34f, 34f);
                gemRect.anchoredPosition = offsets[i];
            }
        }

        // 광고 제거 — 빨간 슬래시.
        RectTransform removeAds = StyleRow(panel, "Remove Ads", navySquare, 1.1f, BrandUI.NavyButtonLabel);
        PlaceSingleIcon(removeAds, circle, noAds, 52f);

        // 보상 광고 — 무료로 매일 누르는 행이라 유일하게 금색입니다.
        RectTransform rewardAd = StyleRow(panel, "RewardAd", goldSquare, 2.0f, BrandUI.GoldButtonLabel);
        PlaceSingleIcon(rewardAd, circle, play, 46f);

        RectTransform status = BrandUI.EnsureChild(panel, "ShopStatus");
        BrandUI.SetHeight(status, 40f);

        // 헤더가 첫 자식, 상태 텍스트가 마지막이 되도록 한 번에 순서를 박습니다.
        header.SetSiblingIndex(0);
        for (int i = 0; i < Packs.Length; i++)
        {
            Transform row = panel.Find(Packs[i].name);
            if (row != null) row.SetSiblingIndex(i + 1);
        }
        removeAds.SetSiblingIndex(Packs.Length + 1);
        rewardAd.SetSiblingIndex(Packs.Length + 2);
        status.SetSiblingIndex(Packs.Length + 3);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[ShopPageBuilder] 상점 완료 — 금 리본 헤더 / 크리스탈 팩 4장 / 광고 제거 / 보상 광고");
    }

    /// <summary>행을 카드형 버튼으로 다듬습니다. 행 전체가 눌리는 구조는 그대로 둡니다.</summary>
    static RectTransform StyleRow(RectTransform panel, string rowName, Sprite sprite, float ppu, Color labelColor)
    {
        RectTransform row = BrandUI.EnsureChild(panel, rowName);
        BrandUI.SetHeight(row, 118f);
        Image image = BrandUI.Ensure<Image>(row.gameObject);
        image.raycastTarget = true;
        Button button = BrandUI.Ensure<Button>(row.gameObject);
        BrandUI.StyleButton(button, sprite, ppu, labelColor);

        // 라벨은 홀더 오른쪽에 왼쪽 정렬로. 이름은 그대로 둬야 런타임 상품 라벨 갱신이
        // (버튼의 첫 TMP를 찾아 쓰는 방식) 이 텍스트를 계속 잡습니다.
        TextMeshProUGUI label = BrandUI.MakeText(row, "Label", 22f, labelColor, TextAlignmentOptions.Left);
        BrandUI.Anchor((RectTransform)label.transform, new Vector2(0.24f, 0.08f), new Vector2(0.96f, 0.92f));
        return row;
    }

    static RectTransform EnsureHolder(RectTransform row, Sprite circle)
    {
        RectTransform holder = BrandUI.EnsureChild(row, "IconHolder");
        holder.anchorMin = holder.anchorMax = new Vector2(0f, 0.5f);
        holder.pivot = new Vector2(0f, 0.5f);
        holder.sizeDelta = new Vector2(88f, 88f);
        holder.anchoredPosition = new Vector2(14f, 0f);
        Image image = BrandUI.Ensure<Image>(holder.gameObject);
        image.sprite = circle;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        holder.SetSiblingIndex(0); // 라벨보다 앞 — 버튼의 첫 TMP는 라벨이어야 합니다
        return holder;
    }

    static void PlaceSingleIcon(RectTransform row, Sprite circle, Sprite iconSprite, float size)
    {
        RectTransform holder = EnsureHolder(row, circle);
        Image icon = BrandUI.MakeImage(holder, "Icon", Color.white);
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        RectTransform iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(size, size);
        iconRect.anchoredPosition = Vector2.zero;
    }
}
#endif
