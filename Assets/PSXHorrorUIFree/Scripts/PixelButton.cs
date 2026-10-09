using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// A Button as the kit's theme draws one: Sprite Swap for its frame, and per state the label's and
    /// icon's colours and the content's padding (a pressed button's content sits lower). The focus frame
    /// shows while the button is selected. <see cref="on"/> holds the pressed look: a toggle button.
    /// </summary>
    public class PixelButton : Button
    {
        public TMP_Text label;
        public Image icon;
        public Graphic focus;
        public Color textNormal = Color.white, textHover = Color.white, textPressed = Color.white, textDisabled = Color.gray;
        public Color iconNormal = Color.white, iconHover = Color.white, iconPressed = Color.white, iconDisabled = Color.gray;
        public RectOffset padding = new RectOffset(), pressedPadding = new RectOffset();
        [SerializeField] bool isOn;

        /// <summary>The label's text; an empty one hides the label, for an icon alone.</summary>
        public string text
        {
            get => label.text;
            set
            {
                label.text = value;
                label.gameObject.SetActive(!string.IsNullOrEmpty(value));
            }
        }

        public bool on
        {
            get => isOn;
            set
            {
                isOn = value;
                Refresh();
            }
        }

        /// <summary>Shows the icon, or hides it for null.</summary>
        public void SetIcon(Sprite sprite)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
        }

        /// <summary>Draws the current state again, after its colours or sprites changed.</summary>
        public void Refresh() => DoStateTransition(currentSelectionState, true);

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (isOn && state != SelectionState.Disabled)
                state = SelectionState.Pressed;
            base.DoStateTransition(state, instant);
            if (targetGraphic is Image frame && frame.sprite == null)  // an empty normal style: a frame only where the state has one
                frame.color = frame.overrideSprite != null ? Color.white : Color.clear;
            bool pressed = state == SelectionState.Pressed, hover = state == SelectionState.Highlighted, off = state == SelectionState.Disabled;
            if (label != null)
                label.color = off ? textDisabled : pressed ? textPressed : hover ? textHover : textNormal;
            if (icon != null)
                icon.color = off ? iconDisabled : pressed ? iconPressed : hover ? iconHover : iconNormal;
            if (focus != null)
                focus.enabled = currentSelectionState == SelectionState.Selected;
            if (TryGetComponent(out HorizontalLayoutGroup layout))
            {
                var want = pressed ? pressedPadding : padding;
                var now = layout.padding;
                if (now.left != want.left || now.top != want.top || now.right != want.right || now.bottom != want.bottom)
                    layout.padding = new RectOffset(want.left, want.right, want.top, want.bottom);
            }
        }
    }
}
