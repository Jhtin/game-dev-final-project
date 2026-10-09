using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;
using HorrorEscape.Managers;
using HorrorEscape.Player;

namespace HorrorEscape.UI
{
    /// <summary>
    /// Centralized HUD Manager controlling the psychological horror Backrooms UI.
    /// Fully styled with the PSX Horror UI Free (Fog Theme) kit:
    /// - 9-sliced PSX window and panel frames
    /// - Classic survival horror ECG pulse waveform monitor (FINE, CAUTION, DANGER)
    /// - PSX pixel progress bars for Health and Flashlight battery
    /// - PSX large handgun icon on the Defense Ammo equipment HUD
    /// - PSX document and note inspection modal with note icon
    /// - PSX themed interaction prompts with pointer and item icons
    /// - Authentic PSX sprite-swap buttons on Pause, Game Over, and Victory menus
    /// - Custom PSX horror hardware pointer cursor
    /// - Fullscreen analog horror scanlines, film grain, dynamic vignette & red damage flash
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        #region UI Palette Colors
        // Backrooms / PSX Fog Psychological Horror Palette
        public static readonly Color ColorPaperOffWhite = new Color(0.90f, 0.87f, 0.78f, 0.95f);
        public static readonly Color ColorMutedTan      = new Color(0.58f, 0.55f, 0.46f, 0.85f);
        public static readonly Color ColorFadedBorder   = new Color(0.24f, 0.22f, 0.17f, 0.85f);
        public static readonly Color ColorDarkPanel     = new Color(0.06f, 0.05f, 0.04f, 0.90f);
        public static readonly Color ColorSystemGreen   = new Color(0.46f, 0.68f, 0.50f, 0.95f);
        public static readonly Color ColorWarningAmber  = new Color(0.85f, 0.55f, 0.24f, 0.95f);
        public static readonly Color ColorCriticalRed   = new Color(0.72f, 0.26f, 0.22f, 0.95f);
        #endregion

        #region PSX Horror UI Sprites & Assets
        [Header("PSX Horror UI Assets (Fog Theme)")]
        [SerializeField] private Sprite psxWindowSprite;
        [SerializeField] private Sprite psxPanelSprite;
        [SerializeField] private Sprite psxDocumentSprite;
        [SerializeField] private Sprite psxButtonNormalSprite;
        [SerializeField] private Sprite psxButtonHoverSprite;
        [SerializeField] private Sprite psxButtonPressedSprite;
        [SerializeField] private Sprite psxProgressBgSprite;
        [SerializeField] private Sprite psxProgressFillSprite;
        [SerializeField] private Sprite psxPistolIcon;
        [SerializeField] private Sprite psxNoteIcon;
        [SerializeField] private Sprite psxKeyIcon;
        [SerializeField] private Sprite psxPointerIcon;
        [SerializeField] private Sprite psxMarkItemIcon;
        [SerializeField] private Sprite psxMarkDoorIcon;
        [SerializeField] private Sprite psxEcgFineSprite;
        [SerializeField] private Sprite psxEcgCautionSprite;
        [SerializeField] private Sprite psxEcgDangerSprite;
        [SerializeField] private Sprite psxBarFineSprite;
        [SerializeField] private Sprite psxBarCautionSprite;
        [SerializeField] private Sprite psxBarDangerSprite;
        [SerializeField] private Texture2D psxCursorTexture;
        #endregion

        #region HUD Element References
        [Header("HUD References (Auto-Built if empty)")]
        [SerializeField] private Text promptText;
        [SerializeField] private CanvasGroup promptCanvasGroup;
        [SerializeField] private Image promptIconImage;
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Text objectiveHeaderText;
        [SerializeField] private Text objectiveBodyText;
        [SerializeField] private Text timerHeaderText;
        [SerializeField] private Text timerBodyText;
        [SerializeField] private Text batteryHeaderText;
        [SerializeField] private Text batteryBodyText;
        [SerializeField] private Image batteryBarFillImage;
        [SerializeField] private Text ammoHeaderText;
        [SerializeField] private Text ammoBodyText;
        [SerializeField] private Image pistolIconImage;
        [SerializeField] private Text healthHeaderText;
        [SerializeField] private Text healthBodyText;
        [SerializeField] private Image healthBarFillImage;
        [SerializeField] private Image ecgWaveformImage;
        [SerializeField] private Text notificationText;
        [SerializeField] private CanvasGroup notificationCanvasGroup;

        [Header("Screen Effects & Overlays")]
        [SerializeField] private Image analogHorrorOverlay;
        [SerializeField] private Material analogHorrorMaterial;
        [SerializeField] private Image damageFlashOverlay;

        [Header("Pause Menu")]
        [SerializeField] private GameObject pausePanel;
        private bool isPaused = false;
        public bool IsPaused => isPaused;

        [Header("Note Inspection Modal")]
        [SerializeField] private GameObject notePanel;
        [SerializeField] private Text noteTitleText;
        [SerializeField] private Text noteBodyText;
        [SerializeField] private Image noteIconImage;
        private bool isReadingNote = false;
        public bool IsReadingNote => isReadingNote;

        [Header("End Game Screens")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text gameOverTitleText;
        [SerializeField] private Text gameOverSubText;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Text victoryTitleText;
        [SerializeField] private Text victoryTimeText;
        #endregion

        // Player / State references
        private FirstPersonController playerController;
        private FlashlightController flashlightController;
        private PlayerCombat playerCombat;
        private StalkerAI nearbyStalker;

        // Coroutines & State Tracking
        private Coroutine objectiveTypewriterCoroutine;
        private Coroutine notificationCoroutine;
        private Coroutine damageFlashCoroutine;
        private string currentObjectiveString = string.Empty;
        private float lastKnownHealth = 100f;
        private float lastKnownMaxHealth = 100f;
        private float lastKnownBattery = 100f;
        private float lastKnownMaxBattery = 100f;
        private int lastKnownAmmo = 6;
        private int lastKnownReserve = 6;

        // Screen Effects Dynamic Modulation
        private float horrorSpikeTimer = 0f;
        private float baseNoise = 0.035f;
        private float baseScanlines = 0.08f;
        private float baseVignette = 0.42f;

        // Monospace Font Cache
        private static Font cachedTerminalFont;
        public static Font GetTerminalFont()
        {
            if (cachedTerminalFont == null)
            {
                string[] preferredFonts = new string[] { "Lucida Console", "Consolas", "Courier New", "Lucida Sans Typewriter" };
                cachedTerminalFont = Font.CreateDynamicFontFromOSFont(preferredFonts, 14);
                if (cachedTerminalFont == null)
                {
                    cachedTerminalFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                if (cachedTerminalFont == null)
                {
                    cachedTerminalFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
            }
            return cachedTerminalFont;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

#if UNITY_EDITOR
            LoadFallbackPSXAssets();
#endif

            EnsureEventSystem();
            BuildBackroomsUIHierarchy();
        }

#if UNITY_EDITOR
        private void LoadFallbackPSXAssets()
        {
            const string root = "Assets/PSXHorrorUIFree";
            if (psxWindowSprite == null) psxWindowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/window.png");
            if (psxPanelSprite == null) psxPanelSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/panel.png");
            if (psxDocumentSprite == null) psxDocumentSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/document.png");
            if (psxButtonNormalSprite == null) psxButtonNormalSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/button_normal.png");
            if (psxButtonHoverSprite == null) psxButtonHoverSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/button_hover.png");
            if (psxButtonPressedSprite == null) psxButtonPressedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/button_pressed.png");
            if (psxProgressBgSprite == null) psxProgressBgSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/progress_bg.png");
            if (psxProgressFillSprite == null) psxProgressFillSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/progress_fill.png");
            if (psxPistolIcon == null) psxPistolIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/icons/large/pistol.png");
            if (psxNoteIcon == null) psxNoteIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/icons/large/note.png");
            if (psxKeyIcon == null) psxKeyIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/icons/large/key_skeleton.png");
            if (psxPointerIcon == null) psxPointerIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/pointer.png");
            if (psxMarkItemIcon == null) psxMarkItemIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/mark_item.png");
            if (psxMarkDoorIcon == null) psxMarkDoorIcon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/themes/fog/mark_door.png");
            if (psxEcgFineSprite == null) psxEcgFineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/status/ecg_fine.png");
            if (psxEcgCautionSprite == null) psxEcgCautionSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/status/ecg_caution.png");
            if (psxEcgDangerSprite == null) psxEcgDangerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/status/ecg_danger.png");
            if (psxBarFineSprite == null) psxBarFineSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/status/bar_fine.png");
            if (psxBarCautionSprite == null) psxBarCautionSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/status/bar_caution.png");
            if (psxBarDangerSprite == null) psxBarDangerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{root}/Sprites/status/bar_danger.png");
            if (psxCursorTexture == null) psxCursorTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>($"{root}/Cursors/cursor_2x.png");
        }
#endif

        private void Start()
        {
            FindPlayerReferences();

            if (notePanel != null) notePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (damageFlashOverlay != null) damageFlashOverlay.color = new Color(0.7f, 0.1f, 0.1f, 0f);

            SetCustomCursor(false);

            int req = GameManager.Instance != null ? GameManager.Instance.RequiredObjectiveCount : 0;
            UpdateObjectiveText(0, req);
            UpdateHealth(100f, 100f);
        }

        private void FindPlayerReferences()
        {
            playerController = FindFirstObjectByType<FirstPersonController>();
            flashlightController = FindFirstObjectByType<FlashlightController>();
            playerCombat = FindFirstObjectByType<PlayerCombat>();
            nearbyStalker = FindFirstObjectByType<StalkerAI>();
        }

        private void Update()
        {
            if (playerController == null) FindPlayerReferences();

            UpdateMeters();
            UpdateScreenEffects();
            UpdateECGAnimation();
            HandlePauseInput();
            HandleNoteInput();
        }

        #region Hardware Cursor
        public void SetCustomCursor(bool enable)
        {
            if (enable && psxCursorTexture != null)
            {
                Cursor.SetCursor(psxCursorTexture, Vector2.zero, CursorMode.Auto);
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }
        #endregion

        #region Input Handlers
        private void HandlePauseInput()
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                if (isReadingNote)
                {
                    CloseNote();
                }
                else
                {
                    TogglePause();
                }
            }
        }

        private void HandleNoteInput()
        {
            if (isReadingNote && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space)))
            {
                CloseNote();
            }
        }

        public void TogglePause()
        {
            if (isReadingNote || (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsVictory))) return;

            isPaused = !isPaused;
            Time.timeScale = isPaused ? 0f : 1f;
            if (pausePanel != null) pausePanel.SetActive(isPaused);
            if (playerController != null) playerController.LockCursor(!isPaused);
            SetCustomCursor(isPaused);

            if (isPaused && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTerminalBeep(0.3f);
            }
        }

        public void ResumeGame()
        {
            if (isPaused) TogglePause();
        }
        #endregion

        #region HUD Meters & Data Updates
        private void UpdateMeters()
        {
            // Flashlight Meter
            if (flashlightController != null)
            {
                lastKnownBattery = flashlightController.CurrentBattery;
                lastKnownMaxBattery = flashlightController.MaxBattery;
                RenderFlashlightUI(flashlightController.CurrentBattery, flashlightController.MaxBattery, flashlightController.IsOn);
            }

            // Stalker Proximity Tension on Screen Effects
            if (nearbyStalker != null && nearbyStalker.gameObject.activeInHierarchy)
            {
                float dist = Vector3.Distance(transform.position, nearbyStalker.transform.position);
                if (dist < 18f && nearbyStalker.CurrentState == StalkerState.Chase)
                {
                    horrorSpikeTimer = Mathf.Max(horrorSpikeTimer, 0.8f);
                }
            }
        }

        private void UpdateECGAnimation()
        {
            if (ecgWaveformImage == null || !ecgWaveformImage.gameObject.activeInHierarchy) return;

            // Heartbeat rhythm: faster when in danger, gentle and steady when fine
            float healthRatio = lastKnownMaxHealth > 0 ? (lastKnownHealth / lastKnownMaxHealth) : 1f;
            float pulseFreq = Mathf.Lerp(4.5f, 1.8f, healthRatio);
            float pulseScale = 1.0f + Mathf.Sin(Time.time * pulseFreq * Mathf.PI * 2f) * 0.05f;
            ecgWaveformImage.rectTransform.localScale = new Vector3(pulseScale, pulseScale, 1f);
        }

        private void RenderFlashlightUI(float current, float max, bool isOn)
        {
            float pct = Mathf.Clamp01(current / Mathf.Max(1f, max));
            int pctInt = Mathf.CeilToInt(pct * 100f);

            if (batteryBarFillImage != null)
            {
                batteryBarFillImage.fillAmount = pct;
                if (pct <= 0.05f)
                    batteryBarFillImage.color = ColorCriticalRed;
                else if (pct <= 0.20f)
                    batteryBarFillImage.color = ColorWarningAmber;
                else
                    batteryBarFillImage.color = isOn ? ColorPaperOffWhite : ColorMutedTan;
            }

            if (batteryBodyText == null) return;

            if (pct <= 0.05f)
            {
                if (batteryHeaderText != null)
                {
                    batteryHeaderText.text = "FLASHLIGHT // CRITICAL";
                    batteryHeaderText.color = ColorCriticalRed;
                }
                batteryBodyText.text = $"{pctInt:00}%\n<color=#B84237>LOW BATTERY</color>";
                batteryBodyText.color = ColorCriticalRed;
            }
            else if (pct <= 0.20f)
            {
                bool flickerTick = (Mathf.FloorToInt(Time.time * 6f) % 2 == 0);
                if (batteryHeaderText != null)
                {
                    batteryHeaderText.text = "FLASHLIGHT";
                    batteryHeaderText.color = ColorWarningAmber;
                }
                batteryBodyText.text = $"{pctInt:00}%" + (flickerTick ? " !" : "");
                batteryBodyText.color = ColorWarningAmber;
            }
            else
            {
                if (batteryHeaderText != null)
                {
                    batteryHeaderText.text = isOn ? "FLASHLIGHT" : "FLASHLIGHT [OFF]";
                    batteryHeaderText.color = ColorMutedTan;
                }
                batteryBodyText.text = $"{pctInt:00}%";
                batteryBodyText.color = isOn ? ColorPaperOffWhite : ColorMutedTan;
            }
        }

        public void UpdateBattery(float pct)
        {
            RenderFlashlightUI(pct * 100f, 100f, flashlightController != null ? flashlightController.IsOn : true);
        }

        public void UpdateHealth(float current, float max)
        {
            lastKnownHealth = current;
            lastKnownMaxHealth = max;

            float pct = Mathf.Clamp01(current / Mathf.Max(1f, max));

            // Update Progress Fill
            if (healthBarFillImage != null)
            {
                healthBarFillImage.fillAmount = pct;
                if (pct <= 0.25f)
                {
                    if (psxBarDangerSprite != null) healthBarFillImage.sprite = psxBarDangerSprite;
                    healthBarFillImage.color = ColorCriticalRed;
                }
                else if (pct <= 0.60f)
                {
                    if (psxBarCautionSprite != null) healthBarFillImage.sprite = psxBarCautionSprite;
                    healthBarFillImage.color = ColorWarningAmber;
                }
                else
                {
                    if (psxBarFineSprite != null) healthBarFillImage.sprite = psxBarFineSprite;
                    healthBarFillImage.color = ColorSystemGreen;
                }
            }

            // Update ECG Monitor and Text
            if (pct <= 0.25f)
            {
                if (healthHeaderText != null)
                {
                    healthHeaderText.text = "CONDITION: DANGER";
                    healthHeaderText.color = ColorCriticalRed;
                }
                if (healthBodyText != null)
                {
                    healthBodyText.text = $"{Mathf.CeilToInt(current):00} HP";
                    healthBodyText.color = ColorCriticalRed;
                }
                if (ecgWaveformImage != null)
                {
                    if (psxEcgDangerSprite != null) ecgWaveformImage.sprite = psxEcgDangerSprite;
                    ecgWaveformImage.color = ColorCriticalRed;
                }
            }
            else if (pct <= 0.60f)
            {
                if (healthHeaderText != null)
                {
                    healthHeaderText.text = "CONDITION: CAUTION";
                    healthHeaderText.color = ColorWarningAmber;
                }
                if (healthBodyText != null)
                {
                    healthBodyText.text = $"{Mathf.CeilToInt(current):00} HP";
                    healthBodyText.color = ColorWarningAmber;
                }
                if (ecgWaveformImage != null)
                {
                    if (psxEcgCautionSprite != null) ecgWaveformImage.sprite = psxEcgCautionSprite;
                    ecgWaveformImage.color = ColorWarningAmber;
                }
            }
            else
            {
                if (healthHeaderText != null)
                {
                    healthHeaderText.text = "CONDITION: FINE";
                    healthHeaderText.color = ColorSystemGreen;
                }
                if (healthBodyText != null)
                {
                    healthBodyText.text = $"{Mathf.CeilToInt(current):00} HP";
                    healthBodyText.color = ColorSystemGreen;
                }
                if (ecgWaveformImage != null)
                {
                    if (psxEcgFineSprite != null) ecgWaveformImage.sprite = psxEcgFineSprite;
                    ecgWaveformImage.color = ColorSystemGreen;
                }
            }

            // Adjust heartbeat audio
            if (AudioManager.Instance != null)
            {
                float intensity = pct <= 0.35f ? (1f - (pct / 0.35f)) : 0f;
                AudioManager.Instance.SetHeartbeatIntensity(intensity);
            }
        }

        public void UpdateAmmoText(int current, int reserve)
        {
            lastKnownAmmo = current;
            lastKnownReserve = reserve;

            if (ammoBodyText == null) return;

            if (current <= 0)
            {
                ammoBodyText.text = $"00 / {reserve:00}\n<color=#B84237>NO AMMUNITION</color>";
                ammoBodyText.color = ColorCriticalRed;
                if (ammoHeaderText != null) ammoHeaderText.color = ColorCriticalRed;
                if (pistolIconImage != null) pistolIconImage.color = ColorCriticalRed;
            }
            else if (current <= 2)
            {
                ammoBodyText.text = $"{current:00} / {reserve:00}";
                ammoBodyText.color = ColorWarningAmber;
                if (ammoHeaderText != null) ammoHeaderText.color = ColorMutedTan;
                if (pistolIconImage != null) pistolIconImage.color = ColorWarningAmber;
            }
            else
            {
                ammoBodyText.text = $"{current:00} / {reserve:00}";
                ammoBodyText.color = ColorPaperOffWhite;
                if (ammoHeaderText != null) ammoHeaderText.color = ColorMutedTan;
                if (pistolIconImage != null) pistolIconImage.color = ColorPaperOffWhite;
            }
        }

        public void UpdateTimer(string timerStr)
        {
            if (timerBodyText == null) return;

            string cleanTime = timerStr.Replace("TIME: ", "").Replace("TIME REMAINING: ", "").Trim();
            timerBodyText.text = cleanTime;

            if (cleanTime.StartsWith("00:") || cleanTime.StartsWith("01:0") || cleanTime.StartsWith("01:1") || cleanTime.StartsWith("01:2"))
            {
                timerBodyText.color = ColorWarningAmber;
                if (timerHeaderText != null) timerHeaderText.color = ColorWarningAmber;
            }
            else
            {
                timerBodyText.color = ColorPaperOffWhite;
                if (timerHeaderText != null) timerHeaderText.color = ColorMutedTan;
            }
        }

        public void SetInteractionPrompt(string prompt, bool visible)
        {
            if (promptCanvasGroup == null) return;

            if (!visible || string.IsNullOrEmpty(prompt))
            {
                StartCoroutine(FadeCanvasGroup(promptCanvasGroup, 0f, 0.15f));
                if (crosshairImage != null)
                {
                    crosshairImage.color = new Color(0.90f, 0.87f, 0.78f, 0.45f);
                }
                return;
            }

            string upper = prompt.ToUpper();
            string actionText = "INTERACT";
            Sprite icon = psxPointerIcon;

            if (upper.Contains("PICK UP") || upper.Contains("COLLECT"))
            {
                icon = psxMarkItemIcon != null ? psxMarkItemIcon : psxPointerIcon;
                if (upper.Contains("BATTERY")) actionText = "PICK UP BATTERY";
                else if (upper.Contains("AMMO") || upper.Contains("AMMUNITION")) actionText = "PICK UP AMMUNITION";
                else if (upper.Contains("KEYCARD") || upper.Contains("KEY")) actionText = "ACQUIRE KEYCARD";
                else if (upper.Contains("ALMOND") || upper.Contains("WATER")) actionText = "PICK UP WATER";
                else if (upper.Contains("FIRST AID")) actionText = "PICK UP FIRST AID";
                else actionText = "PICK UP ITEM";
            }
            else if (upper.Contains("OPEN") || upper.Contains("DOOR"))
            {
                icon = psxMarkDoorIcon != null ? psxMarkDoorIcon : psxPointerIcon;
                actionText = "OPEN DOOR";
            }
            else if (upper.Contains("POWER") || upper.Contains("BREAKER") || upper.Contains("SWITCH"))
            {
                icon = psxMarkItemIcon != null ? psxMarkItemIcon : psxPointerIcon;
                actionText = "RESTORE POWER";
            }
            else if (upper.Contains("EXIT") || upper.Contains("ESCAPE"))
            {
                icon = psxMarkDoorIcon != null ? psxMarkDoorIcon : psxPointerIcon;
                actionText = "ESCAPE FACILITY";
            }
            else if (upper.Contains("READ") || upper.Contains("NOTE"))
            {
                icon = psxNoteIcon != null ? psxNoteIcon : psxPointerIcon;
                actionText = "INSPECT LOG";
            }

            if (promptIconImage != null && icon != null)
            {
                promptIconImage.sprite = icon;
                promptIconImage.gameObject.SetActive(true);
            }

            if (promptText != null)
            {
                promptText.text = $"[E]\n{actionText}";
            }

            StartCoroutine(FadeCanvasGroup(promptCanvasGroup, 1f, 0.15f));

            if (crosshairImage != null)
            {
                crosshairImage.color = new Color(0.95f, 0.92f, 0.82f, 0.85f);
            }
        }
        #endregion

        #region Objectives & Typewriter Anims
        public void SetObjective(string text)
        {
            string cleaned = text.Replace("OBJECTIVE: ", "").Replace("Objective: ", "").Trim().ToUpper();
            if (cleaned == currentObjectiveString) return;

            currentObjectiveString = cleaned;

            if (objectiveTypewriterCoroutine != null)
            {
                StopCoroutine(objectiveTypewriterCoroutine);
            }
            objectiveTypewriterCoroutine = StartCoroutine(AnimateObjectiveTypewriter(cleaned));
        }

        public void UpdateObjectiveText(int current, int required)
        {
            if (required <= 0)
            {
                SetObjective("Find a way out.");
            }
            else
            {
                SetObjective($"Locate emergency fuses ({current}/{required}).");
            }
        }

        public void ShowObjectiveBanner(string title, string subtitle)
        {
            string bannerHeader = title.ToUpper();
            string bannerBody = subtitle.ToUpper();

            if (objectiveTypewriterCoroutine != null)
            {
                StopCoroutine(objectiveTypewriterCoroutine);
            }
            objectiveTypewriterCoroutine = StartCoroutine(AnimateObjectiveUpdate(bannerHeader, bannerBody));
        }

        private IEnumerator AnimateObjectiveTypewriter(string newObjective)
        {
            if (objectiveBodyText == null) yield break;

            if (objectiveHeaderText != null)
            {
                objectiveHeaderText.text = "OBJECTIVE";
                objectiveHeaderText.color = ColorMutedTan;
            }

            objectiveBodyText.text = "";
            for (int i = 0; i <= newObjective.Length; i++)
            {
                objectiveBodyText.text = newObjective.Substring(0, i) + (i < newObjective.Length ? "█" : "");

                if (AudioManager.Instance != null && i % 3 == 0)
                {
                    AudioManager.Instance.PlayTypewriterClick();
                }

                yield return new WaitForSeconds(0.025f);
            }

            objectiveBodyText.text = newObjective;
        }

        private IEnumerator AnimateObjectiveUpdate(string headerText, string bodyText)
        {
            if (objectiveHeaderText != null)
            {
                objectiveHeaderText.text = $"[!] {headerText}";
                objectiveHeaderText.color = ColorSystemGreen;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTerminalBeep(0.4f);
            }

            yield return StartCoroutine(AnimateObjectiveTypewriter(bodyText));

            yield return new WaitForSeconds(2.5f);

            if (objectiveHeaderText != null)
            {
                objectiveHeaderText.text = "OBJECTIVE";
                objectiveHeaderText.color = ColorMutedTan;
            }
        }
        #endregion

        #region Notifications
        public void ShowNotification(string message)
        {
            if (notificationText == null || notificationCanvasGroup == null) return;

            if (notificationCoroutine != null)
            {
                StopCoroutine(notificationCoroutine);
            }
            notificationCoroutine = StartCoroutine(AnimateSystemNotification(message));
        }

        private IEnumerator AnimateSystemNotification(string message)
        {
            string cleanMsg = message.Trim().ToUpper();
            notificationText.text = $"SYSTEM NOTIFICATION //\n{cleanMsg}";

            yield return StartCoroutine(FadeCanvasGroup(notificationCanvasGroup, 1f, 0.2f));

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTerminalBeep(0.3f);
            }

            yield return new WaitForSeconds(2.0f);

            yield return StartCoroutine(FadeCanvasGroup(notificationCanvasGroup, 0f, 0.35f));
        }
        #endregion

        #region Screen Effects Modulation
        public void TriggerDamageFlash()
        {
            horrorSpikeTimer = 0.6f;
            if (damageFlashCoroutine != null) StopCoroutine(damageFlashCoroutine);
            damageFlashCoroutine = StartCoroutine(AnimateDamageFlash());
        }

        private IEnumerator AnimateDamageFlash()
        {
            if (damageFlashOverlay == null) yield break;

            damageFlashOverlay.color = new Color(0.7f, 0.1f, 0.1f, 0.35f);
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.deltaTime * 3.0f;
                damageFlashOverlay.color = new Color(0.7f, 0.1f, 0.1f, Mathf.Clamp01(t * 0.35f));
                yield return null;
            }
            damageFlashOverlay.color = new Color(0.7f, 0.1f, 0.1f, 0f);
        }

        public void SetDangerVignette(float intensity)
        {
            horrorSpikeTimer = Mathf.Max(horrorSpikeTimer, intensity * 0.5f);
        }

        private void UpdateScreenEffects()
        {
            if (analogHorrorMaterial == null) return;

            if (horrorSpikeTimer > 0f)
            {
                horrorSpikeTimer -= Time.deltaTime;
            }

            float healthRatio = lastKnownMaxHealth > 0 ? (lastKnownHealth / lastKnownMaxHealth) : 1f;
            float healthVignetteBoost = Mathf.Lerp(0.35f, 0.0f, healthRatio);

            float spike = Mathf.Clamp01(horrorSpikeTimer);

            float targetScanlines = baseScanlines + spike * 0.12f;
            float targetNoise = baseNoise + spike * 0.08f;
            float targetVignette = baseVignette + healthVignetteBoost + spike * 0.15f;
            float targetDistortion = spike * 0.012f;

            analogHorrorMaterial.SetFloat("_ScanlineIntensity", targetScanlines);
            analogHorrorMaterial.SetFloat("_NoiseIntensity", targetNoise);
            analogHorrorMaterial.SetFloat("_VignetteIntensity", targetVignette);
            analogHorrorMaterial.SetFloat("_Distortion", targetDistortion);

            if (healthRatio < 0.25f)
            {
                analogHorrorMaterial.SetColor("_VignetteColor", new Color(0.12f, 0.02f, 0.02f, 1.0f));
            }
            else
            {
                analogHorrorMaterial.SetColor("_VignetteColor", new Color(0.04f, 0.03f, 0.02f, 1.0f));
            }
        }
        #endregion

        #region Notes, Game Over & Victory Screens
        public void ShowNote(string title, string body)
        {
            if (notePanel == null) return;

            isReadingNote = true;
            notePanel.SetActive(true);
            if (noteTitleText != null) noteTitleText.text = title.ToUpper();
            if (noteBodyText != null) noteBodyText.text = body;

            Time.timeScale = 0f;
            if (playerController != null) playerController.LockCursor(false);
            SetCustomCursor(true);

            if (AudioManager.Instance != null && AudioManager.Instance.noteOpenClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.noteOpenClip, 0.7f);
            }
        }

        public void CloseNote()
        {
            if (notePanel == null) return;

            isReadingNote = false;
            notePanel.SetActive(false);
            Time.timeScale = 1f;
            if (playerController != null) playerController.LockCursor(true);
            SetCustomCursor(false);
        }

        public void ShowGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                StartCoroutine(FadePanelIn(gameOverPanel, 2.0f));
            }
            if (playerController != null) playerController.LockCursor(false);
            SetCustomCursor(true);
        }

        public void ShowVictory(string finalTime = null)
        {
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                if (victoryTimeText != null && !string.IsNullOrEmpty(finalTime))
                {
                    victoryTimeText.text = $"TIME ELAPSED: {finalTime}";
                }
                StartCoroutine(FadePanelIn(victoryPanel, 1.8f));
            }
            if (playerController != null) playerController.LockCursor(false);
            SetCustomCursor(true);
        }

        private IEnumerator FadePanelIn(GameObject panel, float duration)
        {
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                cg.alpha = Mathf.Clamp01(t);
                yield return null;
            }
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
        {
            if (cg == null) yield break;
            float startAlpha = cg.alpha;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.01f, duration);
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }
            cg.alpha = targetAlpha;
        }
        #endregion

        #region Procedural PSX Horror UI Hierarchy Builder
        private void BuildBackroomsUIHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("HUDCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasGO.AddComponent<GraphicRaycaster>();
                transform.SetParent(canvasGO.transform, false);
            }

            Font font = GetTerminalFont();

            // 1. Fullscreen Analog Horror Overlay (Scanlines, Film Grain, Vignette)
            if (analogHorrorOverlay == null)
            {
                GameObject overlayGO = new GameObject("AnalogHorrorOverlay");
                overlayGO.transform.SetParent(canvas.transform, false);
                analogHorrorOverlay = overlayGO.AddComponent<Image>();
                analogHorrorOverlay.raycastTarget = false;

                Shader shader = Shader.Find("UI/AnalogHorrorUI");
                if (shader != null)
                {
                    analogHorrorMaterial = new Material(shader);
                    analogHorrorOverlay.material = analogHorrorMaterial;
                }
                else
                {
                    analogHorrorOverlay.color = new Color(0.04f, 0.03f, 0.02f, 0.15f);
                }

                RectTransform rt = overlayGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
            }

            // 2. Damage Flash Overlay
            if (damageFlashOverlay == null)
            {
                GameObject dfGO = new GameObject("DamageFlashOverlay");
                dfGO.transform.SetParent(canvas.transform, false);
                damageFlashOverlay = dfGO.AddComponent<Image>();
                damageFlashOverlay.color = new Color(0.7f, 0.1f, 0.1f, 0f);
                damageFlashOverlay.raycastTarget = false;
                RectTransform rt = dfGO.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
            }

            // 3. Center Crosshair (Tiny 4x4 px dot)
            if (crosshairImage == null)
            {
                GameObject chGO = new GameObject("CrosshairDot");
                chGO.transform.SetParent(canvas.transform, false);
                crosshairImage = chGO.AddComponent<Image>();
                crosshairImage.color = new Color(0.90f, 0.87f, 0.78f, 0.45f);
                crosshairImage.raycastTarget = false;
                RectTransform rt = chGO.GetComponent<RectTransform>();
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(4f, 4f);
            }

            // 4. Center Interaction Prompt ([E] \n INTERACT)
            if (promptText == null)
            {
                GameObject promptRoot = new GameObject("InteractionPromptContainer");
                promptRoot.transform.SetParent(canvas.transform, false);
                promptCanvasGroup = promptRoot.AddComponent<CanvasGroup>();
                promptCanvasGroup.alpha = 0f;

                SetupSlicedImage(promptRoot, psxPanelSprite, Color.white);

                RectTransform prt = promptRoot.GetComponent<RectTransform>();
                prt.anchoredPosition = new Vector2(0f, -60f);
                prt.sizeDelta = new Vector2(210f, 48f);

                // Icon
                GameObject pIconGO = new GameObject("PromptIcon");
                pIconGO.transform.SetParent(promptRoot.transform, false);
                promptIconImage = pIconGO.AddComponent<Image>();
                promptIconImage.sprite = psxPointerIcon;
                promptIconImage.color = ColorPaperOffWhite;
                promptIconImage.raycastTarget = false;
                RectTransform pirt = pIconGO.GetComponent<RectTransform>();
                pirt.anchorMin = new Vector2(0f, 0.5f);
                pirt.anchorMax = new Vector2(0f, 0.5f);
                pirt.pivot = new Vector2(0f, 0.5f);
                pirt.anchoredPosition = new Vector2(10f, 0f);
                pirt.sizeDelta = new Vector2(22f, 22f);

                // Text
                GameObject pTextGO = new GameObject("PromptText");
                pTextGO.transform.SetParent(promptRoot.transform, false);
                promptText = pTextGO.AddComponent<Text>();
                promptText.font = font;
                promptText.fontSize = 12;
                promptText.fontStyle = FontStyle.Bold;
                promptText.alignment = TextAnchor.MiddleLeft;
                promptText.color = ColorPaperOffWhite;
                promptText.lineSpacing = 1.05f;
                promptText.raycastTarget = false;

                RectTransform trt = pTextGO.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0f, 0.5f);
                trt.anchoredPosition = new Vector2(38f, 0f);
                trt.sizeDelta = new Vector2(-46f, 0f);
            }

            // 5. TOP LEFT: Minimalist Objective Panel
            if (objectiveHeaderText == null || objectiveBodyText == null)
            {
                GameObject objPanel = new GameObject("ObjectiveTerminalPanel");
                objPanel.transform.SetParent(canvas.transform, false);

                SetupSlicedImage(objPanel, psxPanelSprite, Color.white);

                RectTransform prt = objPanel.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0f, 1f);
                prt.anchorMax = new Vector2(0f, 1f);
                prt.pivot = new Vector2(0f, 1f);
                prt.anchoredPosition = new Vector2(24f, -24f);
                prt.sizeDelta = new Vector2(350f, 56f);

                // Header Text
                GameObject headerGO = new GameObject("ObjectiveHeader");
                headerGO.transform.SetParent(objPanel.transform, false);
                objectiveHeaderText = headerGO.AddComponent<Text>();
                objectiveHeaderText.font = font;
                objectiveHeaderText.fontSize = 11;
                objectiveHeaderText.fontStyle = FontStyle.Bold;
                objectiveHeaderText.alignment = TextAnchor.UpperLeft;
                objectiveHeaderText.color = ColorMutedTan;
                objectiveHeaderText.text = "OBJECTIVE";
                objectiveHeaderText.raycastTarget = false;
                RectTransform hrt = headerGO.GetComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(0f, 1f);
                hrt.anchoredPosition = new Vector2(12f, -8f);
                hrt.sizeDelta = new Vector2(-24f, 16f);

                // Body Text
                GameObject bodyGO = new GameObject("ObjectiveBody");
                bodyGO.transform.SetParent(objPanel.transform, false);
                objectiveBodyText = bodyGO.AddComponent<Text>();
                objectiveBodyText.font = font;
                objectiveBodyText.fontSize = 13;
                objectiveBodyText.fontStyle = FontStyle.Bold;
                objectiveBodyText.alignment = TextAnchor.UpperLeft;
                objectiveBodyText.color = ColorPaperOffWhite;
                objectiveBodyText.text = "FIND A WAY OUT.";
                objectiveBodyText.raycastTarget = false;
                RectTransform brt = bodyGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.pivot = new Vector2(0f, 0f);
                brt.anchoredPosition = new Vector2(12f, 8f);
                brt.sizeDelta = new Vector2(-24f, -26f);
            }

            // 6. TOP RIGHT: Emergency System Timer
            if (timerHeaderText == null || timerBodyText == null)
            {
                GameObject timerPanel = new GameObject("TimerTerminalPanel");
                timerPanel.transform.SetParent(canvas.transform, false);

                SetupSlicedImage(timerPanel, psxPanelSprite, Color.white);

                RectTransform prt = timerPanel.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(1f, 1f);
                prt.anchorMax = new Vector2(1f, 1f);
                prt.pivot = new Vector2(1f, 1f);
                prt.anchoredPosition = new Vector2(-24f, -24f);
                prt.sizeDelta = new Vector2(170f, 54f);

                GameObject headerGO = new GameObject("TimerHeader");
                headerGO.transform.SetParent(timerPanel.transform, false);
                timerHeaderText = headerGO.AddComponent<Text>();
                timerHeaderText.font = font;
                timerHeaderText.fontSize = 10;
                timerHeaderText.fontStyle = FontStyle.Bold;
                timerHeaderText.alignment = TextAnchor.UpperRight;
                timerHeaderText.color = ColorMutedTan;
                timerHeaderText.text = "TIME REMAINING";
                timerHeaderText.raycastTarget = false;
                RectTransform hrt = headerGO.GetComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(1f, 1f);
                hrt.anchoredPosition = new Vector2(-12f, -8f);
                hrt.sizeDelta = new Vector2(-24f, 14f);

                GameObject bodyGO = new GameObject("TimerBody");
                bodyGO.transform.SetParent(timerPanel.transform, false);
                timerBodyText = bodyGO.AddComponent<Text>();
                timerBodyText.font = font;
                timerBodyText.fontSize = 17;
                timerBodyText.fontStyle = FontStyle.Bold;
                timerBodyText.alignment = TextAnchor.MiddleRight;
                timerBodyText.color = ColorPaperOffWhite;
                timerBodyText.text = "10:00";
                timerBodyText.raycastTarget = false;
                RectTransform brt = bodyGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.pivot = new Vector2(1f, 0f);
                brt.anchoredPosition = new Vector2(-12f, 6f);
                brt.sizeDelta = new Vector2(-24f, -22f);
            }

            // 7. BOTTOM LEFT: PSX Horror ECG Pulse & Health Monitor
            if (healthHeaderText == null || healthBodyText == null)
            {
                GameObject hpPanel = new GameObject("HealthTerminalPanel");
                hpPanel.transform.SetParent(canvas.transform, false);

                SetupSlicedImage(hpPanel, psxPanelSprite, Color.white);

                RectTransform prt = hpPanel.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0f, 0f);
                prt.anchorMax = new Vector2(0f, 0f);
                prt.pivot = new Vector2(0f, 0f);
                prt.anchoredPosition = new Vector2(24f, 24f);
                prt.sizeDelta = new Vector2(230f, 66f);

                // ECG Waveform Image
                GameObject ecgGO = new GameObject("ECGWaveform");
                ecgGO.transform.SetParent(hpPanel.transform, false);
                ecgWaveformImage = ecgGO.AddComponent<Image>();
                ecgWaveformImage.sprite = psxEcgFineSprite;
                ecgWaveformImage.color = ColorSystemGreen;
                ecgWaveformImage.raycastTarget = false;
                RectTransform ecgrt = ecgGO.GetComponent<RectTransform>();
                ecgrt.anchorMin = new Vector2(0f, 0.5f);
                ecgrt.anchorMax = new Vector2(0f, 0.5f);
                ecgrt.pivot = new Vector2(0.5f, 0.5f);
                ecgrt.anchoredPosition = new Vector2(34f, 0f);
                ecgrt.sizeDelta = new Vector2(50f, 34f);

                // Condition Header
                GameObject headerGO = new GameObject("HealthHeader");
                headerGO.transform.SetParent(hpPanel.transform, false);
                healthHeaderText = headerGO.AddComponent<Text>();
                healthHeaderText.font = font;
                healthHeaderText.fontSize = 11;
                healthHeaderText.fontStyle = FontStyle.Bold;
                healthHeaderText.alignment = TextAnchor.UpperLeft;
                healthHeaderText.color = ColorSystemGreen;
                healthHeaderText.text = "CONDITION: FINE";
                healthHeaderText.raycastTarget = false;
                RectTransform hrt = headerGO.GetComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(0f, 1f);
                hrt.anchoredPosition = new Vector2(66f, -10f);
                hrt.sizeDelta = new Vector2(-76f, 16f);

                // Health Progress Bar Background
                GameObject barBgGO = new GameObject("HealthBarBg");
                barBgGO.transform.SetParent(hpPanel.transform, false);
                Image barBgImg = barBgGO.AddComponent<Image>();
                barBgImg.sprite = psxProgressBgSprite;
                barBgImg.type = Image.Type.Sliced;
                barBgImg.color = Color.white;
                barBgImg.raycastTarget = false;
                RectTransform bbrt = barBgGO.GetComponent<RectTransform>();
                bbrt.anchorMin = new Vector2(0f, 0f);
                bbrt.anchorMax = new Vector2(1f, 0f);
                bbrt.pivot = new Vector2(0f, 0f);
                bbrt.anchoredPosition = new Vector2(66f, 16f);
                bbrt.sizeDelta = new Vector2(-78f, 12f);

                // Health Progress Bar Fill
                GameObject barFillGO = new GameObject("HealthBarFill");
                barFillGO.transform.SetParent(barBgGO.transform, false);
                healthBarFillImage = barFillGO.AddComponent<Image>();
                healthBarFillImage.sprite = psxBarFineSprite != null ? psxBarFineSprite : psxProgressFillSprite;
                healthBarFillImage.type = Image.Type.Filled;
                healthBarFillImage.fillMethod = Image.FillMethod.Horizontal;
                healthBarFillImage.fillAmount = 1.0f;
                healthBarFillImage.color = ColorSystemGreen;
                healthBarFillImage.raycastTarget = false;
                RectTransform bfrt = barFillGO.GetComponent<RectTransform>();
                bfrt.anchorMin = Vector2.zero;
                bfrt.anchorMax = Vector2.one;
                bfrt.sizeDelta = Vector2.zero;

                // Health numeric body (subtle)
                GameObject bodyGO = new GameObject("HealthBody");
                bodyGO.transform.SetParent(hpPanel.transform, false);
                healthBodyText = bodyGO.AddComponent<Text>();
                healthBodyText.font = font;
                healthBodyText.fontSize = 10;
                healthBodyText.alignment = TextAnchor.LowerRight;
                healthBodyText.color = ColorMutedTan;
                healthBodyText.text = "100 HP";
                healthBodyText.raycastTarget = false;
                RectTransform brt = bodyGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 0f);
                brt.pivot = new Vector2(1f, 0f);
                brt.anchoredPosition = new Vector2(-12f, 4f);
                brt.sizeDelta = new Vector2(-78f, 12f);
            }

            // 8. BOTTOM RIGHT: Flashlight & Defense Ammo Equipment Status
            if (batteryBodyText == null || ammoBodyText == null)
            {
                // Ammo Panel
                GameObject ammoPanel = new GameObject("AmmoTerminalPanel");
                ammoPanel.transform.SetParent(canvas.transform, false);

                SetupSlicedImage(ammoPanel, psxPanelSprite, Color.white);

                RectTransform aprt = ammoPanel.GetComponent<RectTransform>();
                aprt.anchorMin = new Vector2(1f, 0f);
                aprt.anchorMax = new Vector2(1f, 0f);
                aprt.pivot = new Vector2(1f, 0f);
                aprt.anchoredPosition = new Vector2(-24f, 86f);
                aprt.sizeDelta = new Vector2(210f, 52f);

                // Handgun Icon
                GameObject pistolGO = new GameObject("PistolIcon");
                pistolGO.transform.SetParent(ammoPanel.transform, false);
                pistolIconImage = pistolGO.AddComponent<Image>();
                pistolIconImage.sprite = psxPistolIcon;
                pistolIconImage.color = ColorPaperOffWhite;
                pistolIconImage.raycastTarget = false;
                RectTransform pirt = pistolGO.GetComponent<RectTransform>();
                pirt.anchorMin = new Vector2(0f, 0.5f);
                pirt.anchorMax = new Vector2(0f, 0.5f);
                pirt.pivot = new Vector2(0f, 0.5f);
                pirt.anchoredPosition = new Vector2(12f, 0f);
                pirt.sizeDelta = new Vector2(34f, 34f);

                GameObject aHeaderGO = new GameObject("AmmoHeader");
                aHeaderGO.transform.SetParent(ammoPanel.transform, false);
                ammoHeaderText = aHeaderGO.AddComponent<Text>();
                ammoHeaderText.font = font;
                ammoHeaderText.fontSize = 10;
                ammoHeaderText.fontStyle = FontStyle.Bold;
                ammoHeaderText.alignment = TextAnchor.UpperRight;
                ammoHeaderText.color = ColorMutedTan;
                ammoHeaderText.text = "DEFENSE AMMO";
                ammoHeaderText.raycastTarget = false;
                RectTransform ahrt = aHeaderGO.GetComponent<RectTransform>();
                ahrt.anchorMin = new Vector2(0f, 1f);
                ahrt.anchorMax = new Vector2(1f, 1f);
                ahrt.pivot = new Vector2(1f, 1f);
                ahrt.anchoredPosition = new Vector2(-12f, -8f);
                ahrt.sizeDelta = new Vector2(-54f, 14f);

                GameObject aBodyGO = new GameObject("AmmoBody");
                aBodyGO.transform.SetParent(ammoPanel.transform, false);
                ammoBodyText = aBodyGO.AddComponent<Text>();
                ammoBodyText.font = font;
                ammoBodyText.fontSize = 15;
                ammoBodyText.fontStyle = FontStyle.Bold;
                ammoBodyText.alignment = TextAnchor.LowerRight;
                ammoBodyText.color = ColorPaperOffWhite;
                ammoBodyText.text = "06 / 06";
                ammoBodyText.raycastTarget = false;
                RectTransform abrt = aBodyGO.GetComponent<RectTransform>();
                abrt.anchorMin = new Vector2(0f, 0f);
                abrt.anchorMax = new Vector2(1f, 1f);
                abrt.pivot = new Vector2(1f, 0f);
                abrt.anchoredPosition = new Vector2(-12f, 6f);
                abrt.sizeDelta = new Vector2(-54f, -20f);

                // Flashlight Panel
                GameObject battPanel = new GameObject("FlashlightTerminalPanel");
                battPanel.transform.SetParent(canvas.transform, false);

                SetupSlicedImage(battPanel, psxPanelSprite, Color.white);

                RectTransform bprt = battPanel.GetComponent<RectTransform>();
                bprt.anchorMin = new Vector2(1f, 0f);
                bprt.anchorMax = new Vector2(1f, 0f);
                bprt.pivot = new Vector2(1f, 0f);
                bprt.anchoredPosition = new Vector2(-24f, 24f);
                bprt.sizeDelta = new Vector2(210f, 54f);

                GameObject bHeaderGO = new GameObject("BatteryHeader");
                bHeaderGO.transform.SetParent(battPanel.transform, false);
                batteryHeaderText = bHeaderGO.AddComponent<Text>();
                batteryHeaderText.font = font;
                batteryHeaderText.fontSize = 10;
                batteryHeaderText.fontStyle = FontStyle.Bold;
                batteryHeaderText.alignment = TextAnchor.UpperLeft;
                batteryHeaderText.color = ColorMutedTan;
                batteryHeaderText.text = "FLASHLIGHT";
                batteryHeaderText.raycastTarget = false;
                RectTransform bhrt = bHeaderGO.GetComponent<RectTransform>();
                bhrt.anchorMin = new Vector2(0f, 1f);
                bhrt.anchorMax = new Vector2(1f, 1f);
                bhrt.pivot = new Vector2(0f, 1f);
                bhrt.anchoredPosition = new Vector2(12f, -8f);
                bhrt.sizeDelta = new Vector2(-24f, 14f);

                // Battery Progress Bar Background
                GameObject battBarBg = new GameObject("BatteryBarBg");
                battBarBg.transform.SetParent(battPanel.transform, false);
                Image battBgImg = battBarBg.AddComponent<Image>();
                battBgImg.sprite = psxProgressBgSprite;
                battBgImg.type = Image.Type.Sliced;
                battBgImg.color = Color.white;
                battBgImg.raycastTarget = false;
                RectTransform bbart = battBarBg.GetComponent<RectTransform>();
                bbart.anchorMin = new Vector2(0f, 0f);
                bbart.anchorMax = new Vector2(1f, 0f);
                bbart.pivot = new Vector2(0f, 0f);
                bbart.anchoredPosition = new Vector2(12f, 12f);
                bbart.sizeDelta = new Vector2(-70f, 12f);

                // Battery Progress Bar Fill
                GameObject battBarFill = new GameObject("BatteryBarFill");
                battBarFill.transform.SetParent(battBarBg.transform, false);
                batteryBarFillImage = battBarFill.AddComponent<Image>();
                batteryBarFillImage.sprite = psxProgressFillSprite;
                batteryBarFillImage.type = Image.Type.Filled;
                batteryBarFillImage.fillMethod = Image.FillMethod.Horizontal;
                batteryBarFillImage.fillAmount = 1.0f;
                batteryBarFillImage.color = ColorPaperOffWhite;
                batteryBarFillImage.raycastTarget = false;
                RectTransform bbfrt = battBarFill.GetComponent<RectTransform>();
                bbfrt.anchorMin = Vector2.zero;
                bbfrt.anchorMax = Vector2.one;
                bbfrt.sizeDelta = Vector2.zero;

                GameObject bBodyGO = new GameObject("BatteryBody");
                bBodyGO.transform.SetParent(battPanel.transform, false);
                batteryBodyText = bBodyGO.AddComponent<Text>();
                batteryBodyText.font = font;
                batteryBodyText.fontSize = 12;
                batteryBodyText.fontStyle = FontStyle.Bold;
                batteryBodyText.alignment = TextAnchor.MiddleRight;
                batteryBodyText.color = ColorPaperOffWhite;
                batteryBodyText.text = "100%";
                batteryBodyText.raycastTarget = false;
                RectTransform bbrt = bBodyGO.GetComponent<RectTransform>();
                bbrt.anchorMin = new Vector2(1f, 0f);
                bbrt.anchorMax = new Vector2(1f, 0f);
                bbrt.pivot = new Vector2(1f, 0f);
                bbrt.anchoredPosition = new Vector2(-12f, 10f);
                bbrt.sizeDelta = new Vector2(50f, 16f);
            }

            // 9. SYSTEM NOTIFICATION (Center-Bottom)
            if (notificationText == null)
            {
                GameObject notifRoot = new GameObject("NotificationRoot");
                notifRoot.transform.SetParent(canvas.transform, false);
                notificationCanvasGroup = notifRoot.AddComponent<CanvasGroup>();
                notificationCanvasGroup.alpha = 0f;

                SetupSlicedImage(notifRoot, psxPanelSprite, Color.white);

                RectTransform nrt = notifRoot.GetComponent<RectTransform>();
                nrt.anchoredPosition = new Vector2(0f, 105f);
                nrt.sizeDelta = new Vector2(380f, 58f);

                GameObject textGO = new GameObject("NotifText");
                textGO.transform.SetParent(notifRoot.transform, false);
                notificationText = textGO.AddComponent<Text>();
                notificationText.font = font;
                notificationText.fontSize = 12;
                notificationText.fontStyle = FontStyle.Bold;
                notificationText.alignment = TextAnchor.MiddleCenter;
                notificationText.color = ColorPaperOffWhite;
                notificationText.lineSpacing = 1.15f;
                notificationText.raycastTarget = false;

                RectTransform trt = textGO.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;
            }

            // 10. PAUSE MENU PANEL
            if (pausePanel == null)
            {
                pausePanel = BuildPauseMenu(canvas, font);
            }

            // 11. NOTE INSPECTION MODAL
            if (notePanel == null)
            {
                notePanel = BuildNoteModal(canvas, font);
            }

            // 12. GAME OVER PANEL
            if (gameOverPanel == null)
            {
                gameOverPanel = BuildGameOverScreen(canvas, font);
            }

            // 13. VICTORY PANEL
            if (victoryPanel == null)
            {
                victoryPanel = BuildVictoryScreen(canvas, font);
            }
        }

        private GameObject BuildPauseMenu(Canvas canvas, Font font)
        {
            GameObject menuRoot = new GameObject("PauseMenuPanel");
            menuRoot.transform.SetParent(canvas.transform, false);

            Image bg = menuRoot.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.03f, 0.92f);
            RectTransform mrt = menuRoot.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.sizeDelta = Vector2.zero;

            // PSX Window in center
            GameObject box = new GameObject("TerminalWindow");
            box.transform.SetParent(menuRoot.transform, false);
            SetupSlicedImage(box, psxWindowSprite, Color.white);
            RectTransform brt = box.GetComponent<RectTransform>();
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(480f, 380f);

            // Title
            GameObject titleGO = new GameObject("MenuTitle");
            titleGO.transform.SetParent(box.transform, false);
            Text title = titleGO.AddComponent<Text>();
            title.font = font;
            title.fontSize = 15;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = ColorSystemGreen;
            title.text = "SYSTEM PAUSED // LEVEL 0";
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 130f);
            trt.sizeDelta = new Vector2(440f, 40f);

            // PSX Styled Buttons
            CreatePSXButton(box, "ResumeBtn", new Vector2(0f, 45f), new Vector2(260f, 42f), "RESUME EXPLORATION", font, ResumeGame);
            CreatePSXButton(box, "RestartBtn", new Vector2(0f, -15f), new Vector2(260f, 42f), "RESTART SYSTEM", font, () => GameManager.Instance.RestartGame());
            CreatePSXButton(box, "ExitBtn", new Vector2(0f, -75f), new Vector2(260f, 42f), "TERMINATE FEED", font, () => Application.Quit());

            // Controls Hint Footer
            GameObject footerGO = new GameObject("ControlsFooter");
            footerGO.transform.SetParent(box.transform, false);
            Text footer = footerGO.AddComponent<Text>();
            footer.font = font;
            footer.fontSize = 11;
            footer.alignment = TextAnchor.MiddleCenter;
            footer.color = ColorMutedTan;
            footer.text = "WASD: NAVIGATE | F: LIGHT | LMB: DEFENSE | E: INTERACT";
            RectTransform frt = footerGO.GetComponent<RectTransform>();
            frt.anchoredPosition = new Vector2(0f, -145f);
            frt.sizeDelta = new Vector2(440f, 30f);

            menuRoot.SetActive(false);
            return menuRoot;
        }

        private GameObject BuildNoteModal(Canvas canvas, Font font)
        {
            GameObject modalRoot = new GameObject("NoteModalPanel");
            modalRoot.transform.SetParent(canvas.transform, false);

            Image bg = modalRoot.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.03f, 0.90f);
            RectTransform mrt = modalRoot.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.sizeDelta = Vector2.zero;

            GameObject doc = new GameObject("DocumentWindow");
            doc.transform.SetParent(modalRoot.transform, false);
            SetupSlicedImage(doc, psxDocumentSprite != null ? psxDocumentSprite : psxWindowSprite, Color.white);
            RectTransform drt = doc.GetComponent<RectTransform>();
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(600f, 400f);

            // Note Icon
            GameObject noteIconGO = new GameObject("NoteIcon");
            noteIconGO.transform.SetParent(doc.transform, false);
            noteIconImage = noteIconGO.AddComponent<Image>();
            noteIconImage.sprite = psxNoteIcon;
            noteIconImage.color = ColorPaperOffWhite;
            noteIconImage.raycastTarget = false;
            RectTransform nirt = noteIconGO.GetComponent<RectTransform>();
            nirt.anchoredPosition = new Vector2(-220f, 145f);
            nirt.sizeDelta = new Vector2(30f, 30f);

            GameObject titleGO = new GameObject("DocTitle");
            titleGO.transform.SetParent(doc.transform, false);
            noteTitleText = titleGO.AddComponent<Text>();
            noteTitleText.font = font;
            noteTitleText.fontSize = 14;
            noteTitleText.fontStyle = FontStyle.Bold;
            noteTitleText.alignment = TextAnchor.MiddleCenter;
            noteTitleText.color = ColorSystemGreen;
            noteTitleText.text = "FACILITY MAINTENANCE LOG";
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(20f, 145f);
            trt.sizeDelta = new Vector2(460f, 36f);

            GameObject bodyGO = new GameObject("DocBody");
            bodyGO.transform.SetParent(doc.transform, false);
            noteBodyText = bodyGO.AddComponent<Text>();
            noteBodyText.font = font;
            noteBodyText.fontSize = 13;
            noteBodyText.alignment = TextAnchor.UpperLeft;
            noteBodyText.color = ColorPaperOffWhite;
            noteBodyText.lineSpacing = 1.3f;
            noteBodyText.text = "...";
            RectTransform brt = bodyGO.GetComponent<RectTransform>();
            brt.anchoredPosition = new Vector2(0f, 5f);
            brt.sizeDelta = new Vector2(520f, 220f);

            GameObject hintGO = new GameObject("CloseHint");
            hintGO.transform.SetParent(doc.transform, false);
            Text hint = hintGO.AddComponent<Text>();
            hint.font = font;
            hint.fontSize = 11;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = ColorMutedTan;
            hint.text = "[E] / [ESC] DISMISS LOG";
            RectTransform hrt = hintGO.GetComponent<RectTransform>();
            hrt.anchoredPosition = new Vector2(0f, -155f);
            hrt.sizeDelta = new Vector2(500f, 30f);

            modalRoot.SetActive(false);
            return modalRoot;
        }

        private GameObject BuildGameOverScreen(Canvas canvas, Font font)
        {
            GameObject goRoot = new GameObject("GameOverPanel");
            goRoot.transform.SetParent(canvas.transform, false);

            Image bg = goRoot.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.015f, 0.98f);
            RectTransform rt = goRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            GameObject box = new GameObject("GameOverBox");
            box.transform.SetParent(goRoot.transform, false);
            SetupSlicedImage(box, psxWindowSprite, Color.white);
            RectTransform brt = box.GetComponent<RectTransform>();
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(520f, 360f);

            GameObject titleGO = new GameObject("GameOverTitle");
            titleGO.transform.SetParent(box.transform, false);
            gameOverTitleText = titleGO.AddComponent<Text>();
            gameOverTitleText.font = font;
            gameOverTitleText.fontSize = 20;
            gameOverTitleText.fontStyle = FontStyle.Bold;
            gameOverTitleText.alignment = TextAnchor.MiddleCenter;
            gameOverTitleText.color = ColorCriticalRed;
            gameOverTitleText.text = "CONNECTION LOST";
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 100f);
            trt.sizeDelta = new Vector2(460f, 36f);

            GameObject subGO = new GameObject("GameOverSub");
            subGO.transform.SetParent(box.transform, false);
            gameOverSubText = subGO.AddComponent<Text>();
            gameOverSubText.font = font;
            gameOverSubText.fontSize = 13;
            gameOverSubText.alignment = TextAnchor.MiddleCenter;
            gameOverSubText.color = ColorMutedTan;
            gameOverSubText.text = "YOU WERE NOT ALONE IN THE CORRIDORS.";
            RectTransform srt = subGO.GetComponent<RectTransform>();
            srt.anchoredPosition = new Vector2(0f, 50f);
            srt.sizeDelta = new Vector2(460f, 40f);

            CreatePSXButton(box, "RetryBtn", new Vector2(0f, -25f), new Vector2(240f, 42f), "RETRY", font, () => GameManager.Instance.RestartGame());
            CreatePSXButton(box, "MenuBtn", new Vector2(0f, -85f), new Vector2(240f, 42f), "MAIN MENU", font, () => SceneManager.LoadScene(0));

            goRoot.SetActive(false);
            return goRoot;
        }

        private GameObject BuildVictoryScreen(Canvas canvas, Font font)
        {
            GameObject vicRoot = new GameObject("VictoryPanel");
            vicRoot.transform.SetParent(canvas.transform, false);

            Image bg = vicRoot.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.02f, 0.015f, 0.98f);
            RectTransform rt = vicRoot.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            GameObject box = new GameObject("VictoryBox");
            box.transform.SetParent(vicRoot.transform, false);
            SetupSlicedImage(box, psxWindowSprite, Color.white);
            RectTransform brt = box.GetComponent<RectTransform>();
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(520f, 380f);

            GameObject titleGO = new GameObject("VictoryTitle");
            titleGO.transform.SetParent(box.transform, false);
            victoryTitleText = titleGO.AddComponent<Text>();
            victoryTitleText.font = font;
            victoryTitleText.fontSize = 20;
            victoryTitleText.fontStyle = FontStyle.Bold;
            victoryTitleText.alignment = TextAnchor.MiddleCenter;
            victoryTitleText.color = ColorSystemGreen;
            victoryTitleText.text = "EXIT FOUND";
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 110f);
            trt.sizeDelta = new Vector2(460f, 36f);

            GameObject subGO = new GameObject("VictorySub");
            subGO.transform.SetParent(box.transform, false);
            Text sub = subGO.AddComponent<Text>();
            sub.font = font;
            sub.fontSize = 13;
            sub.alignment = TextAnchor.MiddleCenter;
            sub.color = ColorPaperOffWhite;
            sub.text = "FACILITY SURVIVED // EXIT DOOR UNLOCKED";
            RectTransform srt = subGO.GetComponent<RectTransform>();
            srt.anchoredPosition = new Vector2(0f, 65f);
            srt.sizeDelta = new Vector2(460f, 36f);

            GameObject timeGO = new GameObject("VictoryTime");
            timeGO.transform.SetParent(box.transform, false);
            victoryTimeText = timeGO.AddComponent<Text>();
            victoryTimeText.font = font;
            victoryTimeText.fontSize = 13;
            victoryTimeText.alignment = TextAnchor.MiddleCenter;
            victoryTimeText.color = ColorMutedTan;
            victoryTimeText.text = "TIME ELAPSED: 00:00";
            RectTransform ttrt = timeGO.GetComponent<RectTransform>();
            ttrt.anchoredPosition = new Vector2(0f, 20f);
            ttrt.sizeDelta = new Vector2(460f, 26f);

            CreatePSXButton(box, "PlayAgainBtn", new Vector2(0f, -40f), new Vector2(240f, 42f), "CONTINUE", font, () => GameManager.Instance.RestartGame());
            CreatePSXButton(box, "MenuBtn", new Vector2(0f, -100f), new Vector2(240f, 42f), "MAIN MENU", font, () => SceneManager.LoadScene(0));

            vicRoot.SetActive(false);
            return vicRoot;
        }

        private void SetupSlicedImage(GameObject go, Sprite sprite, Color tint)
        {
            Image img = go.AddComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = tint;
            }
            else
            {
                img.color = ColorDarkPanel;
                CreateBorderOutline(go, ColorFadedBorder);
            }
            img.raycastTarget = false;
        }

        private GameObject CreatePSXButton(GameObject parent, string name, Vector2 pos, Vector2 size, string label, Font font, UnityAction onClick)
        {
            GameObject btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent.transform, false);

            Image img = btnGO.AddComponent<Image>();
            if (psxButtonNormalSprite != null)
            {
                img.sprite = psxButtonNormalSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.12f, 0.11f, 0.09f, 0.95f);
                CreateBorderOutline(btnGO, ColorFadedBorder);
            }

            Button btn = btnGO.AddComponent<Button>();
            if (psxButtonNormalSprite != null && psxButtonHoverSprite != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                SpriteState ss = new SpriteState();
                ss.highlightedSprite = psxButtonHoverSprite;
                ss.pressedSprite = psxButtonPressedSprite != null ? psxButtonPressedSprite : psxButtonHoverSprite;
                ss.selectedSprite = psxButtonHoverSprite;
                btn.spriteState = ss;
            }
            else
            {
                ColorBlock cb = btn.colors;
                cb.normalColor = new Color(0.12f, 0.11f, 0.09f, 0.95f);
                cb.highlightedColor = new Color(0.24f, 0.22f, 0.17f, 1f);
                cb.pressedColor = new Color(0.35f, 0.32f, 0.25f, 1f);
                cb.selectedColor = cb.highlightedColor;
                btn.colors = cb;
            }

            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayTerminalBeep(0.35f);
                }
                onClick?.Invoke();
            });

            RectTransform brt = btnGO.GetComponent<RectTransform>();
            brt.anchoredPosition = pos;
            brt.sizeDelta = size;

            GameObject textGO = new GameObject("Label");
            textGO.transform.SetParent(btnGO.transform, false);
            Text t = textGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = 12;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = ColorPaperOffWhite;
            t.text = label;
            t.raycastTarget = false;

            RectTransform trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;

            return btnGO;
        }

        private static void CreateBorderOutline(GameObject parent, Color borderColor)
        {
            GameObject borderGO = new GameObject("BorderOutline");
            borderGO.transform.SetParent(parent.transform, false);
            RectTransform brt = borderGO.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.sizeDelta = Vector2.zero;

            CreateLine(borderGO, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0f), new Vector2(0f, 1.2f), borderColor);
            CreateLine(borderGO, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1.2f), borderColor);
            CreateLine(borderGO, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(1.2f, 0f), borderColor);
            CreateLine(borderGO, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(1.2f, 0f), borderColor);
        }

        private static void CreateLine(GameObject parent, string name, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, Color color)
        {
            GameObject line = new GameObject("Line_" + name);
            line.transform.SetParent(parent.transform, false);
            Image img = line.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            RectTransform rt = line.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }
        #endregion
    }
}
