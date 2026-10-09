using TMPro;
using UnityEngine;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// A text with the theme's drop shadow: a copy of the text, drawn first and so under it, a pixel
    /// down and right (TMP's bitmap shader has no underlay). Set <see cref="text"/> here or on the Text
    /// child; the shadow follows.
    /// </summary>
    [ExecuteAlways]
    public class PixelLabel : MonoBehaviour
    {
        [SerializeField] TMP_Text main, shadow;

        public TMP_Text Text => main;

        public string text
        {
            get => main.text;
            set
            {
                main.text = value;
                LateUpdate();
            }
        }

        public Color color
        {
            get => main.color;
            set => main.color = value;
        }

        /// <summary>The shadow's colour; clear for none.</summary>
        public Color shadowColor
        {
            get => shadow.color;
            set => shadow.color = value;
        }

        void LateUpdate()
        {
            if (main == null || shadow == null)
                return;
            if (shadow.text != main.text)
                shadow.text = main.text;
            if (shadow.alignment != main.alignment)
                shadow.alignment = main.alignment;
            if (shadow.fontSize != main.fontSize)
                shadow.fontSize = main.fontSize;
            if (shadow.enabled != main.enabled)
                shadow.enabled = main.enabled;
        }
    }
}
