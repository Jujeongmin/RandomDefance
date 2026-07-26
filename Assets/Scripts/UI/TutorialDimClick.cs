using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>딤 이미지의 탭을 오버레이로 전달합니다. Button을 쓰면 색 전환이 끼어들어 별도 컴포넌트로 둡니다.</summary>
public class TutorialDimClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] TutorialOverlay m_overlay;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (m_overlay != null) m_overlay.OnDimClicked();
    }
}
