using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorEscape.UI
{
    /// <summary>
    /// Subtle analog surveillance watermark for Backrooms exploration.
    /// Positioned unobtrusively at the bottom-left with faded alpha so it never
    /// obstructs gameplay, objectives, or the environment.
    /// </summary>
    public class VHSOverlay : MonoBehaviour
    {
        [Header("Surveillance Feed Settings")]
        [SerializeField] private string fixedDate = "1991-08-24";
        [SerializeField] private bool useFixedRetroDate = true;
        [SerializeField] private float blinkInterval = 0.9f;

        private Text vhsText;
        private float blinkTimer;
        private bool isDotVisible = true;
        private DateTime sessionStartTime;

        private void Awake()
        {
            sessionStartTime = DateTime.Now;
            EnsureVHSElements();
        }

        private void EnsureVHSElements()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Font retroFont = HUDManager.GetTerminalFont();

            // Unobtrusive bottom-left archive watermark above health panel
            GameObject vhsGO = new GameObject("VHS_SurveillanceWatermark");
            vhsGO.transform.SetParent(canvas.transform, false);

            vhsText = vhsGO.AddComponent<Text>();
            vhsText.font = retroFont;
            vhsText.fontSize = 11;
            vhsText.alignment = TextAnchor.LowerLeft;
            vhsText.color = new Color(0.75f, 0.72f, 0.62f, 0.42f); // Faded, non-intrusive
            vhsText.raycastTarget = false;

            RectTransform rt = vhsGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(28f, 82f);
            rt.sizeDelta = new Vector2(320f, 24f);
        }

        private void Update()
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= blinkInterval)
            {
                blinkTimer = 0f;
                isDotVisible = !isDotVisible;
            }

            if (vhsText != null)
            {
                string dot = isDotVisible ? "<color=#B84237>●</color>" : " ";
                TimeSpan elapsed = DateTime.Now - sessionStartTime;

                string dateStr = useFixedRetroDate ? fixedDate : DateTime.Now.ToString("yyyy-MM-dd");
                DateTime displayTime = DateTime.Parse(dateStr + " 03:14:02").Add(elapsed);

                vhsText.text = $"ARCHIVE FEED 04 // {displayTime:yyyy-MM-dd HH:mm:ss} [REC {dot}]";
            }
        }
    }
}
