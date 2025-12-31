using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;

/// <summary>
/// Dữ liệu cho mỗi model 3D
/// </summary>
[Serializable]
public class Model3DData
{
    public string id;
    public string name;
    public string url;
    public string author;
    public string description;
    public float rotation_x;
    public float rotation_y;
    public float rotation_z;
    public float position_x;
    public float position_y;
    public float position_z;
    public float scale_x = 1;
    public float scale_y = 1;
    public float scale_z = 1;
    public string public_id;
    public string created_at;
    public string updated_at;
    public string created_by;
    public string updated_by;

    // Helper properties để chuyển đổi dữ liệu sang các kiểu Unity
    public Vector3 position
    {
        get { return new Vector3(position_x, position_y, position_z); }
        set { position_x = value.x; position_y = value.y; position_z = value.z; }
    }

    public Vector3 rotation
    {
        get { return new Vector3(rotation_x, rotation_y, rotation_z); }
        set { rotation_x = value.x; rotation_y = value.y; rotation_z = value.z; }
    }

    public Vector3 scale
    {
        get { return new Vector3(scale_x, scale_y, scale_z); }
        set { scale_x = value.x; scale_y = value.y; scale_z = value.z; }
    }
}

/// <summary>
/// Response chung cho các API trả về danh sách model 3D
/// </summary>
[Serializable]
public class Model3DsResponse
{
    public bool success;
    public List<Model3DData> data;
    public string message;
}

/// <summary>
/// Response cho API thao tác (tạo, cập nhật) model 3D
/// </summary>
[Serializable]
public class Model3DActionResponse
{
    public bool success;
    public Model3DData data;
    public string message;
}

/// <summary>
/// API3DModelManager - Singleton quản lý tất cả các API gọi đến server liên quan đến model 3D
/// CHỈ CHỊU TRÁCH NHIỆM: Gọi API và trả về response, KHÔNG cache, KHÔNG xử lý UI
/// </summary>
public class API3DModelManager : MonoBehaviour
{
    private static API3DModelManager _instance;

    [Header("Server Settings")]
    [SerializeField] private string baseUrl = "https://gallery-server-mutilplayer.onrender.com";
    [SerializeField] private string apiUrl = "https://gallery-server-mutilplayer.onrender.com/api";
    [SerializeField] private string adminUrl = "https://gallery-server-mutilplayer.onrender.com/admin";
    [SerializeField] private float requestTimeout = 30f; // Tăng timeout cho upload
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float retryDelay = 2f;

    [Header("Debug")]
    [SerializeField] private bool logRequests = true;
    [SerializeField] private bool logResponses = true;
    [SerializeField] private bool logDetailedData = true;

    private int currentRequests = 0;
    private const int MAX_CONCURRENT_REQUESTS = 10;

    // Delegates cho callbacks
    public delegate void Model3DResponseCallback(bool success, Model3DData model, string error);
    public delegate void Model3DsResponseCallback(bool success, List<Model3DData> models, string error);
    public delegate void ActionResponseCallback(bool success, string message);

    public static API3DModelManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("API3DModelManager");
                _instance = go.AddComponent<API3DModelManager>();
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

        if (logRequests) Debug.Log("[API3DModelManager] Initialized");
    }

    #region GET APIs - Model3D Data

    /// <summary>
    /// Lấy danh sách tất cả model 3D - Endpoint: GET /api/model3d
    /// </summary>
    public void GetAllModels(Model3DsResponseCallback callback)
    {
        string url = $"{apiUrl}/model3d";
        StartCoroutine(GetRequest<Model3DsResponse>(url, (success, response, error) =>
        {
            if (success && response != null && response.success)
            {
                callback?.Invoke(true, response.data, null);
            }
            else
            {
                callback?.Invoke(false, null, error ?? response?.message ?? "Unknown error");
            }
        }));
    }

    /// <summary>
    /// Lấy thông tin model 3D theo ID - Endpoint: GET /api/model3d/:id
    /// </summary>
    public void GetModelById(string id, Model3DResponseCallback callback)
    {
        string url = $"{apiUrl}/model3d/{id}";

        if (logRequests)
            Debug.Log($"[API3DModelManager] GetModelById: {url}");

        StartCoroutine(GetRequest<Model3DActionResponse>(url, (success, response, error) =>
        {
            if (logResponses)
            {
                if (success && response != null)
                {
                    Debug.Log($"[API3DModelManager] GetModelById Response - Success: {response.success}, HasData: {response.data != null}");
                    if (response.data != null)
                    {
                        Debug.Log($"[API3DModelManager] Model Data - ID: {response.data.id}, Name: {response.data.name}, URL: {response.data.url}");
                    }
                }
                else
                {
                    Debug.LogError($"[API3DModelManager] GetModelById Failed - Error: {error}");
                }
            }

            if (success && response != null && response.success && response.data != null)
            {
                callback?.Invoke(true, response.data, null);
            }
            else
            {
                string errorMsg = error ?? response?.message ?? "Unknown error";
                Debug.LogError($"[API3DModelManager] GetModelById failed for ID {id}: {errorMsg}");
                callback?.Invoke(false, null, errorMsg);
            }
        }));
    }

    #endregion

    #region POST APIs - Create Model3D

    /// <summary>
    /// Tạo model 3D mới - Endpoint: POST /api/model3d
    /// </summary>
    public void CreateModel(Model3DData modelData, byte[] modelFileBytes, ActionResponseCallback callback)
    {
        string url = $"{apiUrl}/model3d";

        if (logRequests)
        {
            Debug.Log($"[API3DModelManager] Creating model 3D:");
            Debug.Log($"  URL: {url}");
            Debug.Log($"  Name: {modelData.name}");
        }

        StartCoroutine(UploadModelRequest(url, modelData, modelFileBytes, "POST", callback));
    }

    #endregion

    #region PUT APIs - Update Model3D

    /// <summary>
    /// Cập nhật model 3D - Endpoint: PUT /api/model3d/:id
    /// </summary>
    public void UpdateModel(string id, Model3DData modelData, byte[] modelFileBytes, ActionResponseCallback callback)
    {
        string url = $"{apiUrl}/model3d/{id}";

        if (logRequests)
        {
            Debug.Log($"[API3DModelManager] Updating model 3D:");
            Debug.Log($"  URL: {url}");
            Debug.Log($"  ID: {id}");
            Debug.Log($"  Name: {modelData.name}");
        }

        StartCoroutine(UploadModelRequest(url, modelData, modelFileBytes, "PUT", callback));
    }

    /// <summary>
    /// Upload model request - Unified cho cả POST và PUT
    /// </summary>
    private IEnumerator UploadModelRequest(string url, Model3DData modelData, byte[] modelFileBytes, string method, ActionResponseCallback callback)
    {
        if (currentRequests >= MAX_CONCURRENT_REQUESTS)
        {
            yield return new WaitUntil(() => currentRequests < MAX_CONCURRENT_REQUESTS);
        }

        currentRequests++;

        if (logRequests)
            Debug.Log($"[API3DModelManager] {method} Request: {url}");

        WWWForm form = new WWWForm();

        // Add model data fields
        form.AddField("name", modelData.name ?? "");

        if (!string.IsNullOrEmpty(modelData.author))
            form.AddField("author", modelData.author);

        if (!string.IsNullOrEmpty(modelData.description))
            form.AddField("description", modelData.description);

        // Add transform data
        form.AddField("rotationX", modelData.rotation_x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("rotationY", modelData.rotation_y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("rotationZ", modelData.rotation_z.ToString(System.Globalization.CultureInfo.InvariantCulture));

        form.AddField("positionX", modelData.position_x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("positionY", modelData.position_y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("positionZ", modelData.position_z.ToString(System.Globalization.CultureInfo.InvariantCulture));

        form.AddField("scaleX", modelData.scale_x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("scaleY", modelData.scale_y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("scaleZ", modelData.scale_z.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (logDetailedData)
        {
            Debug.Log($"[API3DModelManager] Position: ({modelData.position_x}, {modelData.position_y}, {modelData.position_z})");
            Debug.Log($"[API3DModelManager] Rotation: ({modelData.rotation_x}, {modelData.rotation_y}, {modelData.rotation_z})");
            Debug.Log($"[API3DModelManager] Scale: ({modelData.scale_x}, {modelData.scale_y}, {modelData.scale_z})");
        }

        // Add model file if provided
        if (modelFileBytes != null && modelFileBytes.Length > 0)
        {
            form.AddBinaryData("model3dFile", modelFileBytes, "model.glb", "application/octet-stream");

            if (logDetailedData)
                Debug.Log($"[API3DModelManager] Model file size: {modelFileBytes.Length / 1024}KB");
        }

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            // Override method nếu là PUT
            if (method == "PUT")
            {
                request.method = "PUT";
            }

            request.timeout = (int)requestTimeout;

            yield return request.SendWebRequest();

            currentRequests--;

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (logResponses)
                    Debug.Log($"[API3DModelManager] {method} successful: {request.downloadHandler.text}");

                callback?.Invoke(true, $"Model {(method == "POST" ? "created" : "updated")} successfully");
            }
            else
            {
                string error = $"{method} failed: {request.error}";
                Debug.LogError($"[API3DModelManager] {error}");
                Debug.LogError($"[API3DModelManager] Response code: {request.responseCode}");
                Debug.LogError($"[API3DModelManager] Response: {request.downloadHandler.text}");

                callback?.Invoke(false, error);
            }
        }
    }

    #endregion

    #region DELETE APIs

    /// <summary>
    /// Xóa model 3D - Endpoint: DELETE /api/model3d/:id
    /// </summary>
    public void DeleteModel(string id, ActionResponseCallback callback)
    {
        string url = $"{apiUrl}/model3d/{id}";
        StartCoroutine(DeleteRequest(url, callback));
    }

    private IEnumerator DeleteRequest(string url, ActionResponseCallback callback)
    {
        if (logRequests)
            Debug.Log($"[API3DModelManager] Deleting model: {url}");

        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            request.timeout = (int)requestTimeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (logResponses)
                    Debug.Log($"[API3DModelManager] Delete successful");

                callback?.Invoke(true, "Model deleted successfully");
            }
            else
            {
                string error = $"Delete failed: {request.error}";
                Debug.LogError($"[API3DModelManager] {error}");
                callback?.Invoke(false, error);
            }
        }
    }

    #endregion

    #region Generic Request Handlers

    private IEnumerator GetRequest<T>(string url, Action<bool, T, string> callback) where T : class
    {
        currentRequests++;

        if (logRequests)
            Debug.Log($"[API3DModelManager] GET Request: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = (int)requestTimeout;

            yield return request.SendWebRequest();

            currentRequests--;

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string jsonResponse = request.downloadHandler.text;

                    if (logResponses)
                    {
                        Debug.Log($"[API3DModelManager] Response ({request.responseCode}): {jsonResponse}");
                    }

                    T response = JsonUtility.FromJson<T>(jsonResponse);

                    if (response == null)
                    {
                        Debug.LogError($"[API3DModelManager] Failed to parse JSON response");
                        callback?.Invoke(false, null, "Failed to parse JSON");
                    }
                    else
                    {
                        callback?.Invoke(true, response, null);
                    }
                }
                catch (Exception ex)
                {
                    string error = $"JSON Parse Error: {ex.Message}";
                    Debug.LogError($"[API3DModelManager] {error}");
                    Debug.LogError($"[API3DModelManager] Response was: {request.downloadHandler.text}");
                    callback?.Invoke(false, null, error);
                }
            }
            else
            {
                string error = $"Request failed ({request.responseCode}): {request.error}";
                Debug.LogError($"[API3DModelManager] {error}");
                Debug.LogError($"[API3DModelManager] Response: {request.downloadHandler.text}");
                callback?.Invoke(false, null, error);
            }
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Kiểm tra xem có đang có request nào đang chạy không
    /// </summary>
    public bool HasActiveRequests()
    {
        return currentRequests > 0;
    }

    /// <summary>
    /// Lấy số lượng request đang chạy
    /// </summary>
    public int GetActiveRequestCount()
    {
        return currentRequests;
    }

    #endregion
}