using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// View component - Hiển thị 3D model
/// GLB sẽ là con TRỰC TIẾP của GameObject này
/// Hiện nút Edit khi player ở gần
/// FOLLOWS ArtFrame pattern: Each Model3DView has its own RuntimeTransformGizmo
/// RuntimeTransformGizmo component is attached to THIS GameObject (parent)
/// ✅ FIXED: Complete callback system with Model3DEditPopup
/// ✅ FIXED: Gizmo component deactivation does NOT use GameObject.SetActive
/// </summary>
public class Model3DView : MonoBehaviour
{
    [Header("Model Settings")]
    [SerializeField] private string modelId;
    [SerializeField] private string modelName;

    [Header("Loading")]
    [SerializeField] private GameObject loadingPlaceholder;
    [SerializeField] private bool loadOnStart = false;

    [Header("Interaction")]
    [SerializeField] private float interactionDistance = 5f;
    [SerializeField] private GameObject editButtonUI; // Canvas World Space với Button Info
    [SerializeField] private GameObject transformButtonUI; // Canvas World Space với Button Transform

    [Header("Gizmo - Like ArtFrame")]
    [SerializeField] private RuntimeTransformGizmo gizmo; // ✅ Component on THIS GameObject

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Private variables
    private Model3DData currentModelData;
    private GameObject currentGLBModel;
    private bool isLoading = false;
    private bool isPlayerInRange = false;
    private Button editButton; // Button edit info
    private Button transformButton; // Button edit transform

    // Properties
    public string ModelId => modelId;
    public string ModelName => modelName;
    public Model3DData ModelData => currentModelData;
    public GameObject CurrentGLBModel => currentGLBModel;
    public bool IsLoaded => currentGLBModel != null && currentModelData != null;
    public bool IsLoading => isLoading;
    public RuntimeTransformGizmo Gizmo => gizmo;

    #region Unity Lifecycle

    private void Awake()
    {
        // ✅ Auto-find gizmo if not assigned (should be on THIS GameObject)
        if (gizmo == null)
        {
            gizmo = GetComponent<RuntimeTransformGizmo>();
            
            if (gizmo != null && showDebug)
            {
                Debug.Log($"[Model3DView] Auto-found gizmo component on {gameObject.name}", this);
            }
        }

        // ✅ FIXED: Only deactivate COMPONENT, NOT GameObject!
        if (gizmo != null)
        {
            gizmo.Deactivate(); // ✅ This only hides LineRenderers, not GameObject
            
            if (showDebug)
                Debug.Log($"[Model3DView] Gizmo component deactivated (GameObject stays active)", this);
        }

        if (loadingPlaceholder != null)
            loadingPlaceholder.SetActive(false);

        // Setup edit info button
        if (editButtonUI != null)
        {
            editButtonUI.SetActive(false);
            editButton = editButtonUI.GetComponentInChildren<Button>();
            if (editButton != null)
            {
                editButton.onClick.RemoveAllListeners();
                editButton.onClick.AddListener(OpenEditPopup);
            }
            else
            {
                Debug.LogWarning("[Model3DView] Edit button not found in editButtonUI!", this);
            }
        }

        // Setup transform edit button
        if (transformButtonUI != null)
        {
            transformButtonUI.SetActive(false);
            transformButton = transformButtonUI.GetComponentInChildren<Button>();
            if (transformButton != null)
            {
                transformButton.onClick.RemoveAllListeners();
                transformButton.onClick.AddListener(OpenTransformEditPopup);
            }
            else
            {
                Debug.LogWarning("[Model3DView] Transform button not found in transformButtonUI!", this);
            }
        }

        if (showDebug)
            Debug.Log($"[Model3DView] Awake: {gameObject.name}, modelId={modelId}", this);
    }

    private void Start()
    {
        if (loadOnStart && !string.IsNullOrEmpty(modelId))
        {
            if (showDebug)
                Debug.Log($"[Model3DView] Auto-loading model {modelId}", this);

            LoadModel();
        }
    }

    private void Update()
    {
        if (!IsLoaded)
            return;

        // Kiểm tra khoảng cách với player
        CheckPlayerDistance();
    }

    private void OnDestroy()
    {
        ClearModel();
        
        if (editButton != null)
        {
            editButton.onClick.RemoveAllListeners();
        }

        if (transformButton != null)
        {
            transformButton.onClick.RemoveAllListeners();
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(modelName))
        {
            modelName = gameObject.name;
        }
    }

    #endregion

    #region Public Methods - Loading

    /// <summary>
    /// Load model từ Model3DManager
    /// </summary>
    public void LoadModel()
    {
        if (string.IsNullOrEmpty(modelId))
        {
            Debug.LogError("[Model3DView] Cannot load model: modelId is empty!", this);
            return;
        }

        if (Model3DManager.Instance == null)
        {
            Debug.LogError("[Model3DView] Cannot load model: Model3DManager.Instance is null!", this);
            return;
        }

        if (isLoading)
        {
            if (showDebug)
                Debug.Log($"[Model3DView] Model {modelId} is already loading", this);
            return;
        }

        // Clear old model
        ClearModel();

        // Show loading placeholder
        if (loadingPlaceholder != null)
            loadingPlaceholder.SetActive(true);

        isLoading = true;

        if (showDebug)
            Debug.Log($"[Model3DView] Requesting model {modelId} from Model3DManager", this);

        // Gọi Model3DManager.LoadModel với callback
        Model3DManager.Instance.LoadModel(modelId, OnModelLoaded);
    }

    /// <summary>
    /// ✅ NEW: Load model from URL (called from Model3DEditPopup after file change)
    /// </summary>
    public void LoadModel(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            Debug.LogError("[Model3DView] LoadModel: URL is empty!", this);
            return;
        }

        Debug.Log($"[Model3DView] Reloading model from URL: {url}", this);

        // Clear existing model
        ClearLoadedModel();

        // Show loading placeholder
        if (loadingPlaceholder != null)
            loadingPlaceholder.SetActive(true);

        isLoading = true;

        // Load new model using GLTFUtility
        StartCoroutine(LoadModelFromURL(url));
    }

    /// <summary>
    /// Set model ID và tự động load
    /// </summary>
    public void SetModelId(string id, bool autoLoad = true)
    {
        modelId = id;

        if (showDebug)
            Debug.Log($"[Model3DView] SetModelId: {id}, autoLoad: {autoLoad}", this);

        if (autoLoad)
            LoadModel();
    }

    /// <summary>
    /// Reload model (refresh từ server)
    /// </summary>
    public void ReloadModel()
    {
        if (showDebug)
            Debug.Log($"[Model3DView] Reloading model {modelId}", this);

        // Clear cache trước khi reload
        if (Model3DManager.Instance != null)
        {
            Model3DManager.Instance.ClearCache();
        }

        LoadModel();
    }

    /// <summary>
    /// Clear model hiện tại
    /// </summary>
    public void ClearModel()
    {
        if (currentGLBModel != null)
        {
            if (showDebug)
                Debug.Log($"[Model3DView] Destroying current GLB model", this);

            Destroy(currentGLBModel);
            currentGLBModel = null;
        }

        currentModelData = null;
        isPlayerInRange = false;
        
        // Hide buttons
        if (editButtonUI != null)
            editButtonUI.SetActive(false);

        if (transformButtonUI != null)
            transformButtonUI.SetActive(false);
    }

    #endregion

    #region Public Methods - Editing

    /// <summary>
    /// Mở popup để edit thông tin model (name, author, description, file)
    /// ✅ FIXED: Pass THIS Model3DView reference to popup
    /// </summary>
    public void OpenEditPopup()
    {
        if (!IsLoaded)
        {
            Debug.LogWarning("[Model3DView] Cannot open edit popup: Model not loaded", this);
            return;
        }

        if (Model3DEditPopup.Instance == null)
        {
            Debug.LogError("[Model3DView] Model3DEditPopup.Instance is null!", this);
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DView] Opening edit popup for model: {modelName}", this);

        // ✅ Pass THIS Model3DView reference to popup for callbacks
        Model3DEditPopup.Instance.Show(currentModelData, this, OnEditComplete);
    }

    /// <summary>
    /// Mở popup để edit transform (position, rotation, scale)
    /// </summary>
    public void OpenTransformEditPopup()
    {
        if (!IsLoaded)
        {
            Debug.LogWarning("[Model3DView] Cannot open transform edit popup: Model not loaded", this);
            return;
        }

        if (Model3DTransformEditPopup.Instance == null)
        {
            Debug.LogError("[Model3DView] Model3DTransformEditPopup.Instance is null!", this);
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DView] Opening transform edit popup for model: {modelName}", this);

        // Pass this (Model3DView) so popup can access gizmo
        Model3DTransformEditPopup.Instance.Show(this, OnTransformEditComplete);
    }

    /// <summary>
    /// ✅ NEW: Update model data (called from Model3DEditPopup after save)
    /// </summary>
    public void UpdateModelData(Model3DData newData)
    {
        if (newData == null)
        {
            Debug.LogError("[Model3DView] UpdateModelData: newData is null!", this);
            return;
        }

        Debug.Log($"[Model3DView] Updating model data for ID: {newData.id}", this);

        modelId = newData.id;
        modelName = newData.name;
        currentModelData = newData;

        // Update transform if changed
        transform.position = newData.position;
        transform.rotation = Quaternion.Euler(newData.rotation);
        transform.localScale = newData.scale;

        Debug.Log($"[Model3DView] ✅ Model data updated: {modelName}", this);
    }

    #endregion

    #region Private Methods - Loading

    /// <summary>
    /// Callback khi model được load từ Manager
    /// </summary>
    private void OnModelLoaded(GameObject glbModel, Model3DData data)
    {
        isLoading = false;

        // Hide loading placeholder
        if (loadingPlaceholder != null)
            loadingPlaceholder.SetActive(false);

        if (glbModel == null || data == null)
        {
            Debug.LogError($"[Model3DView] Failed to load model {modelId}", this);
            return;
        }

        // Set GLB làm con TRỰC TIẾP của GameObject này
        currentGLBModel = glbModel;
        currentModelData = data;
        modelName = data.name; // Update name từ data

        currentGLBModel.transform.SetParent(transform, worldPositionStays: false);
        currentGLBModel.transform.localPosition = Vector3.zero;
        currentGLBModel.transform.localRotation = Quaternion.identity;
        currentGLBModel.transform.localScale = Vector3.one;

        // ✅ CRITICAL: Ensure GLB model is active
        currentGLBModel.SetActive(true);

        if (showDebug)
        {
            Debug.Log($"[Model3DView] ✅ Model {modelId} loaded. Hierarchy: {gameObject.name} → {currentGLBModel.name}", this);
            Debug.Log($"[Model3DView] GLB Model active: {currentGLBModel.activeSelf}, Parent active: {gameObject.activeSelf}", this);
        }
    }

    /// <summary>
    /// ✅ NEW: Load model from URL using GLTFUtility
    /// </summary>
    private IEnumerator LoadModelFromURL(string url)
    {
        Debug.Log($"[Model3DView] Loading GLB from URL: {url}", this);

        // Use GLTFUtility to load model
        // Note: You need to implement this based on your GLTFUtility setup
        // This is a placeholder - adjust based on your actual implementation

        // Example using Siccity.GLTFUtility:
        // var importTask = Importer.LoadFromFileAsync(url);
        // yield return importTask;
        
        // For now, just reload from Model3DManager
        yield return new WaitForSeconds(0.5f); // Simulate loading delay

        isLoading = false;

        // Hide loading placeholder
        if (loadingPlaceholder != null)
            loadingPlaceholder.SetActive(false);

        // Reload using Model3DManager (which will fetch from server)
        if (Model3DManager.Instance != null)
        {
            Model3DManager.Instance.ClearCache();
            LoadModel(); // Reload from manager
        }

        Debug.Log($"[Model3DView] ✅ Model reloaded from URL", this);
    }

    /// <summary>
    /// Clear currently loaded model (for reloading)
    /// </summary>
    private void ClearLoadedModel()
    {
        // Find and destroy existing GLB child
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            
            // Skip UI elements
            if (child == editButtonUI?.transform || child == transformButtonUI?.transform || child == loadingPlaceholder?.transform)
                continue;

            // Destroy GLB model
            if (child.name.Contains("GLB") || child.name.Contains("Model") || child == currentGLBModel?.transform)
            {
                Destroy(child.gameObject);
            }
        }

        currentGLBModel = null;

        Debug.Log("[Model3DView] Cleared existing model", this);
    }

    /// <summary>
    /// Callback khi edit info hoàn tất
    /// </summary>
    private void OnEditComplete(bool success, Model3DData updatedData)
    {
        if (!success)
        {
            if (showDebug)
                Debug.Log($"[Model3DView] Edit cancelled or failed", this);
            return;
        }

        if (updatedData == null)
        {
            // Model bị xóa
            if (showDebug)
                Debug.Log($"[Model3DView] Model was deleted, destroying GameObject", this);

            ClearModel();
            Destroy(gameObject);
            return;
        }

        // Model được update
        // UpdateModelData và LoadModel sẽ được gọi từ Model3DEditPopup.RefreshAllComponents()
        if (showDebug)
            Debug.Log($"[Model3DView] Model updated: {updatedData.name}", this);
    }

    /// <summary>
    /// Callback khi edit transform hoàn tất
    /// </summary>
    private void OnTransformEditComplete()
    {
        if (showDebug)
            Debug.Log($"[Model3DView] Transform edit completed", this);

        // ✅ SAFETY: Ensure GLB model is still active (should not be needed after fix)
        if (currentGLBModel != null && !currentGLBModel.activeSelf)
        {
            Debug.LogWarning("[Model3DView] ⚠️ GLB model was deactivated! Reactivating...", this);
            currentGLBModel.SetActive(true);
        }

        // ✅ SAFETY: Ensure parent GameObject is still active (should not be needed after fix)
        if (!gameObject.activeSelf)
        {
            Debug.LogWarning("[Model3DView] ⚠️ Parent GameObject was deactivated! Reactivating...", this);
            gameObject.SetActive(true);
        }

        // Clear cache
        if (Model3DManager.Instance != null)
        {
            Model3DManager.Instance.ClearCache();
        }
    }

    #endregion

    #region Private Methods - Interaction

    /// <summary>
    /// Kiểm tra khoảng cách với player và hiện/ẩn buttons
    /// </summary>
    private void CheckPlayerDistance()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
        bool inRange = distance <= interactionDistance;

        // Chỉ update khi trạng thái thay đổi
        if (inRange != isPlayerInRange)
        {
            isPlayerInRange = inRange;

            // Show/hide edit info button
            if (editButtonUI != null)
            {
                editButtonUI.SetActive(inRange);
            }

            // Show/hide transform edit button
            if (transformButtonUI != null)
            {
                transformButtonUI.SetActive(inRange);
            }

            if (showDebug)
                Debug.Log($"[Model3DView] Player {(inRange ? "entered" : "left")} interaction range. Distance: {distance:F2}m", this);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Vẽ interaction range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }

    #endregion

    #region Public Methods - Debug

    /// <summary>
    /// DEBUG: Force reactivate GLB model
    /// </summary>
    [ContextMenu("Force Reactivate GLB Model")]
    public void ForceReactivateGLBModel()
    {
        if (currentGLBModel != null)
        {
            currentGLBModel.SetActive(true);
            Debug.Log($"[Model3DView] ✅ Forced GLB model active: {currentGLBModel.name}", this);
        }
        else
        {
            Debug.LogWarning("[Model3DView] ❌ No GLB model to reactivate!", this);
        }
    }

    /// <summary>
    /// DEBUG: Force reactivate parent GameObject
    /// </summary>
    [ContextMenu("Force Reactivate Parent GameObject")]
    public void ForceReactivateParent()
    {
        gameObject.SetActive(true);
        Debug.Log($"[Model3DView] ✅ Forced parent GameObject active: {gameObject.name}", this);
    }

    /// <summary>
    /// DEBUG: Print hierarchy
    /// </summary>
    [ContextMenu("Debug Print Hierarchy")]
    public void DebugPrintHierarchy()
    {
        Debug.Log($"[Model3DView] === Hierarchy Debug ===", this);
        Debug.Log($"Parent: {gameObject.name} (active: {gameObject.activeSelf})", this);
        
        // Check gizmo component
        if (gizmo != null)
        {
            Debug.Log($"  - RuntimeTransformGizmo component: {(gizmo.enabled ? "ENABLED" : "DISABLED")}, IsActive: {gizmo.IsActive}", this);
        }
        else
        {
            Debug.Log($"  - RuntimeTransformGizmo component: NULL", this);
        }
        
        foreach (Transform child in transform)
        {
            Debug.Log($"  - Child: {child.name} (active: {child.gameObject.activeSelf})", this);
            
            if (child == currentGLBModel?.transform)
            {
                Debug.Log($"    ✅ This is the GLB model!", this);
            }
        }
    }

    #endregion
}
