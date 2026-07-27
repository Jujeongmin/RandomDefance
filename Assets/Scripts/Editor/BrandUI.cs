#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬을 구성하는 에디터 툴들이 공유하는 디자인 토큰과 UI 생성 헬퍼.
/// 브랜드 색·스프라이트·폰트의 단일 출처입니다. 새 에디터 툴은 이 클래스를 쓰세요.
/// </summary>
public static class BrandUI
{
    public const int UiLayer = 5;

    public const string ButtonAtlasPath = "Assets/Down/Universal Stylized UI/Atlases/Complete_Stylized_UI_elements_buttons.png";
    public const string IconAtlasPath = "Assets/Down/Universal Stylized UI/Atlases/Complete_Stylized_UI_elements_icons_2.png";
    public const string CompetitiveAtlasPath = "Assets/Down/Universal Stylized UI/Atlases/Complete_Stylized_UI_elements_competetive.png";
    /// <summary>Kenney 보드게임 아이콘(CC0). 흰 단색이라 Image 틴트로 아무 색이나 입힐 수 있습니다.</summary>
    public const string KenneyIconDir = "Assets/Down/kenney_board-game-icons/PNG/Double (128px)";
    public const string FontPath = "Assets/GData/Fonts/Paperlogy-9Black SDF.asset";
    public const string CharacterDir = "Assets/GData/Image/Character";
    /// <summary>재화 바가 쓰는 크리스탈 이미지. 보상이 크리스탈이면 같은 그림을 써서 무엇을 주는지 바로 읽히게 합니다.</summary>
    public const string CrystalIconPath = "Assets/GData/Image/Cristal.png";

    // 아틀라스 스프라이트 이름
    public const string GoldSquare = "Complete_Stylized_UI_elements_buttons_36";
    public const string NavyPill = "Complete_Stylized_UI_elements_buttons_37";
    public const string WhitePill = "Complete_Stylized_UI_elements_buttons_55"; // 순백 라운드 사각 — 틴트로 원하는 색을 낸다
    public const string GoldFlat = "Complete_Stylized_UI_elements_buttons_53";  // 평평한 골드 — 작은 버튼용

    // 베벨과 그림자가 들어간 큰 라운드 사각. 흰 알약을 틴트만 바꿔 쓰는 것과 달리 재질이 살아 있다.
    public const string NavySquare = "Complete_Stylized_UI_elements_buttons_50";

    // 아이콘 아틀라스는 9열 × 5행이고 인덱스는 행*9 + 열이다.
    // 달력·책·두루마리·트로피는 이 아틀라스에 없어 뜻이 가장 가까운 것을 골랐다.
    public const int IconCheck = 38;    // 초록 체크 — 출석
    public const int IconList = 11;     // 3줄 목록 — 퀘스트
    public const int IconCards = 23;    // 카드 더미 — 도감
    public const int IconDiamond = 40;  // 다이아 — 확률
    public const int IconCrown = 33;    // 금관 — 랭킹

    // 경쟁 아틀라스 — 리본 배너와 원형 아이콘 홀더
    public const string CompCircleFilled = "Complete_Stylized_UI_elements_competetive_3";
    public const string CompRibbonGreen = "Complete_Stylized_UI_elements_competetive_4";
    public const string CompRibbonGold = "Complete_Stylized_UI_elements_competetive_6";

    // 상점 페이지 아이콘
    public const int IconPlay = 14;   // 재생 삼각형 — 보상 광고 시청
    public const int IconNoAds = 19;  // 빨간 슬래시 — 광고 제거

    // 브랜드 팔레트
    public static readonly Color GoldButtonLabel = new Color(0.11f, 0.08f, 0.30f, 1f);
    public static readonly Color NavyButtonLabel = new Color(0.96f, 0.90f, 0.78f, 1f);
    public static readonly Color CreamText = new Color(0.96f, 0.90f, 0.78f, 1f);
    public static readonly Color PanelNavy = new Color(0.045f, 0.04f, 0.115f, 0.96f);  // 페이지 패널 배경
    public static readonly Color CardNavy = new Color(0.06f, 0.055f, 0.14f, 1f);       // 설정/확률 카드
    public static readonly Color SlotNavy = new Color(0.10f, 0.09f, 0.20f, 1f);        // 카드 안의 작은 칸
    public static readonly Color BarNavy = new Color(0.05f, 0.05f, 0.12f, 0.92f);      // 재화 바
    public static readonly Color ModalDim = new Color(0f, 0f, 0f, 0.72f);              // 모달 뒤 딤
    public static readonly Color DangerRed = new Color(0.55f, 0.16f, 0.18f, 1f);       // 데이터 초기화
    public static readonly Color BadgeRed = new Color(0.90f, 0.22f, 0.24f, 1f);

    // ---- 에셋 로드 ----

    public static Sprite LoadAtlasSprite(string spriteName)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(ButtonAtlasPath).OfType<Sprite>()
            .FirstOrDefault(s => s.name == spriteName);
        if (sprite == null)
            throw new System.InvalidOperationException($"버튼 아틀라스에서 '{spriteName}'를 찾지 못했습니다: {ButtonAtlasPath}");
        return sprite;
    }

    /// <summary>아이콘 아틀라스에서 인덱스로 스프라이트를 꺼냅니다.</summary>
    public static Sprite LoadIconSprite(int index)
    {
        string spriteName = $"Complete_Stylized_UI_elements_icons_2_{index}";
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(IconAtlasPath).OfType<Sprite>()
            .FirstOrDefault(s => s.name == spriteName);
        if (sprite == null)
            throw new System.InvalidOperationException($"아이콘 아틀라스에서 '{spriteName}'를 찾지 못했습니다: {IconAtlasPath}");
        return sprite;
    }

    /// <summary>경쟁 아틀라스(리본·원형 홀더·트로피)에서 이름으로 스프라이트를 꺼냅니다.</summary>
    public static Sprite LoadCompetitiveSprite(string spriteName)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(CompetitiveAtlasPath).OfType<Sprite>()
            .FirstOrDefault(s => s.name == spriteName);
        if (sprite == null)
            throw new System.InvalidOperationException($"경쟁 아틀라스에서 '{spriteName}'를 찾지 못했습니다: {CompetitiveAtlasPath}");
        return sprite;
    }

    /// <summary>
    /// Kenney 아이콘을 파일 이름으로 불러옵니다. 갓 임포트된 폴더는 텍스처 타입이
    /// Sprite가 아닐 수 있어, 그 경우 임포트 설정을 고쳐서 다시 읽습니다.
    /// </summary>
    public static Sprite LoadKenneyIcon(string fileName)
    {
        string path = $"{KenneyIconDir}/{fileName}";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new System.InvalidOperationException(
                    $"Kenney 아이콘을 찾지 못했습니다: {path}. kenney_board-game-icons 팩이 Assets/Down에 있어야 합니다.");
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        if (sprite == null)
            throw new System.InvalidOperationException($"Kenney 아이콘을 Sprite로 임포트하지 못했습니다: {path}");
        return sprite;
    }

    public static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

    /// <summary>단일 스프라이트 에셋을 경로로 불러옵니다. 아틀라스가 아닌 낱장 이미지용입니다.</summary>
    public static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new System.InvalidOperationException($"스프라이트를 찾지 못했습니다: {path}");
        return sprite;
    }

    // ---- 계층 헬퍼 ----

    public static T Ensure<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    /// <summary>이름이 같은 자식이 있으면 재사용하고, 없으면 만듭니다. 툴을 여러 번 돌려도 안전합니다.</summary>
    public static RectTransform EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return (RectTransform)existing;

        GameObject go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Stretch(rect);
        return rect;
    }

    public static void RemoveChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    /// <summary>앵커를 0~1 비율로 잡고 네 변에 여백(px)을 둡니다.</summary>
    public static void Anchor(RectTransform rect, Vector2 min, Vector2 max, float inset = 0f)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }

    public static RectTransform FindDeep(Transform root, string name)
    {
        foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
            if (rect.name == name) return rect;
        return null;
    }

    // ---- 스타일 ----

    /// <summary>패널·카드류 Image에 라운드 스프라이트와 틴트를 입힙니다.</summary>
    public static Image StylePanel(RectTransform rect, Sprite sprite, Color tint, float pixelsPerUnitMultiplier)
    {
        Image image = Ensure<Image>(rect.gameObject);
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
        image.color = tint;
        return image;
    }

    public static void StyleButton(Button button, Sprite sprite, float pixelsPerUnitMultiplier, Color labelColor)
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
        if (label != null) label.color = labelColor;
    }

    // ---- 생성 ----

    public static TextMeshProUGUI MakeText(Transform parent, string name, float fontSize, Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = EnsureChild(parent, name);
        TextMeshProUGUI text = Ensure<TextMeshProUGUI>(rect.gameObject);
        TMP_FontAsset font = LoadFont();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>스프라이트 배경 + 가운데 라벨을 가진 버튼을 만듭니다.</summary>
    public static Button MakeButton(Transform parent, string name, Sprite sprite, float ppu,
        Color labelColor, float fontSize)
    {
        RectTransform rect = EnsureChild(parent, name);
        Image image = Ensure<Image>(rect.gameObject);
        image.raycastTarget = true;
        Button button = Ensure<Button>(rect.gameObject);

        TextMeshProUGUI label = MakeText(rect, "Text", fontSize, labelColor);
        Stretch((RectTransform)label.transform);

        StyleButton(button, sprite, ppu, labelColor);
        return button;
    }

    public static Image MakeImage(Transform parent, string name, Color color, bool raycast = false)
    {
        RectTransform rect = EnsureChild(parent, name);
        Image image = Ensure<Image>(rect.gameObject);
        image.color = color;
        image.raycastTarget = raycast;
        image.sprite = null;
        image.type = Image.Type.Simple;
        return image;
    }

    /// <summary>세로로 쌓는 컨테이너. 자식 높이는 LayoutElement.preferredHeight로 지정합니다.</summary>
    public static VerticalLayoutGroup MakeColumn(RectTransform rect, RectOffset padding, float spacing)
    {
        VerticalLayoutGroup group = Ensure<VerticalLayoutGroup>(rect.gameObject);
        group.padding = padding;
        group.spacing = spacing;
        group.childAlignment = TextAnchor.UpperCenter;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        return group;
    }

    public static HorizontalLayoutGroup MakeRow(RectTransform rect, RectOffset padding, float spacing)
    {
        HorizontalLayoutGroup group = Ensure<HorizontalLayoutGroup>(rect.gameObject);
        group.padding = padding;
        group.spacing = spacing;
        group.childAlignment = TextAnchor.MiddleCenter;
        group.childControlWidth = true;
        group.childControlHeight = true;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = true;
        return group;
    }

    public static LayoutElement SetHeight(Component target, float preferredHeight)
    {
        LayoutElement element = Ensure<LayoutElement>(target.gameObject);
        element.preferredHeight = preferredHeight;
        element.flexibleHeight = 0f;
        return element;
    }

    /// <summary>CanvasScaler의 기준 해상도. 없으면 프로젝트 기본값(720×1280).</summary>
    public static Vector2 ReferenceResolution(Canvas canvas)
    {
        CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
        return scaler != null && scaler.referenceResolution.x > 0f ? scaler.referenceResolution : new Vector2(720f, 1280f);
    }

    /// <summary>SerializedObject로 private [SerializeField]에 값을 넣습니다.</summary>
    public static void SetRef(SerializedObject so, string propertyPath, Object value)
    {
        SerializedProperty property = so.FindProperty(propertyPath);
        if (property == null)
            throw new System.InvalidOperationException($"직렬화 프로퍼티를 찾지 못했습니다: {propertyPath}");
        property.objectReferenceValue = value;
    }

    public static void SetRefArray(SerializedObject so, string propertyPath, params Object[] values)
    {
        SerializedProperty property = so.FindProperty(propertyPath);
        if (property == null)
            throw new System.InvalidOperationException($"직렬화 프로퍼티를 찾지 못했습니다: {propertyPath}");
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
#endif
