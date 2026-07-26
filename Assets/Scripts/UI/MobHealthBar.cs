using UnityEngine;

/// <summary>
/// 몹/보스 아래에 표시되는 월드 공간 체력바. 런타임에 몹의 자식으로 생성됩니다.
/// 체력 비율에 따라 채움 막대 길이와 색(초록→빨강)이 바뀝니다.
/// </summary>
public class MobHealthBar : MonoBehaviour
{
    // 몹 로컬 좌표 기준 크기/위치 (몹 스케일이 곱해져 화면에 표시됨. 보스는 자동으로 더 큼)
    const float Width = 1.5f;
    const float Height = 0.28f;
    const float YOffset = -1.05f;

    // 게임씬 sorting order 대역: 바닥 -10~-9, 유닛·몹·이펙트 0~101,
    // 지역 외곽선과 스왑 화살표 90~111, UICanvas 200.
    // 체력바는 지역 선 위에 떠야 읽히지만 UI 아래여야 한다 — 200은 캔버스와 같은 값이라
    // 소환 텍스트·설정 패널·튜토리얼 오버레이까지 전부 뚫고 올라왔다.
    const int SortOrder = 120;

    static Sprite s_centerSprite;
    static Sprite s_leftSprite;

    ParentsController m_owner;
    Transform m_fill;
    SpriteRenderer m_fillRenderer;

    public static MobHealthBar Attach(ParentsController owner)
    {
        GameObject go = new GameObject("HealthBar");
        go.transform.SetParent(owner.transform, false);
        MobHealthBar bar = go.AddComponent<MobHealthBar>();
        bar.Build(owner);
        return bar;
    }

    void Build(ParentsController owner)
    {
        m_owner = owner;
        EnsureSprites();
        transform.localPosition = new Vector3(0f, YOffset, 0f);
        transform.localRotation = Quaternion.identity;

        SpriteRenderer bg = CreatePart("BG", s_centerSprite, new Color(0f, 0f, 0f, 0.65f), SortOrder);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(Width, Height, 1f);

        m_fillRenderer = CreatePart("Fill", s_leftSprite, Color.green, SortOrder + 1);
        m_fill = m_fillRenderer.transform;
        m_fill.localPosition = new Vector3(-Width * 0.5f, 0f, 0f);
        m_fill.localScale = new Vector3(Width, Height * 0.72f, 1f);
    }

    SpriteRenderer CreatePart(string name, Sprite sprite, Color color, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return renderer;
    }

    void LateUpdate()
    {
        if (m_owner == null || m_fill == null) return;
        float ratio = m_owner.HpRatio;
        Vector3 scale = m_fill.localScale;
        scale.x = Width * ratio;
        m_fill.localScale = scale;
        // 체력에 따라 초록(가득)→노랑→빨강(위험)
        m_fillRenderer.color = ratio > 0.5f
            ? Color.Lerp(Color.yellow, Color.green, (ratio - 0.5f) * 2f)
            : Color.Lerp(Color.red, Color.yellow, ratio * 2f);
    }

    static void EnsureSprites()
    {
        if (s_centerSprite != null) return;
        Texture2D tex = Texture2D.whiteTexture;
        Rect rect = new Rect(0f, 0f, tex.width, tex.height);
        s_centerSprite = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), tex.width);
        s_leftSprite = Sprite.Create(tex, rect, new Vector2(0f, 0.5f), tex.width);
    }
}
