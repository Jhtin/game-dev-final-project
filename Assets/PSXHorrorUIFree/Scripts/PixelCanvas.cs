using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// Scales its canvas by the largest whole number that still fits <see cref="artHeight"/> art pixels
    /// on screen, so every pixel stays square. With the sprites at 100 pixels per unit, an art pixel is
    /// a canvas unit.
    /// </summary>
    [ExecuteAlways, RequireComponent(typeof(CanvasScaler))]
    public class PixelCanvas : MonoBehaviour
    {
        public int artHeight = 240;

        CanvasScaler scaler;

        void OnEnable()
        {
            scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100;
            Update();
        }

        void Update()
        {
            var scale = Scale(artHeight);
            if (scaler.scaleFactor != scale)
                scaler.scaleFactor = scale;
        }

        public static int Scale(int artHeight) => Mathf.Max(1, Screen.height / Mathf.Max(1, artHeight));
    }
}
