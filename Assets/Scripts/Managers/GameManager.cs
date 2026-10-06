using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HorrorEscape.Audio;
using HorrorEscape.UI;

namespace HorrorEscape.Managers
{
    /// <summary>
    /// Master Game Manager tracking inventory keys, quest progress,
    /// victory/defeat conditions, and scene reloading.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Objectives")]
        [SerializeField] private int requiredObjectiveCount = 3;
        private int currentObjectiveCount = 0;

        private readonly HashSet<string> collectedKeys = new HashSet<string>();
        private bool isGameOver = false;
        private bool isVictory = false;

        public int CurrentObjectiveCount => currentObjectiveCount;
        public int RequiredObjectiveCount => requiredObjectiveCount;
        public bool IsGameOver => isGameOver;
        public bool IsVictory => isVictory;

        public void SetRequiredObjectives(int count)
        {
            requiredObjectiveCount = count;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void AddKey(string keyId)
        {
            if (!string.IsNullOrEmpty(keyId) && !collectedKeys.Contains(keyId))
            {
                collectedKeys.Add(keyId);
            }
        }

        public bool HasKey(string keyId)
        {
            if (string.IsNullOrEmpty(keyId)) return true;
            return collectedKeys.Contains(keyId);
        }

        public void RegisterObjectiveProgress()
        {
            currentObjectiveCount++;
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.UpdateObjectiveText(currentObjectiveCount, requiredObjectiveCount);
            }
        }

        public void TriggerGameOver()
        {
            if (isGameOver || isVictory) return;
            isGameOver = true;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowGameOver();
            }
        }

        public void TriggerVictory()
        {
            if (isGameOver || isVictory) return;
            isVictory = true;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowVictory();
            }
        }

        public void RestartGame()
        {
            Time.timeScale = 1.0f;
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);
        }
    }
}
