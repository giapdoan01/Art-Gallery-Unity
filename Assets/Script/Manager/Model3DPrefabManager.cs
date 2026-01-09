using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Model3DPrefabManager - Quản lý việc tạo và load các Model3D prefabs
/// ✅ UPDATED: Added CreateModelInstance() and RemoveModelInstance() for efficient single model operations
/// </summary>
public class Model3DPrefabManager : MonoBehaviour
{
    private static Model3DPrefabManager _instance;
    public static Model3DPrefabManager Instance => _instance;

    [Header("Prefab Template")]
    [SerializeField] private GameObject model3DPrefab;

    [Header("Settings")]
    [SerializeField] private bool loadModelsOnStart = true;
    [SerializeField] private float loadDelay = 1f;
    [SerializeField] private bool showDebug = true;

    [Header("Performance")]
    [SerializeField] private bool useParallelLoading = true;
    [SerializeField] private int maxConcurrentLoads = 4;
    private int currentLoadingCount = 0;

    private Dictionary<string, Model3DController> modelInstances = new Dictionary<string, Model3DController>();

    #region Unity Lifecycle

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[Model3DPrefabManager] Multiple instances detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void Start()
    {
        if (model3DPrefab == null)
        {
            Debug.LogError("[Model3DPrefabManager] Model3D Prefab is not assigned!");
            return;
        }

        if (model3DPrefab.GetComponent<Model3DView>() == null)
        {
            Debug.LogError("[Model3DPrefabManager] Prefab does not have Model3DView component!");
            return;
        }

        if (model3DPrefab.GetComponent<Model3DController>() == null)
        {
            Debug.LogError("[Model3DPrefabManager] Prefab does not have Model3DController component!");
            return;
        }

        if (loadModelsOnStart)
        {
            Invoke(nameof(LoadAllModelsFromServer), loadDelay);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    #endregion

    #region Public Methods - Load All Models

    /// <summary>
    /// Load tất cả models từ server và tạo prefabs cho models chưa có trong scene
    /// </summary>
    public void LoadAllModelsFromServer()
    {
        if (API3DModelManager.Instance == null)
        {
            Debug.LogError("[Model3DPrefabManager] Cannot load models: API3DModelManager.Instance is null");
            return;
        }

        if (showDebug) 
            Debug.Log("[Model3DPrefabManager] Loading all models from server...");

        float startTime = Time.time;

        API3DModelManager.Instance.GetAllModels((success, models, error) =>
        {
            if (!success || models == null)
            {
                Debug.LogError($"[Model3DPrefabManager] Failed to get models: {error}");
                return;
            }

            if (showDebug) 
                Debug.Log($"[Model3DPrefabManager] Found {models.Count} models from server");

            // Kiểm tra models nào đã có trong scene
            List<string> existingModelIds = new List<string>();
            Model3DController[] existingControllers = FindObjectsByType<Model3DController>(FindObjectsSortMode.None);

            foreach (var controller in existingControllers)
            {
                if (controller != null)
                {
                    Model3DData data = controller.GetModelData();
                    if (data != null && !string.IsNullOrEmpty(data.id))
                    {
                        existingModelIds.Add(data.id);
                        
                        // Update dictionary if not already tracked
                        if (!modelInstances.ContainsKey(data.id))
                        {
                            modelInstances[data.id] = controller;
                        }
                    }
                }
            }

            if (showDebug) 
                Debug.Log($"[Model3DPrefabManager] Found {existingModelIds.Count} models in scene");

            // Tạo prefabs cho models chưa có
            List<Model3DData> modelsToLoad = new List<Model3DData>();

            foreach (var modelData in models)
            {
                if (!existingModelIds.Contains(modelData.id))
                {
                    modelsToLoad.Add(modelData);
                }
                else
                {
                    if (showDebug)
                        Debug.Log($"[Model3DPrefabManager] Model {modelData.name} already exists in scene, skipping");
                }
            }

            if (modelsToLoad.Count == 0)
            {
                if (showDebug)
                    Debug.Log("[Model3DPrefabManager] All models already loaded");
                return;
            }

            // Load models
            if (useParallelLoading)
            {
                StartCoroutine(LoadModelsParallel(modelsToLoad, startTime));
            }
            else
            {
                StartCoroutine(LoadModelsSequential(modelsToLoad, startTime));
            }
        });
    }

    #endregion

    #region Public Methods - Single Model Operations (Option 2)

    /// <summary>
    /// ✅ NEW: Create a single model instance in scene
    /// Called from Model3DEditPopup after CREATE operation
    /// </summary>
    public void CreateModelInstance(Model3DData modelData)
    {
        if (modelData == null)
        {
            Debug.LogError("[Model3DPrefabManager] Cannot create instance: modelData is null!");
            return;
        }

        if (string.IsNullOrEmpty(modelData.id))
        {
            Debug.LogError("[Model3DPrefabManager] Cannot create instance: modelData.id is empty!");
            return;
        }

        // Check if already exists
        if (modelInstances.ContainsKey(modelData.id))
        {
            if (showDebug)
                Debug.Log($"[Model3DPrefabManager] Model {modelData.name} (ID: {modelData.id}) already exists, skipping");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] ✨ Creating single model instance: {modelData.name} (ID: {modelData.id})");

        // Create model using existing coroutine
        StartCoroutine(LoadModelAndGLB(modelData, () =>
        {
            if (showDebug)
                Debug.Log($"[Model3DPrefabManager] ✅ Model instance created successfully: {modelData.name}");
        }));
    }

    /// <summary>
    /// ✅ NEW: Remove a model instance from scene
    /// Called from Model3DEditPopup after DELETE operation
    /// </summary>
    public void RemoveModelInstance(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            Debug.LogError("[Model3DPrefabManager] Cannot remove instance: modelId is empty!");
            return;
        }

        if (!modelInstances.TryGetValue(modelId, out Model3DController controller))
        {
            if (showDebug)
                Debug.Log($"[Model3DPrefabManager] Model {modelId} not found in instances, might already be removed");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] 🗑️ Removing model instance: {modelId}");

        // Destroy GameObject
        if (controller != null && controller.gameObject != null)
        {
            Destroy(controller.gameObject);
        }

        // Remove from dictionary
        modelInstances.Remove(modelId);

        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] ✅ Model instance removed successfully: {modelId}");
    }

    /// <summary>
    /// ✅ NEW: Check if a model instance exists in scene
    /// </summary>
    public bool HasModelInstance(string modelId)
    {
        return !string.IsNullOrEmpty(modelId) && modelInstances.ContainsKey(modelId);
    }

    /// <summary>
    /// ✅ NEW: Update an existing model instance (remove + recreate)
    /// Useful when model file changes
    /// </summary>
    public void UpdateModelInstance(Model3DData modelData)
    {
        if (modelData == null || string.IsNullOrEmpty(modelData.id))
        {
            Debug.LogError("[Model3DPrefabManager] Cannot update instance: invalid modelData");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] 🔄 Updating model instance: {modelData.name}");

        // Remove old instance
        RemoveModelInstance(modelData.id);

        // Create new instance
        CreateModelInstance(modelData);
    }

    #endregion

    #region Public Methods - Data Access

    /// <summary>
    /// Get model instance by ID
    /// </summary>
    public Model3DController GetModelInstance(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            return null;
        }

        modelInstances.TryGetValue(modelId, out Model3DController controller);
        return controller;
    }

    /// <summary>
    /// Get all model instances
    /// </summary>
    public Dictionary<string, Model3DController> GetAllModelInstances()
    {
        return new Dictionary<string, Model3DController>(modelInstances);
    }

    /// <summary>
    /// Get count of loaded models
    /// </summary>
    public int GetLoadedModelCount()
    {
        return modelInstances.Count;
    }

    /// <summary>
    /// Check if currently loading models
    /// </summary>
    public bool IsLoading()
    {
        return currentLoadingCount > 0;
    }

    #endregion

    #region Private Methods - Loading

    /// <summary>
    /// Load models in parallel (faster but uses more resources)
    /// </summary>
    private IEnumerator LoadModelsParallel(List<Model3DData> models, float startTime)
    {
        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] Starting parallel loading of {models.Count} models...");

        int totalModels = models.Count;
        int loadedCount = 0;

        foreach (var modelData in models)
        {
            // Đợi nếu đã đạt max concurrent loads
            yield return new WaitUntil(() => currentLoadingCount < maxConcurrentLoads);

            StartCoroutine(LoadModelAndGLB(modelData, () =>
            {
                loadedCount++;

                if (showDebug)
                    Debug.Log($"[Model3DPrefabManager] Progress: {loadedCount}/{totalModels} models loaded");

                if (loadedCount >= totalModels)
                {
                    float loadTime = Time.time - startTime;
                    if (showDebug)
                        Debug.Log($"[Model3DPrefabManager] ✅ Completed loading {totalModels} models in {loadTime:F2}s");
                }
            }));
        }
    }

    /// <summary>
    /// Load models sequentially (slower but more stable)
    /// </summary>
    private IEnumerator LoadModelsSequential(List<Model3DData> models, float startTime)
    {
        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] Starting sequential loading of {models.Count} models...");

        int totalModels = models.Count;

        for (int i = 0; i < models.Count; i++)
        {
            Model3DData modelData = models[i];

            bool loadComplete = false;

            StartCoroutine(LoadModelAndGLB(modelData, () =>
            {
                loadComplete = true;
            }));

            yield return new WaitUntil(() => loadComplete);

            if (showDebug)
                Debug.Log($"[Model3DPrefabManager] Progress: {i + 1}/{totalModels} models loaded");
        }

        float loadTime = Time.time - startTime;
        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] ✅ Completed loading {totalModels} models in {loadTime:F2}s");
    }

    /// <summary>
    /// Load a single model and its GLB file
    /// </summary>
    private IEnumerator LoadModelAndGLB(Model3DData modelData, System.Action onComplete = null)
    {
        if (model3DPrefab == null)
        {
            Debug.LogError("[Model3DPrefabManager] model3DPrefab is null!");
            onComplete?.Invoke();
            yield break;
        }

        if (modelData == null || string.IsNullOrEmpty(modelData.id))
        {
            Debug.LogError("[Model3DPrefabManager] Invalid modelData!");
            onComplete?.Invoke();
            yield break;
        }

        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] Creating prefab for model: {modelData.name} (ID: {modelData.id})");

        currentLoadingCount++;

        // Bước 1: Instantiate prefab
        GameObject modelInstance = Instantiate(model3DPrefab);
        modelInstance.name = $"Model3D_{modelData.name}_{modelData.id}";

        // Bước 2: Lấy controller và view
        Model3DController controller = modelInstance.GetComponent<Model3DController>();
        Model3DView view = modelInstance.GetComponent<Model3DView>();

        if (controller == null || view == null)
        {
            Debug.LogError("[Model3DPrefabManager] Prefab does not have Model3DController or Model3DView!");
            Destroy(modelInstance);
            currentLoadingCount--;
            onComplete?.Invoke();
            yield break;
        }

        // Bước 3: Initialize controller (set transform from modelData)
        controller.Initialize(modelData);

        // Bước 4: Set model ID cho view và load GLB
        view.SetModelId(modelData.id, autoLoad: true);

        // Bước 5: Đợi load xong
        yield return new WaitUntil(() => !view.IsLoading);

        // Bước 6: Kiểm tra kết quả
        if (view.IsLoaded)
        {
            // Add to dictionary
            modelInstances[modelData.id] = controller;

            if (showDebug)
                Debug.Log($"[Model3DPrefabManager] ✅ Successfully created and loaded model: {modelData.name}");
        }
        else
        {
            Debug.LogError($"[Model3DPrefabManager] Failed to load model: {modelData.name}, destroying instance");
            Destroy(modelInstance);
        }

        currentLoadingCount--;
        onComplete?.Invoke();
    }

    #endregion

    #region Public Methods - Utility

    /// <summary>
    /// ✅ NEW: Clear all model instances from scene
    /// </summary>
    public void ClearAllInstances()
    {
        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] Clearing all {modelInstances.Count} model instances...");

        foreach (var kvp in modelInstances)
        {
            if (kvp.Value != null && kvp.Value.gameObject != null)
            {
                Destroy(kvp.Value.gameObject);
            }
        }

        modelInstances.Clear();

        if (showDebug)
            Debug.Log("[Model3DPrefabManager] ✅ All model instances cleared");
    }

    /// <summary>
    /// ✅ NEW: Refresh all models (reload from server)
    /// </summary>
    public void RefreshAllModels()
    {
        if (showDebug)
            Debug.Log("[Model3DPrefabManager] Refreshing all models...");

        ClearAllInstances();
        LoadAllModelsFromServer();
    }

    #endregion
}
