using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// Tabs over pages: the n-th PixelButton under <see cref="bar"/> shows the n-th child of
    /// <see cref="pages"/>. Duplicate a tab and a page to add one.
    /// </summary>
    public class PixelTabs : MonoBehaviour
    {
        public RectTransform bar, pages;
        public Sprite selected, unselected, hovered;
        public Color textSelected = Color.white, textUnselected = Color.gray, textHovered = Color.white;
        public RectOffset selectedPadding = new RectOffset(), unselectedPadding = new RectOffset();
        public int current;

        PixelButton[] tabs;

        PixelButton[] Tabs => tabs ??= bar.GetComponentsInChildren<PixelButton>(true);

        void Start()
        {
            for (int i = 0; i < Tabs.Length; i++)
            {
                int n = i;
                Tabs[i].onClick.AddListener(() => Select(n));
            }
            Select(current);
        }

        public void Select(int index)
        {
            current = index;
            for (int i = 0; i < Tabs.Length; i++)
            {
                bool on = i == index;
                var tab = Tabs[i];
                ((Image)tab.targetGraphic).sprite = on ? selected : unselected;
                tab.spriteState = new SpriteState { highlightedSprite = on ? selected : hovered, pressedSprite = on ? selected : hovered, selectedSprite = on ? selected : unselected };
                tab.textNormal = on ? textSelected : textUnselected;
                tab.textHover = tab.textPressed = on ? textSelected : textHovered;
                tab.padding = tab.pressedPadding = on ? selectedPadding : unselectedPadding;
                tab.Refresh();
                if (i < pages.childCount)
                    pages.GetChild(i).gameObject.SetActive(on);
            }
        }
    }
}
