using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// The demo's screens, a survival-horror game's: the title, a line of dialogue, the items, examining
    /// one, the map, the files, the typewriter save and the pause menu, on a dark corridor. The menu on
    /// the left changes the screen. A sampler without an item's icon leaves its slot empty.
    /// </summary>
    public partial class UIDemo
    {
        static readonly Color Background = new Color(0.03f, 0.035f, 0.05f);
        static readonly string[] Screens = { "title", "game", "items", "examine", "map", "files", "save", "options" };
        static readonly string[] Conditions = { "fine", "caution", "danger", "poison" };
        static readonly Dictionary<string, int> Health = new() { ["fine"] = 90, ["caution"] = 55, ["danger"] = 20, ["poison"] = 45 };

        /// <summary>name: label, description, what checking it turns up</summary>
        static readonly Dictionary<string, string[]> Items = new()
        {
            ["pistol"] = new[] { "Handgun", "A 9mm service pistol. Fifteen rounds to a magazine.", "The serial number has been filed off." },
            ["pump_shotgun"] = new[] { "Shotgun", "A pump-action shotgun. Slow, loud and very persuasive.", "There's a name scratched into the stock: K. Ward." },
            ["ammo_handgun"] = new[] { "Handgun Ammo", "A box of 9mm rounds for the handgun.", "Twenty-two rounds left in the box." },
            ["herb"] = new[] { "Green Herb", "A potted herb with a bitter smell. Restores a little health.", "Some leaves have been torn off already." },
            ["first_aid_kit"] = new[] { "First Aid Kit", "Bandages, gauze and antiseptic. Restores all health.", "A label inside the lid: WARD C." },
            ["key_skeleton"] = new[] { "Old Key", "An iron key, worn smooth. There is a crest on the bow.", "There is a heart carved into the crest." },
            ["flashlight"] = new[] { "Flashlight", "It flickers when shaken. The batteries are nearly gone.", "Something is taped inside the battery cover." },
            ["keycard_red"] = new[] { "Red Keycard", "A security card for the east lab.", "The photo on it has been scratched out." },
            ["crank_handle"] = new[] { "Crank Handle", "An iron crank with a square socket.", "It would fit the shutter in the kitchen." },
            ["fuse"] = new[] { "Fuse", "A ceramic fuse. Still good.", "Rated for the generator in the basement." },
            ["ink_ribbon"] = new[] { "Ink Ribbon", "A typewriter ribbon. Use one at a typewriter to save.", "Enough ink for one entry." },
            ["typewriter"] = new[] { "Typewriter", "An old office typewriter with a sheet already in.", "The last line typed: DON'T OPEN THE LAB." },
        };

        static readonly (string item, int count)[] Bag =
            { ("pistol", 15), ("pump_shotgun", 6), ("ammo_handgun", 22), ("herb", 2), ("first_aid_kit", 0), ("flashlight", 0), ("", 0), ("", 0) };

        static readonly (string item, int count)[] KeyItems =
            { ("key_skeleton", 0), ("keycard_red", 0), ("crank_handle", 0), ("fuse", 0), ("", 0), ("", 0), ("", 0), ("", 0) };

        static readonly (string name, string text)[] Files =
        {
            ("Diary", "May 9\nThe subject in cell 4 has stopped eating. Dr. Ash says this is expected.\n\nMay 11\nIt scratched at the glass all night. I asked to be moved to the west wing.\n\nMay 12\nItchy. Itchy. The guard left the east door open."),
            ("Staff Memo", "To all night staff:\n\nThe east wing is closed until further notice. Keycards for the lab are held at the security desk.\n\nDo not answer the phone in the gallery."),
            ("Patient Log", "Room 3 - sedated\nRoom 4 - restless, fever\nRoom 5 - empty (?)\nRoom 6 - do not enter\n\nThe herbs in the greenhouse help the fever. Nothing helps room 6."),
            ("Torn Page", "...the crest on the old key matches the door in the dining room. The heart, the spade, the club and the diamond. Four doors, four keys, and behind the last..."),
        };

        static readonly (string who, string said)[] Lines =
        {
            ("", "The door is locked. There is a heart carved above the keyhole."),
            ("Nurse Hale", "You're alive... Stay out of the east wing. Whatever got out of the lab is still in there."),
            ("", "There is a first aid kit on the table. Will you take it?"),
        };

        const int Unit = 7;  // the map's squares, in pixels

        /// <summary>The map's rooms: x, y, w, h in squares, their variation and name.</summary>
        static readonly (int x, int y, int w, int h, string type, string name)[] Rooms =
        {
            (1, 1, 6, 8, "MapRoom", "West Gallery"), (7, 3, 5, 4, "MapRoomItems", "Dining Room"), (12, 1, 6, 8, "MapRoomCurrent", "Main Hall"),
            (7, 7, 3, 3, "MapRoom", "Safe Room"), (18, 4, 10, 2, "MapRoom", "East Corridor"), (28, 1, 7, 6, "MapRoomUnknown", "Library"),
            (20, 6, 4, 5, "MapRoomItems", "Kitchen"), (28, 7, 7, 11, "MapRoomUnknown", "Laboratory"), (12, 9, 6, 4, "MapRoom", "Stairs"),
            (1, 9, 6, 6, "MapRoomUnknown", "Greenhouse"), (24, 11, 4, 3, "MapRoom", "Storage"),
        };

        /// <summary>The map's marks: which, and where in squares.</summary>
        static readonly (string mark, float x, float y)[] MapMarks =
        {
            ("door", 12, 5), ("door", 18, 5), ("door_locked", 28, 5), ("door", 8.5f, 7), ("door", 22, 6), ("door", 15, 9),
            ("door_locked", 4, 9), ("save", 8.5f, 8.7f), ("item", 9.5f, 5), ("item", 22, 8.5f), ("you", 15, 4.5f), ("door", 24, 12.5f),
        };

        string current = "items", selected = "herb";
        int line;
        Dictionary<string, RectTransform> screens;
        Dictionary<string, RectTransform> slots;
        PixelButton[] nav;
        PixelLabel bagName, bagText, examineName, examineText;
        Image examineArt;

        /// <summary>A dark corridor, dithered in 15-bit colour at 320x180 like a PS1 backdrop, stretched over the screen.</summary>
        static Texture2D Backdrop()
        {
            int[,] dither = { { -4, 0, -3, 1 }, { 2, -2, 3, -1 }, { -3, 1, -4, 0 }, { 3, -1, 2, -2 } };
            static byte Q(float v)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(v), 0, 255);
                return (byte)((i >> 3) << 3 | i >> 5);
            }
            const int w = 320, h = 180;
            var t = new Texture2D(w, h, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // walls lit from the far end, the floor and ceiling falling away to black
                    float dx = Mathf.Abs(x - w * 0.5f) / (w * 0.5f), dy = Mathf.Abs(y - h * 0.46f) / (h * 0.5f);
                    float glow = Mathf.Clamp01(1 - Mathf.Max(dx, dy * 1.4f));
                    float v = 10 + 34 * glow * glow;
                    if (Mathf.Abs(dx - dy * 1.6f) < 0.012f || dy > 0.3f && (int)(dy * 40) % 5 == 0 && y > h / 2)
                        v *= 1.4f;
                    int d = dither[y % 4, x % 4];
                    t.SetPixel(x, h - 1 - y, new Color32(Q(v * 0.8f + d), Q(v * 0.95f + d), Q(v + d), 255));
                }
            t.Apply();
            return t;
        }

        void Layout()
        {
            if (canvas.transform.Find("Backdrop") is RectTransform backdrop)
                Stretch(backdrop);
            screens = new Dictionary<string, RectTransform>();
            slots = new Dictionary<string, RectTransform>();
            examineArt = null;
            var menu = Place(Win(screen, "Menu", 72, 0), 4, 4);
            nav = Screens.Select(s => MenuItem(menu, Title(s))).ToArray();
            var area = Place(Rect("Area", screen), 80, 4, fit: false);
            area.sizeDelta = new Vector2(342, 214);
            foreach (var s in Screens)
            {
                var c = Rect(s, area);
                Stretch(c);
                screens[s] = c;
            }
            TitleScreen(screens["title"]);
            GameScreen(screens["game"]);
            ItemsScreen(screens["items"]);
            ExamineScreen(screens["examine"]);
            MapScreen(screens["map"]);
            FilesScreen(screens["files"]);
            SaveScreen(screens["save"]);
            OptionsScreen(screens["options"]);
            Group(nav, Array.IndexOf(Screens, current), i => Go(Screens[i]));
            Switcher(4, 222, 2);
        }

        void Go(string s)
        {
            current = s;
            foreach (var (name, c) in screens)
                c.gameObject.SetActive(name == s);
            for (int i = 0; i < nav.Length; i++)
                nav[i].on = Screens[i] == s;
        }

        void TitleScreen(RectTransform c)
        {
            var box = TopCentre(Column(c, 1), 40);
            Label(box, "THE HOLLOW HOUSE", "Headline").Text.alignment = TextAlignmentOptions.Top;
            Label(box, "a survival horror", "Hint").Text.alignment = TextAlignmentOptions.Top;
            var items = TopCentre(Column(c, 0), 96);
            MinSize(items, 84);
            var buttons = new[] { "New game", "Continue", "Load game", "Options", "Quit" }.Select(t => MenuItem(items, t)).ToArray();
            buttons[1].interactable = false;
            Group(buttons, 0);
            var start = (RectTransform)Label(c, "PRESS START", "Hint").transform;
            start.anchorMin = start.anchorMax = start.pivot = new Vector2(0.5f, 0);
            start.anchoredPosition = new Vector2(0, 12);
            Fit(start);
        }

        void GameScreen(RectTransform c)
        {
            var hud = Place(Row(c, 4), 4, 4);
            Condition(hud, "caution", false);
            Hold(Slot(hud, "pistol", 15, "equipped"), TextAnchor.UpperLeft);
            var box = Place(Make("DialoguePanel", c), 6, 146);
            MinSize(box, 330, 52);
            box.gameObject.AddComponent<TooltipArea>().text = "Click to read on";
            var text = Column(box, 1);
            var speaker = Label(text, "", "Heading");
            var said = Wrapped(text, "", 300);
            var choice = Row(text, 8);
            foreach (var t in new[] { "Yes", "No" })
                MinSize(MenuItem(choice, t), 40);
            var next = Mark(box, "next");  // in the bottom right corner, as a PanelContainer stacks it there
            var pad = box.GetComponent<VerticalLayoutGroup>().padding;
            Element(next).ignoreLayout = true;
            next.anchorMin = next.anchorMax = next.pivot = new Vector2(1, 0);
            next.anchoredPosition = new Vector2(-pad.right, pad.bottom);
            var blink = next.gameObject.AddComponent<Blink>();
            void Say()
            {
                speaker.text = Lines[line].who;
                speaker.gameObject.SetActive(Lines[line].who != "");
                said.text = Lines[line].said;
                choice.gameObject.SetActive(line == Lines.Length - 1);
                blink.on = line != Lines.Length - 1;
            }
            Say();
            box.gameObject.AddComponent<Click>().action = () =>
            {
                line = (line + 1) % Lines.Length;
                Say();
            };
        }

        void ItemsScreen(RectTransform c)
        {
            var left = Place(Column(c, 4), 0, 0);
            Condition(Win(left, "Condition", 96), "fine", true);
            var r = Row(Win(left, "Equipped", 96), 4);
            Slot(r, "pistol", 15, "equipped");
            var info = Column(r, 0);
            Label(info, "Handgun");
            Label(info, "15/15", "Hint");

            var bag = Place(Win(c, "Items", 170), 100, 0);
            var pages = Tabs(bag, "Items", "Key items");
            foreach (var (page, items) in new[] { (pages[0], Bag), (pages[1], KeyItems) })
            {
                var grid = Grid(page, 4, 2);
                grid.GetComponent<GridLayoutGroup>().cellSize = new Vector2(38, 38);
                foreach (var (item, count) in items)
                {
                    var s = Slot(grid, item, count);
                    if (item == "" || Small(item) == null)
                        continue;
                    slots[item] = s;
                    s.gameObject.AddComponent<Click>().action = () => Choose(item);
                }
            }
            Make("Separator", bag);
            bagName = Label(bag, "", "Heading");
            bagText = Wrapped(bag, "", 158);
            var actions = Place(Win(c, "Command", 68, 0), 274, 0);
            foreach (var t in new[] { "Use", "Equip", "Examine", "Combine", "Discard" })
            {
                var b = MenuItem(actions, t);
                if (t == "Examine")
                    b.onClick.AddListener(() => Go("examine"));
            }
            Choose(selected);
        }

        void Choose(string item)
        {
            selected = item;
            Sprite look(string type) => theme.Prefab(type).GetComponent<Image>().sprite;
            foreach (var (name, s) in slots)
                s.GetComponent<Image>().sprite = look(name == item ? "ItemSlotSelected" : "ItemSlot");
            bagName.text = Items[item][0];
            bagText.text = Items[item][1];
            ShowExamined();
        }

        void ExamineScreen(RectTransform c)
        {
            var view = Place(Make("ExaminePanel", c), 91, 0);
            MinSize(view, 160, 124);
            view.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = true;
            var art = Rect("Art", view);
            examineArt = Rect("Image", art).gameObject.AddComponent<Image>();
            examineArt.raycastTarget = false;
            var box = Place(Make("DialoguePanel", c), 16, 130);
            MinSize(box, 310);
            var text = Column(box, 1);
            examineName = Label(text, "", "Heading");
            examineText = Wrapped(text, "", 288);
            var buttons = Place(Row(c, 4), 111, 186);
            foreach (var t in new[] { "Check", "Back" })
            {
                var b = Button(buttons, t);
                MinSize(b, 58);
                b.onClick.AddListener(() =>
                {
                    if (t == "Back")
                        Go("items");
                    else
                        examineText.text = Items[selected][2];
                });
            }
            ShowExamined();
        }

        void ShowExamined()
        {
            if (examineArt == null)
                return;  // not built yet in this theme
            // an item the sampler leaves out has no picture: the key it does have stands in
            examineArt.sprite = Large(selected) ?? Large("key_skeleton");
            examineArt.enabled = examineArt.sprite != null;
            if (examineArt.sprite != null)
                examineArt.rectTransform.sizeDelta = examineArt.sprite.rect.size;
            examineName.text = Items[selected][0];
            examineText.text = Items[selected][1];
        }

        void MapScreen(RectTransform c)
        {
            var head = Place(Row(c, 6), 0, 0);
            Label(head, "1F", "Headline");
            Centre((RectTransform)Label(head, "East Wing", "Heading").transform);
            var monitor = Place(Make("MonitorPanel", c), 0, 26);
            var plan = Rect("Plan", monitor);
            MinSize(plan, 36 * Unit, 19 * Unit);
            foreach (var (x, y, w, h, type, name) in Rooms)
            {
                var room = Make(type, plan);
                Pin(room, x * Unit, y * Unit).sizeDelta = new Vector2(w * Unit, h * Unit);
                room.gameObject.AddComponent<TooltipArea>().text = name;
            }
            foreach (var (mark, x, y) in MapMarks)
            {
                var m = Mark(plan, mark);
                Pin(m, Mathf.Floor(x * Unit - m.sizeDelta.x / 2), Mathf.Floor(y * Unit - m.sizeDelta.y / 2));
            }
            var legend = Place(Win(c, "Legend", 80, 1), 262, 26);
            foreach (var (mark, text) in new[] { ("you", "You"), ("save", "Safe room"), ("item", "Item"), ("door", "Door"), ("door_locked", "Locked") })
            {
                var r = Row(legend, 4);
                MinSize(Centre(Mark(r, mark)), 10);
                Label(r, text);
            }
            foreach (var (type, text) in new[] { ("MapRoomCurrent", "Here"), ("MapRoomItems", "Items"), ("MapRoomUnknown", "Unseen") })
            {
                var r = Row(legend, 4);
                var room = Make(type, r);
                MinSize(room, 10, 10);
                Centre(room);
                Label(r, text);
            }
            Place((RectTransform)Label(c, "Point at a room for its name", "Hint").transform, 0, 176);
        }

        void FilesScreen(RectTransform c)
        {
            var list = Place(Win(c, "Files", 88, 0), 0, 0);
            var page = Place(Make("DocumentPanel", c), 94, 0);
            MinSize(page, 248, 214);
            var text = Column(page, 6);
            var title = Label(text, "", "Ink");
            title.Text.alignment = TextAlignmentOptions.Top;
            var body = Wrapped(text, "", 230, "Ink");
            Group(Files.Select(f => MenuItem(list, f.name)).ToArray(), 0, i =>
            {
                title.text = Files[i].name.ToUpperInvariant();
                body.text = Files[i].text;
            });
        }

        void SaveScreen(RectTransform c)
        {
            var left = Place(Column(c, 4), 0, 0);
            var view = Make("ExaminePanel", left);
            MinSize(view, 150, 112);
            view.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = true;
            Icon(view, "typewriter", 1, Large("typewriter") ?? Large("ink_ribbon"));
            var text = Column(Make("DialoguePanel", left), 1);
            Wrapped(text, "Will you use an ink ribbon to record your progress?", 128);
            var answer = Row(text, 8);
            foreach (var t in new[] { "Yes", "No" })
                MinSize(MenuItem(answer, t), 40);
            var list = Place(Win(c, "Save", 186), 156, 0);
            var saves = new[] { ("Main Hall", "02:14:36", 4), ("Safe Room", "01:40:02", 3), ("Greenhouse", "00:52:17", 2), ("", "", 0) };
            var slotsShown = saves.Select((s, i) =>
            {
                var b = Make("SaveSlot", list).GetComponent<PixelButton>();
                b.label.alignment = TextAlignmentOptions.TopLeft;
                b.text = s.Item1 != "" ? $"No.{i + 1}  {s.Item1}\n      {s.Item2}  Saves {s.Item3}" : $"No.{i + 1}  NO DATA\n ";
                return b;
            }).ToArray();
            Group(slotsShown, 0);
            var ribbons = Row(list, 6);
            Slot(ribbons, "ink_ribbon", 3);
            Centre((RectTransform)Label(ribbons, "Ink ribbons: 3", "Hint").transform);
        }

        void OptionsScreen(RectTransform c)
        {
            var pause = Place(Win(c, "Paused", 92, 0), 0, 0);
            Group(new[] { "Resume", "Options", "Load game", "Quit to title" }.Select(t => MenuItem(pause, t)).ToArray(), 1);
            var box = Place(Win(c, "Options", 244), 98, 0);
            var pages = Tabs(box, "Sound", "Display", "Controls");
            foreach (var (name, value) in new[] { ("Music", 60), ("Effects", 80), ("Voices", 70) })
                Slider(pages[0], name, value, 48);
            Dropdown(pages[0], 0, "Stereo", "Mono", "Headphones");
            var r = Row(pages[1], 4);
            MinSize(Label(r, "Brightness"), 60);
            var brightness = Bar(r, "Fine", 55, 0, 10);
            Expand(brightness);
            Centre((RectTransform)brightness.transform);
            foreach (var t in new[] { "Subtitles", "Screen shake", "Film grain" })
                Check(pages[1], t, t != "Screen shake");
            var modes = pages[2].gameObject.AddComponent<ToggleGroup>();
            foreach (var t in new[] { "Tank controls", "Modern controls" })
            {
                var radio = Check(pages[2], t, false, true);
                radio.group = modes;
                radio.isOn = t.StartsWith("Tank");
            }
            Input(pages[2], "Profile name");
            Make("Separator", box);
            var buttons = Row(box, 4);
            foreach (var t in new[] { "Back", "Apply" })
                Expand(Button(buttons, t));
        }

        // --- the kit's pieces ---

        /// <summary>A titled window in the parent, its content `gap` apart under the title.</summary>
        RectTransform Win(Transform parent, string title, float width, float gap = 4)
        {
            var w = Make("Window", parent);
            MinSize(w, width);
            w.GetComponent<VerticalLayoutGroup>().spacing = gap;
            w.Find("Title").GetComponent<PixelLabel>().text = title;
            return w;
        }

        /// <summary>A menu line: the pointer and the band show on the one the player is on.</summary>
        PixelButton MenuItem(Transform parent, string text)
        {
            var b = Make("MenuItem", parent).GetComponent<PixelButton>();
            b.label.alignment = TextAlignmentOptions.TopLeft;
            b.text = text;
            return b;
        }

        /// <summary>The buttons as one choice (a ButtonGroup): the one picked shows pressed, the rest not.</summary>
        static void Group(PixelButton[] buttons, int first, Action<int> picked = null)
        {
            void Pick(int n)
            {
                for (int i = 0; i < buttons.Length; i++)
                    buttons[i].on = i == n;
                picked?.Invoke(n);
            }
            for (int i = 0; i < buttons.Length; i++)
            {
                int n = i;
                buttons[i].onClick.AddListener(() => Pick(n));
            }
            Pick(first);
        }

        /// <summary>A label wrapping its words at this width.</summary>
        PixelLabel Wrapped(Transform parent, string text, float width, string variation = "Label")
        {
            var l = Label(parent, text, variation);
            foreach (var t in l.GetComponentsInChildren<TMP_Text>(true))
                t.textWrappingMode = TextWrappingModes.Normal;
            var e = Element(l);
            e.minWidth = e.preferredWidth = width;
            return l;
        }

        /// <summary>A 38x38 item box ("", "selected" or "equipped") holding the item's 32px icon and its count above 0.</summary>
        RectTransform Slot(Transform parent, string item, int count = 0, string kind = "")
        {
            var slot = Make(kind == "selected" ? "ItemSlotSelected" : kind == "equipped" ? "ItemSlotEquipped" : "ItemSlot", parent);
            MinSize(slot, 38, 38);
            var sprite = item == "" ? null : Small(item);
            if (sprite == null)
                return slot;
            var icon = Icon(slot, item, 1, sprite);
            Tip(icon, Items.TryGetValue(item, out var about) ? about[0] : Title(item));
            if (count > 0)
            {
                var n = Free((RectTransform)Label(icon, count.ToString(), "Count").transform);
                n.anchorMin = n.anchorMax = new Vector2(0, 1);
                n.pivot = Vector2.one;
                n.anchoredPosition = new Vector2(32, -21);
            }
            return slot;
        }

        Sprite Small(string item) => icons.FirstOrDefault(s => s != null && s.name == item && s.rect.width <= 32);

        Sprite Large(string item) => icons.FirstOrDefault(s => s != null && s.name == item && s.rect.width > 32);

        /// <summary>One of the theme's map marks or arrows (Marks/&lt;name&gt;) at its size; an empty space in a theme without it.</summary>
        RectTransform Mark(Transform parent, string name) => theme.Has("Marks/" + name) ? Make("Marks/" + name, parent) : Rect(name, parent);

        /// <summary>The heart monitor, its reading and optionally a bar; a click moves it on to the next condition.</summary>
        void Condition(Transform parent, string state, bool withBar)
        {
            var box = Column(parent, 2);
            var monitor = Make("MonitorPanel", box);
            monitor.gameObject.AddComponent<TooltipArea>().text = "Click to change the condition";
            var ecg = Ecg.Make(monitor, Conditions.Select(n => Sprite(sprites, "ecg_" + n)).ToArray());
            GameObject reading = null, bar = null;
            void Show()
            {
                ecg.Set(Array.IndexOf(Conditions, state));
                foreach (var old in new[] { reading, bar }.Where(o => o != null))
                {
                    old.SetActive(false);
                    Destroy(old);
                }
                reading = Label(box, Title(state), Title(state)).gameObject;
                if (withBar)
                    bar = Bar(box, Title(state), Health[state], 0, 10).gameObject;
            }
            Show();
            monitor.gameObject.AddComponent<Click>().action = () =>
            {
                state = Conditions[(Array.IndexOf(Conditions, state) + 1) % Conditions.Length];
                Show();
            };
        }

        /// <summary>Its own size, held to a side of the row's height (SIZE_SHRINK_BEGIN ...).</summary>
        static RectTransform Hold(RectTransform r, TextAnchor side)
        {
            var holder = Centre(r);
            holder.GetComponent<VerticalLayoutGroup>().childAlignment = side;
            return holder;
        }

        /// <summary>Centred across its parent, `y` art pixels from its top, sized to its content.</summary>
        static RectTransform TopCentre(RectTransform r, float y)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 1);
            r.anchoredPosition = new Vector2(0, -y);
            Fit(r);
            return r;
        }

        /// <summary>Out of its parent's layout at (x, y) from its top left, at the size it has.</summary>
        static RectTransform Pin(RectTransform r, float x, float y)
        {
            Element(r).ignoreLayout = true;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            return r;
        }

        /// <summary>An ECG trace scrolling left, faster the worse the condition.</summary>
        class Ecg : MonoBehaviour
        {
            static readonly float[] Speed = { 24, 36, 56, 30 };  // by Conditions
            Sprite[] traces;
            Image[] tiles;
            int state;
            float shift;

            public static Ecg Make(Transform parent, Sprite[] traces)
            {
                var holder = Rect("Ecg", parent);
                holder.gameObject.AddComponent<RectMask2D>();
                var e = Element(holder);
                e.minWidth = e.preferredWidth = 74;
                e.minHeight = e.preferredHeight = 16;
                var ecg = holder.gameObject.AddComponent<Ecg>();
                ecg.traces = traces;
                ecg.tiles = new Image[4];
                for (int i = 0; i < ecg.tiles.Length; i++)
                {
                    var tile = ecg.tiles[i] = Rect("Trace", holder).gameObject.AddComponent<Image>();
                    tile.raycastTarget = false;
                    tile.rectTransform.anchorMin = tile.rectTransform.anchorMax = tile.rectTransform.pivot = new Vector2(0, 1);
                    tile.rectTransform.sizeDelta = new Vector2(64, 16);
                }
                return ecg;
            }

            public void Set(int condition)
            {
                state = condition;
                foreach (var t in tiles)
                {
                    t.sprite = traces[state];
                    t.enabled = t.sprite != null;
                }
            }

            void Update()
            {
                shift = (shift + Time.deltaTime * Speed[state]) % 64;
                for (int i = 0; i < tiles.Length; i++)
                    tiles[i].rectTransform.anchoredPosition = new Vector2(i * 64 - Mathf.Floor(shift), 0);
            }
        }

        /// <summary>Blinks its Image while on, as the "next" arrow under a line of dialogue.</summary>
        class Blink : MonoBehaviour
        {
            public bool on;

            void Update()
            {
                if (TryGetComponent(out Image image))
                    image.enabled = !on || (int)(Time.unscaledTime * 3) % 2 == 0;
            }
        }

        /// <summary>Runs its action on a left click.</summary>
        class Click : MonoBehaviour, IPointerClickHandler
        {
            public Action action;

            public void OnPointerClick(PointerEventData e)
            {
                if (e.button == PointerEventData.InputButton.Left)
                    action?.Invoke();
            }
        }
    }
}
