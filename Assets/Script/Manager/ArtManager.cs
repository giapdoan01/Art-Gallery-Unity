using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

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
    public string hash;
    public long lastModified;
    public string author;
    public string description;

    // Thay đổi: Định nghĩa position và rotation theo cấu trúc từ server
    public Position position;
    public Rotation rotation;

    // Thêm các properties để tương thích với code cũ
    public float positionX { get { return position != null ? position.x : 0; } }
    public float positionY { get { return position != null ? position.y : 0; } }
    public float positionZ { get { return position != null ? position.z : 0; } }
    public float rotationX { get { return rotation != null ? rotation.x : 0; } }
    public float rotationY { get { return rotation != null ? rotation.y : 0; } }
    public float rotationZ { get { return rotation != null ? rotation.z : 0; } }
}

// Các class này đã có trong code, chỉ cần đảm bảo chúng được định nghĩa đúng
[Serializable]
public class Position
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public class Rotation
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
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

    [Header("Loading Settings")]
    [SerializeField] private int maxConcurrentDownloads = 2;
    [SerializeField] private float downloadDelay = 0.5f;
    [SerializeField] private bool onlyRefreshWhenIdle = true;

    [Header("Background Loading")]
    [SerializeField] private bool useBackgroundLoading = true;
    [SerializeField] private float maxLoadTimePerFrame = 0.016f; // Max 16ms/frame

    [Header("Player Detection")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private float idleThreshold = 0.1f;

    [Header("Cache Settings")]
    [SerializeField] private bool enableCache = true;
    [SerializeField] private int maxCacheSize = 50; // Số lượng ảnh tối đa trong cache
    [SerializeField] private float cacheCleanupInterval = 60f; // Dọn cache mỗi 60s

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Cache - CHỈ ArtManager quản lý cache
    private Dictionary<int, Sprite> spriteCache = new Dictionary<int, Sprite>();
    private Dictionary<int, Texture2D> textureCache = new Dictionary<int, Texture2D>();
    private Dictionary<int, ImageData> imageDataCache = new Dictionary<int, ImageData>();
    private Dictionary<int, string> imageHashCache = new Dictionary<int, string>();
    private Dictionary<int, long> lastModifiedCache = new Dictionary<int, long>();
    private Dictionary<int, float> lastAccessTime = new Dictionary<int, float>();

    // Loading queue
    private Queue<int> downloadQueue = new Queue<int>();
    private HashSet<int> currentlyLoading = new HashSet<int>();
    private Dictionary<int, List<Action<Sprite, ImageData>>> pendingCallbacks = new Dictionary<int, List<Action<Sprite, ImageData>>>();

    // Player movement tracking
    private Vector3 lastPlayerPosition;
    private float lastMovementTime;

    // Events
    public event Action<int, Sprite, ImageData> OnImageLoaded;
    public event Action<int, string> OnImageLoadFailed;

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

        if (showDebug) Debug.Log("[ArtManager] Initialized");
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }

        if (playerTransform != null)
            lastPlayerPosition = playerTransform.position;

        // Bắt đầu background loading
        if (useBackgroundLoading)
            StartCoroutine(BackgroundLoadingCoroutine());

        // Bắt đầu cache cleanup
        if (enableCache)
            StartCoroutine(CacheCleanupCoroutine());
    }

    private void Update()
    {
        // Track player movement
        if (playerTransform != null)
        {
            float distance = Vector3.Distance(playerTransform.position, lastPlayerPosition);
            if (distance > idleThreshold)
            {
                lastMovementTime = Time.time;
                lastPlayerPosition = playerTransform.position;
            }
        }
    }

    #region Public API - Load Images

    /// <summary>
    /// Load ảnh theo frame ID - Sử dụng cache nếu có
    /// </summary>
    public void LoadImage(int frameId, Action<Sprite, ImageData> callback)
    {
        // Kiểm tra cache trước
        if (enableCache && spriteCache.ContainsKey(frameId))
        {
            if (showDebug)
                Debug.Log($"[ArtManager] Loading from cache: Frame {frameId}");

            lastAccessTime[frameId] = Time.time;
            callback?.Invoke(spriteCache[frameId], imageDataCache[frameId]);
            return;
        }

        // Thêm callback vào pending list
        if (!pendingCallbacks.ContainsKey(frameId))
            pendingCallbacks[frameId] = new List<Action<Sprite, ImageData>>();
        
        pendingCallbacks[frameId].Add(callback);

        // Nếu đang load rồi thì không cần thêm vào queue
        if (currentlyLoading.Contains(frameId))
        {
            if (showDebug)
                Debug.Log($"[ArtManager] Already loading: Frame {frameId}");
            return;
        }

        // Thêm vào queue để load
        if (!downloadQueue.Contains(frameId))
        {
            downloadQueue.Enqueue(frameId);
            
            if (showDebug)
                Debug.Log($"[ArtManager] Added to queue: Frame {frameId}");
        }
    }

    /// <summary>
    /// Load nhiều ảnh cùng lúc
    /// </summary>
    public void LoadImages(List<int> frameIds, Action<Dictionary<int, Sprite>> callback)
    {
        StartCoroutine(LoadMultipleImagesCoroutine(frameIds, callback));
    }

    private IEnumerator LoadMultipleImagesCoroutine(List<int> frameIds, Action<Dictionary<int, Sprite>> callback)
    {
        Dictionary<int, Sprite> results = new Dictionary<int, Sprite>();
        int completed = 0;

        foreach (int frameId in frameIds)
        {
            LoadImage(frameId, (sprite, data) =>
            {
                if (sprite != null)
                    results[frameId] = sprite;
                completed++;
            });
        }

        // Đợi tất cả load xong
        yield return new WaitUntil(() => completed >= frameIds.Count);

        callback?.Invoke(results);
    }

    #endregion

    #region Background Loading

    private IEnumerator BackgroundLoadingCoroutine()
    {
        while (true)
        {
            // Chỉ load khi idle nếu setting bật
            if (onlyRefreshWhenIdle && !IsPlayerIdle())
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Kiểm tra queue
            if (downloadQueue.Count == 0 || currentlyLoading.Count >= maxConcurrentDownloads)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            // Lấy frame tiếp theo từ queue
            int frameId = downloadQueue.Dequeue();
            
            if (showDebug)
                Debug.Log($"[ArtManager] Background loading: Frame {frameId}");

            // Load frame
            yield return StartCoroutine(LoadFrameCoroutine(frameId));

            // Delay giữa các lần load
            yield return new WaitForSeconds(downloadDelay);
        }
    }

    private IEnumerator LoadFrameCoroutine(int frameId)
    {
        currentlyLoading.Add(frameId);
        float startTime = Time.realtimeSinceStartup;

        bool success = false;
        ImageData imageData = null;
        Sprite sprite = null;

        // Bước 1: Lấy thông tin ảnh từ server qua APIManager
        bool dataLoaded = false;
        string dataError = null;

        APIManager.Instance.GetImageByFrame(frameId, (dataSuccess, data, error) =>
        {
            dataLoaded = true;
            if (dataSuccess && data != null)
            {
                imageData = data;
                success = true;
            }
            else
            {
                dataError = error;
            }
        });

        // Đợi API response
        yield return new WaitUntil(() => dataLoaded);

        if (!success || imageData == null)
        {
            if (showDebug)
                Debug.LogWarning($"[ArtManager] Failed to load data for frame {frameId}: {dataError}");
            
            InvokeCallbacks(frameId, null, null);
            OnImageLoadFailed?.Invoke(frameId, dataError);
            currentlyLoading.Remove(frameId);
            yield break;
        }

        // Cache image data
        if (enableCache)
        {
            imageDataCache[frameId] = imageData;
            if (!string.IsNullOrEmpty(imageData.hash))
                imageHashCache[frameId] = imageData.hash;
            lastModifiedCache[frameId] = imageData.lastModified;
        }

        // Bước 2: Tải texture từ URL qua APIManager
        if (!string.IsNullOrEmpty(imageData.url))
        {
            bool textureLoaded = false;
            Texture2D texture = null;
            string textureError = null;

            APIManager.Instance.DownloadTexture(imageData.url, (textureSuccess, tex, error) =>
            {
                textureLoaded = true;
                if (textureSuccess && tex != null)
                {
                    texture = tex;
                }
                else
                {
                    textureError = error;
                }
            });

            // Đợi texture download
            yield return new WaitUntil(() => textureLoaded);

            if (texture != null)
            {
                // Tạo sprite từ texture
                sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );

                // Cache sprite và texture
                if (enableCache)
                {
                    spriteCache[frameId] = sprite;
                    textureCache[frameId] = texture;
                    lastAccessTime[frameId] = Time.time;
                }

                float loadTime = Time.realtimeSinceStartup - startTime;
                if (showDebug)
                    Debug.Log($"[ArtManager] Loaded frame {frameId} in {loadTime:F2}s");

                // Invoke callbacks
                InvokeCallbacks(frameId, sprite, imageData);
                OnImageLoaded?.Invoke(frameId, sprite, imageData);
            }
            else
            {
                if (showDebug)
                    Debug.LogWarning($"[ArtManager] Failed to load texture for frame {frameId}: {textureError}");
                
                InvokeCallbacks(frameId, null, imageData);
                OnImageLoadFailed?.Invoke(frameId, textureError);
            }
        }
        else
        {
            // Không có URL - chỉ trả về data
            InvokeCallbacks(frameId, null, imageData);
        }

        currentlyLoading.Remove(frameId);
    }

    private void InvokeCallbacks(int frameId, Sprite sprite, ImageData data)
    {
        if (pendingCallbacks.ContainsKey(frameId))
        {
            foreach (var callback in pendingCallbacks[frameId])
            {
                callback?.Invoke(sprite, data);
            }
            pendingCallbacks.Remove(frameId);
        }
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// Lấy sprite từ cache
    /// </summary>
    public Sprite GetCachedSprite(int frameId)
    {
        if (spriteCache.ContainsKey(frameId))
        {
            lastAccessTime[frameId] = Time.time;
            return spriteCache[frameId];
        }
        return null;
    }

    /// <summary>
    /// Lấy ImageData từ cache
    /// </summary>
    public ImageData GetCachedImageData(int frameId)
    {
        if (imageDataCache.ContainsKey(frameId))
        {
            lastAccessTime[frameId] = Time.time;
            return imageDataCache[frameId];
        }
        return null;
    }

    /// <summary>
    /// Kiểm tra xem frame có trong cache không
    /// </summary>
    public bool IsFrameCached(int frameId)
    {
        return spriteCache.ContainsKey(frameId);
    }

    /// <summary>
    /// Xóa frame khỏi cache
    /// </summary>
    public void ClearFrameCache(int frameId)
    {
        if (spriteCache.ContainsKey(frameId))
        {
            Destroy(spriteCache[frameId]);
            spriteCache.Remove(frameId);
        }

        if (textureCache.ContainsKey(frameId))
        {
            Destroy(textureCache[frameId]);
            textureCache.Remove(frameId);
        }

        imageDataCache.Remove(frameId);
        imageHashCache.Remove(frameId);
        lastModifiedCache.Remove(frameId);
        lastAccessTime.Remove(frameId);

        if (showDebug)
            Debug.Log($"[ArtManager] Cleared cache for frame {frameId}");
    }

    /// <summary>
    /// Xóa toàn bộ cache
    /// </summary>
    public void ClearAllCache()
    {
        foreach (var sprite in spriteCache.Values)
            if (sprite != null) Destroy(sprite);

        foreach (var texture in textureCache.Values)
            if (texture != null) Destroy(texture);

        spriteCache.Clear();
        textureCache.Clear();
        imageDataCache.Clear();
        imageHashCache.Clear();
        lastModifiedCache.Clear();
        lastAccessTime.Clear();

        if (showDebug)
            Debug.Log("[ArtManager] Cleared all cache");
    }

    /// <summary>
    /// Tự động dọn cache theo LRU (Least Recently Used)
    /// </summary>
    private IEnumerator CacheCleanupCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(cacheCleanupInterval);

            if (spriteCache.Count > maxCacheSize)
            {
                // Sắp xếp theo thời gian truy cập
                var sortedFrames = lastAccessTime.OrderBy(x => x.Value).ToList();
                int toRemove = spriteCache.Count - maxCacheSize;

                for (int i = 0; i < toRemove && i < sortedFrames.Count; i++)
                {
                    int frameId = sortedFrames[i].Key;
                    ClearFrameCache(frameId);
                }

                if (showDebug)
                    Debug.Log($"[ArtManager] Cache cleanup: Removed {toRemove} old entries");
            }
        }
    }

    #endregion

    #region Refresh & Update

    /// <summary>
    /// Refresh một frame - kiểm tra hash và reload nếu cần
    /// </summary>
    public void RefreshFrame(int frameId, Action<Sprite, ImageData> callback)
    {
        APIManager.Instance.GetImageByFrame(frameId, (success, data, error) =>
        {
            if (!success || data == null)
            {
                callback?.Invoke(null, null);
                return;
            }

            // Kiểm tra hash
            bool needsReload = false;

            if (imageHashCache.ContainsKey(frameId))
            {
                if (imageHashCache[frameId] != data.hash)
                {
                    needsReload = true;
                    if (showDebug)
                        Debug.Log($"[ArtManager] Hash changed for frame {frameId}, reloading...");
                }
            }
            else
            {
                needsReload = true;
            }

            if (needsReload)
            {
                // Xóa cache cũ
                ClearFrameCache(frameId);
                
                // Load lại
                LoadImage(frameId, callback);
            }
            else
            {
                // Dùng cache
                callback?.Invoke(GetCachedSprite(frameId), data);
            }
        });
    }

    /// <summary>
    /// Refresh tất cả frames
    /// </summary>
    public void RefreshAllFrames(Action onComplete = null)
    {
        StartCoroutine(RefreshAllFramesCoroutine(onComplete));
    }

    private IEnumerator RefreshAllFramesCoroutine(Action onComplete)
    {
        // Lấy danh sách tất cả frames
        bool completed = false;
        List<ImageData> allImages = null;

        APIManager.Instance.GetAllImages((success, images, error) =>
        {
            completed = true;
            if (success)
                allImages = images;
        });

        yield return new WaitUntil(() => completed);

        if (allImages != null)
        {
            foreach (var imageData in allImages)
            {
                RefreshFrame(imageData.frameUse, null);
                yield return new WaitForSeconds(downloadDelay);
            }
        }

        onComplete?.Invoke();
    }

    #endregion

    #region Status Queries

    /// <summary>
    /// Kiểm tra xem frame có đang load không
    /// </summary>
    public bool IsImageLoading(int frameId)
    {
        return currentlyLoading.Contains(frameId) || downloadQueue.Contains(frameId);
    }

    /// <summary>
    /// Lấy số lượng frame đang load
    /// </summary>
    public int GetLoadingCount()
    {
        return currentlyLoading.Count;
    }

    /// <summary>
    /// Lấy số lượng frame trong queue
    /// </summary>
    public int GetQueueCount()
    {
        return downloadQueue.Count;
    }

    /// <summary>
    /// Lấy số lượng frame trong cache
    /// </summary>
    public int GetCacheCount()
    {
        return spriteCache.Count;
    }

    /// <summary>
    /// Kiểm tra player có đang idle không
    /// </summary>
    private bool IsPlayerIdle()
    {
        return Time.time - lastMovementTime > 1f;
    }

    #endregion

    #region Cleanup

    private void OnDestroy()
    {
        ClearAllCache();
    }

    #endregion
}