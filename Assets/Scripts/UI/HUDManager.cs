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
    /// Centralized HUD Manager controlling the minimalist, psychological horror Backrooms UI.
    /// Emulates an authentic vintage maintenance/emergency terminal interface with:
    /// - Subtle off-white / yellowed wallpaper typography
    /// - Monospaced terminal font with dynamic typewriter reveals
    /// - 10-segment block meters (Health, Battery)
    /// - Low battery / low ammo subtle glitching & warnings
    /// - Atmospheric [E] interaction prompt with smooth fades
    /// - Subtle system notifications & emergency system timer
    /// - Fullscreen CRT scanlines, organic film grain, and dynamic vignette
    /// - Backrooms maintenance pause menu, defeat (Connection Lost), and escape screens
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        public static HUDManager Instance { get; private set; }

        #region UI Palette Colors
        // Backrooms Psychological Horror Palette
        public static readonly Color ColorPaperOffWhite = new Color(0.90f, 0.87f, 0.78f, 0.95f);
        public static readonly Color ColorMutedTan      = new Color(0.58f, 0.55f, 0.46f, 0.85f);
        public static readonly Color ColorFadedBorder   = new Color(0.24f, 0.22f, 0.17f, 0.85f);
        public static readonly Color ColorDarkPanel     = new Color(0.06f, 0.05f, 0.04f, 0.85f);
        public static readonly Color ColorSystemGreen   = new Color(0.46f, 0.68f, 0.50f, 0.95f);
        public static readonly Color ColorWarningAmber  = new Color(0.85f, 0.55f, 0.24f, 0.95f);
        public static readonly Color ColorCriticalRed   = new Color(0.72f, 0.26f, 0.22f, 0.95f);
        #endregion

        [Header("HUD References (Auto-Built if empty)")]
        [SerializeField] private Text promptText;
        [SerializeField] private CanvasGroup promptCanvasGroup;
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Text objectiveHeaderText;
        [SerializeField] private Text objectiveBodyText;
        [SerializeField] private Text timerHeaderText;
        [SerializeField] private Text timerBodyText;
        [SerializeField] private Text batteryHeaderText;
        [SerializeField] private Text batteryBodyText;
        [SerializeField] private Text ammoHeaderText;
        [SerializeField] private Text ammoBodyText;
        [SerializeField] private Text healthHeaderText;
        [SerializeField] private Text healthBodyText;
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
        private bool isReadingNote = false;
        public bool IsReadingNote => isReadingNote;

        [Header("End Game Screens")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text gameOverTitleText;
        [SerializeField] private Text gameOverSubText;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private Text victoryTitleText;
        [SerializeField] private Text victoryTimeText;

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

            EnsureEventSystem();
            BuildBackroomsUIHierarchy();
        }

        private void Start()
        {
            FindPlayerReferences();

            if (notePanel != null) notePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (victoryPanel != null) victoryPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (damageFlashOverlay != null) damageFlashOverlay.color = new Color(0.7f, 0.1f, 0.1f, 0f);

            int req = GameManager.Instance != null ? GameManager.Instance.RequiredObjectiveCount : 0;
            UpdateObjectiveText(0, req);
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
            HandlePauseInput();
            HandleNoteInput();
        }

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

        private void RenderFlashlightUI(float current, float max, bool isOn)
        {
            if (batteryBodyText == null) return;

            float pct = Mathf.Clamp01(current / Mathf.Max(1f, max));
            int pctInt = Mathf.CeilToInt(pct * 100f);
            int filledBlocks = Mathf.Clamp(Mathf.RoundToInt(pct * 10f), 0, 10);
            int emptyBlocks = 10 - filledBlocks;

            string bar = new string('█', filledBlocks) + new string('░', emptyBlocks);

            if (pct <= 0.05f)
            {
                // Critical Battery (<= 5%)
                if (batteryHeaderText != null)
                {
                    batteryHeaderText.text = "FLASHLIGHT // CRITICAL";
                    batteryHeaderText.color = ColorCriticalRed;
                }
                batteryBodyText.text = $"{bar} {pctInt:00}%\n<color=#B84237>LOW BATTERY</color>";
                batteryBodyText.color = ColorCriticalRed;
            }
            else if (pct <= 0.20f)
            {
                // Low Battery (<= 20%): Subtle glitch flicker
                bool flickerTick = (Mathf.FloorToInt(Time.time * 6f) % 2 == 0);
                if (batteryHeaderText != null)
                {
                    batteryHeaderText.text = "FLASHLIGHT";
                    batteryHeaderText.color = ColorWarningAmber;
                }
                batteryBodyText.text = $"{bar} {pctInt:00}%" + (flickerTick ? " !" : "");
                batteryBodyText.color = ColorWarningAmber;
            }
            else
            {
                // Normal Battery
                if (batteryHeaderText != null)
                {
                    batteryHeaderText.text = isOn ? "FLASHLIGHT" : "FLASHLIGHT [OFF]";
                    batteryHeaderText.color = ColorMutedTan;
                }
                batteryBodyText.text = $"{bar} {pctInt:00}%";
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

            if (healthBodyText == null) return;

            float pct = Mathf.Clamp01(current / Mathf.Max(1f, max));
            int filled = Mathf.Clamp(Mathf.RoundToInt(pct * 10f), 0, 10);
            int empty = 10 - filled;
            string bar = new string('█', filled) + new string('░', empty);

            if (pct <= 0.25f)
            {
                healthBodyText.text = $"{bar} {Mathf.CeilToInt(current):00}";
                healthBodyText.color = ColorCriticalRed;
                if (healthHeaderText != null) healthHeaderText.color = ColorCriticalRed;
            }
            else if (pct <= 0.60f)
            {
                healthBodyText.text = $"{bar} {Mathf.CeilToInt(current):00}";
                healthBodyText.color = ColorWarningAmber;
                if (healthHeaderText != null) healthHeaderText.color = ColorMutedTan;
            }
            else
            {
                healthBodyText.text = $"{bar}";
                healthBodyText.color = ColorSystemGreen;
                if (healthHeaderText != null) healthHeaderText.color = ColorMutedTan;
            }

            // Adjust audio heartbeat in AudioManager if low health
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
            }
            else if (current <= 2)
            {
                ammoBodyText.text = $"{current:00} / {reserve:00}";
                ammoBodyText.color = ColorWarningAmber;
                if (ammoHeaderText != null) ammoHeaderText.color = ColorMutedTan;
            }
            else
            {
                ammoBodyText.text = $"{current:00} / {reserve:00}";
                ammoBodyText.color = ColorPaperOffWhite;
                if (ammoHeaderText != null) ammoHeaderText.color = ColorMutedTan;
            }
        }

        public void UpdateTimer(string timerStr)
        {
            if (timerBodyText == null) return;

            // Strip prefix if string contains "TIME: "
            string cleanTime = timerStr.Replace("TIME: ", "").Replace("TIME REMAINING: ", "").Trim();
            timerBodyText.text = cleanTime;

            // Subtle urgency tint when under 1:30
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

        #endregion

        #region Interaction Prompt

        public void SetInteractionPrompt(string rawPrompt, bool visible)
        {
            if (promptText == null || promptCanvasGroup == null) return;

            if (!visible || string.IsNullOrEmpty(rawPrompt))
            {
                StartCoroutine(FadeCanvasGroup(promptCanvasGroup, 0f, 0.12f));
                return;
            }

            // Format raw text into atmospheric Backrooms format:
            // "[E] Pick up Flashlight Battery" -> "[E]\nPICK UP BATTERY"
            string upper = rawPrompt.ToUpper().Trim();
            string actionText = "INTERACT";

            if (upper.Contains("PICK UP") || upper.Contains("COLLECT"))
            {
                if (upper.Contains("BATTERY")) actionText = "PICK UP BATTERY";
                else if (upper.Contains("AMMO") || upper.Contains("AMMUNITION")) actionText = "PICK UP AMMUNITION";
                else if (upper.Contains("KEYCARD") || upper.Contains("KEY")) actionText = "ACQUIRE KEYCARD";
                else if (upper.Contains("ALMOND") || upper.Contains("WATER")) actionText = "PICK UP WATER";
                else if (upper.Contains("FIRST AID")) actionText = "PICK UP FIRST AID";
                else actionText = "PICK UP ITEM";
            }
            else if (upper.Contains("OPEN"))
            {
                actionText = "OPEN DOOR";
            }
            else if (upper.Contains("POWER") || upper.Contains("BREAKER") || upper.Contains("SWITCH"))
            {
                actionText = "RESTORE POWER";
            }
            else if (upper.Contains("EXIT") || upper.Contains("ESCAPE"))
            {
                actionText = "ESCAPE FACILITY";
            }
            else if (upper.Contains("READ") || upper.Contains("NOTE"))
            {
                actionText = "INSPECT LOG";
            }

            promptText.text = $"[E]\n{actionText}";
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
            // Normalize text: "OBJECTIVE: Find a way out." -> "FIND A WAY OUT."
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
            // The Backrooms UI replaces giant arcade popups with a subtle typewriter reveal in the top-left panel
            SetObjective(subtitle);
        }

        private IEnumerator AnimateObjectiveTypewriter(string fullText)
        {
            if (objectiveHeaderText != null)
            {
                objectiveHeaderText.text = "NEW OBJECTIVE //";
                objectiveHeaderText.color = ColorSystemGreen;
            }

            if (objectiveBodyText != null)
            {
                objectiveBodyText.text = "";
                objectiveBodyText.color = ColorPaperOffWhite;

                for (int i = 0; i <= fullText.Length; i++)
                {
                    objectiveBodyText.text = fullText.Substring(0, i) + (i < fullText.Length ? "█" : "");

                    if (i % 2 == 0 && AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayTypewriterClick(0.22f);
                    }

                    yield return new WaitForSeconds(0.024f);
                }
            }

            yield return new WaitForSeconds(2.5f);

            // Settle header back to clean OBJECTIVE label
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

            // Format into clean vintage terminal system announcement
            notificationText.text = $"--------------------------------\nSYSTEM\n{cleanMsg}\n--------------------------------";

            yield return StartCoroutine(FadeCanvasGroup(notificationCanvasGroup, 1f, 0.2f));

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTerminalBeep(0.3f);
            }

            yield return new WaitForSeconds(1.8f);

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

            // Health-based dynamic vignette
            float healthRatio = lastKnownMaxHealth > 0 ? (lastKnownHealth / lastKnownMaxHealth) : 1f;
            float healthVignetteBoost = Mathf.Lerp(0.35f, 0.0f, healthRatio);

            // Entity / damage interference boost
            float spike = Mathf.Clamp01(horrorSpikeTimer);

            float targetScanlines = baseScanlines + spike * 0.12f;
            float targetNoise = baseNoise + spike * 0.08f;
            float targetVignette = baseVignette + healthVignetteBoost + spike * 0.15f;
            float targetDistortion = spike * 0.012f;

            analogHorrorMaterial.SetFloat("_ScanlineIntensity", targetScanlines);
            analogHorrorMaterial.SetFloat("_NoiseIntensity", targetNoise);
            analogHorrorMaterial.SetFloat("_VignetteIntensity", targetVignette);
            analogHorrorMaterial.SetFloat("_Distortion", targetDistortion);

            // Subtle color tint when dying
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
        }

        public void ShowGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                StartCoroutine(FadePanelIn(gameOverPanel, 2.0f));
            }
            if (playerController != null) playerController.LockCursor(false);
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

        #region Backrooms UI Procedural Hierarchy Builder

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

                Image pBg = promptRoot.AddComponent<Image>();
                pBg.color = new Color(0.06f, 0.05f, 0.04f, 0.75f);
                pBg.raycastTarget = false;

                RectTransform prt = promptRoot.GetComponent<RectTransform>();
                prt.anchoredPosition = new Vector2(0f, -55f);
                prt.sizeDelta = new Vector2(200f, 44f);

                GameObject pTextGO = new GameObject("PromptText");
                pTextGO.transform.SetParent(promptRoot.transform, false);
                promptText = pTextGO.AddComponent<Text>();
                promptText.font = font;
                promptText.fontSize = 13;
                promptText.fontStyle = FontStyle.Bold;
                promptText.alignment = TextAnchor.MiddleCenter;
                promptText.color = ColorPaperOffWhite;
                promptText.lineSpacing = 1.05f;
                promptText.raycastTarget = false;

                RectTransform trt = pTextGO.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;
            }

            // 5. TOP LEFT: Minimalist Objective Panel
            if (objectiveHeaderText == null || objectiveBodyText == null)
            {
                GameObject objPanel = new GameObject("ObjectiveTerminalPanel");
                objPanel.transform.SetParent(canvas.transform, false);

                Image bg = objPanel.AddComponent<Image>();
                bg.color = ColorDarkPanel;
                bg.raycastTarget = false;

                RectTransform prt = objPanel.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0f, 1f);
                prt.anchorMax = new Vector2(0f, 1f);
                prt.pivot = new Vector2(0f, 1f);
                prt.anchoredPosition = new Vector2(28f, -28f);
                prt.sizeDelta = new Vector2(350f, 54f);

                // Thin Border Lines
                CreateBorderOutline(objPanel, ColorFadedBorder);

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
                brt.sizeDelta = new Vector2(-24f, -24f);
            }

            // 6. TOP RIGHT: Emergency System Timer
            if (timerHeaderText == null || timerBodyText == null)
            {
                GameObject timerPanel = new GameObject("TimerTerminalPanel");
                timerPanel.transform.SetParent(canvas.transform, false);

                Image bg = timerPanel.AddComponent<Image>();
                bg.color = ColorDarkPanel;
                bg.raycastTarget = false;

                RectTransform prt = timerPanel.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(1f, 1f);
                prt.anchorMax = new Vector2(1f, 1f);
                prt.pivot = new Vector2(1f, 1f);
                prt.anchoredPosition = new Vector2(-28f, -28f);
                prt.sizeDelta = new Vector2(160f, 50f);

                CreateBorderOutline(timerPanel, ColorFadedBorder);

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

            // 7. BOTTOM LEFT: Subtle Health Block Bar
            if (healthHeaderText == null || healthBodyText == null)
            {
                GameObject hpPanel = new GameObject("HealthTerminalPanel");
                hpPanel.transform.SetParent(canvas.transform, false);

                Image bg = hpPanel.AddComponent<Image>();
                bg.color = ColorDarkPanel;
                bg.raycastTarget = false;

                RectTransform prt = hpPanel.GetComponent<RectTransform>();
                prt.anchorMin = new Vector2(0f, 0f);
                prt.anchorMax = new Vector2(0f, 0f);
                prt.pivot = new Vector2(0f, 0f);
                prt.anchoredPosition = new Vector2(28f, 28f);
                prt.sizeDelta = new Vector2(170f, 48f);

                CreateBorderOutline(hpPanel, ColorFadedBorder);

                GameObject headerGO = new GameObject("HealthHeader");
                headerGO.transform.SetParent(hpPanel.transform, false);
                healthHeaderText = headerGO.AddComponent<Text>();
                healthHeaderText.font = font;
                healthHeaderText.fontSize = 10;
                healthHeaderText.fontStyle = FontStyle.Bold;
                healthHeaderText.alignment = TextAnchor.UpperLeft;
                healthHeaderText.color = ColorMutedTan;
                healthHeaderText.text = "HEALTH";
                healthHeaderText.raycastTarget = false;
                RectTransform hrt = headerGO.GetComponent<RectTransform>();
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(0f, 1f);
                hrt.anchoredPosition = new Vector2(10f, -6f);
                hrt.sizeDelta = new Vector2(-20f, 14f);

                GameObject bodyGO = new GameObject("HealthBody");
                bodyGO.transform.SetParent(hpPanel.transform, false);
                healthBodyText = bodyGO.AddComponent<Text>();
                healthBodyText.font = font;
                healthBodyText.fontSize = 14;
                healthBodyText.alignment = TextAnchor.LowerLeft;
                healthBodyText.color = ColorSystemGreen;
                healthBodyText.text = "██████████";
                healthBodyText.raycastTarget = false;
                RectTransform brt = bodyGO.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.pivot = new Vector2(0f, 0f);
                brt.anchoredPosition = new Vector2(10f, 6f);
                brt.sizeDelta = new Vector2(-20f, -18f);
            }

            // 8. BOTTOM RIGHT: Flashlight & Defense Ammo Equipment Status
            if (batteryBodyText == null || ammoBodyText == null)
            {
                // Ammo Panel
                GameObject ammoPanel = new GameObject("AmmoTerminalPanel");
                ammoPanel.transform.SetParent(canvas.transform, false);
                Image aBg = ammoPanel.AddComponent<Image>();
                aBg.color = ColorDarkPanel;
                aBg.raycastTarget = false;

                RectTransform aprt = ammoPanel.GetComponent<RectTransform>();
                aprt.anchorMin = new Vector2(1f, 0f);
                aprt.anchorMax = new Vector2(1f, 0f);
                aprt.pivot = new Vector2(1f, 0f);
                aprt.anchoredPosition = new Vector2(-28f, 82f);
                aprt.sizeDelta = new Vector2(190f, 46f);
                CreateBorderOutline(ammoPanel, ColorFadedBorder);

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
                ahrt.anchoredPosition = new Vector2(-10f, -6f);
                ahrt.sizeDelta = new Vector2(-20f, 14f);

                GameObject aBodyGO = new GameObject("AmmoBody");
                aBodyGO.transform.SetParent(ammoPanel.transform, false);
                ammoBodyText = aBodyGO.AddComponent<Text>();
                ammoBodyText.font = font;
                ammoBodyText.fontSize = 14;
                ammoBodyText.fontStyle = FontStyle.Bold;
                ammoBodyText.alignment = TextAnchor.LowerRight;
                ammoBodyText.color = ColorPaperOffWhite;
                ammoBodyText.text = "06 / 06";
                ammoBodyText.raycastTarget = false;
                RectTransform abrt = aBodyGO.GetComponent<RectTransform>();
                abrt.anchorMin = new Vector2(0f, 0f);
                abrt.anchorMax = new Vector2(1f, 1f);
                abrt.pivot = new Vector2(1f, 0f);
                abrt.anchoredPosition = new Vector2(-10f, 6f);
                abrt.sizeDelta = new Vector2(-20f, -18f);

                // Flashlight Panel
                GameObject battPanel = new GameObject("FlashlightTerminalPanel");
                battPanel.transform.SetParent(canvas.transform, false);
                Image bBg = battPanel.AddComponent<Image>();
                bBg.color = ColorDarkPanel;
                bBg.raycastTarget = false;

                RectTransform bprt = battPanel.GetComponent<RectTransform>();
                bprt.anchorMin = new Vector2(1f, 0f);
                bprt.anchorMax = new Vector2(1f, 0f);
                bprt.pivot = new Vector2(1f, 0f);
                bprt.anchoredPosition = new Vector2(-28f, 28f);
                bprt.sizeDelta = new Vector2(190f, 48f);
                CreateBorderOutline(battPanel, ColorFadedBorder);

                GameObject bHeaderGO = new GameObject("BatteryHeader");
                bHeaderGO.transform.SetParent(battPanel.transform, false);
                batteryHeaderText = bHeaderGO.AddComponent<Text>();
                batteryHeaderText.font = font;
                batteryHeaderText.fontSize = 10;
                batteryHeaderText.fontStyle = FontStyle.Bold;
                batteryHeaderText.alignment = TextAnchor.UpperRight;
                batteryHeaderText.color = ColorMutedTan;
                batteryHeaderText.text = "FLASHLIGHT";
                batteryHeaderText.raycastTarget = false;
                RectTransform bhrt = bHeaderGO.GetComponent<RectTransform>();
                bhrt.anchorMin = new Vector2(0f, 1f);
                bhrt.anchorMax = new Vector2(1f, 1f);
                bhrt.pivot = new Vector2(1f, 1f);
                bhrt.anchoredPosition = new Vector2(-10f, -6f);
                bhrt.sizeDelta = new Vector2(-20f, 14f);

                GameObject bBodyGO = new GameObject("BatteryBody");
                bBodyGO.transform.SetParent(battPanel.transform, false);
                batteryBodyText = bBodyGO.AddComponent<Text>();
                batteryBodyText.font = font;
                batteryBodyText.fontSize = 13;
                batteryBodyText.alignment = TextAnchor.LowerRight;
                batteryBodyText.color = ColorPaperOffWhite;
                batteryBodyText.text = "██████████ 100%";
                batteryBodyText.raycastTarget = false;
                RectTransform bbrt = bBodyGO.GetComponent<RectTransform>();
                bbrt.anchorMin = new Vector2(0f, 0f);
                bbrt.anchorMax = new Vector2(1f, 1f);
                bbrt.pivot = new Vector2(1f, 0f);
                bbrt.anchoredPosition = new Vector2(-10f, 6f);
                bbrt.sizeDelta = new Vector2(-20f, -18f);
            }

            // 9. SYSTEM NOTIFICATION (Center-Bottom)
            if (notificationText == null)
            {
                GameObject notifRoot = new GameObject("NotificationRoot");
                notifRoot.transform.SetParent(canvas.transform, false);
                notificationCanvasGroup = notifRoot.AddComponent<CanvasGroup>();
                notificationCanvasGroup.alpha = 0f;

                Image bg = notifRoot.AddComponent<Image>();
                bg.color = ColorDarkPanel;
                bg.raycastTarget = false;

                RectTransform nrt = notifRoot.GetComponent<RectTransform>();
                nrt.anchoredPosition = new Vector2(0f, 100f);
                nrt.sizeDelta = new Vector2(380f, 64f);
                CreateBorderOutline(notifRoot, ColorFadedBorder);

                GameObject textGO = new GameObject("NotifText");
                textGO.transform.SetParent(notifRoot.transform, false);
                notificationText = textGO.AddComponent<Text>();
                notificationText.font = font;
                notificationText.fontSize = 12;
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

            // 12. GAME OVER PANEL (Connection Lost)
            if (gameOverPanel == null)
            {
                gameOverPanel = BuildGameOverScreen(canvas, font);
            }

            // 13. VICTORY PANEL (Exit Found)
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

            // Terminal Box in center
            GameObject box = new GameObject("TerminalWindow");
            box.transform.SetParent(menuRoot.transform, false);
            Image boxBg = box.AddComponent<Image>();
            boxBg.color = ColorDarkPanel;
            RectTransform brt = box.GetComponent<RectTransform>();
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(500f, 400f);
            CreateBorderOutline(box, ColorFadedBorder);

            // Title
            GameObject titleGO = new GameObject("MenuTitle");
            titleGO.transform.SetParent(box.transform, false);
            Text title = titleGO.AddComponent<Text>();
            title.font = font;
            title.fontSize = 16;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = ColorSystemGreen;
            title.text = "--------------------------------\nSYSTEM PAUSED // SECTOR 0\n--------------------------------";
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 135f);
            trt.sizeDelta = new Vector2(460f, 60f);

            // Buttons
            CreateTerminalButton(box, "ResumeBtn", new Vector2(0f, 50f), "RESUME EXPLORATION", font, ResumeGame);
            CreateTerminalButton(box, "RestartBtn", new Vector2(0f, -10f), "RESTART SYSTEM", font, () => GameManager.Instance.RestartGame());
            CreateTerminalButton(box, "ExitBtn", new Vector2(0f, -70f), "TERMINATE FEED", font, () => Application.Quit());

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
            frt.sizeDelta = new Vector2(460f, 30f);

            menuRoot.SetActive(false);
            return menuRoot;
        }

        private GameObject BuildNoteModal(Canvas canvas, Font font)
        {
            GameObject modalRoot = new GameObject("NoteModalPanel");
            modalRoot.transform.SetParent(canvas.transform, false);

            Image bg = modalRoot.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.03f, 0.88f);
            RectTransform mrt = modalRoot.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.sizeDelta = Vector2.zero;

            GameObject doc = new GameObject("DocumentWindow");
            doc.transform.SetParent(modalRoot.transform, false);
            Image docBg = doc.AddComponent<Image>();
            docBg.color = ColorDarkPanel;
            RectTransform drt = doc.GetComponent<RectTransform>();
            drt.anchoredPosition = Vector2.zero;
            drt.sizeDelta = new Vector2(580f, 380f);
            CreateBorderOutline(doc, ColorFadedBorder);

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
            trt.anchoredPosition = new Vector2(0f, 140f);
            trt.sizeDelta = new Vector2(520f, 40f);

            GameObject bodyGO = new GameObject("DocBody");
            bodyGO.transform.SetParent(doc.transform, false);
            noteBodyText = bodyGO.AddComponent<Text>();
            noteBodyText.font = font;
            noteBodyText.fontSize = 13;
            noteBodyText.alignment = TextAnchor.UpperLeft;
            noteBodyText.color = ColorPaperOffWhite;
            noteBodyText.lineSpacing = 1.25f;
            noteBodyText.text = "...";
            RectTransform brt = bodyGO.GetComponent<RectTransform>();
            brt.anchoredPosition = new Vector2(0f, 0f);
            brt.sizeDelta = new Vector2(500f, 210f);

            GameObject hintGO = new GameObject("CloseHint");
            hintGO.transform.SetParent(doc.transform, false);
            Text hint = hintGO.AddComponent<Text>();
            hint.font = font;
            hint.fontSize = 11;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = ColorMutedTan;
            hint.text = "[E] / [ESC] DISMISS LOG";
            RectTransform hrt = hintGO.GetComponent<RectTransform>();
            hrt.anchoredPosition = new Vector2(0f, -145f);
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

            GameObject titleGO = new GameObject("GameOverTitle");
            titleGO.transform.SetParent(goRoot.transform, false);
            gameOverTitleText = titleGO.AddComponent<Text>();
            gameOverTitleText.font = font;
            gameOverTitleText.fontSize = 24;
            gameOverTitleText.fontStyle = FontStyle.Bold;
            gameOverTitleText.alignment = TextAnchor.MiddleCenter;
            gameOverTitleText.color = ColorPaperOffWhite;
            gameOverTitleText.text = "CONNECTION LOST";
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 80f);
            trt.sizeDelta = new Vector2(600f, 40f);

            GameObject subGO = new GameObject("GameOverSub");
            subGO.transform.SetParent(goRoot.transform, false);
            gameOverSubText = subGO.AddComponent<Text>();
            gameOverSubText.font = font;
            gameOverSubText.fontSize = 14;
            gameOverSubText.alignment = TextAnchor.MiddleCenter;
            gameOverSubText.color = ColorMutedTan;
            gameOverSubText.text = "--------------------------------\nYOU WERE NOT ALONE.\n--------------------------------";
            RectTransform srt = subGO.GetComponent<RectTransform>();
            srt.anchoredPosition = new Vector2(0f, 30f);
            srt.sizeDelta = new Vector2(600f, 50f);

            CreateTerminalButton(goRoot, "RetryBtn", new Vector2(0f, -40f), "RETRY", font, () => GameManager.Instance.RestartGame());
            CreateTerminalButton(goRoot, "MenuBtn", new Vector2(0f, -100f), "MAIN MENU", font, () => SceneManager.LoadScene(0));

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

            GameObject titleGO = new GameObject("VictoryTitle");
            titleGO.transform.SetParent(vicRoot.transform, false);
            victoryTitleText = titleGO.AddComponent<Text>();
            victoryTitleText.font = font;
            victoryTitleText.fontSize = 24;
            victoryTitleText.fontStyle = FontStyle.Bold;
            victoryTitleText.alignment = TextAnchor.MiddleCenter;
            victoryTitleText.color = ColorSystemGreen;
            victoryTitleText.text = "EXIT FOUND";
            RectTransform trt = titleGO.GetComponent<RectTransform>();
            trt.anchoredPosition = new Vector2(0f, 90f);
            trt.sizeDelta = new Vector2(600f, 40f);

            GameObject subGO = new GameObject("VictorySub");
            subGO.transform.SetParent(vicRoot.transform, false);
            Text sub = subGO.AddComponent<Text>();
            sub.font = font;
            sub.fontSize = 14;
            sub.alignment = TextAnchor.MiddleCenter;
            sub.color = ColorPaperOffWhite;
            sub.text = "--------------------------------\nYOU ESCAPED.\n--------------------------------";
            RectTransform srt = subGO.GetComponent<RectTransform>();
            srt.anchoredPosition = new Vector2(0f, 45f);
            srt.sizeDelta = new Vector2(600f, 50f);

            GameObject timeGO = new GameObject("VictoryTime");
            timeGO.transform.SetParent(vicRoot.transform, false);
            victoryTimeText = timeGO.AddComponent<Text>();
            victoryTimeText.font = font;
            victoryTimeText.fontSize = 14;
            victoryTimeText.alignment = TextAnchor.MiddleCenter;
            victoryTimeText.color = ColorMutedTan;
            victoryTimeText.text = "TIME ELAPSED: 00:00";
            RectTransform ttrt = timeGO.GetComponent<RectTransform>();
            ttrt.anchoredPosition = new Vector2(0f, -5f);
            ttrt.sizeDelta = new Vector2(600f, 30f);

            CreateTerminalButton(vicRoot, "PlayAgainBtn", new Vector2(0f, -60f), "CONTINUE", font, () => GameManager.Instance.RestartGame());
            CreateTerminalButton(vicRoot, "MenuBtn", new Vector2(0f, -120f), "MAIN MENU", font, () => SceneManager.LoadScene(0));

            vicRoot.SetActive(false);
            return vicRoot;
        }

        private static void CreateBorderOutline(GameObject parent, Color borderColor)
        {
            GameObject borderGO = new GameObject("BorderOutline");
            borderGO.transform.SetParent(parent.transform, false);
            RectTransform brt = borderGO.AddComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.sizeDelta = Vector2.zero;

            // 4 edges as thin 1px images
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

        private static void CreateTerminalButton(GameObject parent, string name, Vector2 pos, string label, Font font, UnityAction onClick)
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
            brt.sizeDelta = new Vector2(280f, 42f);
            CreateBorderOutline(btnGO, ColorFadedBorder);

            GameObject textGO = new GameObject("BtnText");
            textGO.transform.SetParent(btnGO.transform, false);
            Text t = textGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = 13;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = ColorPaperOffWhite;
            t.text = $"[ {label} ]";
            t.raycastTarget = false;
            RectTransform trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.sizeDelta = Vector2.zero;
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
