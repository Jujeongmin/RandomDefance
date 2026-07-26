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

    // 메뉴 버튼 크기 — 아이콘형은 정사각, 기존 확률·랭킹은 가로형 그대로.
    static readonly Vector2 IconButtonSize = new Vector2(88f, 88f);
    static readonly Vector2 WideButtonSize = new Vector2(128f, 48f);

    [MenuItem("Tools/Random Defense/Build Collection and Daily UI")]
    public static void BuildMainSceneUI()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        MainMenuManager manager = Object.FindAnyObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (manager == null) throw new System.InvalidOperationException("MainScene에서 MainMenuManager를 찾지 못했습니다.");
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("MainScene에서 Canvas를 찾지 못했습니다.");

        // 메뉴 버튼은 캐러셀의 가운데 페이지 안에 들어가야 상점·연구소에서 따라다니지 않는다.
        RectTransform mainPage = BrandUI.FindDeep(canvas.transform, "MainPage");
        if (mainPage == null)
            throw new System.InvalidOperationException("MainScene에서 MainPage를 찾지 못했습니다. 메뉴 버튼을 붙일 곳입니다.");

        SerializedObject managerSo = new SerializedObject(manager);
        Button odds = managerSo.FindProperty("m_oddsButton").objectReferenceValue as Button;
        if (odds == null)
            throw new System.InvalidOperationException("MainMenuManager의 확률 버튼이 비어 있습니다. 새 버튼을 복제할 원본으로 필요합니다.");
        Button ranking = managerSo.FindProperty("m_rankingButton").objectReferenceValue as Button;
        if (ranking == null)
            throw new System.InvalidOperationException("MainMenuManager의 랭킹 버튼이 비어 있습니다.");

        CollectionPanel collectionPanel = BuildCollectionPanel(canvas);
        AttendancePanel attendancePanel = BuildAttendancePanel(canvas);
        QuestPanel questPanel = BuildQuestPanel(canvas);

        // 출석과 퀘스트로 갈라지기 전의 통합 패널이 남아 있으면 지운다.
        BrandUI.RemoveChild(canvas.transform, "DailyPanel");

        Button collectionButton = EnsureMenuButton(canvas, odds, "CollectionButton");
        Button attendanceButton = EnsureMenuButton(canvas, odds, "AttendanceButton", "DailyButton");
        Button questButton = EnsureMenuButton(canvas, odds, "QuestButton");

        StyleIconButton(attendanceButton, BrandUI.IconCheck);
        StyleIconButton(questButton, BrandUI.IconList);
        StyleIconButton(collectionButton, BrandUI.IconCards);
        StyleWideButton(odds, BrandUI.IconDiamond);
        StyleWideButton(ranking, BrandUI.IconCrown);

        // 출석은 좌상단에서 설정 기어와 대칭을 이루고, 퀘스트·도감은 기어 아래로 쌓인다.
        // 기어 하단이 -80이므로 8px 띄워 -88부터 시작한다.
        PlaceMenuButton(attendanceButton, mainPage, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), IconButtonSize);
        PlaceMenuButton(questButton, mainPage, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -88f), IconButtonSize);
        PlaceMenuButton(collectionButton, mainPage, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -184f), IconButtonSize);
        PlaceMenuButton(odds, mainPage, Vector2.zero, Vector2.zero, new Vector2(16f, 230f), WideButtonSize);
        PlaceMenuButton(ranking, mainPage, Vector2.zero, Vector2.zero, new Vector2(16f, 286f), WideButtonSize);

        GameObject attendanceBadge = BuildBadge(attendanceButton);
        GameObject questBadge = BuildBadge(questButton);

        TitleScreenPanel titleScreen = Object.FindAnyObjectByType<TitleScreenPanel>(FindObjectsInactive.Include);
        if (titleScreen == null)
            throw new System.InvalidOperationException("MainScene에서 TitleScreenPanel을 찾지 못했습니다. 출석 팝업이 타이틀을 기다리려면 필요합니다.");

        BrandUI.SetRef(managerSo, "m_collectionButton", collectionButton);
        BrandUI.SetRef(managerSo, "m_collectionPanel", collectionPanel);
        BrandUI.SetRef(managerSo, "m_attendanceButton", attendanceButton);
        BrandUI.SetRef(managerSo, "m_attendancePanel", attendancePanel);
        BrandUI.SetRef(managerSo, "m_attendanceBadge", attendanceBadge);
        BrandUI.SetRef(managerSo, "m_questButton", questButton);
        BrandUI.SetRef(managerSo, "m_questPanel", questPanel);
        BrandUI.SetRef(managerSo, "m_questBadge", questBadge);
        BrandUI.SetRef(managerSo, "m_titleScreen", titleScreen);
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        collectionPanel.gameObject.SetActive(false);
        attendancePanel.gameObject.SetActive(false);
        questPanel.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[RetentionUIBuilder] 메인화면 완료 — 도감 / 출석 / 퀘스트 패널, 메뉴 버튼 5개를 MainPage로 이동");
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

        // EnsureChild는 재사용 시 순서를 바로잡지 않으므로, VerticalLayoutGroup이 의도한
        // 순서로 쌓이도록 매번 인덱스를 명시적으로 고정합니다.
        title.rectTransform.SetSiblingIndex(0);
        count.rectTransform.SetSiblingIndex(1);
        scrollArea.SetSiblingIndex(2);
        hint.rectTransform.SetSiblingIndex(3);
        close.transform.SetSiblingIndex(4);

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

    // ---- 출석 보상 ----

    static AttendancePanel BuildAttendancePanel(Canvas canvas)
    {
        Sprite whitePill = BrandUI.LoadAtlasSprite(BrandUI.WhitePill);
        Sprite goldFlat = BrandUI.LoadAtlasSprite(BrandUI.GoldFlat);

        RectTransform root = BrandUI.EnsureChild(canvas.transform, "AttendancePanel");
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

        // 출석만 담으므로 카드가 화면 가운데 절반이면 충분하다.
        RectTransform card = BrandUI.EnsureChild(root, "Card");
        BrandUI.Anchor(card, new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.72f));
        BrandUI.StylePanel(card, whitePill, BrandUI.CardNavy, 0.5f).raycastTarget = true;
        BrandUI.MakeColumn(card, new RectOffset(20, 20, 22, 22), 12f);

        TextMeshProUGUI title = BrandUI.MakeText(card, "Title", 44f, BrandUI.CreamText);
        BrandUI.SetHeight(title, 62f);

        RectTransform row = BrandUI.EnsureChild(card, "Row");
        BrandUI.MakeRow(row, new RectOffset(0, 0, 0, 0), 6f);
        BrandUI.SetHeight(row, 110f);

        var cells = new Image[DailyMissionManager.AttendanceCycle];
        var labels = new TextMeshProUGUI[DailyMissionManager.AttendanceCycle];
        for (int day = 1; day <= DailyMissionManager.AttendanceCycle; day++)
        {
            RectTransform cell = BrandUI.EnsureChild(row, $"Day_{day}");
            Image cellImage = BrandUI.StylePanel(cell, whitePill, BrandUI.SlotNavy, 1.6f);
            cellImage.raycastTarget = false;
            TextMeshProUGUI label = BrandUI.MakeText(cell, "Label", 18f, BrandUI.CreamText);
            BrandUI.Stretch((RectTransform)label.transform);
            cells[day - 1] = cellImage;
            labels[day - 1] = label;
        }

        Button claim = BrandUI.MakeButton(card, "Claim", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 28f);
        BrandUI.SetHeight(claim, 76f);

        TextMeshProUGUI status = BrandUI.MakeText(card, "Status", 24f, BrandUI.CreamText);
        BrandUI.SetHeight(status, 34f);

        Button close = BrandUI.MakeButton(card, "Close", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 28f);
        BrandUI.SetHeight(close, 68f);

        // 재실행으로 자식 순서가 흐트러지지 않도록 매번 인덱스를 명시적으로 고정합니다.
        title.rectTransform.SetSiblingIndex(0);
        row.SetSiblingIndex(1);
        claim.transform.SetSiblingIndex(2);
        status.rectTransform.SetSiblingIndex(3);
        close.transform.SetSiblingIndex(4);

        AttendancePanel panel = BrandUI.Ensure<AttendancePanel>(root.gameObject);
        SerializedObject so = new SerializedObject(panel);
        BrandUI.SetRef(so, "m_bgButton", background);
        BrandUI.SetRef(so, "m_closeButton", close);
        BrandUI.SetRef(so, "m_titleText", title);
        BrandUI.SetRef(so, "m_closeText", close.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRefArray(so, "m_cells", cells);
        BrandUI.SetRefArray(so, "m_labels", labels);
        BrandUI.SetRef(so, "m_claimButton", claim);
        BrandUI.SetRef(so, "m_claimText", claim.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRef(so, "m_statusText", status);
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    // ---- 일일 퀘스트 ----

    static QuestPanel BuildQuestPanel(Canvas canvas)
    {
        Sprite whitePill = BrandUI.LoadAtlasSprite(BrandUI.WhitePill);
        Sprite navyPill = BrandUI.LoadAtlasSprite(BrandUI.NavyPill);
        Sprite goldFlat = BrandUI.LoadAtlasSprite(BrandUI.GoldFlat);

        RectTransform root = BrandUI.EnsureChild(canvas.transform, "QuestPanel");
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
        BrandUI.Anchor(card, new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.76f));
        BrandUI.StylePanel(card, whitePill, BrandUI.CardNavy, 0.5f).raycastTarget = true;
        BrandUI.MakeColumn(card, new RectOffset(20, 20, 22, 22), 12f);

        TextMeshProUGUI title = BrandUI.MakeText(card, "Title", 44f, BrandUI.CreamText);
        BrandUI.SetHeight(title, 62f);

        var questTitles = new TextMeshProUGUI[DailyMissionManager.MissionCount];
        var progressTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];
        var claimButtons = new Button[DailyMissionManager.MissionCount];
        var claimTexts = new TextMeshProUGUI[DailyMissionManager.MissionCount];

        for (int i = 0; i < DailyMissionManager.MissionCount; i++)
        {
            RectTransform questRow = BrandUI.EnsureChild(card, $"Quest_{i}");
            // 재실행으로 자식 순서가 흐트러지지 않도록 Title(0) 다음 자리에 고정합니다.
            questRow.SetSiblingIndex(i + 1);
            BrandUI.StylePanel(questRow, whitePill, BrandUI.SlotNavy, 1.2f).raycastTarget = false;
            BrandUI.MakeRow(questRow, new RectOffset(16, 12, 8, 8), 10f);
            BrandUI.SetHeight(questRow, 96f);

            RectTransform info = BrandUI.EnsureChild(questRow, "Info");
            BrandUI.MakeColumn(info, new RectOffset(0, 0, 0, 0), 2f);
            LayoutElement infoElement = BrandUI.Ensure<LayoutElement>(info.gameObject);
            infoElement.flexibleWidth = 1f;

            TextMeshProUGUI rowTitle = BrandUI.MakeText(info, "Title", 24f, BrandUI.CreamText, TextAlignmentOptions.Left);
            BrandUI.SetHeight(rowTitle, 34f);
            TextMeshProUGUI rowProgress = BrandUI.MakeText(info, "Progress", 22f, BrandUI.CreamText, TextAlignmentOptions.Left);
            BrandUI.SetHeight(rowProgress, 30f);

            Button claim = BrandUI.MakeButton(questRow, "Claim", navyPill, 1f, BrandUI.NavyButtonLabel, 22f);
            LayoutElement claimElement = BrandUI.Ensure<LayoutElement>(claim.gameObject);
            claimElement.preferredWidth = 170f;
            claimElement.flexibleWidth = 0f;

            questTitles[i] = rowTitle;
            progressTexts[i] = rowProgress;
            claimButtons[i] = claim;
            claimTexts[i] = claim.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        TextMeshProUGUI status = BrandUI.MakeText(card, "Status", 24f, BrandUI.CreamText);
        BrandUI.SetHeight(status, 34f);

        Button close = BrandUI.MakeButton(card, "Close", goldFlat, 1.5f, BrandUI.GoldButtonLabel, 28f);
        BrandUI.SetHeight(close, 68f);

        // Title은 위에서, Quest_N은 루프 안에서 이미 고정했으므로 Status/Close만 마저 고정합니다.
        title.rectTransform.SetSiblingIndex(0);
        status.rectTransform.SetSiblingIndex(DailyMissionManager.MissionCount + 1);
        close.transform.SetSiblingIndex(DailyMissionManager.MissionCount + 2);

        QuestPanel panel = BrandUI.Ensure<QuestPanel>(root.gameObject);
        SerializedObject so = new SerializedObject(panel);
        BrandUI.SetRef(so, "m_bgButton", background);
        BrandUI.SetRef(so, "m_closeButton", close);
        BrandUI.SetRef(so, "m_titleText", title);
        BrandUI.SetRef(so, "m_closeText", close.GetComponentInChildren<TextMeshProUGUI>(true));
        BrandUI.SetRefArray(so, "m_questTitles", questTitles);
        BrandUI.SetRefArray(so, "m_progressTexts", progressTexts);
        BrandUI.SetRefArray(so, "m_claimButtons", claimButtons);
        BrandUI.SetRefArray(so, "m_claimTexts", claimTexts);
        BrandUI.SetRef(so, "m_statusText", status);
        so.ApplyModifiedPropertiesWithoutUndo();
        return panel;
    }

    // ---- 메인화면 버튼 ----

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

    /// <summary>
    /// 메뉴 버튼을 확보합니다. 이미 있으면 그대로 쓰고, 없으면 확률 버튼을 복제해 만듭니다.
    /// legacyName은 이전 버전이 쓰던 이름으로, 그 오브젝트가 남아 있으면 새 이름으로 바꿔 재사용합니다.
    /// </summary>
    static Button EnsureMenuButton(Canvas canvas, Button template, string name, string legacyName = null)
    {
        RectTransform existing = BrandUI.FindDeep(canvas.transform, name);
        if (existing == null && !string.IsNullOrEmpty(legacyName))
        {
            existing = BrandUI.FindDeep(canvas.transform, legacyName);
            if (existing != null) existing.gameObject.name = name;
        }

        if (existing != null)
        {
            Button found = existing.GetComponent<Button>();
            if (found == null)
                throw new System.InvalidOperationException($"'{name}' 오브젝트에 Button 컴포넌트가 없습니다.");
            return found;
        }

        GameObject clone = Object.Instantiate(template.gameObject, template.transform.parent);
        clone.name = name;
        Button button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent(); // 복제된 리스너 제거 — MainMenuManager가 다시 연결합니다
        return button;
    }

    /// <summary>
    /// 메뉴 버튼을 MainPage 안으로 옮기고 좌표를 잡습니다.
    /// 캐러셀 밖에 두면 상점·연구소 페이지에서도 따라다니므로 가운데 페이지의 자식으로 만듭니다.
    /// </summary>
    static void PlaceMenuButton(Button button, RectTransform mainPage, Vector2 anchor, Vector2 pivot,
        Vector2 position, Vector2 size)
    {
        RectTransform rect = (RectTransform)button.transform;
        rect.SetParent(mainPage, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    /// <summary>아이콘을 위, 라벨을 아래에 둔 정사각 메뉴 버튼으로 만듭니다.</summary>
    static void StyleIconButton(Button button, int iconIndex)
    {
        Image icon = BrandUI.MakeImage(button.transform, "Icon", Color.white);
        icon.sprite = BrandUI.LoadIconSprite(iconIndex);
        icon.preserveAspect = true;
        BrandUI.Anchor((RectTransform)icon.transform, new Vector2(0.18f, 0.34f), new Vector2(0.82f, 0.92f));

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) return;
        label.fontSize = 18f;
        // 영어 라벨("COLLECTION" 등)은 84x19 상자보다 넓어 줄바꿈되어 잘려나가므로
        // 자동 축소를 켭니다. 한글 라벨은 원래 폭에 맞으므로 최대값은 그대로 18f.
        label.enableAutoSizing = true;
        label.fontSizeMin = 11f;
        label.fontSizeMax = 18f;
        label.alignment = TextAlignmentOptions.Center;
        BrandUI.Anchor((RectTransform)label.transform, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.30f));
    }

    /// <summary>아이콘을 왼쪽, 라벨을 오른쪽에 둔 가로 메뉴 버튼으로 만듭니다.</summary>
    static void StyleWideButton(Button button, int iconIndex)
    {
        Image icon = BrandUI.MakeImage(button.transform, "Icon", Color.white);
        icon.sprite = BrandUI.LoadIconSprite(iconIndex);
        icon.preserveAspect = true;
        BrandUI.Anchor((RectTransform)icon.transform, new Vector2(0.05f, 0.16f), new Vector2(0.28f, 0.84f));

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label == null) return;
        label.alignment = TextAlignmentOptions.Left;
        BrandUI.Anchor((RectTransform)label.transform, new Vector2(0.33f, 0.05f), new Vector2(0.96f, 0.95f));
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
            // EnsureChild는 새로 만든 자식을 맨 뒤에 붙이므로, 재실행 때마다 딤이 Bubble보다
            // 뒤로 밀려나 말풍선을 덮어버립니다. 인덱스를 고정해 딤 4장이 항상 0~3번을 차지하게 합니다.
            rect.SetSiblingIndex(i);
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
