using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Text;

/// <summary>
/// Response chung cho các API trả về danh sách ảnh
/// </summary>
[Serializable]
public class ImagesResponse
{
    public bool success;
    public List<ImageData> data;
    public string message;
}

/// <summary>
/// Response cho API thao tác (tạo, cập nhật, xóa) ảnh
/// </summary>
[Serializable]
public class ImageActionResponse
{
    public bool success;
    public ImageData data;
    public string message;
}

/// <summary>
/// APIManager - Singleton quản lý tất cả các API gọi đến server
/// </summary>
public class APIManager : MonoBehaviour
{
    private static APIManager _instance;

    [Header("Server Settings")]
    [SerializeField] private string baseUrl = "https://gallery-server-mutilplayer.onrender.com";
    [SerializeField] private string apiUrl = "https://gallery-server-mutilplayer.onrender.com/api";
    [SerializeField] private string adminUrl = "https://gallery-server-mutilplayer.onrender.com/admin";
    [SerializeField] private float requestTimeout = 10f;
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float retryDelay = 2f;

    [Header("Debug")]
    [SerializeField] private bool logRequests = true;
    [SerializeField] private bool logResponses = false;
    [SerializeField] private bool logDetailedData = true;  // Thêm flag để log chi tiết dữ liệu

    private int currentRequests = 0;
    private const int MAX_CONCURRENT_REQUESTS = 5;

    public delegate void ImageResponseCallback(bool success, ImageData image, string error);
    public delegate void ImagesResponseCallback(bool success, List<ImageData> images, string error);
    public delegate void ActionResponseCallback(bool success, string message);

    public static APIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("APIManager");
                _instance = go.AddComponent<APIManager>();
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

        if (logRequests) Debug.Log("[APIManager] Initialized");
    }

    #region GET APIs

    /// <summary>
    /// Lấy ảnh theo frame ID - Endpoint: GET /admin/getimage/:frame
    /// </summary>
    public void GetImageByFrame(int frameId, ImageResponseCallback callback)
    {
        string url = $"{adminUrl}/getimage/{frameId}";
        StartCoroutine(GetRequest<ImageResponse>(url, (success, response, error) =>
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
    /// Lấy tất cả các ảnh - Endpoint: GET /api/images
    /// </summary>
    public void GetAllImages(ImagesResponseCallback callback)
    {
        string url = $"{apiUrl}/images";
        StartCoroutine(GetRequest<ImagesResponse>(url, (success, response, error) =>
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

    #endregion

    #region PUT APIs

    /// <summary>
    /// Cập nhật ảnh theo frame - Endpoint: PUT /api/images/frame/:frame
    /// </summary>
    public void UpdateImageByFrame(
    int frameId,
    string name,
    string author,
    string description,
    Vector3 position,
    Vector3 rotationEuler,
    Texture2D imageTexture,
    ImageResponseCallback callback)
    {
        string url = $"{apiUrl}/images/frame/{frameId}";

        WWWForm form = new WWWForm();
        form.AddField("name", name);

        if (!string.IsNullOrEmpty(author))
            form.AddField("author", author);

        if (!string.IsNullOrEmpty(description))
            form.AddField("description", description);

        // Gửi các trường positionX, positionY, positionZ riêng biệt theo đúng định dạng server mong đợi
        form.AddField("positionX", position.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("positionY", position.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("positionZ", position.z.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // Gửi các trường rotationX, rotationY, rotationZ riêng biệt
        form.AddField("rotationX", rotationEuler.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("rotationY", rotationEuler.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("rotationZ", rotationEuler.z.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (logDetailedData)
        {
            Debug.Log($"[APIManager] UpdateImageByFrame - Position: ({position.x}, {position.y}, {position.z})");
            Debug.Log($"[APIManager] UpdateImageByFrame - Rotation: ({rotationEuler.x}, {rotationEuler.y}, {rotationEuler.z})");
        }

        if (imageTexture != null)
        {
            byte[] imageBytes = imageTexture.EncodeToPNG();
            form.AddBinaryData("image", imageBytes, "image.png", "image/png");
        }

        StartCoroutine(PutRequest<ImageActionResponse>(url, form, (success, response, error) =>
        {
            if (success && response != null && response.success)
                callback?.Invoke(true, response.data, null);
            else
                callback?.Invoke(false, null, error ?? response?.message ?? "Unknown error");
        }));
    }

    /// <summary>
    /// Cập nhật ảnh theo frame từ đường dẫn file - Endpoint: PUT /api/images/frame/:frame
    /// </summary>
    public void UpdateImageByFrameFromPath(
    int frameId,
    string name,
    string author,
    string description,
    Vector3 position,
    Vector3 rotationEuler,
    string localFilePath,
    ImageResponseCallback callback)
    {
        if (!File.Exists(localFilePath))
        {
            callback?.Invoke(false, null, $"File not found: {localFilePath}");
            return;
        }

        try
        {
            byte[] imageBytes = File.ReadAllBytes(localFilePath);
            string mimeType = GetMimeTypeFromExtension(Path.GetExtension(localFilePath));
            string fileName = Path.GetFileName(localFilePath);

            string url = $"{apiUrl}/images/frame/{frameId}";
            WWWForm form = new WWWForm();
            form.AddField("name", name);

            if (!string.IsNullOrEmpty(author))
                form.AddField("author", author);

            if (!string.IsNullOrEmpty(description))
                form.AddField("description", description);

            // Gửi các trường positionX, positionY, positionZ riêng biệt theo đúng định dạng server mong đợi
            form.AddField("positionX", position.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("positionY", position.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("positionZ", position.z.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // Gửi các trường rotationX, rotationY, rotationZ riêng biệt
            form.AddField("rotationX", rotationEuler.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("rotationY", rotationEuler.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("rotationZ", rotationEuler.z.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (logDetailedData)
            {
                Debug.Log($"[APIManager] UpdateImageByFrameFromPath - Position: ({position.x}, {position.y}, {position.z})");
                Debug.Log($"[APIManager] UpdateImageByFrameFromPath - Rotation: ({rotationEuler.x}, {rotationEuler.y}, {rotationEuler.z})");
            }

            form.AddBinaryData("image", imageBytes, fileName, mimeType);

            StartCoroutine(PutRequest<ImageActionResponse>(url, form, (success, response, error) =>
            {
                if (success && response != null && response.success)
                    callback?.Invoke(true, response.data, null);
                else
                    callback?.Invoke(false, null, error ?? response?.message ?? "Unknown error");
            }));
        }
        catch (Exception ex)
        {
            callback?.Invoke(false, null, $"Error reading file: {ex.Message}");
        }
    }
    #endregion

    #region Helper Methods

    /// <summary>
    /// GET Request generic với retry
    /// </summary>
    private IEnumerator GetRequest<T>(string url, Action<bool, T, string> callback, int retryCount = 0) where T : class
    {
        if (currentRequests >= MAX_CONCURRENT_REQUESTS)
        {
            yield return new WaitUntil(() => currentRequests < MAX_CONCURRENT_REQUESTS);
        }

        currentRequests++;

        if (logRequests) Debug.Log($"[APIManager] GET Request: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.RoundToInt(requestTimeout);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (retryCount < maxRetries)
                {
                    Debug.LogWarning($"[APIManager] Request failed: {request.error}. Retrying {retryCount + 1}/{maxRetries}...");
                    currentRequests--;
                    yield return new WaitForSeconds(retryDelay);
                    yield return StartCoroutine(GetRequest<T>(url, callback, retryCount + 1));
                    yield break;
                }

                Debug.LogError($"[APIManager] Request failed after {maxRetries} retries: {request.error}");
                callback?.Invoke(false, null, request.error);
                currentRequests--;
                yield break;
            }

            string responseText = request.downloadHandler.text;

            if (logResponses) Debug.Log($"[APIManager] Response: {responseText}");

            try
            {
                T response = JsonUtility.FromJson<T>(responseText);
                callback?.Invoke(true, response, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] Failed to parse response: {e.Message}");
                callback?.Invoke(false, null, $"Parsing error: {e.Message}");
            }

            currentRequests--;
        }
    }

    /// <summary>
    /// POST Request generic với retry
    /// </summary>
    private IEnumerator PostRequest<T>(string url, WWWForm form, Action<bool, T, string> callback, int retryCount = 0) where T : class
    {
        if (currentRequests >= MAX_CONCURRENT_REQUESTS)
        {
            yield return new WaitUntil(() => currentRequests < MAX_CONCURRENT_REQUESTS);
        }

        currentRequests++;

        if (logRequests) Debug.Log($"[APIManager] POST Request: {url}");

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            request.timeout = Mathf.RoundToInt(requestTimeout);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (retryCount < maxRetries)
                {
                    Debug.LogWarning($"[APIManager] Request failed: {request.error}. Retrying {retryCount + 1}/{maxRetries}...");
                    currentRequests--;
                    yield return new WaitForSeconds(retryDelay);
                    yield return StartCoroutine(PostRequest<T>(url, form, callback, retryCount + 1));
                    yield break;
                }

                Debug.LogError($"[APIManager] Request failed after {maxRetries} retries: {request.error}");
                callback?.Invoke(false, null, request.error);
                currentRequests--;
                yield break;
            }

            string responseText = request.downloadHandler.text;

            if (logResponses) Debug.Log($"[APIManager] Response: {responseText}");

            try
            {
                T response = JsonUtility.FromJson<T>(responseText);
                callback?.Invoke(true, response, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] Failed to parse response: {e.Message}");
                callback?.Invoke(false, null, $"Parsing error: {e.Message}");
            }

            currentRequests--;
        }
    }

    /// <summary>
    /// PUT Request generic với retry
    /// </summary>
    private IEnumerator PutRequest<T>(string url, WWWForm form, Action<bool, T, string> callback, int retryCount = 0) where T : class
    {
        if (currentRequests >= MAX_CONCURRENT_REQUESTS)
        {
            yield return new WaitUntil(() => currentRequests < MAX_CONCURRENT_REQUESTS);
        }

        currentRequests++;

        if (logRequests) Debug.Log($"[APIManager] PUT Request: {url}");

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            request.method = "PUT";
            request.timeout = Mathf.RoundToInt(requestTimeout);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (retryCount < maxRetries)
                {
                    Debug.LogWarning($"[APIManager] Request failed: {request.error}. Retrying {retryCount + 1}/{maxRetries}...");
                    currentRequests--;
                    yield return new WaitForSeconds(retryDelay);
                    yield return StartCoroutine(PutRequest<T>(url, form, callback, retryCount + 1));
                    yield break;
                }

                Debug.LogError($"[APIManager] Request failed after {maxRetries} retries: {request.error}");
                callback?.Invoke(false, null, request.error);
                currentRequests--;
                yield break;
            }

            string responseText = request.downloadHandler.text;

            if (logResponses) Debug.Log($"[APIManager] Response: {responseText}");

            try
            {
                T response = JsonUtility.FromJson<T>(responseText);
                callback?.Invoke(true, response, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"[APIManager] Failed to parse response: {e.Message}");
                callback?.Invoke(false, null, $"Parsing error: {e.Message}");
            }

            currentRequests--;
        }
    }

    /// <summary>
    /// Lấy MIME type từ extension của file
    /// </summary>
    private string GetMimeTypeFromExtension(string extension)
    {
        extension = extension.ToLower().TrimStart('.');

        switch (extension)
        {
            case "jpg":
            case "jpeg":
                return "image/jpeg";
            case "png":
                return "image/png";
            case "gif":
                return "image/gif";
            case "bmp":
                return "image/bmp";
            case "webp":
                return "image/webp";
            default:
                return "application/octet-stream";
        }
    }

    #endregion

    #region Utils

    /// <summary>
    /// Phương thức trợ giúp tải Texture2D từ URL
    /// </summary>
    public void LoadTextureFromUrl(string url, Action<Texture2D> callback)
    {
        StartCoroutine(DownloadTexture(url, callback));
    }

    private IEnumerator DownloadTexture(string url, Action<Texture2D> callback)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                callback?.Invoke(texture);
            }
            else
            {
                Debug.LogError($"[APIManager] Failed to download texture: {request.error}");
                callback?.Invoke(null);
            }
        }
    }

    /// <summary>
    /// Chuyển đổi ảnh từ đường dẫn thành Texture2D
    /// </summary>
    public Texture2D LoadTextureFromPath(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                byte[] data = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2);
                texture.LoadImage(data);
                return texture;
            }
            else
            {
                Debug.LogError($"[APIManager] File not found: {path}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[APIManager] Error loading texture: {ex.Message}");
            return null;
        }
    }

    #endregion

    // Public getter cho các URL
    public string BaseURL => baseUrl;
    public string ApiURL => apiUrl;
    public string AdminURL => adminUrl;

    // Setter để có thể thay đổi URLs trong runtime nếu cần
    public void SetURLs(string newBaseUrl)
    {
        baseUrl = newBaseUrl;
        apiUrl = $"{newBaseUrl}/api";
        adminUrl = $"{newBaseUrl}/admin";

        if (logRequests) Debug.Log($"[APIManager] URLs updated. Base: {baseUrl}");
    }
}