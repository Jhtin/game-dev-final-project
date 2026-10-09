using UnityEngine;
using UnityEngine.EventSystems;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>Shows its text in its canvas's <see cref="PixelTooltip"/> while the pointer is over it.</summary>
    public class TooltipArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [TextArea] public string text;

        PixelTooltip tip;

        PixelTooltip Tip => tip != null ? tip : tip = GetComponentInParent<Canvas>(true)?.rootCanvas.GetComponentInChildren<PixelTooltip>(true);

        public void OnPointerEnter(PointerEventData e) => Tip?.Show(text, e.position);

        public void OnPointerMove(PointerEventData e) => Tip?.Move(e.position);

        public void OnPointerExit(PointerEventData e) => Tip?.Hide();

        void OnDisable()
        {
            if (tip != null)
                tip.Hide();
        }
    }
}
