using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HorrorEscape.UI
{
    /// <summary>
    /// Handles Main Menu UI navigation with a minimalist, psychological horror Backrooms aesthetic.
    /// Title: "THE BACKROOMS // LEVEL 0"
    /// Terminal-inspired buttons: [ ENTER ], [ CONTROLS ], [ EXIT ]
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Menu Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject controlsPanel;

        [Header("Scene to Load")]
        [SerializeField] private string targetSceneName = "HorrorEscapeLevel";

        private void Awake()
        {
            BuildProceduralMenuIfNeeded();
        }

        private void Start()
        {
            Time.timeScale = 1.0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        public void PlayGame()
        {
            if (!string.IsNullOrEmpty(targetSceneName) && Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                SceneManager.LoadScene(targetSceneName);
            }
            else if (SceneManager.sceneCountInBuildSettings > 1)
            {
                SceneManager.LoadScene(1);
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }

        public void OpenControls()
        {
            if (controlsPanel != null) controlsPanel.SetActive(true);
            if (mainPanel != null) mainPanel.SetActive(false);
        }

        public void CloseControls()
        {
            if (controlsPanel != null) controlsPanel.SetActive(false);
            if (mainPanel != null) mainPanel.SetActive(true);
        }

        public void QuitGame()
        {
            Debug.Log("[MainMenu] Exiting feed...");
            Application.Quit();
        }

        private void BuildProceduralMenuIfNeeded()
        {
            if (mainPanel != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("MainMenuCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasGO.AddComponent<GraphicRaycaster>();
                transform.SetParent(canvasGO.transform, false);
            }

            Font font = HUDManager.GetTerminalFont();

            // Main Panel
            mainPanel = new GameObject("MainTerminalPanel");
            mainPanel.transform.SetParent(canvas.transform, false);
            RectTransform mrt = mainPanel.AddComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.sizeDelta = Vector2.zero;

            // Terminal Window Box
            GameObject window = new GameObject("TerminalBox");
            window.transform.SetParent(mainPanel.transform, false);
            Image wBg = window.AddComponent<Image>();
            wBg.color = HUDManager.ColorDarkPanel;
            RectTransform wrt = window.GetComponent<RectTransform>();
            wrt.anchoredPosition = Vector2.zero;
            wrt.sizeDelta = new Vector2(540f, 440f);

            // Title
            GameObject titleGO = new GameObject("MenuTitle");
            titleGO.transform.SetParent(window.transform, false);
            Text title = titleGO.AddComponent<Text>();
            title.font = font;
            title.fontSize = 22;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = HUDManager.ColorPaperOffWhite;
            title.text = "THE BACKROOMS\n<size=12><color=#66996D>LEVEL 0 // EMERGENCY INTERFACE</color></size>";
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 130f);
            trt.sizeDelta = new Vector2(480f, 70f);

            // Buttons
            CreateMenuButton(window, "PlayBtn", new Vector2(0f, 30f), "ENTER", font, PlayGame);
            CreateMenuButton(window, "ControlsBtn", new Vector2(0f, -35f), "CONTROLS", font, OpenControls);
            CreateMenuButton(window, "ExitBtn", new Vector2(0f, -100f), "EXIT FEED", font, QuitGame);

            // Controls Panel
            controlsPanel = new GameObject("ControlsTerminalPanel");
            controlsPanel.transform.SetParent(canvas.transform, false);
            RectTransform crt = controlsPanel.AddComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.sizeDelta = Vector2.zero;

            GameObject cBox = new GameObject("ControlsBox");
            cBox.transform.SetParent(controlsPanel.transform, false);
            Image cBg = cBox.AddComponent<Image>();
            cBg.color = HUDManager.ColorDarkPanel;
            RectTransform cbrt = cBox.GetComponent<RectTransform>();
            cbrt.anchoredPosition = Vector2.zero;
            cbrt.sizeDelta = new Vector2(580f, 400f);

            GameObject cTitleGO = new GameObject("ControlsTitle");
            cTitleGO.transform.SetParent(cBox.transform, false);
            Text cTitle = cTitleGO.AddComponent<Text>();
            cTitle.font = font;
            cTitle.fontSize = 16;
            cTitle.fontStyle = FontStyle.Bold;
            cTitle.alignment = TextAnchor.MiddleCenter;
            cTitle.color = HUDManager.ColorSystemGreen;
            cTitle.text = "OPERATIONAL CONTROLS //";
            RectTransform ctr = cTitleGO.GetComponent<RectTransform>();
            ctr.anchoredPosition = new Vector2(0f, 130f);
            ctr.sizeDelta = new Vector2(500f, 40f);

            GameObject cBodyGO = new GameObject("ControlsBody");
            cBodyGO.transform.SetParent(cBox.transform, false);
            Text cBody = cBodyGO.AddComponent<Text>();
            cBody.font = font;
            cBody.fontSize = 13;
            cBody.alignment = TextAnchor.UpperLeft;
            cBody.lineSpacing = 1.35f;
            cBody.color = HUDManager.ColorPaperOffWhite;
            cBody.text = "W A S D        : NAVIGATE LEVEL 0\n" +
                         "LEFT SHIFT     : SPRINT (DRAINS STAMINA)\n" +
                         "LEFT CTRL      : CROUCH (STEALTH EVASION)\n" +
                         "F / RMB        : TOGGLE FLASHLIGHT\n" +
                         "LEFT CLICK     : FIRE DEFENSE PISTOL\n" +
                         "R              : RELOAD AMMUNITION\n" +
                         "E              : INTERACT / PICK UP ITEMS\n" +
                         "ESC / P        : EMERGENCY SYSTEM MENU";
            RectTransform cbtr = cBodyGO.GetComponent<RectTransform>();
            cbtr.anchoredPosition = new Vector2(0f, -5f);
            cbtr.sizeDelta = new Vector2(480f, 200f);

            CreateMenuButton(cBox, "BackBtn", new Vector2(0f, -140f), "RETURN", font, CloseControls);
            controlsPanel.SetActive(false);
        }

        private static void CreateMenuButton(GameObject parent, string name, Vector2 pos, string label, Font font, UnityAction onClick)
        {
            GameObject btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent.transform, false);

            Image img = btnGO.AddComponent<Image>();
            img.color = new Color(0.09f, 0.08f, 0.07f, 0.95f);

            Button btn = btnGO.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.09f, 0.08f, 0.07f, 0.95f);
            cb.highlightedColor = new Color(0.18f, 0.16f, 0.13f, 1f);
            cb.pressedColor = new Color(0.24f, 0.22f, 0.18f, 1f);
            cb.selectedColor = cb.highlightedColor;
            btn.colors = cb;
            btn.onClick.AddListener(onClick);

            RectTransform brt = btnGO.GetComponent<RectTransform>();
            brt.anchoredPosition = pos;
            brt.sizeDelta = new Vector2(260f, 42f);

            GameObject textGO = new GameObject("BtnText");
            textGO.transform.SetParent(btnGO.transform, false);
            Text t = textGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = 13;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = HUDManager.ColorPaperOffWhite;
            t.text = $"[ {label} ]";
            t.raycastTarget = false;
            RectTransform trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
        }
    }
}
