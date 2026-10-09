using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// The kit in use: a game screen built in code from one theme's prefabs (its layout is in
    /// UIDemo.Layout.cs), on a PixelCanvas at <see cref="artHeight"/>. The buttons along the bottom
    /// build it again in another theme. The helpers here are a start for building your own screens.
    /// </summary>
    public partial class UIDemo : MonoBehaviour
    {
        public PixelTheme[] themes;
        [Tooltip("The theme shown first")] public string first;
        public int artHeight = 240;
        public Sprite[] icons, sprites;
        [Tooltip("The mouse pointer at 1x, 2x, 3x and 4x: the one matching the canvas scale is used")] public Texture2D[] cursors;

        PixelTheme theme;
        Canvas canvas;
        RectTransform screen;
        int cursorScale;

        void Start()
        {
            DemoInput.Ensure();
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            go.AddComponent<PixelCanvas>().artHeight = artHeight;
            if (Camera.main != null)
                Camera.main.backgroundColor = Background;
            var backdrop = Backdrop();
            if (backdrop != null)
            {
                backdrop.filterMode = FilterMode.Point;
                var image = Rect("Backdrop", canvas.transform).gameObject.AddComponent<RawImage>();
                image.texture = backdrop;
                image.raycastTarget = false;
                Place(image.rectTransform, 0, 0, fit: false).sizeDelta = new Vector2(backdrop.width, backdrop.height);
            }
            Show(themes.FirstOrDefault(t => string.Equals(t.name, first, StringComparison.OrdinalIgnoreCase)) ?? themes[0]);
        }

        void Update()
        {
            var scale = PixelCanvas.Scale(artHeight);
            if (scale == cursorScale || cursors == null || cursors.Length == 0)
                return;
            cursorScale = scale;
            Cursor.SetCursor(cursors[Mathf.Min(scale, cursors.Length) - 1], Vector2.zero, CursorMode.Auto);
        }

        /// <summary>The screen, built again in the theme.</summary>
        void Show(PixelTheme next)
        {
            theme = next;
            if (screen != null)
                Destroy(screen.gameObject);
            screen = Rect("Screen", canvas.transform);
            Stretch(screen);
            Layout();
            theme.Make("Tooltip", screen);
        }

        /// <summary>A toggle button per theme, the one shown pressed.</summary>
        void Switcher(float x, float y, float gap)
        {
            var row = Place(Row(screen, gap), x, y);
            foreach (var t in themes)
            {
                var b = Button(row, t.name);
                b.on = t == theme;
                b.onClick.AddListener(() => Show(t));
            }
        }

        // --- helpers ---

        RectTransform Make(string name, Transform parent) => (RectTransform)theme.Make(name, parent).transform;

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        /// <summary>At (x, y) art pixels from the screen's top left, sized to its content unless not to fit.</summary>
        static RectTransform Place(RectTransform r, float x, float y, bool fit = true)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            if (fit)
                Fit(r);
            return r;
        }

        static void Fit(RectTransform r)
        {
            var fitter = r.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        static LayoutElement Element(Component c) => c.TryGetComponent(out LayoutElement e) ? e : c.gameObject.AddComponent<LayoutElement>();

        /// <summary>At least this size (Godot's custom_minimum_size); -1 leaves a side as it is.</summary>
        static void MinSize(Component c, float width, float height = -1)
        {
            var e = Element(c);
            if (width >= 0)
                e.minWidth = width;
            if (height >= 0)
                e.minHeight = height;
        }

        /// <summary>Takes the row's spare width, shared evenly with the others that do (SIZE_EXPAND_FILL).</summary>
        static void Expand(Component c)
        {
            var e = Element(c);
            e.flexibleWidth = 1;
            e.preferredWidth = 0;
        }

        /// <summary>Its own height, centred in the row's (SIZE_SHRINK_CENTER), instead of the row's.</summary>
        static RectTransform Centre(RectTransform r)
        {
            var holder = Rect(r.name, r.parent);
            holder.SetSiblingIndex(r.GetSiblingIndex());
            r.SetParent(holder, false);
            var v = holder.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childAlignment = TextAnchor.MiddleLeft;
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = v.childForceExpandHeight = false;
            return holder;
        }

        /// <summary>Out of its parent's layout, sized to its content.</summary>
        static RectTransform Free(RectTransform r)
        {
            Element(r).ignoreLayout = true;
            Fit(r);
            return r;
        }

        /// <summary>Takes no spare room unless told to (Expand), as a Godot container, where a layout group that
        /// expands its children would report itself flexible.</summary>
        static void Fixed(Component c)
        {
            var e = Element(c);
            e.flexibleWidth = e.flexibleHeight = 0;
        }

        /// <summary>Side by side, `gap` apart, each as tall as the row (an HBoxContainer).</summary>
        static RectTransform Row(Transform parent, float gap = 3)
        {
            var r = Rect("Row", parent);
            var h = r.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = gap;
            Fixed(r);
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;
            return r;
        }

        /// <summary>Stacked, `gap` apart, each as wide as the column (a VBoxContainer).</summary>
        static RectTransform Column(Transform parent, float gap)
        {
            var r = Rect("Column", parent);
            var v = r.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = gap;
            Fixed(r);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return r;
        }

        static RectTransform Gap(Transform parent, float width)
        {
            var r = Rect("Gap", parent);
            MinSize(r, width);
            return r;
        }

        /// <summary>A grid of `columns`, `gap` apart, of 24x24 slots.</summary>
        static RectTransform Grid(Transform parent, int columns, float gap)
        {
            var r = Rect("Grid", parent);
            var g = r.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(24, 24);
            g.spacing = new Vector2(gap, gap);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = columns;
            return r;
        }

        /// <summary>A titled window; its content goes under the title.</summary>
        RectTransform Window(string title, float x, float y, float width)
        {
            var w = Place(Make("Window", screen), x, y);
            MinSize(w, width);
            w.Find("Title").GetComponent<PixelLabel>().text = title;
            return w;
        }

        PixelLabel Label(Transform parent, string text, string variation = "Label")
        {
            var l = Make(theme.Has(variation) ? variation : "Label", parent).GetComponent<PixelLabel>();
            l.text = text;
            return l;
        }

        PixelButton Button(Transform parent, string text, string icon = null)
        {
            var b = Make("Button", parent).GetComponent<PixelButton>();
            b.text = text;
            b.SetIcon(icon == null ? null : Sprite(icons, icon));
            return b;
        }

        Toggle Check(Transform parent, string text, bool on, bool radio = false)
        {
            var t = Make(radio ? "Radio" : "Checkbox", parent).GetComponent<Toggle>();
            t.transform.Find("Label").GetComponent<TMP_Text>().text = text;
            t.isOn = on;
            return t;
        }

        TMP_Dropdown Dropdown(Transform parent, int value, params string[] options)
        {
            var d = Make("Dropdown", parent).GetComponent<TMP_Dropdown>();
            d.ClearOptions();
            d.AddOptions(options.ToList());
            d.SetValueWithoutNotify(value);
            return d;
        }

        TMP_InputField Input(Transform parent, string placeholder)
        {
            var input = Make("InputField", parent).GetComponent<TMP_InputField>();
            ((TMP_Text)input.placeholder).text = placeholder;
            return input;
        }

        /// <summary>A labelled slider: the label this wide, the slider taking the rest.</summary>
        Slider Slider(Transform parent, string label, float value, float labelWidth)
        {
            var row = Row(parent);
            MinSize(Label(row, label), labelWidth);
            var s = Make("Slider", row).GetComponent<Slider>();
            s.value = value;
            Expand(s);
            return s;
        }

        /// <summary>A bar ("Red", "Gold" ... or "" for the theme's own) at value 0-100, this wide.</summary>
        PixelBar Bar(Transform parent, string colour, float value, float width, float height)
        {
            var name = theme.Has(colour + "Bar") ? colour + "Bar" : "Bar";
            var b = Make(name, parent).GetComponent<PixelBar>();
            b.value = value / 100;
            var e = Element(b);
            e.minWidth = e.preferredWidth = width;
            e.minHeight = e.preferredHeight = height;
            return b;
        }

        /// <summary>
        /// The icon at its size (times `scale`), centred in the space it's given; tinted with the
        /// theme's icon colour. An icon not in the kit leaves an empty space.
        /// </summary>
        RectTransform Icon(Transform parent, string name, int scale = 1, Sprite sprite = null)
        {
            var holder = Rect(name, parent);
            sprite ??= Sprite(icons, name);
            if (sprite == null)
                return holder;
            var size = sprite.rect.size * scale;
            var e = Element(holder);
            e.minWidth = e.preferredWidth = size.x;
            e.minHeight = e.preferredHeight = size.y;
            var image = Rect("Image", holder).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = theme.iconColor;
            image.raycastTarget = false;
            image.rectTransform.sizeDelta = size;
            return holder;
        }

        static string Title(string name) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Replace('_', ' '));

        bool Has(string icon) => Sprite(icons, icon) != null;

        static Sprite Sprite(Sprite[] from, string name) => from.FirstOrDefault(s => s != null && s.name == name);

        /// <summary>Shows the text in the canvas's Tooltip while the pointer is over the icon.</summary>
        static void Tip(RectTransform icon, string text)
        {
            var image = icon.GetComponentInChildren<Image>();
            if (image == null)
                return;
            image.raycastTarget = true;
            icon.gameObject.AddComponent<TooltipArea>().text = text;
        }

        /// <summary>A text over a slot, out of its layout: `at` art pixels from the slot's top left to the text's top right (or left).</summary>
        PixelLabel Over(RectTransform slot, string text, Vector2 at, bool right, Color? colour, Color shadow)
        {
            var l = Label(slot, text);
            if (colour != null)
                l.color = colour.Value;
            l.shadowColor = shadow;
            var r = Free((RectTransform)l.transform);
            r.anchorMin = r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(right ? 1 : 0, 1);
            r.anchoredPosition = new Vector2(at.x, -at.y);
            return l;
        }

        /// <summary>The Tabs with a tab and an empty page per name; returns the pages.</summary>
        RectTransform[] Tabs(Transform parent, params string[] names)
        {
            var tabs = Make("Tabs", parent);
            var bar = tabs.Find("Bar");
            var pages = tabs.Find("Pages");
            while (bar.childCount < names.Length)
                Instantiate(bar.GetChild(0).gameObject, bar, false);
            while (pages.childCount < names.Length)
                Instantiate(pages.GetChild(0).gameObject, pages, false);
            for (int i = bar.childCount - 1; i >= names.Length; i--)
                DestroyImmediate(bar.GetChild(i).gameObject);
            for (int i = pages.childCount - 1; i >= names.Length; i--)
                DestroyImmediate(pages.GetChild(i).gameObject);
            var result = new RectTransform[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var tab = bar.GetChild(i).GetComponent<PixelButton>();
                tab.name = names[i];
                tab.text = names[i];
                result[i] = (RectTransform)pages.GetChild(i);
                result[i].name = names[i];
                foreach (Transform child in result[i])
                    Destroy(child.gameObject);
            }
            return result;
        }

        /// <summary>A texture to draw on, cleared, and its pixels set as Godot draws: y down.</summary>
        static Texture2D Paper(int width, int height, out Action<int, int, int, int, Color> rect)
        {
            var t = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(new Color32[width * height]);
            rect = (x, y, w, h, c) =>
            {
                for (int yy = Mathf.Max(0, y); yy < Mathf.Min(height, y + h); yy++)
                    for (int xx = Mathf.Max(0, x); xx < Mathf.Min(width, x + w); xx++)
                        t.SetPixel(xx, height - 1 - yy, c);
            };
            return t;
        }

        static void Circle(Texture2D t, float cx, float cy, float radius, Color c)
        {
            for (int y = 0; y < t.height; y++)
                for (int x = 0; x < t.width; x++)
                    if ((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy) <= radius * radius)
                        t.SetPixel(x, t.height - 1 - y, c);
        }
    }
}
