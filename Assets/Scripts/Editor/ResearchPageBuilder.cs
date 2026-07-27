#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 연구소 페이지를 씬에 구성하는 에디터 툴. 텍스트 5줄이던 페이지를
/// 아이콘 · 레벨 게이지 · 가격 버튼이 있는 카드 5장으로 다시 만듭니다.
///
/// 여러 번 실행해도 안전합니다. 행 오브젝트(Attack 등)는 이름으로 재사용하고,
/// MainMenuCarouselUI의 연구 바인딩을 매번 다시 연결합니다.
/// </summary>
public static class ResearchPageBuilder
{
    const string MainScenePath = "Assets/Scenes/MainScene.unity";

    // 행 이름은 씬에 이미 있는 오브젝트와 일치해야 재사용됩니다. 순서 = 화면 순서.
    static readonly (string name, ResearchType type, string kenneyIcon)[] Rows =
    {
        ("Attack", ResearchType.Attack, "sword.png"),
        ("StartGold", ResearchType.StartGold, "pouch.png"),
        ("GoldGain", ResearchType.GoldGain, "pouch_add.png"),
        ("RareSummon", ResearchType.RareSummon, "dice_question.png"),
        ("BossDamage", ResearchType.BossDamage, "skull.png"),
    };

    static readonly Color MutedText = new Color(0.55f, 0.58f, 0.66f, 1f);
    static readonly Color GaugeBack = new Color(0.05f, 0.05f, 0.12f, 1f);
    static readonly Color GaugeGold = new Color(1.00f, 0.83f, 0.28f, 1f);

    [MenuItem("Tools/Random Defense/Build Research Page")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("MainScene에서 Canvas를 찾지 못했습니다.");
        RectTransform panel = BrandUI.FindDeep(canvas.transform, "LABORATORYPanel");
        if (panel == null) throw new System.InvalidOperationException("ResearchPage의 LABORATORYPanel을 찾지 못했습니다.");
        MainMenuCarouselUI carousel = Object.FindAnyObjectByType<MainMenuCarouselUI>(FindObjectsInactive.Include);
        if (carousel == null) throw new System.InvalidOperationException("MainMenuCarouselUI를 찾지 못했습니다.");

        Sprite navySquare = BrandUI.LoadAtlasSprite(BrandUI.NavySquare);
        Sprite goldSquare = BrandUI.LoadAtlasSprite(BrandUI.GoldSquare);
        Sprite circle = BrandUI.LoadCompetitiveSprite(BrandUI.CompCircleFilled);
        Sprite ribbon = BrandUI.LoadCompetitiveSprite(BrandUI.CompRibbonGreen);
        Sprite crystal = BrandUI.LoadSprite(BrandUI.CrystalIconPath);

        BrandUI.MakeColumn(panel, new RectOffset(16, 16, 14, 14), 10f);

        // 헤더: 리본 배너. 기존 Title 텍스트를 리본 안으로 옮깁니다 —
        // ApplyStaticLanguage가 페이지의 첫 TMP에 "연구소"를 쓰므로 헤더가 첫 자식이어야 합니다.
        RectTransform header = BrandUI.EnsureChild(panel, "Header");
        BrandUI.SetHeight(header, 96f);
        Image headerImage = BrandUI.Ensure<Image>(header.gameObject);
        headerImage.sprite = ribbon;
        headerImage.type = Image.Type.Simple;
        headerImage.preserveAspect = true;
        headerImage.raycastTarget = false;

        Transform oldTitle = panel.Find("Title");
        if (oldTitle != null) oldTitle.SetParent(header, false);
        TextMeshProUGUI title = BrandUI.MakeText(header, "Title", 30f, Color.white);
        RectTransform titleRect = (RectTransform)title.transform;
        // 리본 양끝 갈래를 피해 가운데 몸통 위에만 얹습니다.
        BrandUI.Anchor(titleRect, new Vector2(0.28f, 0.25f), new Vector2(0.72f, 0.80f));

        SerializedObject so = new SerializedObject(carousel);
        SerializedProperty rowsProperty = so.FindProperty("m_researchRows");
        if (rowsProperty == null) throw new System.InvalidOperationException("m_researchRows 프로퍼티를 찾지 못했습니다.");
        rowsProperty.arraySize = Rows.Length;

        for (int i = 0; i < Rows.Length; i++)
        {
            (string rowName, ResearchType type, string kenneyIcon) = Rows[i];
            RectTransform row = BrandUI.EnsureChild(panel, rowName);
            BrandUI.SetHeight(row, 150f);
            BrandUI.StylePanel(row, navySquare, Color.white, 1.1f).raycastTarget = false;

            // 예전 구조의 잔재 정리: 통짜 텍스트 한 장과 행 전체를 덮던 버튼.
            // 버튼을 남겨 두면 새 가격 버튼과 클릭이 겹칩니다.
            BrandUI.RemoveChild(row, "Label");
            Button oldRowButton = row.GetComponent<Button>();
            if (oldRowButton != null) Object.DestroyImmediate(oldRowButton);

            // 왼쪽: 원형 홀더 + 아이콘. Kenney 아이콘은 흰 단색이라 네이비로 틴트합니다.
            RectTransform holder = BrandUI.EnsureChild(row, "IconHolder");
            holder.anchorMin = holder.anchorMax = new Vector2(0f, 0.5f);
            holder.pivot = new Vector2(0f, 0.5f);
            holder.sizeDelta = new Vector2(96f, 96f);
            holder.anchoredPosition = new Vector2(14f, 0f);
            Image holderImage = BrandUI.Ensure<Image>(holder.gameObject);
            holderImage.sprite = circle;
            holderImage.type = Image.Type.Simple;
            holderImage.preserveAspect = true;
            holderImage.raycastTarget = false;

            Image icon = BrandUI.MakeImage(holder, "Icon", BrandUI.GoldButtonLabel);
            icon.sprite = BrandUI.LoadKenneyIcon(kenneyIcon);
            icon.preserveAspect = true;
            BrandUI.Anchor((RectTransform)icon.transform, new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f));

            // 가운데: 이름, 효과 요약, 레벨 게이지.
            TextMeshProUGUI name = BrandUI.MakeText(row, "Name", 24f, BrandUI.CreamText, TextAlignmentOptions.Left);
            BrandUI.Anchor((RectTransform)name.transform, new Vector2(0.21f, 0.60f), new Vector2(0.66f, 0.94f));

            TextMeshProUGUI effect = BrandUI.MakeText(row, "Effect", 16f, MutedText, TextAlignmentOptions.Left);
            BrandUI.Anchor((RectTransform)effect.transform, new Vector2(0.21f, 0.38f), new Vector2(0.70f, 0.58f));

            // 게이지는 스프라이트 없는 민짜 사각형입니다. 알약 스프라이트를 Filled로 자르면
            // 채움 끝이 타원을 따라 깎여 차오르는 모양이 둥글게 보입니다.
            RectTransform gaugeBack = BrandUI.EnsureChild(row, "Gauge");
            BrandUI.Anchor(gaugeBack, new Vector2(0.21f, 0.10f), new Vector2(0.70f, 0.32f));
            Image gaugeBackImage = BrandUI.Ensure<Image>(gaugeBack.gameObject);
            gaugeBackImage.sprite = null; // 이전 실행이 알약 스프라이트를 넣어 뒀을 수 있다
            gaugeBackImage.type = Image.Type.Simple;
            gaugeBackImage.color = GaugeBack;
            gaugeBackImage.raycastTarget = false;

            // 스프라이트 없는 Image는 Filled여도 fillAmount를 무시하고 통짜로 그립니다.
            // 대신 앵커 오른쪽 끝(anchorMax.x)을 레벨 비율로 움직여 채웁니다 — RefreshAll이 갱신.
            Image gaugeFill = BrandUI.MakeImage(gaugeBack, "Fill", GaugeGold);
            gaugeFill.type = Image.Type.Simple;
            RectTransform fillRect = (RectTransform)gaugeFill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f); // x는 런타임이 레벨 비율로 채운다
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);

            TextMeshProUGUI level = BrandUI.MakeText(gaugeBack, "Level", 15f, BrandUI.CreamText);
            BrandUI.Stretch((RectTransform)level.transform);

            // 오른쪽: 가격 버튼. 행에서 실제로 눌리는 건 이것뿐입니다.
            RectTransform costRect = BrandUI.EnsureChild(row, "CostButton");
            costRect.anchorMin = costRect.anchorMax = new Vector2(1f, 0.5f);
            costRect.pivot = new Vector2(1f, 0.5f);
            costRect.sizeDelta = new Vector2(128f, 96f);
            costRect.anchoredPosition = new Vector2(-14f, 0f);
            Image costImage = BrandUI.Ensure<Image>(costRect.gameObject);
            costImage.raycastTarget = true;
            Button costButton = BrandUI.Ensure<Button>(costRect.gameObject);
            BrandUI.StyleButton(costButton, goldSquare, 2.0f, BrandUI.GoldButtonLabel);

            Image costIcon = BrandUI.MakeImage(costRect, "Icon", Color.white);
            costIcon.sprite = crystal;
            costIcon.preserveAspect = true;
            BrandUI.Anchor((RectTransform)costIcon.transform, new Vector2(0.10f, 0.32f), new Vector2(0.38f, 0.68f));

            TextMeshProUGUI cost = BrandUI.MakeText(costRect, "Cost", 24f, BrandUI.GoldButtonLabel, TextAlignmentOptions.Left);
            BrandUI.Anchor((RectTransform)cost.transform, new Vector2(0.42f, 0.15f), new Vector2(0.94f, 0.85f));

            SerializedProperty element = rowsProperty.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("type").enumValueIndex = (int)type;
            element.FindPropertyRelative("button").objectReferenceValue = costButton;
            element.FindPropertyRelative("nameText").objectReferenceValue = name;
            element.FindPropertyRelative("effectText").objectReferenceValue = effect;
            element.FindPropertyRelative("levelText").objectReferenceValue = level;
            element.FindPropertyRelative("gaugeFill").objectReferenceValue = gaugeFill;
            element.FindPropertyRelative("costIcon").objectReferenceValue = costIcon;
            element.FindPropertyRelative("costText").objectReferenceValue = cost;
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        RectTransform hint = BrandUI.EnsureChild(panel, "ResearchHint");
        BrandUI.SetHeight(hint, 40f);

        // 모든 자식이 준비된 뒤 한 번에 순서를 박습니다. 헤더는 반드시 첫 자식이어야 하고
        // (ApplyStaticLanguage가 첫 TMP에 제목을 씀), 힌트는 마지막이어야 합니다(마지막 TMP에 문구를 씀).
        // 새로 만든 오브젝트는 맨 뒤에 붙기 때문에, 여기서 고정하지 않으면 헤더가 목록 중간으로 밀립니다.
        header.SetSiblingIndex(0);
        for (int i = 0; i < Rows.Length; i++)
        {
            Transform row = panel.Find(Rows[i].name);
            if (row != null) row.SetSiblingIndex(i + 1);
        }
        hint.SetSiblingIndex(Rows.Length + 1);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[ResearchPageBuilder] 연구소 완료 — 리본 헤더 / 카드 5장(아이콘·게이지·가격 버튼)");
    }
}
#endif
