#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 리텐션 UI(유닛 도감 · 일일 보상 · 튜토리얼 코치마크)를 씬에 만들어 넣는 에디터 툴.
/// 디자인 토큰은 BrandUI를 따르므로 기존 화면과 같은 언어를 씁니다.
///
/// 여러 번 실행해도 안전합니다. 만들어진 뒤에는 인스펙터에서 위치·크기를 직접 다듬어도 되고,
/// 다시 실행해도 이미 있는 오브젝트를 재사용하므로 참조가 끊기지 않습니다.
/// 다만 재실행은 앵커·크기를 기본값으로 되돌리므로, 손으로 배치를 잡은 뒤에는 실행하지 마세요.
/// </summary>
public static class RetentionUIBuilder
{
    const string MainScenePath = "Assets/Scenes/MainScene.unity";
    const string GameScenePath = "Assets/Scenes/GameScene.unity";

    // 도감 격자: 열 = 직업(마법사·궁수·전사), 행 = 등급(일반~태초)
    static readonly string[] ClassAssetNames = { "Wizard", "Archer", "Warrior" };
    const int IdlePoseFrame = 1; // 정면 idle 포즈

    [MenuItem("Tools/Random Defense/Build Collection and Daily UI")]
    public static void BuildMainSceneUI()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        MainMenuManager manager = Object.FindAnyObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (manager == null) throw new System.InvalidOperationException("MainScene에서 MainMenuManager를 찾지 못했습니다.");
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("MainScene에서 Canvas를 찾지 못했습니다.");

        SerializedObject managerSo = new SerializedObject(manager);
        Button odds = managerSo.FindProperty("m_oddsButton").objectReferenceValue as Button;
        if (odds == null)
            throw new System.InvalidOperationException("MainMenuManager의 확률 버튼이 비어 있습니다. 버튼 배치의 기준으로 필요합니다.");

        CollectionPanel collection = BuildCollectionPanel(canvas);
        DailyPanel daily = BuildDailyPanel(canvas);

        Button collectionButton = CloneMenuButton(odds, "CollectionButton", 1);
        Button dailyButton = CloneMenuButton(odds, "DailyButton", 2);
        GameObject badge = BuildBadge(dailyButton);

        BrandUI.SetRef(managerSo, "m_collectionButton", collectionButton);
        BrandUI.SetRef(managerSo, "m_collectionPanel", collection);
        BrandUI.SetRef(managerSo, "m_dailyButton", dailyButton);
        BrandUI.SetRef(managerSo, "m_dailyPanel", daily);
        BrandUI.SetRef(managerSo, "m_dailyBadge", badge);
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        collection.gameObject.SetActive(false);
        daily.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RetentionUIBuilder] 메인화면 완료 — 도감 패널 / 일일 보상 패널 / 버튼 2개 / 알림 배지");
    }

    // ---- 유닛 도감 ----

    static CollectionPanel BuildCollectionPanel(Canvas canvas)
    {
        Vector2 reference = BrandUI.ReferenceResolution(canvas);
        Sprite whitePill = BrandUI.LoadAtlasSprite(BrandUI.WhitePill);
        Sprite goldFlat = BrandUI.LoadAtlasSprite(BrandUI.GoldFlat);

        RectTransform root = BrandUI.EnsureChild(canvas.transform, "CollectionPanel");
        root.SetAsLastSibling();
        BrandUI.Stretch(root);
        Image dim = BrandUI.Ensure<Image>(root.gameObject);
        dim.sprite = null;
        dim.type = Image.Type.Simple;
        dim.color = BrandUI.ModalDim;
        dim.raycastTarget = true;
        Button background = BrandUI.Ensure<Button>(root.gameObject);
        background.transition = Selectable.Transition.None;
        background.targetGraphic = dim;

        RectTransform card = BrandUI.EnsureChild(root, "Card");
        BrandUI.Anchor(card, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.93f));
        BrandUI.StylePanel(card, whitePill, BrandUI.CardNavy, 0.5f).raycastTarget = true;
        BrandUI.MakeColumn(card, new RectOffset(18, 18, 22, 22), 12f);

        TextMeshProUGUI title = BrandUI.MakeText(card, "Title", 44f, BrandUI.CreamText);
        BrandUI.SetHeight(title, 90f);
        TextMeshProUGUI count = BrandUI.MakeText(card, "Count", 34f, BrandUI.CreamText);
        BrandUI.SetHeight(count, 70f);

        // 격자: 3열 × 6행. 6행 전체 높이는 카드보다 커질 수 있으므로 스크롤 영역에 담습니다.
        // 이전 버전(스크롤 없이 Card 바로 아래 Grid)의 잔여물이 있으면 정리합니다.
        BrandUI.RemoveChild(card, "Grid");
        RectTransform scrollArea = BrandUI.EnsureChild(card, "GridScroll");
        LayoutElement scrollElement = BrandUI.Ensure<LayoutElement>(scrollArea.gameObject);
        scrollElement.preferredHeight = 0f;
        scrollElement.flexibleHeight = 1f; // 카드 안에서 남는 공간을 모두 차지
        Image scrollBg = BrandUI.Ensure<Image>(scrollArea.gameObject);
        scrollBg.color = Color.clear;
        BrandUI.Ensure<RectMask2D>(scrollArea.gameObject);
        ScrollRect scrollRect = BrandUI.Ensure<ScrollRect>(scrollArea.gameObject);
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        RectTransform grid = BrandUI.EnsureChild(scrollArea, "Grid");
        grid.anchorMin = new Vector2(0f, 1f);
        grid.anchorMax = new Vector2(1f, 1f);
        grid.pivot = new Vector2(0.5f, 1f);
        grid.anchoredPosition = Vector2.zero;
        float cardWidth = reference.x * (0.96f - 0.04f);
        float innerWidth = cardWidth - 36f;
        const float spacing = 8f;
        float cellWidth = (innerWidth - spacing * 2f) / 3f;
        float cellHeight = cellWidth * 1.25f; // 아이콘 + 두 줄 라벨
        GridLayoutGroup layout = BrandUI.Ensure<GridLayoutGroup>(grid.gameObject);
        layout.cellSize = new Vector2(cellWidth, cellHeight);
        layout.spacing = new Vector2(spacing, spacing);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
        layout.childAlignment = TextAnchor.UpperCenter;
        ContentSizeFitter fitter = BrandUI.Ensure<ContentSizeFitter>(grid.gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = grid;

        var icons = new Image[CollectionManager.TotalEntries];
        var labels = new TextMeshProUGUI[CollectionManager.TotalEntries];
        var sprites = new Sprite[CollectionManager.TotalEntries];

        // 계층 순서는 행(등급) 우선이지만, 직렬화 배열은 도감 index(직업*6+등급) 순서로 채웁니다.
        for (int rarity = 0; rarity < CollectionManager.RarityCount; rarity++)
        {
            for (int classSlot = 0; classSlot < CollectionManager.ClassCount; classSlot++)
            {
                int index = classSlot * CollectionManager.RarityCount + rarity;
                RectTransform cell = BrandUI.EnsureChild(grid, $"Cell_{rarity}_{classSlot}");
                BrandUI.StylePanel(cell, whitePill, BrandUI.SlotNavy, 1.2f).raycastTarget = false;

                Image icon = BrandUI.MakeImage(cell, "Icon", Color.white);
                RectTransform iconRect = (RectTransform)icon.transform;
                BrandUI.Anchor(iconRect, new Vector2(0.12f, 0.34f), new Vector2(0.88f, 0.94f));
                icon.preserveAspect = true;

                TextMeshProUGUI label = BrandUI.MakeText(cell, "Label", 18f, BrandUI.CreamText);
                RectTransform labelRect = (RectTransform)label.transform;
                BrandUI.Anchor(labelRect, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.32f));

                icons[index] = icon;
                labels[index] = label;
                sprites[index] = LoadUnitSprite(classSlot, rarity);
            }
        }

        TextMeshProUGUI hint = BrandUI.MakeText(card, "Hint", 20f, BrandUI.CreamText);
        BrandUI.SetHeight(hint, 54f);

        Button close = BrandUI.MakeButton(card, "Close", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 30f);
        BrandUI.SetHeight(close, 76f);

        CollectionPanel panel = BrandUI.Ensure<CollectionPanel>(root.gameObject);
        SerializedObject so = new SerializedObject(panel);
        BrandUI.SetRef(so, "m_bgButton", background);
        BrandUI.SetRef(so, "m_closeButton", close);
        BrandUI.SetRef(so, "m_titleText", title);
        BrandUI.SetRef(so, "m_countText", count);
        BrandUI.SetRef(so, "m_closeText", close.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRef(so, "m_hintText", hint);
        BrandUI.SetRefArray(so, "m_icons", icons);
        BrandUI.SetRefArray(so, "m_labels", labels);
        BrandUI.SetRefArray(so, "m_unitSprites", sprites);
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    /// <summary>메인화면 유닛 쇼케이스와 같은 정면 idle 포즈를 도감 아이콘으로 씁니다.</summary>
    static Sprite LoadUnitSprite(int classSlot, int rarity)
    {
        string className = ClassAssetNames[classSlot];
        string path = $"{BrandUI.CharacterDir}/{className}_{rarity}.png";
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .FirstOrDefault(s => s.name == $"{className}_{rarity}_{IdlePoseFrame}");
    }

    // ---- 일일 보상 ----

    static DailyPanel BuildDailyPanel(Canvas canvas)
    {
        Sprite whitePill = BrandUI.LoadAtlasSprite(BrandUI.WhitePill);
        Sprite navyPill = BrandUI.LoadAtlasSprite(BrandUI.NavyPill);
        Sprite goldFlat = BrandUI.LoadAtlasSprite(BrandUI.GoldFlat);

        RectTransform root = BrandUI.EnsureChild(canvas.transform, "DailyPanel");
        root.SetAsLastSibling();
        BrandUI.Stretch(root);
        Image dim = BrandUI.Ensure<Image>(root.gameObject);
        dim.sprite = null;
        dim.type = Image.Type.Simple;
        dim.color = BrandUI.ModalDim;
        dim.raycastTarget = true;
        Button background = BrandUI.Ensure<Button>(root.gameObject);
        background.transition = Selectable.Transition.None;
        background.targetGraphic = dim;

        RectTransform card = BrandUI.EnsureChild(root, "Card");
        BrandUI.Anchor(card, new Vector2(0.05f, 0.12f), new Vector2(0.95f, 0.90f));
        BrandUI.StylePanel(card, whitePill, BrandUI.CardNavy, 0.5f).raycastTarget = true;
        BrandUI.MakeColumn(card, new RectOffset(20, 20, 22, 22), 10f);

        TextMeshProUGUI title = BrandUI.MakeText(card, "Title", 44f, BrandUI.CreamText);
        BrandUI.SetHeight(title, 56f);

        // 출석 7칸
        TextMeshProUGUI attendanceTitle = BrandUI.MakeText(card, "AttendanceTitle", 28f, BrandUI.CreamText);
        BrandUI.SetHeight(attendanceTitle, 38f);

        RectTransform attendanceRow = BrandUI.EnsureChild(card, "AttendanceRow");
        BrandUI.MakeRow(attendanceRow, new RectOffset(0, 0, 0, 0), 6f);
        BrandUI.SetHeight(attendanceRow, 96f);

        var cells = new Image[DailyMissionManager.AttendanceCycle];
        var cellLabels = new TextMeshProUGUI[DailyMissionManager.AttendanceCycle];
        for (int day = 1; day <= DailyMissionManager.AttendanceCycle; day++)
        {
            RectTransform cell = BrandUI.EnsureChild(attendanceRow, $"Day_{day}");
            Image cellImage = BrandUI.StylePanel(cell, whitePill, BrandUI.SlotNavy, 1.6f);
            cellImage.raycastTarget = false;
            TextMeshProUGUI label = BrandUI.MakeText(cell, "Label", 18f, BrandUI.CreamText);
            BrandUI.Stretch((RectTransform)label.transform);
            cells[day - 1] = cellImage;
            cellLabels[day - 1] = label;
        }

        Button attendanceClaim = BrandUI.MakeButton(card, "AttendanceClaim", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 26f);
        BrandUI.SetHeight(attendanceClaim, 66f);

        // 미션 3줄
        TextMeshProUGUI missionTitle = BrandUI.MakeText(card, "MissionTitle", 28f, BrandUI.CreamText);
        BrandUI.SetHeight(missionTitle, 38f);

        var missionTitles = new TextMeshProUGUI[DailyMissionManager.MissionCount];
        var missionProgress = new TextMeshProUGUI[DailyMissionManager.MissionCount];
        var missionButtons = new Button[DailyMissionManager.MissionCount];
        var missionButtonTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];

        for (int i = 0; i < DailyMissionManager.MissionCount; i++)
        {
            RectTransform row = BrandUI.EnsureChild(card, $"Mission_{i}");
            BrandUI.StylePanel(row, whitePill, BrandUI.SlotNavy, 1.2f).raycastTarget = false;
            BrandUI.MakeRow(row, new RectOffset(16, 12, 8, 8), 10f);
            BrandUI.SetHeight(row, 96f);

            RectTransform info = BrandUI.EnsureChild(row, "Info");
            BrandUI.MakeColumn(info, new RectOffset(0, 0, 0, 0), 2f);
            LayoutElement infoElement = BrandUI.Ensure<LayoutElement>(info.gameObject);
            infoElement.flexibleWidth = 1f;

            TextMeshProUGUI rowTitle = BrandUI.MakeText(info, "Title", 24f, BrandUI.CreamText, TextAlignmentOptions.Left);
            BrandUI.SetHeight(rowTitle, 34f);
            TextMeshProUGUI rowProgress = BrandUI.MakeText(info, "Progress", 22f, BrandUI.CreamText, TextAlignmentOptions.Left);
            BrandUI.SetHeight(rowProgress, 30f);

            Button claim = BrandUI.MakeButton(row, "Claim", navyPill, 1f, BrandUI.NavyButtonLabel, 22f);
            LayoutElement claimElement = BrandUI.Ensure<LayoutElement>(claim.gameObject);
            claimElement.preferredWidth = 170f;
            claimElement.flexibleWidth = 0f;

            missionTitles[i] = rowTitle;
            missionProgress[i] = rowProgress;
            missionButtons[i] = claim;
            missionButtonTexts[i] = claim.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        TextMeshProUGUI status = BrandUI.MakeText(card, "Status", 24f, BrandUI.CreamText);
        BrandUI.SetHeight(status, 34f);

        Button close = BrandUI.MakeButton(card, "Close", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 30f);
        BrandUI.SetHeight(close, 76f);

        DailyPanel panel = BrandUI.Ensure<DailyPanel>(root.gameObject);
        SerializedObject so = new SerializedObject(panel);
        BrandUI.SetRef(so, "m_bgButton", background);
        BrandUI.SetRef(so, "m_closeButton", close);
        BrandUI.SetRef(so, "m_titleText", title);
        BrandUI.SetRef(so, "m_closeText", close.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRef(so, "m_attendanceTitle", attendanceTitle);
        BrandUI.SetRefArray(so, "m_attendanceCells", cells);
        BrandUI.SetRefArray(so, "m_attendanceLabels", cellLabels);
        BrandUI.SetRef(so, "m_attendanceClaimButton", attendanceClaim);
        BrandUI.SetRef(so, "m_attendanceClaimText", attendanceClaim.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRef(so, "m_missionTitle", missionTitle);
        BrandUI.SetRefArray(so, "m_missionTitles", missionTitles);
        BrandUI.SetRefArray(so, "m_missionProgressTexts", missionProgress);
        BrandUI.SetRefArray(so, "m_missionClaimButtons", missionButtons);
        BrandUI.SetRefArray(so, "m_missionClaimTexts", missionButtonTexts);
        BrandUI.SetRef(so, "m_statusText", status);
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    // ---- 메인화면 버튼 ----

    /// <summary>
    /// 확률 버튼을 복제해 같은 스타일·크기의 메뉴 버튼을 만듭니다.
    /// 부모에 레이아웃 그룹이 있으면 자리 배치는 그룹에 맡기고, 없으면 아래로 한 칸씩 내려 놓습니다.
    /// </summary>
    static Button CloneMenuButton(Button source, string name, int slotBelowSource)
    {
        RectTransform parent = (RectTransform)source.transform.parent;
        Transform existing = parent.Find(name);

        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = Object.Instantiate(source.gameObject, parent);
            go.name = name;
        }

        Button button = go.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent(); // 복제된 리스너 제거 — 코드에서 다시 연결합니다

        if (parent.GetComponent<LayoutGroup>() == null)
        {
            RectTransform sourceRect = (RectTransform)source.transform;
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = sourceRect.anchorMin;
            rect.anchorMax = sourceRect.anchorMax;
            rect.pivot = sourceRect.pivot;
            rect.sizeDelta = sourceRect.sizeDelta;
            rect.localScale = sourceRect.localScale;
            float step = sourceRect.rect.height + 14f;
            rect.anchoredPosition = sourceRect.anchoredPosition - new Vector2(0f, step * slotBelowSource);
        }

        return button;
    }

    /// <summary>버튼 오른쪽 위에 붙는 빨간 알림 점.</summary>
    static GameObject BuildBadge(Button owner)
    {
        RectTransform badge = BrandUI.EnsureChild(owner.transform, "Badge");
        badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.sizeDelta = new Vector2(22f, 22f);
        badge.anchoredPosition = new Vector2(-8f, -6f);
        badge.localScale = Vector3.one;

        Sprite whitePill = BrandUI.LoadAtlasSprite(BrandUI.WhitePill);
        BrandUI.StylePanel(badge, whitePill, BrandUI.BadgeRed, 6f).raycastTarget = false;
        return badge.gameObject;
    }

    // ---- 튜토리얼 오버레이 ----

    [MenuItem("Tools/Random Defense/Build Tutorial Overlay")]
    public static void BuildTutorialOverlay()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("GameScene에서 Canvas를 찾지 못했습니다.");

        Sprite whitePill = BrandUI.LoadAtlasSprite(BrandUI.WhitePill);
        Sprite goldFlat = BrandUI.LoadAtlasSprite(BrandUI.GoldFlat);

        RectTransform root = BrandUI.EnsureChild(canvas.transform, "TutorialOverlay");
        root.SetAsLastSibling(); // 모든 HUD 위에
        BrandUI.Stretch(root);
        BrandUI.Ensure<CanvasGroup>(root.gameObject);

        TutorialOverlay overlay = BrandUI.Ensure<TutorialOverlay>(root.gameObject);

        // 구멍을 만드는 딤 4장. 각 장이 탭을 받아 다음 단계로 넘깁니다.
        var dims = new RectTransform[4];
        string[] dimNames = { "Dim_Top", "Dim_Bottom", "Dim_Left", "Dim_Right" };
        for (int i = 0; i < dimNames.Length; i++)
        {
            // TutorialDimClick이 TutorialOverlay.cs 안에 있던 시절 만들어진 딤에는 스크립트 참조가 끊긴
            // 컴포넌트가 남아 GetComponent로 잡히지 않습니다. 통째로 지우고 새로 만듭니다.
            BrandUI.RemoveChild(root, dimNames[i]);
            Image dim = BrandUI.MakeImage(root, dimNames[i], BrandUI.ModalDim, raycast: true);
            RectTransform rect = (RectTransform)dim.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            TutorialDimClick click = BrandUI.Ensure<TutorialDimClick>(dim.gameObject);
            SerializedObject clickSo = new SerializedObject(click);
            BrandUI.SetRef(clickSo, "m_overlay", overlay);
            clickSo.ApplyModifiedPropertiesWithoutUndo();
            dims[i] = rect;
        }

        // 말풍선
        RectTransform bubble = BrandUI.EnsureChild(root, "Bubble");
        bubble.anchorMin = bubble.anchorMax = bubble.pivot = Vector2.zero;
        bubble.sizeDelta = new Vector2(BrandUI.ReferenceResolution(canvas).x - 80f, 210f);
        BrandUI.StylePanel(bubble, whitePill, BrandUI.CardNavy, 0.5f).raycastTarget = false;

        TextMeshProUGUI bubbleText = BrandUI.MakeText(bubble, "Text", 26f, BrandUI.CreamText);
        RectTransform bubbleTextRect = (RectTransform)bubbleText.transform;
        BrandUI.Anchor(bubbleTextRect, new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.92f));

        TextMeshProUGUI tapHint = BrandUI.MakeText(bubble, "TapHint", 20f, new Color(1f, 0.83f, 0.28f, 1f));
        RectTransform tapHintRect = (RectTransform)tapHint.transform;
        BrandUI.Anchor(tapHintRect, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.28f));

        // 건너뛰기 — 항상 눌리도록 딤보다 뒤에 두지 않고 마지막 자식으로
        Button skip = BrandUI.MakeButton(root, "Skip", goldFlat, 2.5f, BrandUI.GoldButtonLabel, 22f);
        RectTransform skipRect = (RectTransform)skip.transform;
        skipRect.anchorMin = skipRect.anchorMax = new Vector2(1f, 1f);
        skipRect.pivot = new Vector2(1f, 1f);
        skipRect.sizeDelta = new Vector2(150f, 62f);
        skipRect.anchoredPosition = new Vector2(-20f, -20f);
        skipRect.SetAsLastSibling();

        SerializedObject so = new SerializedObject(overlay);
        BrandUI.SetRef(so, "m_canvas", canvas);
        BrandUI.SetRefArray(so, "m_dims", dims);
        BrandUI.SetRef(so, "m_bubble", bubble);
        BrandUI.SetRef(so, "m_bubbleText", bubbleText);
        BrandUI.SetRef(so, "m_tapHintText", tapHint);
        BrandUI.SetRef(so, "m_skipButton", skip);
        BrandUI.SetRef(so, "m_skipButtonText", skip.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRef(so, "m_spawnButton", RequireTarget(canvas, "SpawnBtn"));
        BrandUI.SetRef(so, "m_jobButtons", RequireTarget(canvas, "JobBtns"));
        BrandUI.SetRef(so, "m_sellButton", RequireTarget(canvas, "SellBtn"));
        so.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false); // 진입 시 GManager가 필요할 때만 켭니다

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RetentionUIBuilder] 튜토리얼 오버레이 완료 — 딤 4장 / 말풍선 / 건너뛰기 / 단계 대상 3곳");
    }

    static RectTransform RequireTarget(Canvas canvas, string name)
    {
        RectTransform target = BrandUI.FindDeep(canvas.transform, name);
        if (target == null)
            throw new System.InvalidOperationException($"GameScene에서 튜토리얼 대상 '{name}'을(를) 찾지 못했습니다.");
        return target;
    }
}
#endif
