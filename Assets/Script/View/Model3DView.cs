using UnityEngine;

/// <summary>
/// View component - Hiển thị 3D model
/// GLB sẽ là con TRỰC TIẾP của GameObject này
/// </summary>
public class Model3DView : MonoBehaviour
{
    [Header("Model Settings")]
    [SerializeField] private string modelId;
    [SerializeField] private string modelName;

    [Header("Loading")]
    [SerializeField] private GameObject loadingPlaceholder;
    [SerializeField] private bool loadOnStart = false;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Private variables
    private Model3DData currentModelData;
    private GameObject currentGLBModel;
    private bool isLoading = false;

    // Properties
    public string ModelId => modelId;
    public string ModelName => modelName;
    public Model3DData ModelData => currentModelData;
    public GameObject CurrentGLBModel => currentGLBModel;
    public bool IsLoaded => currentGLBModel != null && currentModelData != null;
    public bool IsLoading => isLoading;

    #region Unity Lifecycle

    private void Awake()
    {
        if (loadingPlaceholder != null)
            loadingPlaceholder.SetActive(false);

        if (showDebug)
            Debug.Log($"[Model3DView] Awake: {gameObject.name}, modelId={modelId}, loadOnStart={loadOnStart}", this);
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

    private void OnDestroy()
    {
        ClearModel();
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(modelName))
        {
            modelName = gameObject.name;
        }
    }

    #endregion

    #region Public Methods

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

        // ✅ GỌI Model3DManager.LoadModel với callback
        Model3DManager.Instance.LoadModel(modelId, OnModelLoaded);
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
    }

    #endregion

    #region Private Methods

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

        // ✅ Set GLB làm con TRỰC TIẾP của GameObject này
        currentGLBModel = glbModel;
        currentModelData = data;

        currentGLBModel.transform.SetParent(transform, worldPositionStays: false);
        currentGLBModel.transform.localPosition = Vector3.zero;
        currentGLBModel.transform.localRotation = Quaternion.identity;
        currentGLBModel.transform.localScale = Vector3.one;

        currentGLBModel.SetActive(true);

        if (showDebug)
            Debug.Log($"[Model3DView] ✅ Model {modelId} loaded and applied. Hierarchy: {gameObject.name} → {currentGLBModel.name}", this);
    }

    #endregion
}
