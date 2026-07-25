using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 최초 1회 코치마크 튜토리얼. 화면을 어둡게 덮되 안내 대상만 구멍을 내어,
/// 그 순간 눌러야 할 버튼만 만질 수 있게 합니다.
///
/// 구멍은 셰이더 없이 대상 사각형의 상·하·좌·우 네 장의 Image로 만듭니다
/// (프로젝트가 Built-in 렌더 파이프라인이라 마스킹 셰이더를 새로 넣지 않습니다).
/// UI는 씬에 미리 배치돼 있고, 직렬화 참조로 연결됩니다.
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    /// <summary>한 단계의 안내. 대상이 null이면 구멍 없이 화면 전체를 덮고 가운데에 말풍선만 띄웁니다.</summary>
    struct Step
    {
        public RectTransform Target;
        public string Korean;
        public string English;
        /// <summary>탭으로 넘길 수 있는지. false면 실제 행동(소환)을 해야 넘어갑니다.</summary>
        public bool AdvanceOnTap;
    }

    [Header("References (씬에서 할당)")]
    [SerializeField] Canvas m_canvas;
    [Tooltip("구멍을 만드는 딤 4장 — 순서: 위 / 아래 / 좌 / 우")]
    [SerializeField] RectTransform[] m_dims = new RectTransform[4];
    [SerializeField] RectTransform m_bubble;
    [SerializeField] TextMeshProUGUI m_bubbleText;
    [SerializeField] TextMeshProUGUI m_tapHintText;
    [SerializeField] Button m_skipButton;
    [SerializeField] TextMeshProUGUI m_skipButtonText;

    [Header("Step Targets (씬에서 할당)")]
    [Tooltip("1단계: 소환 버튼")]
    [SerializeField] RectTransform m_spawnButton;
    [Tooltip("3단계: 직업 강화 버튼 묶음")]
    [SerializeField] RectTransform m_jobButtons;
    [Tooltip("4단계: 판매 버튼")]
    [SerializeField] RectTransform m_sellButton;

    [Header("Layout")]
    [SerializeField] float m_holePadding = 16f;
    [SerializeField] float m_bubbleGap = 28f;

    Step[] m_steps;
    int m_stepIndex = -1;
    bool m_running;

    public bool IsRunning => m_running;

    void Awake()
    {
        if (m_canvas == null) m_canvas = GetComponentInParent<Canvas>();
        if (m_skipButton != null) m_skipButton.onClick.AddListener(Finish);
        gameObject.SetActive(false);
    }

    /// <summary>아직 튜토리얼을 보지 않았다면 시작합니다. 이미 봤으면 아무 일도 하지 않습니다.</summary>
    public void BeginIfNeeded()
    {
        PlayerProgressManager progress = GManager.Instance != null ? GManager.Instance.IsProgress : null;
        if (progress == null || progress.TutorialDone) return;

        BuildSteps();
        m_running = true;
        m_stepIndex = -1;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ApplyLanguage();
        Advance();
    }

    void BuildSteps()
    {
        m_steps = new[]
        {
            new Step
            {
                Target = m_spawnButton,
                Korean = "소환 버튼을 눌러 유닛을 뽑아보세요.\n직업과 등급은 무작위로 정해집니다.",
                English = "Tap Summon to draw a unit.\nIts class and grade are random.",
                AdvanceOnTap = false // 실제로 소환해야 넘어갑니다
            },
            new Step
            {
                Target = null,
                Korean = "뽑은 유닛은 4개 지역에 직업별로 배치됩니다.\n지역을 드래그하면 서로 위치를 바꿀 수 있어요.",
                English = "Units are placed into four regions by class.\nDrag a region to swap it with another.",
                AdvanceOnTap = true
            },
            new Step
            {
                Target = m_jobButtons,
                Korean = "직업 위의 숫자는 이번 웨이브 종족에게 주는 피해량입니다.\n100%인 직업을 집중해서 강화하세요.",
                English = "The number on each class is its damage against this wave's species.\nUpgrade the class showing 100%.",
                AdvanceOnTap = true
            },
            new Step
            {
                Target = m_sellButton,
                Korean = "낮은 등급은 팔아 골드로 바꾸세요.\n자동 판매를 켜면 알아서 정리됩니다.",
                English = "Sell low grades to turn them back into gold.\nTurn on auto-sell to clear them automatically.",
                AdvanceOnTap = true
            }
        };
    }

    void ApplyLanguage()
    {
        if (m_skipButtonText != null) m_skipButtonText.text = GameLanguage.Choose("건너뛰기", "SKIP");
    }

    /// <summary>딤을 탭했을 때 (TutorialDimClick이 호출).</summary>
    public void OnDimClicked()
    {
        if (!m_running || m_steps == null) return;
        if (m_stepIndex >= 0 && m_stepIndex < m_steps.Length && !m_steps[m_stepIndex].AdvanceOnTap) return;
        Advance();
    }

    /// <summary>유닛을 실제로 소환했을 때 (GManager가 호출). 1단계를 넘깁니다.</summary>
    public void NotifySummonPerformed()
    {
        if (!m_running || m_steps == null) return;
        if (m_stepIndex != 0) return;
        Advance();
    }

    void Advance()
    {
        m_stepIndex++;
        if (m_steps == null || m_stepIndex >= m_steps.Length)
        {
            Finish();
            return;
        }

        Step step = m_steps[m_stepIndex];
        if (m_bubbleText != null) m_bubbleText.text = GameLanguage.Choose(step.Korean, step.English);
        if (m_tapHintText != null)
        {
            m_tapHintText.text = step.AdvanceOnTap
                ? GameLanguage.Choose("화면을 탭하여 계속", "Tap anywhere to continue")
                : GameLanguage.Choose("소환하면 다음으로 넘어갑니다", "Summon to continue");
        }

        Layout();
    }

    /// <summary>튜토리얼 도중 판이 끝났을 때. 결과 패널을 가리지 않도록 닫되, 완료로 기록하지는 않습니다.</summary>
    public void Abort()
    {
        if (!m_running) return;
        m_running = false;
        gameObject.SetActive(false);
    }

    void Finish()
    {
        m_running = false;
        if (GManager.Instance != null && GManager.Instance.IsProgress != null)
            GManager.Instance.IsProgress.MarkTutorialDone();
        gameObject.SetActive(false);
    }

    // 레이아웃 그룹이나 세이프에어리어가 한 프레임 뒤에 자리를 잡을 수 있어 매 프레임 다시 계산합니다.
    void LateUpdate()
    {
        if (m_running) Layout();
    }

    void Layout()
    {
        if (m_steps == null || m_stepIndex < 0 || m_stepIndex >= m_steps.Length) return;

        RectTransform self = (RectTransform)transform;
        Vector2 size = self.rect.size;
        RectTransform target = m_steps[m_stepIndex].Target;

        // 구멍을 좌하단 원점 좌표로 구합니다. 대상이 없으면 넓이 0짜리 구멍(= 화면 전체 딤).
        Rect hole = new Rect(0f, size.y, 0f, 0f);
        if (target != null && TryGetLocalRect(target, self, out Rect local))
        {
            hole = new Rect(
                local.xMin + size.x * 0.5f - m_holePadding,
                local.yMin + size.y * 0.5f - m_holePadding,
                local.width + m_holePadding * 2f,
                local.height + m_holePadding * 2f);
        }

        float holeRight = hole.x + hole.width;
        float holeTop = hole.y + hole.height;

        SetDim(0, 0f, holeTop, size.x, size.y - holeTop);        // 위
        SetDim(1, 0f, 0f, size.x, hole.y);                       // 아래
        SetDim(2, 0f, hole.y, hole.x, hole.height);              // 좌
        SetDim(3, holeRight, hole.y, size.x - holeRight, hole.height); // 우

        PlaceBubble(hole, size, target != null);
    }

    void SetDim(int index, float x, float y, float width, float height)
    {
        if (m_dims == null || index >= m_dims.Length || m_dims[index] == null) return;
        RectTransform dim = m_dims[index];
        dim.anchorMin = dim.anchorMax = dim.pivot = Vector2.zero;
        dim.anchoredPosition = new Vector2(x, y);
        dim.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    /// <summary>말풍선을 구멍과 겹치지 않는 쪽(넓은 쪽)에 놓습니다. 구멍이 없으면 화면 가운데.</summary>
    void PlaceBubble(Rect hole, Vector2 size, bool hasHole)
    {
        if (m_bubble == null) return;

        m_bubble.anchorMin = m_bubble.anchorMax = m_bubble.pivot = Vector2.zero;
        float bubbleHeight = m_bubble.rect.height;
        float bubbleWidth = m_bubble.rect.width;

        float y;
        if (!hasHole)
        {
            y = (size.y - bubbleHeight) * 0.5f;
        }
        else
        {
            float spaceAbove = size.y - (hole.y + hole.height);
            float spaceBelow = hole.y;
            y = spaceAbove >= spaceBelow
                ? hole.y + hole.height + m_bubbleGap
                : hole.y - bubbleHeight - m_bubbleGap;
        }
        y = Mathf.Clamp(y, m_bubbleGap, Mathf.Max(m_bubbleGap, size.y - bubbleHeight - m_bubbleGap));

        m_bubble.anchoredPosition = new Vector2((size.x - bubbleWidth) * 0.5f, y);
    }

    /// <summary>대상의 사각형을 이 오버레이의 로컬 좌표(중심 원점)로 변환합니다.</summary>
    bool TryGetLocalRect(RectTransform target, RectTransform self, out Rect result)
    {
        result = default;
        if (!target.gameObject.activeInHierarchy) return false;

        Camera camera = m_canvas != null && m_canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? m_canvas.worldCamera
            : null;

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(self, screen, camera, out Vector2 local))
                return false;
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        result = new Rect(min, max - min);
        return result.width > 0f && result.height > 0f;
    }
}

/// <summary>딤 이미지의 탭을 오버레이로 전달합니다. Button을 쓰면 색 전환이 끼어들어 별도 컴포넌트로 둡니다.</summary>
public class TutorialDimClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TutorialOverlay m_overlay;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (m_overlay != null) m_overlay.OnDimClicked();
    }
}
