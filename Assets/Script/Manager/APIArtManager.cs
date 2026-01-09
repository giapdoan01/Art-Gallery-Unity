using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
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

[Serializable]
public class FrameData
{
    public string id;
    public int frameUse;
    public string name;
}

[Serializable]
public class FramesResponse
{
    public bool success;
    public List<FrameData> data;
    public string message;
}

/// <summary>
/// APIArtManager - Singleton quản lý tất cả các API gọi đến server
/// CHỈ CHỊU TRÁCH NHIỆM: Gọi API và trả về response, KHÔNG cache, KHÔNG xử lý UI
/// </summary>
public class APIArtManager : MonoBehaviour
{
    private static APIArtManager _instance;

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
    public delegate void ImageResponseCallback(bool success, ImageData image, string error);
    public delegate void ImagesResponseCallback(bool success, List<ImageData> images, string error);
    public delegate void ActionResponseCallback(bool success, string message);
    public delegate void TextureResponseCallback(bool success, Texture2D texture, string error);

    public static APIArtManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("APIArtManager");
                _instance = go.AddComponent<APIArtManager>();
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

        if (logRequests) Debug.Log("[APIArtManager] Initialized");
    }

    #region GET APIs - Image Data

    /// <summary>
    /// Lấy ảnh theo frame ID - Endpoint: GET /admin/getimage/:frame
    /// </summary>
    public void GetImageByFrame(int frameId, ImageResponseCallback callback)
    {
        string url = $"{adminUrl}/getimage/{frameId}";
        StartCoroutine(GetRequest<ImageActionResponse>(url, (success, response, error) =>
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

    /// <summary>
    /// ✅ THÊM MỚI: Lấy tất cả ảnh với đầy đủ thông tin (dùng cho parallel loading)
    /// Endpoint: GET /admin/getallimages
    /// </summary>
    public void GetAllImagesWithDetails(ImagesResponseCallback callback)
    {
        string url = $"{adminUrl}/getallimages";
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

    /// <summary>
    /// Lấy tất cả các frames - Endpoint: GET /api/frames
    /// </summary>
    public void GetAllFrames(Action<bool, List<FrameData>, string> callback)
    {
        string url = $"{apiUrl}/frames";
        StartCoroutine(GetRequest<FramesResponse>(url, (success, response, error) =>
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

    #region GET APIs - Texture Download

    /// <summary>
    /// Tải texture từ URL - KHÔNG cache, chỉ tải và trả về
    /// </summary>
    public void DownloadTexture(string url, TextureResponseCallback callback)
    {
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(false, null, "URL is empty");
            return;
        }

        StartCoroutine(DownloadTextureCoroutine(url, callback));
    }

    private IEnumerator DownloadTextureCoroutine(string url, TextureResponseCallback callback)
    {
        Debug.Log($"[APIArtManager] 🔍 DEBUG - Starting download");
        Debug.Log($"[APIArtManager] 🔍 DEBUG - URL: {url}");
        Debug.Log($"[APIArtManager] 🔍 DEBUG - URL Length: {url?.Length}");
        Debug.Log($"[APIArtManager] 🔍 DEBUG - Starts with https: {url?.StartsWith("https://")}");
        if (logRequests)
            Debug.Log($"[APIArtManager] Downloading texture from: {url}");

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = (int)requestTimeout;

            yield return request.SendWebRequest();
            Debug.Log($"[APIArtManager] 🔍 DEBUG - Request completed");
            Debug.Log($"[APIArtManager] 🔍 DEBUG - Result: {request.result}");
            Debug.Log($"[APIArtManager] 🔍 DEBUG - Error: {request.error}");
            Debug.Log($"[APIArtManager] 🔍 DEBUG - Response Code: {request.responseCode}");
            Debug.Log($"[APIArtManager] 🔍 DEBUG - Response Text: {request.downloadHandler?.text}");

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);

                if (logResponses)
                    Debug.Log($"[APIArtManager] Texture downloaded successfully: {texture.width}x{texture.height}");

                callback?.Invoke(true, texture, null);
            }
            else
            {
                string error = $"Failed to download texture: {request.error}";
                Debug.LogError($"[APIArtManager] {error}");
                callback?.Invoke(false, null, error);
            }
        }
    }

    #endregion

    #region POST APIs - Create Image

    /// <summary>
    /// ✅ Tạo ảnh mới - Endpoint: POST /api/images
    /// </summary>
    public void CreateImage(ImageData imageData, byte[] imageBytes, ActionResponseCallback callback)
    {
        string url = $"{apiUrl}/images";

        if (logRequests)
        {
            Debug.Log($"[APIArtManager] Creating image:");
            Debug.Log($"  URL: {url}");
            Debug.Log($"  Frame: {imageData.frameUse}");
            Debug.Log($"  Name: {imageData.name}");
            Debug.Log($"  ImageType: {imageData.imageType}");
        }

        StartCoroutine(UploadImageRequest(url, imageData, imageBytes, "POST", callback));
    }

    #endregion

    #region PUT APIs - Update Image

    /// <summary>
    /// ✅ Cập nhật ảnh - Endpoint: PUT /api/images/frame/:frame
    /// </summary>
    public void UpdateImage(ImageData imageData, byte[] imageBytes, ActionResponseCallback callback)
    {
        string url = $"{apiUrl}/images/frame/{imageData.frameUse}";

        if (logRequests)
        {
            Debug.Log($"[APIArtManager] Updating image:");
            Debug.Log($"  URL: {url}");
            Debug.Log($"  Frame: {imageData.frameUse}");
            Debug.Log($"  Name: {imageData.name}");
            Debug.Log($"  ImageType: {imageData.imageType}"); // ✅ Log imageType
        }

        StartCoroutine(UploadImageRequest(url, imageData, imageBytes, "PUT", callback));
    }

    /// <summary>
    /// ✅ Upload image request - Unified cho cả POST và PUT
    /// Bao gồm imageType
    /// </summary>
    private IEnumerator UploadImageRequest(string url, ImageData imageData, byte[] imageBytes, string method, ActionResponseCallback callback)
    {
        if (currentRequests >= MAX_CONCURRENT_REQUESTS)
        {
            yield return new WaitUntil(() => currentRequests < MAX_CONCURRENT_REQUESTS);
        }

        currentRequests++;

        if (logRequests)
            Debug.Log($"[APIArtManager] {method} Request: {url}");

        WWWForm form = new WWWForm();

        // Add image data fields
        form.AddField("name", imageData.name ?? "");
        form.AddField("frameUse", imageData.frameUse);
        form.AddField("author", imageData.author ?? "");
        form.AddField("description", imageData.description ?? "");

        // ✅ Add imageType
        if (!string.IsNullOrEmpty(imageData.imageType))
        {
            form.AddField("imageType", imageData.imageType);

            if (logDetailedData)
                Debug.Log($"[APIArtManager] ImageType: {imageData.imageType}");
        }
        else
        {
            // Default nếu không có
            form.AddField("imageType", "ngang");

            if (logDetailedData)
                Debug.Log($"[APIArtManager] ImageType: ngang (default)");
        }

        // Add position (sử dụng format positionX, positionY, positionZ)
        if (imageData.position != null)
        {
            form.AddField("positionX", imageData.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("positionY", imageData.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("positionZ", imageData.position.z.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (logDetailedData)
                Debug.Log($"[APIArtManager] Position: ({imageData.position.x}, {imageData.position.y}, {imageData.position.z})");
        }

        // Add rotation (sử dụng format rotationX, rotationY, rotationZ)
        if (imageData.rotation != null)
        {
            form.AddField("rotationX", imageData.rotation.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("rotationY", imageData.rotation.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
            form.AddField("rotationZ", imageData.rotation.z.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (logDetailedData)
                Debug.Log($"[APIArtManager] Rotation: ({imageData.rotation.x}, {imageData.rotation.y}, {imageData.rotation.z})");
        }

        // Add image file if provided
        if (imageBytes != null && imageBytes.Length > 0)
        {
            form.AddBinaryData("image", imageBytes, "image.jpg", "image/jpeg");

            if (logDetailedData)
                Debug.Log($"[APIArtManager] Image size: {imageBytes.Length / 1024}KB");
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
                    Debug.Log($"[APIArtManager] {method} successful: {request.downloadHandler.text}");

                callback?.Invoke(true, "Image uploaded successfully");
            }
            else
            {
                string error = $"{method} failed: {request.error}";
                Debug.LogError($"[APIArtManager] {error}");
                Debug.LogError($"[APIArtManager] Response code: {request.responseCode}");
                Debug.LogError($"[APIArtManager] Response: {request.downloadHandler.text}");

                callback?.Invoke(false, error);
            }
        }
    }

    #endregion

    #region PUT APIs - Update Transform

    /// <summary>
    /// ✅ Cập nhật transform - Endpoint: PUT /api/images/frame/:frame
    /// Chỉ update position và rotation, KHÔNG update ảnh
    /// </summary>
    public void UpdateTransform(int frameId, Vector3 position, Vector3 rotation, ActionResponseCallback callback)
    {
        string url = $"{apiUrl}/images/frame/{frameId}";
        StartCoroutine(UpdateTransformRequest(url, frameId, position, rotation, callback));
    }

    private IEnumerator UpdateTransformRequest(string url, int frameId, Vector3 position, Vector3 rotation, ActionResponseCallback callback)
    {
        if (currentRequests >= MAX_CONCURRENT_REQUESTS)
        {
            yield return new WaitUntil(() => currentRequests < MAX_CONCURRENT_REQUESTS);
        }

        currentRequests++;

        if (logRequests)
            Debug.Log($"[APIArtManager] Updating transform for frame {frameId}");

        WWWForm form = new WWWForm();

        // Chỉ gửi position và rotation (sử dụng format positionX, positionY, positionZ)
        form.AddField("positionX", position.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("positionY", position.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("positionZ", position.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("rotationX", rotation.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("rotationY", rotation.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        form.AddField("rotationZ", rotation.z.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (logDetailedData)
        {
            Debug.Log($"[APIArtManager] UpdateTransform - Frame: {frameId}");
            Debug.Log($"[APIArtManager] UpdateTransform - Position: ({position.x}, {position.y}, {position.z})");
            Debug.Log($"[APIArtManager] UpdateTransform - Rotation: ({rotation.x}, {rotation.y}, {rotation.z})");
        }

        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            // Override method thành PUT
            request.method = "PUT";
            request.timeout = (int)requestTimeout;

            yield return request.SendWebRequest();

            currentRequests--;

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (logResponses)
                    Debug.Log($"[APIArtManager] Transform updated successfully: {request.downloadHandler.text}");

                callback?.Invoke(true, "Transform updated successfully");
            }
            else
            {
                string error = $"Transform update failed: {request.error}";
                Debug.LogError($"[APIArtManager] {error}");
                Debug.LogError($"[APIArtManager] Response code: {request.responseCode}");
                Debug.LogError($"[APIArtManager] Response: {request.downloadHandler.text}");

                callback?.Invoke(false, error);
            }
        }
    }

    #endregion

    #region DELETE APIs

    /// <summary>
    /// ✅ Xóa ảnh - Endpoint: DELETE /api/images/frame/:frame
    /// </summary>
    public void DeleteImage(int frameId, ActionResponseCallback callback)
    {
        string url = $"{apiUrl}/images/frame/{frameId}";
        StartCoroutine(DeleteRequest(url, callback));
    }

    private IEnumerator DeleteRequest(string url, ActionResponseCallback callback)
    {
        if (logRequests)
            Debug.Log($"[APIArtManager] Deleting image: {url}");

        using (UnityWebRequest request = UnityWebRequest.Delete(url))
        {
            request.timeout = (int)requestTimeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                if (logResponses)
                    Debug.Log($"[APIArtManager] Delete successful");

                callback?.Invoke(true, "Image deleted successfully");
            }
            else
            {
                string error = $"Delete failed: {request.error}";
                Debug.LogError($"[APIArtManager] {error}");
                callback?.Invoke(false, error);
            }
        }
    }

    #endregion

    #region Generic Request Handlers

    private IEnumerator GetRequest<T>(string url, Action<bool, T, string> callback) where T : class
    {
        // ✅ KHÔNG ĐỢI NẾU ĐẠT MAX - Cho phép nhiều requests song song hơn
        // if (currentRequests >= MAX_CONCURRENT_REQUESTS)
        // {
        //     yield return new WaitUntil(() => currentRequests < MAX_CONCURRENT_REQUESTS);
        // }

        currentRequests++;

        if (logRequests)
            Debug.Log($"[APIArtManager] GET Request: {url}");

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
                        Debug.Log($"[APIArtManager] Response: {jsonResponse}");

                    T response = JsonUtility.FromJson<T>(jsonResponse);
                    callback?.Invoke(true, response, null);
                }
                catch (Exception ex)
                {
                    string error = $"JSON Parse Error: {ex.Message}";
                    Debug.LogError($"[APIArtManager] {error}");
                    callback?.Invoke(false, null, error);
                }
            }
            else
            {
                string error = $"Request failed: {request.error}";
                Debug.LogError($"[APIArtManager] {error}");
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
