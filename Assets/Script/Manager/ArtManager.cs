using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Security.Cryptography;
using System.Text;

[Serializable]
public class ImageResponse
{
    public bool success;
    public ImageData data;
    public string message;
}

[Serializable]
public class ImageData
{
    public int id;
    public string name;
    public string url;
    public int frameUse;
    public string hash; // Hash từ server
    public long lastModified; // Timestamp
    public string author; // Thêm trường author
    public string description; // Thêm trường description
}

[Serializable]
public class FrameListResponse
{
    public bool success;
    public FrameInfo[] frames;
    public string message;
}

[Serializable]
public class FrameInfo
{
    public int id;
    public string hash;
    public long lastModified;
}

public class ArtManager : MonoBehaviour
{
    private static ArtManager _instance;

    [Header("Server Settings")]
    [SerializeField] private string baseUrl = "https://gallery-server-mutilplayer.onrender.com/admin";
    [SerializeField] private float requestTimeout = 10f;
    [SerializeField] private bool cacheImages = true;

    [Header("Loading Settings")]
    [SerializeField] private int maxConcurrentDownloads = 1; // Tải tối đa 1 ảnh/lúc
    [SerializeField] private float downloadDelay = 0.5f; // Delay giữa các lần tải
    [SerializeField] private bool onlyRefreshWhenIdle = true; // Chỉ refresh khi không di chuyển

    [Header("Background Loading")]
    [SerializeField] private bool useBackgroundLoading = true;
    [SerializeField] private int maxFramesPerSecond = 30; // Giới hạn FPS khi load
    [SerializeField] private float maxLoadTimePerFrame = 0.016f; // Max 16ms/frame

    [Header("Player Detection")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float idleThreshold = 0.1f; // Tốc độ < 0.1 = idle

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Cache
    private Dictionary<int, Sprite> imageCache = new Dictionary<int, Sprite>();
    private Dictionary<int, string> imageHashCache = new Dictionary<int, string>();
    private Dictionary<int, long> lastModifiedCache = new Dictionary<int, long>();
    private Dictionary<int, float> lastLoadTime = new Dictionary<int, float>();

    // Loading queue
    private Queue<int> downloadQueue = new Queue<int>();
    private HashSet<int> loadingFrames = new HashSet<int>();
    private Dictionary<int, List<ImageLoadCallback>> pendingCallbacks = new Dictionary<int, List<ImageLoadCallback>>();

    // Performance tracking
    private int currentDownloads = 0;
    private float lastFrameTime = 0f;
    private Vector3 lastPlayerPosition;
    private bool isPlayerIdle = true;

    public delegate void ImageLoadCallback(Sprite sprite);
    public event Action<int, Sprite> OnImageUpdated;
    public event Action<int, string> OnImageLoadError;

    public static ArtManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ArtManager");
                _instance = go.AddComponent<ArtManager>();
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

        if (showDebug) Debug.Log("[ArtManager] Initialized - Manual Refresh Version");
    }

    private void Start()
    {
        // Tự động tìm player nếu chưa gán
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerTransform != null)
        {
            lastPlayerPosition = playerTransform.position;
        }

        // Khởi động coroutine tải ảnh nền
        StartCoroutine(BackgroundDownloadRoutine());
    }

    private void Update()
    {
        // Kiểm tra player có đang idle không
        if (playerTransform != null && onlyRefreshWhenIdle)
        {
            float distance = Vector3.Distance(playerTransform.position, lastPlayerPosition);
            float speed = distance / Time.deltaTime;
            
            isPlayerIdle = speed < idleThreshold;
            lastPlayerPosition = playerTransform.position;
        }
        else
        {
            isPlayerIdle = true; // Luôn cho phép refresh nếu không check idle
        }
    }

    /// <summary>
    /// Kiểm tra một frame có cần cập nhật không (chỉ lấy metadata)
    /// </summary>
    private IEnumerator CheckFrameForUpdate(int frameId)
    {
        string url = $"{baseUrl}/getimage/{frameId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.RoundToInt(requestTimeout);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (showDebug) Debug.LogWarning($"[ArtManager] Không thể check frame {frameId}: {request.error}");
                yield break;
            }

            ImageResponse response = JsonUtility.FromJson<ImageResponse>(request.downloadHandler.text);

            if (!response.success || response.data == null)
            {
                yield break;
            }

            // So sánh hash hoặc lastModified
            bool needsUpdate = false;

            // Nếu server trả về hash
            if (!string.IsNullOrEmpty(response.data.hash))
            {
                if (!imageHashCache.ContainsKey(frameId) || imageHashCache[frameId] != response.data.hash)
                {
                    needsUpdate = true;
                }
            }
            // Hoặc dùng lastModified
            else if (response.data.lastModified > 0)
            {
                if (!lastModifiedCache.ContainsKey(frameId) || lastModifiedCache[frameId] < response.data.lastModified)
                {
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                if (showDebug) Debug.Log($"[ArtManager] Frame {frameId} có cập nhật mới, thêm vào queue");
                
                // Thêm vào queue để tải sau
                if (!downloadQueue.Contains(frameId) && !loadingFrames.Contains(frameId))
                {
                    downloadQueue.Enqueue(frameId);
                }
            }
        }
    }

    /// <summary>
    /// Background download - Tải ảnh từ queue một cách mượt mà
    /// </summary>
    private IEnumerator BackgroundDownloadRoutine()
    {
        while (true)
        {
            yield return null;

            // Không tải nếu đang có quá nhiều downloads
            if (currentDownloads >= maxConcurrentDownloads)
            {
                continue;
            }

            // Không tải nếu player đang di chuyển
            if (!isPlayerIdle && onlyRefreshWhenIdle)
            {
                continue;
            }

            // Không tải nếu FPS thấp
            if (useBackgroundLoading && Time.deltaTime > maxLoadTimePerFrame * 2)
            {
                if (showDebug) Debug.LogWarning("[ArtManager] FPS thấp, tạm dừng download");
                yield return new WaitForSeconds(1f);
                continue;
            }

            // Lấy frame từ queue
            if (downloadQueue.Count > 0)
            {
                int frameId = downloadQueue.Dequeue();

                if (showDebug) Debug.Log($"[ArtManager] Bắt đầu tải frame {frameId} từ queue ({downloadQueue.Count} còn lại)");

                // Tải ảnh
                StartCoroutine(DownloadFrameInBackground(frameId));

                // Delay giữa các lần tải
                yield return new WaitForSeconds(downloadDelay);
            }
            else
            {
                // Queue rỗng, đợi
                yield return new WaitForSeconds(0.5f);
            }
        }
    }

    /// <summary>
    /// Tải ảnh trong background không block main thread
    /// </summary>
    private IEnumerator DownloadFrameInBackground(int frameId)
    {
        currentDownloads++;
        loadingFrames.Add(frameId);

        // Lấy URL từ server
        string imageUrl = null;
        string url = $"{baseUrl}/getimage/{frameId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = Mathf.RoundToInt(requestTimeout);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ImageResponse response = JsonUtility.FromJson<ImageResponse>(request.downloadHandler.text);
                if (response.success && response.data != null)
                {
                    imageUrl = response.data.url;
                    
                    // Lưu hash nếu có
                    if (!string.IsNullOrEmpty(response.data.hash))
                    {
                        imageHashCache[frameId] = response.data.hash;
                    }
                    
                    if (response.data.lastModified > 0)
                    {
                        lastModifiedCache[frameId] = response.data.lastModified;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(imageUrl))
        {
            currentDownloads--;
            loadingFrames.Remove(frameId);
            yield break;
        }

        // Tải ảnh với cache busting
        string cacheBustedUrl = $"{imageUrl}?_t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(cacheBustedUrl))
        {
            request.timeout = Mathf.RoundToInt(requestTimeout);
            request.SetRequestHeader("Cache-Control", "no-cache");

            // Download với progress tracking
            var operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                // Yield mỗi frame để không block
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);

                if (texture != null)
                {
                    // Tạo sprite
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );

                    // Cleanup old sprite
                    if (imageCache.ContainsKey(frameId) && imageCache[frameId] != null)
                    {
                        Texture2D oldTexture = imageCache[frameId].texture;
                        if (oldTexture != null && oldTexture != texture)
                        {
                            Destroy(oldTexture);
                        }
                        Destroy(imageCache[frameId]);
                    }

                    // Cache
                    imageCache[frameId] = sprite;
                    lastLoadTime[frameId] = Time.time;

                    if (showDebug) Debug.Log($"[ArtManager] ✓ Đã cập nhật frame {frameId} thành công");

                    // Notify
                    OnImageUpdated?.Invoke(frameId, sprite);
                    InvokeAllCallbacks(frameId, sprite);
                }
            }
            else
            {
                if (showDebug) Debug.LogError($"[ArtManager] Lỗi tải ảnh frame {frameId}: {request.error}");
                OnImageLoadError?.Invoke(frameId, request.error);
            }
        }

        currentDownloads--;
        loadingFrames.Remove(frameId);
    }

    /// <summary>
    /// Get image - Ưu tiên cache, không block
    /// </summary>
    public void GetImageForFrame(int frameId, ImageLoadCallback onSuccess, bool forceRefresh = false)
    {
        if (frameId <= 0)
        {
            Debug.LogError($"[ArtManager] Frame ID không hợp lệ: {frameId}");
            onSuccess?.Invoke(null);
            return;
        }

        // Check cache
        if (!forceRefresh && imageCache.TryGetValue(frameId, out Sprite cachedSprite))
        {
            if (showDebug) Debug.Log($"[ArtManager] Frame {frameId} từ cache");
            onSuccess?.Invoke(cachedSprite);
            return;
        }

        // Thêm callback vào pending
        if (!pendingCallbacks.ContainsKey(frameId))
        {
            pendingCallbacks[frameId] = new List<ImageLoadCallback>();
        }
        if (onSuccess != null)
        {
            pendingCallbacks[frameId].Add(onSuccess);
        }

        // Thêm vào queue nếu chưa có
        if (!loadingFrames.Contains(frameId) && !downloadQueue.Contains(frameId))
        {
            if (forceRefresh)
            {
                // Force refresh - ưu tiên cao, thêm vào đầu queue
                var tempQueue = new Queue<int>();
                tempQueue.Enqueue(frameId);
                while (downloadQueue.Count > 0)
                {
                    tempQueue.Enqueue(downloadQueue.Dequeue());
                }
                downloadQueue = tempQueue;
            }
            else
            {
                downloadQueue.Enqueue(frameId);
            }

            if (showDebug) Debug.Log($"[ArtManager] Thêm frame {frameId} vào download queue");
        }
    }

    private void InvokeAllCallbacks(int frameId, Sprite sprite)
    {
        if (pendingCallbacks.ContainsKey(frameId))
        {
            foreach (var callback in pendingCallbacks[frameId])
            {
                try
                {
                    callback?.Invoke(sprite);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ArtManager] Lỗi callback frame {frameId}: {e.Message}");
                }
            }
            pendingCallbacks[frameId].Clear();
            pendingCallbacks.Remove(frameId);
        }
    }

    // ============================================
    // PUBLIC METHODS
    // ============================================

    /// <summary>
    /// Kiểm tra cập nhật cho một frame cụ thể theo yêu cầu
    /// </summary>
    public void CheckFrameUpdate(int frameId)
    {
        if (showDebug) Debug.Log($"[ArtManager] Kiểm tra cập nhật cho frame {frameId}");
        StartCoroutine(CheckFrameForUpdate(frameId));
    }

    /// <summary>
    /// Kiểm tra cập nhật cho tất cả các frame đã cache
    /// </summary>
    public void CheckAllFrameUpdates()
    {
        if (showDebug) Debug.Log("[ArtManager] Kiểm tra cập nhật cho tất cả các frames");
        List<int> framesToCheck = new List<int>(imageCache.Keys);
        
        foreach (int frameId in framesToCheck)
        {
            StartCoroutine(CheckFrameForUpdate(frameId));
        }
    }

    public void ForceRefreshFrame(int frameId)
    {
        if (showDebug) Debug.Log($"[ArtManager] Force refresh frame {frameId}");
        
        // Clear cache
        if (imageCache.ContainsKey(frameId))
        {
            imageCache.Remove(frameId);
        }
        if (imageHashCache.ContainsKey(frameId))
        {
            imageHashCache.Remove(frameId);
        }
        
        // Thêm vào đầu queue
        var tempQueue = new Queue<int>();
        tempQueue.Enqueue(frameId);
        while (downloadQueue.Count > 0)
        {
            int id = downloadQueue.Dequeue();
            if (id != frameId) // Tránh duplicate
            {
                tempQueue.Enqueue(id);
            }
        }
        downloadQueue = tempQueue;
    }

    public void ForceRefreshAllFrames()
    {
        if (showDebug) Debug.Log("[ArtManager] Force refresh all frames");
        
        List<int> frameIds = new List<int>(imageCache.Keys);
        
        foreach (int frameId in frameIds)
        {
            if (!downloadQueue.Contains(frameId) && !loadingFrames.Contains(frameId))
            {
                downloadQueue.Enqueue(frameId);
            }
        }
    }

    public void ClearImageFromCache(int frameId)
    {
        if (imageCache.ContainsKey(frameId))
        {
            if (imageCache[frameId] != null && imageCache[frameId].texture != null)
            {
                Destroy(imageCache[frameId].texture);
            }
            imageCache.Remove(frameId);
        }
        
        if (imageHashCache.ContainsKey(frameId))
            imageHashCache.Remove(frameId);
        
        if (lastModifiedCache.ContainsKey(frameId))
            lastModifiedCache.Remove(frameId);
        
        if (lastLoadTime.ContainsKey(frameId))
            lastLoadTime.Remove(frameId);
    }

    public void ClearCache()
    {
        foreach (var sprite in imageCache.Values)
        {
            if (sprite != null && sprite.texture != null)
            {
                Destroy(sprite.texture);
            }
        }
        
        imageCache.Clear();
        imageHashCache.Clear();
        lastModifiedCache.Clear();
        lastLoadTime.Clear();
        downloadQueue.Clear();
    }

    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
        if (player != null)
        {
            lastPlayerPosition = player.position;
        }
    }

    // Getters
    public bool IsImageCached(int frameId) => imageCache.ContainsKey(frameId);
    public bool IsImageLoading(int frameId) => loadingFrames.Contains(frameId);
    public Sprite GetCachedImage(int frameId) => imageCache.ContainsKey(frameId) ? imageCache[frameId] : null;
    public int GetCachedImageCount() => imageCache.Count;
    public int GetLoadingFrameCount() => loadingFrames.Count;
    public int GetQueueSize() => downloadQueue.Count;
    public bool IsPlayerIdle() => isPlayerIdle;
    public int GetCurrentDownloads() => currentDownloads;
}