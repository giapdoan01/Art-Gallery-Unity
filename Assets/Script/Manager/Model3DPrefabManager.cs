using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Model3DPrefabManager - Quản lý việc tạo và load các Model3D prefabs
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

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
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

    public void LoadAllModelsFromServer()
    {
        if (API3DModelManager.Instance == null)
        {
            Debug.LogError("[Model3DPrefabManager] Cannot load models: API3DModelManager.Instance is null");
            return;
        }

        if (showDebug) Debug.Log("[Model3DPrefabManager] Loading all models from server...");

        float startTime = Time.time;

        API3DModelManager.Instance.GetAllModels((success, models, error) =>
        {
            if (!success || models == null)
            {
                Debug.LogError($"[Model3DPrefabManager] Failed to get models: {error}");
                return;
            }

            if (showDebug) Debug.Log($"[Model3DPrefabManager] Found {models.Count} models from server");

            // Kiểm tra models nào đã có trong scene
            List<string> existingModelIds = new List<string>();
            Model3DController[] existingControllers = FindObjectsByType<Model3DController>(FindObjectsSortMode.None);

            foreach (var controller in existingControllers)
            {
                if (controller != null)
                {
                    Model3DData data = controller.GetModelData();
                    if (data != null)
                    {
                        existingModelIds.Add(data.id);
                        modelInstances[data.id] = controller;
                    }
                }
            }

            if (showDebug) Debug.Log($"[Model3DPrefabManager] Found {existingModelIds.Count} models in scene");

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

    private IEnumerator LoadModelAndGLB(Model3DData modelData, System.Action onComplete = null)
    {
        if (model3DPrefab == null)
        {
            Debug.LogError("[Model3DPrefabManager] model3DPrefab is null!");
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

        // Bước 3: Initialize controller (set transform)
        controller.Initialize(modelData);

        // Bước 4: Set model ID cho view và load GLB
        view.SetModelId(modelData.id, autoLoad: true);

        // Bước 5: Đợi load xong
        yield return new WaitUntil(() => !view.IsLoading);

        if (view.IsLoaded)
        {
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

    public Model3DController GetModelInstance(string modelId)
    {
        modelInstances.TryGetValue(modelId, out Model3DController controller);
        return controller;
    }

    public Dictionary<string, Model3DController> GetAllModelInstances()
    {
        return new Dictionary<string, Model3DController>(modelInstances);
    }
}
