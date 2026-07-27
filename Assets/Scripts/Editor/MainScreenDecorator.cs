#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// MainScene 타이틀 화면 꾸미기.
/// - 앱 콜드스타트 시 뜨는 타이틀 패널(루비 아트 + 로고 + "터치하여 시작"). 터치하면 사라진다.
/// - 메인화면 배경은 게임씬과 같은 원래 맵(Ground 타일). UI 배경을 깔지 않는다.
/// - 비어 있던 Mid 영역을 랜덤 유닛 3명으로 채운다(진입할 때마다 조합이 바뀜, 정지 idle 포즈).
/// - START/MODE/ODDS/RANKING 버튼을 Universal Stylized UI 스프라이트로 교체.
///
/// 여러 번 실행해도 안전합니다. 설정 버튼은 자체 아이콘이 있어 건드리지 않습니다.
/// </summary>
public static class MainScreenDecorator
{
    const string MainScenePath = "Assets/Scenes/MainScene.unity";
    const string RubyArtPath = "Assets/Store/ruby-portrait-1080x1920.png";
    const string LogoPath = "Assets/GData/Image/UI/title-logo.png";
    // 디자인 토큰의 단일 출처는 BrandUI입니다. 여기서는 이름만 짧게 빌려 씁니다.
    const string ButtonAtlasPath = BrandUI.ButtonAtlasPath;
    const string FontPath = BrandUI.FontPath;
    const string CharacterDir = BrandUI.CharacterDir;

    const string GoldSquare = BrandUI.GoldSquare;
    const string NavyPill = BrandUI.NavyPill;
    const string WhitePill = BrandUI.WhitePill;
    const string GoldFlat = BrandUI.GoldFlat;

    static readonly Color GoldButtonLabel = BrandUI.GoldButtonLabel;
    static readonly Color NavyButtonLabel = BrandUI.NavyButtonLabel;
    static readonly Color TapTextColor = BrandUI.CreamText;
    static readonly Color CreamText = BrandUI.CreamText;
    static readonly Color PanelNavy = BrandUI.PanelNavy;
    static readonly Color CardNavy = BrandUI.CardNavy;
    static readonly Color BarNavy = BrandUI.BarNavy;
    static readonly Color DangerRed = BrandUI.DangerRed;

    static readonly string[] UnitClasses = { "Warrior", "Wizard", "Archer" };
    const int IdlePoseFrame = 1; // 정면 idle 포즈 (뒷모습 9~11 제외)

    const int UiLayer = BrandUI.UiLayer;

    [MenuItem("Tools/Random Defense/Decorate Main Screen")]
    public static void Decorate()
    {
        EnsureSpriteImport(RubyArtPath, SpriteImportMode.Single);
        EnsureSpriteImport(LogoPath, SpriteImportMode.Single);

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        MainMenuManager manager = Object.FindAnyObjectByType<MainMenuManager>(FindObjectsInactive.Include);
        if (manager == null) throw new System.InvalidOperationException("MainScene에서 MainMenuManager를 찾지 못했습니다.");
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("MainScene에서 Canvas를 찾지 못했습니다.");

        SerializedObject so = new SerializedObject(manager);
        Button start = so.FindProperty("m_startButton").objectReferenceValue as Button;
        Button mode = so.FindProperty("m_modeButton").objectReferenceValue as Button;
        Button odds = so.FindProperty("m_oddsButton").objectReferenceValue as Button;
        Button ranking = so.FindProperty("m_rankingButton").objectReferenceValue as Button;
        if (start == null) throw new System.InvalidOperationException("MainMenuManager의 Start 버튼이 비어 있습니다.");

        // 이전 버전이 남긴 오브젝트/컴포넌트 정리
        RemoveChild(canvas.transform, "Vignette");
        RemoveChild(canvas.transform, "TitleLogo");
        RectTransform midEarly = FindDeep(canvas.transform, "Mid");
        if (midEarly != null) RemoveChild(midEarly, "TitleLogo");
        // 구 버전이 붙였던 MainScreenPresenter는 스크립트가 삭제돼 Missing 컴포넌트로 남는다. 제거한다.
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(manager.gameObject);

        // 1) 배경 UI 제거 — 게임씬과 같은 원래 맵(Ground 타일)이 보이게 한다
        RemoveChild(canvas.transform, "Background");

        // 2) 랜덤 유닛 쇼케이스 (빈 Mid 영역)
        RectTransform mid = FindDeep(canvas.transform, "Mid");
        if (mid == null) throw new System.InvalidOperationException("MainPage의 Mid 영역을 찾지 못했습니다.");
        BuildUnitShowcase(mid);

        // 3) 버튼 스타일 (설정 버튼 제외)
        Sprite goldSquare = LoadAtlasSprite(GoldSquare);
        Sprite navyPill = LoadAtlasSprite(NavyPill);
        StyleButton(start, goldSquare, 2.0f, GoldButtonLabel);
        StyleButton(mode, navyPill, 1.0f, NavyButtonLabel);
        StyleButton(odds, navyPill, 1.5f, NavyButtonLabel);
        StyleButton(ranking, navyPill, 1.5f, NavyButtonLabel);

        // 4) 콜드스타트 타이틀 패널 (최상단, 모든 것을 덮음)
        BuildTitlePanel(canvas);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MainScreenDecorator] 메인화면 갱신 완료 — 원래 맵 배경 / 랜덤 유닛(정지) / 버튼 / 타이틀 패널");
    }

    // ---- 유닛 쇼케이스 ----

    static void BuildUnitShowcase(RectTransform mid)
    {
        RectTransform root = EnsureChild(mid, "UnitShowcase");
        Stretch(root);

        // 좌·중·우 3자리. 가운데를 살짝 크고 높게 배치해 주인공처럼.
        Image left = EnsureSlot(root, "Slot_L", new Vector2(-190f, -30f), 150f);
        Image center = EnsureSlot(root, "Slot_C", new Vector2(0f, 10f), 180f);
        Image right = EnsureSlot(root, "Slot_R", new Vector2(190f, -30f), 150f);

        MainMenuUnitShowcase showcase = Ensure<MainMenuUnitShowcase>(root.gameObject);
        SerializedObject so = new SerializedObject(showcase);

        SerializedProperty slots = so.FindProperty("m_slots");
        slots.arraySize = 3;
        slots.GetArrayElementAtIndex(0).objectReferenceValue = left;
        slots.GetArrayElementAtIndex(1).objectReferenceValue = center;
        slots.GetArrayElementAtIndex(2).objectReferenceValue = right;

        List<(string name, Sprite pose)> looks = CollectUnitLooks();
        SerializedProperty looksProp = so.FindProperty("m_looks");
        looksProp.arraySize = looks.Count;
        for (int i = 0; i < looks.Count; i++)
        {
            SerializedProperty element = looksProp.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("Name").stringValue = looks[i].name;
            element.FindPropertyRelative("Pose").objectReferenceValue = looks[i].pose;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[MainScreenDecorator] 유닛 룩 {looks.Count}종 수집");
    }

    static Image EnsureSlot(RectTransform parent, string name, Vector2 pos, float size)
    {
        RectTransform rect = EnsureChild(parent, name);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(size, size);
        rect.localScale = Vector3.one;

        Image image = Ensure<Image>(rect.gameObject);
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        return image;
    }

    static List<(string, Sprite)> CollectUnitLooks()
    {
        var looks = new List<(string, Sprite)>();
        foreach (string unitClass in UnitClasses)
        {
            for (int tier = 0; tier <= 5; tier++)
            {
                string path = $"{CharacterDir}/{unitClass}_{tier}.png";
                Sprite pose = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                    .FirstOrDefault(s => s.name == $"{unitClass}_{tier}_{IdlePoseFrame}");
                if (pose != null) looks.Add(($"{unitClass}_{tier}", pose));
            }
        }
        if (looks.Count == 0)
            throw new System.InvalidOperationException($"{CharacterDir}에서 유닛 스프라이트를 찾지 못했습니다.");
        return looks;
    }

    // ---- 게임씬 HUD 통일 (하단 바·결과 패널) ----

    const string GameScenePath = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Random Defense/Unify Game HUD")]
    public static void UnifyGameHud()
    {
        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("GameScene에서 Canvas를 찾지 못했습니다.");

        Sprite goldFlat = LoadAtlasSprite(GoldFlat);
        Sprite navyPill = LoadAtlasSprite(NavyPill);
        Sprite whitePill = LoadAtlasSprite(WhitePill);

        // 0) 캔버스를 region 구분선(sortingOrder 90~111)보다 앞으로.
        //    선은 맵·유닛 위에 그대로 남고, UI(특히 모달 패널)가 그 위를 덮는다.
        Canvas rootCanvas = canvas.GetComponent<Canvas>();
        rootCanvas.sortingOrder = 200;

        // 1) 하단 바 — 검은 사각형을 라운드 브랜드 네이비로
        RectTransform under = FindDeep(canvas.transform, "Under");
        if (under == null) throw new System.InvalidOperationException("Under를 찾지 못했습니다.");
        StylePanel(under, whitePill, PanelNavy, 0.5f);

        // 2) 직업 강화 버튼들 — 물빠진 반투명 흰색을 네이비 필로 (텍스트 색은 유저가 잡은 그대로)
        RectTransform jobBtns = FindDeep(under, "JobBtns");
        if (jobBtns != null) StylePanel(jobBtns, whitePill, new Color(0.02f, 0.02f, 0.07f, 0.45f), 0.7f);
        foreach (string job in new[] { "WizardBtn", "ArcherBtn", "WarriorBtn" })
        {
            RectTransform btn = FindDeep(under, job);
            if (btn == null) continue;
            StyleSpriteKeepLabels(btn, navyPill, 1f, Color.white);
            // 보유 수 배지
            RectTransform count = FindDeep(btn, job.Replace("Btn", "Count"));
            if (count != null) StylePanel(count, whitePill, new Color(0.06f, 0.055f, 0.14f, 0.9f), 2f);
        }

        // 3) 소환 버튼 — 게임의 핵심 CTA이므로 골드
        RectTransform spawn = FindDeep(under, "SpawnBtn");
        if (spawn != null)
        {
            StyleSpriteKeepLabels(spawn, goldFlat, 1.5f, Color.white);
            TintAllLabels(spawn, GoldButtonLabel);
        }

        // 4) 판매 버튼 — 네이비
        RectTransform sell = FindDeep(under, "SellBtn");
        if (sell != null) StyleSpriteKeepLabels(sell, navyPill, 1f, Color.white);

        // 5) 판매 패널 — 카드·닫기만. HintText/Title 텍스트·색, 토글/탭의 색은
        //    유저가 직접 잡았거나 코드가 제어하므로 건드리지 않는다 (스프라이트만 교체).
        RectTransform sellPanel = FindDeep(under, "SellPanel");
        if (sellPanel != null)
        {
            RectTransform content = FindDeep(sellPanel, "SellContent");
            if (content != null) StylePanel(content, whitePill, CardNavy, 0.5f);
            RectTransform cards = FindDeep(sellPanel, "Cards");
            if (cards != null) StylePanel(cards, whitePill, new Color(0.10f, 0.09f, 0.20f, 1f), 0.7f);
            RectTransform close = FindDeep(sellPanel, "CloseButton");
            if (close != null) StyleSpriteKeepLabels(close, goldFlat, 3f, Color.white);
            RectTransform autoSell = FindDeep(sellPanel, "AutoSellToggle");
            if (autoSell != null) StyleSpriteKeepColor(autoSell, whitePill, 1f);
            RectTransform classTabs = FindDeep(sellPanel, "ClassTabs");
            if (classTabs != null)
                foreach (Button tab in classTabs.GetComponentsInChildren<Button>(true))
                    StyleSpriteKeepColor((RectTransform)tab.transform, whitePill, 1f);
        }

        // 6) 결과 패널 — 투명에 가깝던 카드를 정식 모달로
        RectTransform result = FindDeep(canvas.transform, "ResultPanel");
        if (result == null) throw new System.InvalidOperationException("ResultPanel을 찾지 못했습니다.");
        Image dim = result.GetComponent<Image>();
        if (dim != null) dim.color = new Color(0f, 0f, 0f, 0.72f); // 설정 패널과 같은 배경 딤
        RectTransform card = FindDeep(result, "ResultPanelImg");
        if (card != null)
        {
            StylePanel(card, whitePill, CardNavy, 0.5f);
            TintText(card, "TitleText", CreamText);
            TintText(card, "DetailText", CreamText);
        }
        RectTransform basic = FindDeep(result, "BasicRewardButton");
        if (basic != null) { StyleSpriteKeepLabels(basic, navyPill, 1f, Color.white); TintAllLabels(basic, NavyButtonLabel); }
        RectTransform dbl = FindDeep(result, "DoubleRewardButton");
        if (dbl != null) { StyleSpriteKeepLabels(dbl, goldFlat, 1.5f, Color.white); TintAllLabels(dbl, GoldButtonLabel); }

        // 7) 일시정지 설정 패널 — 결과 패널과 같은 모달 문법
        //    (루트 'Setting'은 캔버스 직속. Top의 설정 버튼과 이름이 같아 캔버스 자식에서 찾는다)
        RectTransform pause = null;
        foreach (Transform child in canvas.transform)
            if (child.name == "Setting") { pause = (RectTransform)child; break; }
        if (pause != null)
        {
            Image pauseDim = pause.GetComponent<Image>();
            if (pauseDim != null) pauseDim.color = new Color(0f, 0f, 0f, 0.72f);
            RectTransform pauseCard = FindDeep(pause, "PanelImg");
            if (pauseCard != null) StylePanel(pauseCard, whitePill, CardNavy, 0.5f);
            // 재시작 = 이어서 하게 만드는 주요 행동(골드), 나가기 = 판을 버리는 행동(경고 빨강)
            RectTransform restart = FindDeep(pause, "RestartBtn");
            if (restart != null) { StyleSpriteKeepLabels(restart, goldFlat, 1.5f, Color.white); TintAllLabels(restart, GoldButtonLabel); }
            RectTransform exit = FindDeep(pause, "ExitBtn");
            if (exit != null) { StyleSpriteKeepLabels(exit, whitePill, 1f, DangerRed); TintAllLabels(exit, CreamText); }
            foreach (string audio in new[] { "BgmButton", "SfxButton" })
            {
                RectTransform btn = FindDeep(pause, audio);
                if (btn != null) { StyleSpriteKeepLabels(btn, navyPill, 1f, Color.white); TintAllLabels(btn, NavyButtonLabel); }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MainScreenDecorator] 게임 HUD 통일 완료 — 하단 바 / 강화·소환·판매 버튼 / 판매 패널 / 결과 패널 / 일시정지 패널");
    }

    /// <summary>버튼/이미지에 스프라이트를 입히되 라벨 색은 건드리지 않습니다.</summary>
    static void StyleSpriteKeepLabels(RectTransform rect, Sprite sprite, float ppu, Color tint)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = ppu;
        image.color = tint;

        Button button = rect.GetComponent<Button>();
        if (button != null)
        {
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.pressedColor = new Color(0.82f, 0.82f, 0.86f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
        }
    }

    /// <summary>스프라이트만 교체하고 색은 그대로 둡니다 (코드가 색을 제어하는 토글/탭용).</summary>
    static void StyleSpriteKeepColor(RectTransform rect, Sprite sprite, float ppu)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = ppu;
    }

    static void TintAllLabels(RectTransform root, Color color)
    {
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            text.color = color;
    }

    // ---- 가독성·분위기 다듬기 ----

    const string EdgeScrimPath = "Assets/GData/Image/UI/edge-scrim.png";

    [MenuItem("Tools/Random Defense/Polish Readability")]
    public static void PolishReadability()
    {
        EnsureSpriteImport(EdgeScrimPath, SpriteImportMode.Single);

        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("MainScene에서 Canvas를 찾지 못했습니다.");

        // 1) 황혼 스크림 — 화면 위·아래 가장자리에만 브랜드 네이비가 스며들어
        //    밝은 맵과 다크 UI를 잇고, 최고웨이브/버튼 텍스트의 가독성을 확보한다.
        //    가운데는 완전 투명이라 맵과 유닛 쇼케이스는 그대로 보인다.
        RectTransform scrim = EnsureChild(canvas.transform, "EdgeScrim");
        scrim.SetSiblingIndex(0); // 모든 페이지 뒤
        Stretch(scrim);
        Image scrimImage = Ensure<Image>(scrim.gameObject);
        scrimImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EdgeScrimPath);
        scrimImage.color = Color.white;
        scrimImage.type = Image.Type.Simple;
        scrimImage.raycastTarget = false;

        // 2) 최고웨이브 텍스트 — 순백 대신 시스템 크림색으로 통일
        RectTransform highWave = FindDeep(canvas.transform, "HighWave");
        if (highWave != null)
        {
            TextMeshProUGUI text = highWave.GetComponent<TextMeshProUGUI>();
            if (text != null) text.color = CreamText;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MainScreenDecorator] 가독성 다듬기 완료 — 황혼 스크림 / 최고웨이브 크림색");
    }

    // ---- 페이지 디자인 통일 (상점·연구소·설정·확률·재화 바) ----

    [MenuItem("Tools/Random Defense/Unify Page Designs")]
    public static void UnifyPages()
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) throw new System.InvalidOperationException("MainScene에서 Canvas를 찾지 못했습니다.");

        Sprite goldFlat = LoadAtlasSprite(GoldFlat);
        Sprite navyPill = LoadAtlasSprite(NavyPill);
        Sprite whitePill = LoadAtlasSprite(WhitePill);

        // 상점과 연구소는 이제 전용 빌더(Build Shop Page / Build Research Page)가 만듭니다.
        // 예전 방식(민짜 알약 버튼)으로 되돌려 버리므로 여기서는 두 페이지의 패널 배경만 입힙니다.
        RectTransform shop = FindDeep(canvas.transform, "SHOPPanel");
        if (shop != null) StylePanel(shop, whitePill, PanelNavy, 0.5f);
        RectTransform lab = FindDeep(canvas.transform, "LABORATORYPanel");
        if (lab != null) StylePanel(lab, whitePill, PanelNavy, 0.5f);

        // 1) 재화 바
        RectTransform currency = FindDeep(canvas.transform, "CurrencyBar");
        if (currency != null) StylePanel(currency, whitePill, BarNavy, 1f);

        // 2) 설정 패널: 카드 + 버튼들. 한국어/영어 버튼은 흰 스프라이트를 유지해
        //    MainMenuManager.SetButtonSelected의 색 틴트(선택 파랑/비선택 네이비)가 그대로 동작한다.
        RectTransform settings = FindDeep(canvas.transform, "SettingsCard");
        if (settings != null)
        {
            StylePanel(settings, whitePill, CardNavy, 0.7f);
            foreach (string item in new[] { "BgmButton", "SfxButton", "CloseButton" })
                StyleNamedButton(settings, item, navyPill, 1f, NavyButtonLabel);
            foreach (string item in new[] { "KoreanButton", "EnglishButton" })
            {
                Button b = StyleNamedButton(settings, item, whitePill, 1f, NavyButtonLabel);
                if (b != null && b.targetGraphic != null)
                    b.targetGraphic.color = new Color(0.16f, 0.24f, 0.38f, 1f); // 초기 비선택색 (코드가 갱신)
            }
            Button reset = StyleNamedButton(settings, "ResetDataButton", whitePill, 1f, NavyButtonLabel);
            if (reset != null && reset.targetGraphic != null) reset.targetGraphic.color = DangerRed;
            TintText(settings, "SettingsTitle", CreamText);
            TintText(settings, "LanguageLabel", CreamText);
        }

        // 3) 확률 패널: 카드 + 닫기(골드)
        RectTransform oddsCard = FindDeep(canvas.transform, "RarityOddsPanel");
        if (oddsCard != null)
        {
            RectTransform card = FindDeep(oddsCard, "Card");
            if (card != null) StylePanel(card, whitePill, CardNavy, 0.7f);
            Button close = StyleNamedButton(oddsCard, "Close", goldFlat, 1.5f, GoldButtonLabel);
            if (close != null)
            {
                TextMeshProUGUI label = close.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.color = GoldButtonLabel;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MainScreenDecorator] 페이지 디자인 통일 완료 — 재화 바 / 설정 / 확률 패널 (상점·연구소는 전용 빌더가 담당)");
    }

    /// <summary>패널·카드류 Image에 라운드 스프라이트와 틴트를 입힙니다. 배치는 건드리지 않습니다.</summary>
    static void StylePanel(RectTransform rect, Sprite sprite, Color tint, float pixelsPerUnitMultiplier)
    {
        Image image = rect.GetComponent<Image>();
        if (image == null) return;
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        image.color = tint;
    }

    static Button StyleNamedButton(RectTransform root, string name, Sprite sprite, float ppu, Color labelColor)
    {
        RectTransform target = FindDeep(root, name);
        if (target == null)
        {
            Debug.LogWarning($"[MainScreenDecorator] '{name}'를 찾지 못해 건너뜁니다.");
            return null;
        }
        Button button = target.GetComponent<Button>();
        if (button != null)
        {
            StyleButton(button, sprite, ppu, labelColor);
            return button;
        }
        // 버튼이 아니면 이미지만 입힌다
        StylePanel(target, sprite, Color.white, ppu);
        TextMeshProUGUI label = target.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.color = labelColor;
        return null;
    }

    static void TintText(RectTransform root, string name, Color color)
    {
        RectTransform target = FindDeep(root, name);
        if (target == null) return;
        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        if (text != null) text.color = color;
    }

    // ---- 타이틀 패널 ----

    static void BuildTitlePanel(Canvas canvas)
    {
        RectTransform panel = EnsureChild(canvas.transform, "TitleScreen");
        panel.SetAsLastSibling(); // 모든 UI 위에
        Stretch(panel);

        Image bg = Ensure<Image>(panel.gameObject);
        bg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RubyArtPath);
        bg.color = Color.white;
        bg.type = Image.Type.Simple;
        bg.preserveAspect = false;
        bg.raycastTarget = true; // 탭을 받아 패널을 닫는다

        AspectRatioFitter fitter = Ensure<AspectRatioFitter>(panel.gameObject);
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1080f / 1920f;

        Ensure<CanvasGroup>(panel.gameObject);

        // 로고
        RectTransform logo = EnsureChild(panel, "Logo");
        logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 1f);
        logo.pivot = new Vector2(0.5f, 1f);
        logo.sizeDelta = new Vector2(600f, 196f);
        logo.anchoredPosition = new Vector2(0f, -159f);
        logo.localScale = Vector3.one;
        Image logoImage = Ensure<Image>(logo.gameObject);
        logoImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        logoImage.color = Color.white;
        logoImage.preserveAspect = true;
        logoImage.raycastTarget = false;

        // "터치하여 시작"
        RectTransform tap = EnsureChild(panel, "TapText");
        tap.anchorMin = tap.anchorMax = new Vector2(0.5f, 0f);
        tap.pivot = new Vector2(0.5f, 0f);
        tap.sizeDelta = new Vector2(600f, 80f);
        tap.anchoredPosition = new Vector2(0f, 150f);
        tap.localScale = Vector3.one;
        TextMeshProUGUI tapText = Ensure<TextMeshProUGUI>(tap.gameObject);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) tapText.font = font;
        tapText.fontSize = 40f;
        tapText.alignment = TextAlignmentOptions.Center;
        tapText.color = TapTextColor;
        tapText.raycastTarget = false;
        tapText.text = GameLanguage.IsEnglish ? "TAP TO START" : "터치하여 시작";

        TitleScreenPanel component = Ensure<TitleScreenPanel>(panel.gameObject);
        SerializedObject so = new SerializedObject(component);
        so.FindProperty("m_logo").objectReferenceValue = logo;
        so.FindProperty("m_tapText").objectReferenceValue = tapText;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- 버튼 ----

    static void StyleButton(Button button, Sprite sprite, float pixelsPerUnitMultiplier, Color labelColor)
    {
        if (button == null || sprite == null) return;

        Image image = button.GetComponent<Image>();
        if (image == null) return;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        image.color = Color.white;
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.82f, 0.82f, 0.86f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        button.colors = colors;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.color = labelColor; // 텍스트 내용은 건드리지 않는다
    }

    // ---- helpers ----

    static Sprite LoadAtlasSprite(string spriteName)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(ButtonAtlasPath).OfType<Sprite>()
            .FirstOrDefault(s => s.name == spriteName);
        if (sprite == null)
            throw new System.InvalidOperationException($"버튼 아틀라스에서 '{spriteName}'를 찾지 못했습니다: {ButtonAtlasPath}");
        return sprite;
    }

    static RectTransform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;

        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Stretch(rect);
        return rect;
    }

    static void RemoveChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    static RectTransform FindDeep(Transform root, string name)
    {
        foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            if (rect.name == name) return rect;
        return null;
    }

    static void EnsureSpriteImport(string path, SpriteImportMode mode)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.InvalidOperationException($"텍스처를 찾지 못했습니다: {path}");

        bool ok = importer.textureType == TextureImporterType.Sprite
            && importer.spriteImportMode == mode
            && !importer.mipmapEnabled
            && importer.alphaIsTransparency;
        if (ok) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = mode;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
    }
}
#endif
