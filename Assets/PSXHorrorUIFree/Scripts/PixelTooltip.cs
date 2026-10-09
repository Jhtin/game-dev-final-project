using UnityEngine;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// The tooltip every <see cref="TooltipArea"/> under its canvas shows: one per canvas, moved last
    /// when shown so it draws over the rest.
    /// </summary>
    public class PixelTooltip : MonoBehaviour
    {
        public PixelLabel label;
        public Vector2 offset = new Vector2(8, -8);

        public void Show(string text, Vector2 screen)
        {
            label.text = text;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Move(screen);
        }

        public void Move(Vector2 screen)
        {
            var canvas = GetComponentInParent<Canvas>(true).rootCanvas;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rect.parent, screen,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            rect.localPosition = new Vector2(Mathf.Round(local.x + offset.x), Mathf.Round(local.y + offset.y));
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
