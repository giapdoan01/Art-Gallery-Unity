using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using GLTFast;
using System.Threading.Tasks;

/// <summary>
/// Model3DManager - Quản lý cache và loading của 3D models sử dụng Unity glTFast
/// CHỈ TẠO 1 GLB DUY NHẤT CHO VIEW (không tính cache)
/// </summary>
public class Model3DManager : MonoBehaviour
{
    private static Model3DManager _instance;

    [Header("Loading Settings")]
    [SerializeField] private int maxConcurrentDownloads = 2;
    [SerializeField] private float downloadDelay = 0.5f;

    [Header("Cache Settings")]
    [SerializeField] private bool enableCache = true;
    [SerializeField] private int maxCacheSize = 20;
    [SerializeField] private float cacheCleanupInterval = 120f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Cache - Lưu GLB đã load để tái sử dụng
    private Dictionary<string, GameObject> glbCache = new Dictionary<string, GameObject>();
    private Dictionary<string, Model3DData> modelDataCache = new Dictionary<string, Model3DData>();
    private Dictionary<string, float> lastAccessTime = new Dictionary<string, float>();

    // Loading queue
    private Queue<string> downloadQueue = new Queue<string>();
    private HashSet<string> currentlyLoading = new HashSet<string>();
    private Dictionary<string, List<Action<GameObject, Model3DData>>> pendingCallbacks =
        new Dictionary<string, List<Action<GameObject, Model3DData>>>();

    public static Model3DManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("Model3DManager");
                _instance = go.AddComponent<Model3DManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (showDebug) Debug.Log("[Model3DManager] Initialized with Unity glTFast");
    }

    private void Start()
    {
        if (enableCache)
        {
            InvokeRepeating(nameof(CleanupCache), cacheCleanupInterval, cacheCleanupInterval);
        }
    }

    #region Public Methods

    /// <summary>
    /// Load model - Trả về 1 GLB instance duy nhất qua callback
    /// </summary>
    public void LoadModel(string modelId, Action<GameObject, Model3DData> callback = null)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            Debug.LogError("[Model3DManager] Cannot load model: modelId is empty!");
            callback?.Invoke(null, null);
            return;
        }

        // ✅ Kiểm tra cache
        if (enableCache && glbCache.TryGetValue(modelId, out GameObject cachedGLB) &&
            modelDataCache.TryGetValue(modelId, out Model3DData cachedData))
        {
            if (showDebug)
                Debug.Log($"[Model3DManager] Using cached model: {modelId}");

            lastAccessTime[modelId] = Time.time;

            // Clone từ cache
            GameObject glbInstance = Instantiate(cachedGLB);
            glbInstance.SetActive(true);
            glbInstance.name = $"GLB_{modelId}"; // Tên chuẩn

            callback?.Invoke(glbInstance, cachedData);
            return;
        }

        // ✅ Đang loading - thêm callback vào hàng đợi
        if (currentlyLoading.Contains(modelId))
        {
            if (showDebug)
                Debug.Log($"[Model3DManager] Model {modelId} is already loading, adding callback to queue");

            if (callback != null)
            {
                if (!pendingCallbacks.ContainsKey(modelId))
                    pendingCallbacks[modelId] = new List<Action<GameObject, Model3DData>>();

                pendingCallbacks[modelId].Add(callback);
            }
            return;
        }

        // ✅ Thêm vào download queue
        if (callback != null)
        {
            if (!pendingCallbacks.ContainsKey(modelId))
                pendingCallbacks[modelId] = new List<Action<GameObject, Model3DData>>();

            pendingCallbacks[modelId].Add(callback);
        }

        downloadQueue.Enqueue(modelId);

        if (showDebug)
            Debug.Log($"[Model3DManager] Added model {modelId} to download queue");

        StartCoroutine(ProcessDownloadQueue());
    }

    public bool IsModelLoading(string modelId)
    {
        return currentlyLoading.Contains(modelId);
    }

    public bool IsModelCached(string modelId)
    {
        return glbCache.ContainsKey(modelId) && modelDataCache.ContainsKey(modelId);
    }

    public void ClearCache()
    {
        foreach (var glb in glbCache.Values)
        {
            if (glb != null)
                Destroy(glb);
        }

        glbCache.Clear();
        modelDataCache.Clear();
        lastAccessTime.Clear();

        if (showDebug)
            Debug.Log("[Model3DManager] Cache cleared");
    }

    #endregion

    #region Private Methods - Download

    private IEnumerator ProcessDownloadQueue()
    {
        while (downloadQueue.Count > 0 && currentlyLoading.Count < maxConcurrentDownloads)
        {
            string modelId = downloadQueue.Dequeue();

            if (!currentlyLoading.Contains(modelId))
            {
                StartCoroutine(DownloadAndLoadModel(modelId));
                yield return new WaitForSeconds(downloadDelay);
            }
        }
    }

    private IEnumerator DownloadAndLoadModel(string modelId)
    {
        currentlyLoading.Add(modelId);

        if (showDebug)
            Debug.Log($"[Model3DManager] Starting download for model: {modelId}");

        // ========================================
        // Bước 1: Lấy model data từ API
        // ========================================
        bool apiCallComplete = false;
        bool dataSuccess = false;
        Model3DData modelData = null;
        string apiError = "";

        API3DModelManager.Instance.GetModelById(modelId, (success, data, error) =>
        {
            apiCallComplete = true;
            dataSuccess = success;
            modelData = data;
            apiError = error;
        });

        yield return new WaitUntil(() => apiCallComplete);

        if (!dataSuccess || modelData == null)
        {
            HandleLoadFailed(modelId, $"Failed to get model data from API: {apiError}");
            yield break;
        }

        if (showDebug)
            Debug.Log($"[Model3DManager] Model data received: {modelData.name}, URL: {modelData.url}");

        // ========================================
        // Bước 2: Download GLB file
        // ========================================
        if (string.IsNullOrEmpty(modelData.url))
        {
            HandleLoadFailed(modelId, "Model URL is empty");
            yield break;
        }

        if (showDebug)
            Debug.Log($"[Model3DManager] Downloading GLB from: {modelData.url}");

        UnityWebRequest request = UnityWebRequest.Get(modelData.url);
        request.timeout = 60;

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            HandleLoadFailed(modelId, $"Download failed: {request.error}");
            yield break;
        }

        byte[] glbBytes = request.downloadHandler.data;

        if (showDebug)
            Debug.Log($"[Model3DManager] Downloaded GLB ({glbBytes.Length / 1024}KB) for model: {modelId}");

        // ========================================
        // Bước 3: Import GLB
        // ========================================
        bool importComplete = false;
        GameObject glbObject = null;

        yield return StartCoroutine(ImportGLBFromBytes(glbBytes, modelId, (success, imported) =>
        {
            importComplete = true;
            glbObject = imported;
        }));

        yield return new WaitUntil(() => importComplete);

        if (glbObject == null)
        {
            HandleLoadFailed(modelId, "Failed to import GLB");
            yield break;
        }

        if (showDebug)
            Debug.Log($"[Model3DManager] GLB imported successfully for model: {modelId}");

        // ========================================
        // Bước 4: Xử lý callbacks và cache
        // ========================================

        currentlyLoading.Remove(modelId);

        // ✅ Lấy danh sách callbacks
        List<Action<GameObject, Model3DData>> callbacks = null;
        if (pendingCallbacks.TryGetValue(modelId, out callbacks))
        {
            pendingCallbacks.Remove(modelId);
        }

        // ✅ Nếu có callbacks
        if (callbacks != null && callbacks.Count > 0)
        {
            if (showDebug)
                Debug.Log($"[Model3DManager] Processing {callbacks.Count} callback(s) for model: {modelId}");

            // Callback đầu tiên nhận glbObject GỐC (không clone)
            callbacks[0]?.Invoke(glbObject, modelData);

            if (showDebug)
                Debug.Log($"[Model3DManager] ✅ Delivered GLB to view for model: {modelId}");

            // Nếu có nhiều callbacks (hiếm khi xảy ra), clone cho các callbacks còn lại
            for (int i = 1; i < callbacks.Count; i++)
            {
                GameObject glbClone = Instantiate(glbObject);
                glbClone.SetActive(true);
                glbClone.name = $"GLB_{modelId}";
                callbacks[i]?.Invoke(glbClone, modelData);

                if (showDebug)
                    Debug.Log($"[Model3DManager] Delivered cloned GLB to additional callback #{i + 1}");
            }

            // ✅ Cache GLB (clone từ glbObject đã được giao cho view)
            if (enableCache)
            {
                GameObject cacheObject = Instantiate(glbObject);
                cacheObject.SetActive(false);
                cacheObject.name = $"GLB_{modelId}_Cache";
                cacheObject.transform.SetParent(transform); // Set parent là Model3DManager
                glbCache[modelId] = cacheObject;
                modelDataCache[modelId] = modelData;
                lastAccessTime[modelId] = Time.time;

                if (showDebug)
                    Debug.Log($"[Model3DManager] Cached model: {modelId}");
            }
        }
        else
        {
            // Không có callback nào, destroy glbObject
            if (showDebug)
                Debug.LogWarning($"[Model3DManager] No callbacks for model {modelId}, destroying GLB");

            Destroy(glbObject);
        }
    }

    private void HandleLoadFailed(string modelId, string error)
    {
        currentlyLoading.Remove(modelId);

        Debug.LogError($"[Model3DManager] Failed to load model {modelId}: {error}");

        if (pendingCallbacks.TryGetValue(modelId, out var callbacks))
        {
            foreach (var callback in callbacks)
            {
                callback?.Invoke(null, null);
            }

            pendingCallbacks.Remove(modelId);
        }
    }

    #endregion

    #region Private Methods - GLB Import

    /// <summary>
    /// Import GLB từ bytes sử dụng Unity glTFast
    /// </summary>
    private IEnumerator ImportGLBFromBytes(byte[] glbBytes, string modelId, Action<bool, GameObject> callback)
    {
        if (showDebug)
            Debug.Log($"[Model3DManager] Importing GLB using Unity glTFast for model: {modelId}");

        // Tạo GameObject root
        GameObject glbRoot = new GameObject($"GLB_{modelId}");

        // Tạo GltfImport instance
        var gltf = new GltfImport();

        // Load GLB từ bytes
        Task<bool> loadTask = gltf.LoadGltfBinary(glbBytes, uri: null);

        // Đợi load task hoàn thành
        yield return new WaitUntil(() => loadTask.IsCompleted);

        // Kiểm tra exception
        if (loadTask.Exception != null)
        {
            Debug.LogError($"[Model3DManager] GLTFast load exception: {loadTask.Exception.Message}");
            Destroy(glbRoot);
            callback?.Invoke(false, null);
            yield break;
        }

        if (!loadTask.Result)
        {
            Debug.LogError($"[Model3DManager] GLTFast failed to load GLB for model: {modelId}");
            Destroy(glbRoot);
            callback?.Invoke(false, null);
            yield break;
        }

        if (showDebug)
            Debug.Log($"[Model3DManager] GLTFast loaded GLB successfully, instantiating scene...");

        // Instantiate main scene
        Task<bool> instantiateTask = gltf.InstantiateMainSceneAsync(glbRoot.transform);

        // Đợi instantiate task hoàn thành
        yield return new WaitUntil(() => instantiateTask.IsCompleted);

        // Kiểm tra exception
        if (instantiateTask.Exception != null)
        {
            Debug.LogError($"[Model3DManager] GLTFast instantiate exception: {instantiateTask.Exception.Message}");
            Destroy(glbRoot);
            callback?.Invoke(false, null);
            yield break;
        }

        if (!instantiateTask.Result)
        {
            Debug.LogError($"[Model3DManager] GLTFast failed to instantiate scene for model: {modelId}");
            Destroy(glbRoot);
            callback?.Invoke(false, null);
            yield break;
        }

        if (showDebug)
            Debug.Log($"[Model3DManager] ✅ GLTFast successfully imported GLB for model: {modelId}");

        glbRoot.SetActive(true);

        callback?.Invoke(true, glbRoot);
    }

    #endregion

    #region Private Methods - Cache Cleanup

    private void CleanupCache()
    {
        if (!enableCache || glbCache.Count <= maxCacheSize)
            return;

        if (showDebug)
            Debug.Log($"[Model3DManager] Cleaning up cache (current size: {glbCache.Count})");

        var sortedCache = new List<KeyValuePair<string, float>>(lastAccessTime);
        sortedCache.Sort((a, b) => a.Value.CompareTo(b.Value));

        int itemsToRemove = glbCache.Count - maxCacheSize;
        for (int i = 0; i < itemsToRemove && i < sortedCache.Count; i++)
        {
            string modelId = sortedCache[i].Key;

            if (glbCache.TryGetValue(modelId, out GameObject glb))
            {
                Destroy(glb);
                glbCache.Remove(modelId);
            }

            modelDataCache.Remove(modelId);
            lastAccessTime.Remove(modelId);

            if (showDebug)
                Debug.Log($"[Model3DManager] Removed from cache: {modelId}");
        }
    }

    #endregion
}
