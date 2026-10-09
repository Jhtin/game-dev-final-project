using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>A Toggle whose label takes the hover colour with its background: a dropdown's item.</summary>
    public class PixelToggle : Toggle
    {
        public TMP_Text label;
        public Color textNormal = Color.white, textHover = Color.white;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            if (label != null)
                label.color = state == SelectionState.Normal || state == SelectionState.Disabled ? textNormal : textHover;
        }
    }
}
