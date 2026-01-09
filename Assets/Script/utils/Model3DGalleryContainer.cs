using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Model3DGalleryContainer - Quản lý danh sách 3D models trong gallery
/// Tương tự ImageGalleryContainer nhưng cho 3D models
/// ✅ FIXED: Added Singleton pattern for Model3DEditPopup callbacks
/// </summary>
public class Model3DGalleryContainer : MonoBehaviour
{
    // ✅ Singleton pattern
    private static Model3DGalleryContainer _instance;
    public static Model3DGalleryContainer Instance => _instance;

    [Header("UI References")]
    [SerializeField] private Transform contentContainer;
    [SerializeField] private GameObject model3DItemPrefab;
    [SerializeField] private Button refreshButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingPanel;

    [Header("Settings")]
    [SerializeField] private bool loadOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private List<Model3DItem> modelItems = new List<Model3DItem>();
    private List<Model3DData> currentModels = new List<Model3DData>();

    #region Unity Lifecycle

    private void Awake()
    {
        // ✅ Setup singleton
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[Model3DGallery] Multiple instances detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void Start()
    {
        // Setup refresh button
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshGallery);
        }

        if (loadOnStart)
        {
            LoadAllModels();
        }


        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshGallery);
        }

        // ✅ Clear singleton reference
        if (_instance == this)
        {
            _instance = null;
        }
    }

    #endregion

    #region Public Methods - Loading

    /// <summary>
    /// Load tất cả 3D models từ server
    /// </summary>
    public void LoadAllModels()
    {
        if (showDebug) 
            Debug.Log("[Model3DGallery] Loading all 3D models...");

        ShowLoading(true);
        UpdateStatus("Loading 3D models...");

        // ✅ Sử dụng API3DModelManager
        API3DModelManager.Instance.GetAllModels((success, models, error) =>
        {
            ShowLoading(false);

            if (success && models != null)
            {
                if (showDebug) 
                    Debug.Log($"[Model3DGallery] Loaded {models.Count} 3D models");

                currentModels = models;
                DisplayModels(models);
                UpdateStatus($"Loaded {models.Count} 3D models");
            }
            else
            {
                Debug.LogError($"[Model3DGallery] Failed to load 3D models: {error}");
                UpdateStatus($"Error: {error}");
            }
        });
    }

    /// <summary>
    /// ✅ Refresh gallery (called from Model3DEditPopup after save/delete)
    /// </summary>
    public void RefreshGallery()
    {
        if (showDebug) 
            Debug.Log("[Model3DGallery] Refreshing gallery...");

        // Clear existing items first
        ClearItems();
        
        // Reload all models
        LoadAllModels();
    }

    #endregion

    #region Public Methods - Data Access

    /// <summary>
    /// Get current models
    /// </summary>
    public List<Model3DData> GetCurrentModels()
    {
        return new List<Model3DData>(currentModels);
    }

    /// <summary>
    /// Get model count
    /// </summary>
    public int GetModelCount()
    {
        return currentModels.Count;
    }

    /// <summary>
    /// Find model by ID
    /// </summary>
    public Model3DData FindModelById(string modelId)
    {
        return currentModels.Find(m => m.id == modelId);
    }

    /// <summary>
    /// ✅ NEW: Check if gallery is currently loading
    /// </summary>
    public bool IsLoading()
    {
        return loadingPanel != null && loadingPanel.activeSelf;
    }

    #endregion

    #region Private Methods - Display

    /// <summary>
    /// Hiển thị danh sách 3D models
    /// </summary>
    private void DisplayModels(List<Model3DData> models)
    {
        // Clear existing items
        ClearItems();

        if (models == null || models.Count == 0)
        {
            if (showDebug) 
                Debug.Log("[Model3DGallery] No 3D models to display");
            
            UpdateStatus("No 3D models found");
            return;
        }

        // Create new items
        foreach (Model3DData modelData in models)
        {
            CreateModelItem(modelData);
        }

        if (showDebug)
            Debug.Log($"[Model3DGallery] ✅ Displayed {models.Count} model items");

        // Force rebuild layout
        StartCoroutine(RebuildLayoutNextFrame());
    }

    /// <summary>
    /// Tạo một model item
    /// </summary>
    private void CreateModelItem(Model3DData modelData)
    {
        if (model3DItemPrefab == null || contentContainer == null)
        {
            Debug.LogError("[Model3DGallery] Missing prefab or container!");
            return;
        }

        // Instantiate prefab
        GameObject itemObj = Instantiate(model3DItemPrefab, contentContainer);
        Model3DItem item = itemObj.GetComponent<Model3DItem>();

        if (item != null)
        {
            // Setup item
            item.Setup(modelData);
            modelItems.Add(item);

            if (showDebug) 
                Debug.Log($"[Model3DGallery] Created item for: {modelData.name}");
        }
        else
        {
            Debug.LogError("[Model3DGallery] Model3DItem component not found on prefab!");
            Destroy(itemObj);
        }
    }

    /// <summary>
    /// ✅ Clear tất cả items (public for external access)
    /// </summary>
    public void ClearItems()
    {
        if (showDebug && modelItems.Count > 0)
            Debug.Log($"[Model3DGallery] Clearing {modelItems.Count} items...");

        foreach (Model3DItem item in modelItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }

        modelItems.Clear();

        if (showDebug)
            Debug.Log("[Model3DGallery] ✅ All items cleared");
    }

    /// <summary>
    /// Rebuild layout sau 1 frame
    /// </summary>
    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return null;

        // Force rebuild
        if (contentContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);
            
            if (showDebug)
                Debug.Log("[Model3DGallery] ✅ Layout rebuilt");
        }
    }

    #endregion

    #region Private Methods - UI

    /// <summary>
    /// Show/hide loading panel
    /// </summary>
    private void ShowLoading(bool show)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(show);
        }
    }

    /// <summary>
    /// Update status text
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (showDebug)
            Debug.Log($"[Model3DGallery] Status: {message}");
    }

    #endregion

    #region Public Methods - Utility

    /// <summary>
    /// ✅ NEW: Show/hide gallery panel
    /// </summary>
    public void Show(bool show)
    {
        gameObject.SetActive(show);

        if (show && showDebug)
            Debug.Log("[Model3DGallery] Gallery shown");
    }

    /// <summary>
    /// ✅ NEW: Toggle gallery visibility
    /// </summary>
    public void Toggle()
    {
        Show(!gameObject.activeSelf);
    }

    #endregion
}
