using UnityEngine;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// A progress bar: the fill covers <see cref="value"/> of the bar past its own end caps, rounded to
    /// whole pixels, as Godot's ProgressBar draws it.
    /// </summary>
    [ExecuteAlways]
    public class PixelBar : MonoBehaviour
    {
        [Range(0, 1)] public float value = 0.5f;
        public RectTransform fill;
        [Tooltip("The fill's end caps, drawn whole at any value above 0")] public float caps;

        void Update()
        {
            if (fill == null)
                return;
            var v = Mathf.Clamp01(value);
            var width = Mathf.Round(v * (((RectTransform)transform).rect.width - caps));
            var on = width > 0;
            if (fill.gameObject.activeSelf != on)
                fill.gameObject.SetActive(on);
            var max = new Vector2(width + caps, 0);
            if (fill.anchorMax != Vector2.up || fill.offsetMax != max)
            {
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.up;
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = max;
            }
        }
    }
}
