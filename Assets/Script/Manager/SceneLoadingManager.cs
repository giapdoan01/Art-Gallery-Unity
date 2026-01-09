using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SceneLoadingManager - Quản lý loading panel cho toàn bộ scene
/// Chờ tất cả managers load xong (ArtPrefabManager, Model3DPrefabManager) trước khi tắt loading
/// ✅ BASED ON ACTUAL CODE: Sử dụng currentLoadingCount và frameInstances/modelInstances có sẵn
/// </summary>
public class SceneLoadingManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Settings")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private float minLoadingTime = 1f; // Minimum time to show loading
    [SerializeField] private float checkInterval = 0.2f; // Check every 0.2s
    [SerializeField] private float initialDelay = 0.5f; // Wait for managers to start loading

    [Header("Loading Messages")]
    [SerializeField] private string[] loadingMessages = new string[]
    {
        "Loading artworks...",
        "Loading 3D models...",
        "Preparing scene...",
        "Almost done..."
    };

    private bool isLoading = true;
    private float loadingStartTime;
    private bool artManagerStartedLoading = false;
    private bool modelManagerStartedLoading = false;

    private void Start()
    {
        loadingStartTime = Time.time;

        // Show loading panel
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        if (showDebug)
            Debug.Log("[SceneLoadingManager] 🔄 Scene loading started...");

        // Start loading check
        StartCoroutine(CheckLoadingProgress());
    }

    /// <summary>
    /// Main coroutine - Check loading progress của tất cả managers
    /// </summary>
    private IEnumerator CheckLoadingProgress()
    {
        // Wait for managers to initialize and start loading
        yield return new WaitForSeconds(initialDelay);

        int totalSteps = 0;
        int completedSteps = 0;

        // Count total managers to wait for
        bool hasArtManager = ArtPrefabManager.Instance != null;
        bool hasModelManager = Model3DPrefabManager.Instance != null;

        if (hasArtManager) totalSteps++;
        if (hasModelManager) totalSteps++;

        if (totalSteps == 0)
        {
            if (showDebug)
                Debug.LogWarning("[SceneLoadingManager] No managers found, hiding loading immediately");
            
            HideLoading();
            yield break;
        }

        if (showDebug)
            Debug.Log($"[SceneLoadingManager] Waiting for {totalSteps} managers to complete loading...");

        // Loop until all managers finish loading
        while (isLoading)
        {
            completedSteps = 0;

            // Check ArtPrefabManager
            if (hasArtManager)
            {
                if (IsArtPrefabManagerReady())
                {
                    completedSteps++;
                }
            }

            // Check Model3DPrefabManager
            if (hasModelManager)
            {
                if (IsModel3DPrefabManagerReady())
                {
                    completedSteps++;
                }
            }

            // Calculate progress
            float progress = totalSteps > 0 ? (float)completedSteps / totalSteps : 1f;

            // Update UI
            UpdateLoadingUI(progress, completedSteps, totalSteps);

            // Check if all completed
            if (completedSteps >= totalSteps)
            {
                // Ensure minimum loading time
                float elapsedTime = Time.time - loadingStartTime;
                if (elapsedTime < minLoadingTime)
                {
                    yield return new WaitForSeconds(minLoadingTime - elapsedTime);
                }

                if (showDebug)
                    Debug.Log($"[SceneLoadingManager] ✅ All managers loaded! ({completedSteps}/{totalSteps})");

                isLoading = false;
                break;
            }

            // Wait before next check
            yield return new WaitForSeconds(checkInterval);
        }

        // Hide loading panel
        HideLoading();
    }

    /// <summary>
    /// ✅ Check if ArtPrefabManager has finished loading
    /// Uses reflection to access private currentLoadingCount field
    /// </summary>
    private bool IsArtPrefabManagerReady()
    {
        if (ArtPrefabManager.Instance == null)
            return false;

        try
        {
            // Access private field using reflection
            var type = typeof(ArtPrefabManager);
            var field = type.GetField("currentLoadingCount", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                int loadingCount = (int)field.GetValue(ArtPrefabManager.Instance);
                
                // Check if started loading (to avoid false positive at start)
                if (loadingCount > 0)
                {
                    artManagerStartedLoading = true;
                }

                bool isReady = artManagerStartedLoading && loadingCount == 0;

                if (showDebug && isReady && !artManagerStartedLoading)
                {
                    Debug.Log($"[SceneLoadingManager] ✅ ArtPrefabManager ready");
                }

                return isReady;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SceneLoadingManager] Error checking ArtPrefabManager: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// ✅ Check if Model3DPrefabManager has finished loading
    /// Uses reflection to access private currentLoadingCount field
    /// </summary>
    private bool IsModel3DPrefabManagerReady()
    {
        if (Model3DPrefabManager.Instance == null)
            return false;

        try
        {
            // Access private field using reflection
            var type = typeof(Model3DPrefabManager);
            var field = type.GetField("currentLoadingCount", 
                System.Reflection.BindingFlags.NonPublic | 
                System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                int loadingCount = (int)field.GetValue(Model3DPrefabManager.Instance);
                
                // Check if started loading (to avoid false positive at start)
                if (loadingCount > 0)
                {
                    modelManagerStartedLoading = true;
                }

                bool isReady = modelManagerStartedLoading && loadingCount == 0;

                if (showDebug && isReady && !modelManagerStartedLoading)
                {
                    Debug.Log($"[SceneLoadingManager] ✅ Model3DPrefabManager ready");
                }

                return isReady;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SceneLoadingManager] Error checking Model3DPrefabManager: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Update loading UI (only text message)
    /// </summary>
    private void UpdateLoadingUI(float progress, int completed, int total)
    {
        // Update loading message
        if (loadingText != null)
        {
            int messageIndex = Mathf.FloorToInt(progress * (loadingMessages.Length - 1));
            messageIndex = Mathf.Clamp(messageIndex, 0, loadingMessages.Length - 1);
            
            loadingText.text = loadingMessages[messageIndex];
        }
    }

    /// <summary>
    /// Hide loading panel with fade animation
    /// </summary>
    private void HideLoading()
    {
        if (loadingPanel != null)
        {
            StartCoroutine(FadeOutLoading());
        }
        else
        {
            if (showDebug)
                Debug.Log("[SceneLoadingManager] ✅ Loading complete!");
        }
    }

    /// <summary>
    /// Fade out animation for loading panel
    /// </summary>
    private IEnumerator FadeOutLoading()
    {
        CanvasGroup canvasGroup = loadingPanel.GetComponent<CanvasGroup>();
        
        if (canvasGroup == null)
        {
            canvasGroup = loadingPanel.AddComponent<CanvasGroup>();
        }

        float fadeDuration = 0.5f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = 1f - (elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
        loadingPanel.SetActive(false);

        if (showDebug)
            Debug.Log("[SceneLoadingManager] ✅ Loading panel hidden!");
    }

    /// <summary>
    /// Public method to manually show loading
    /// </summary>
    public void ShowLoading()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            
            CanvasGroup canvasGroup = loadingPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        isLoading = true;
        artManagerStartedLoading = false;
        modelManagerStartedLoading = false;
    }

    /// <summary>
    /// Public method to check if still loading
    /// </summary>
    public bool IsStillLoading()
    {
        return isLoading;
    }
}
