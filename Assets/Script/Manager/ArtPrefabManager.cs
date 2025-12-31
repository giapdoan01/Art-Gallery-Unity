using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArtPrefabManager : MonoBehaviour
{
    private static ArtPrefabManager _instance;
    public static ArtPrefabManager Instance => _instance;

    [Header("References")]
    [SerializeField] private GameObject artFramePrefab;

    [Header("Settings")]
    [SerializeField] private bool loadFramesOnStart = true;
    [SerializeField] private float loadDelay = 1f;
    [SerializeField] private bool showDebug = true;

    [Header("Performance")]
    [SerializeField] private bool useParallelLoading = true;
    [SerializeField] private int maxConcurrentLoads = 8;
    private int currentLoadingCount = 0;

    private Dictionary<int, ArtFrame> frameInstances = new Dictionary<int, ArtFrame>();

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
        if (loadFramesOnStart)
        {
            Invoke(nameof(LoadAllFramesFromServer), loadDelay);
        }
    }

    /// <summary>
    /// Tải và hiển thị tất cả frame từ server
    /// </summary>
    public void LoadAllFramesFromServer()
    {
        if (APIArtManager.Instance == null)
        {
            Debug.LogError("[ArtPrefabManager] Không thể tải frame: APIArtManager.Instance is null");
            return;
        }

        if (showDebug) Debug.Log("[ArtPrefabManager] Đang tải danh sách frame từ server...");

        float startTime = Time.time;

        // Lấy tất cả frame từ server
        APIArtManager.Instance.GetAllFrames((success, frames, error) =>
        {
            if (!success || frames == null)
            {
                Debug.LogError($"[ArtPrefabManager] Lỗi khi tải danh sách frame: {error}");
                return;
            }

            if (showDebug) Debug.Log($"[ArtPrefabManager] Đã tìm thấy {frames.Count} frame từ server");

            // Kiểm tra frame nào đã có trong scene
            List<int> existingFrameIds = new List<int>();
            ArtFrame[] existingFrames = FindObjectsByType<ArtFrame>(FindObjectsSortMode.None);

            foreach (var frame in existingFrames)
            {
                if (frame != null)
                {
                    existingFrameIds.Add(frame.FrameId);
                    if (!frameInstances.ContainsKey(frame.FrameId))
                    {
                        frameInstances[frame.FrameId] = frame;
                    }
                }
            }

            if (showDebug) Debug.Log($"[ArtPrefabManager] Đã tìm thấy {existingFrameIds.Count} frame trong scene hiện tại");

            // Lọc frames cần tải
            List<int> framesToLoad = new List<int>();
            foreach (var frameData in frames)
            {
                if (!existingFrameIds.Contains(frameData.frameUse))
                {
                    framesToLoad.Add(frameData.frameUse);
                }
            }

            if (framesToLoad.Count > 0)
            {
                if (useParallelLoading)
                {
                    StartCoroutine(LoadFramesParallel(framesToLoad, startTime));
                }
                else
                {
                    foreach (var frameId in framesToLoad)
                    {
                        StartCoroutine(LoadFrameAndImage(frameId));
                    }
                }
            }
            else
            {
                if (showDebug) Debug.Log("[ArtPrefabManager] Tất cả frames đã có trong scene");
            }
        });
    }

    /// <summary>
    ///   Tải nhiều frames song song
    /// </summary>
    private IEnumerator LoadFramesParallel(List<int> frameIds, float startTime)
    {
        int totalFrames = frameIds.Count;
        int completedFrames = 0;

        if (showDebug) Debug.Log($"[ArtPrefabManager] 🚀 Bắt đầu tải {totalFrames} frames PARALLEL (max {maxConcurrentLoads} cùng lúc)");

        foreach (int frameId in frameIds)
        {
            // Đợi nếu đang tải quá nhiều
            while (currentLoadingCount >= maxConcurrentLoads)
            {
                yield return new WaitForSeconds(0.1f);
            }

            currentLoadingCount++;

            // Tải frame async
            StartCoroutine(LoadFrameAndImageAsync(frameId, () =>
            {
                completedFrames++;
                currentLoadingCount--;

                // Log tiến độ
                if (showDebug && completedFrames % 3 == 0)
                {
                    float elapsed = Time.time - startTime;
                    Debug.Log($"[ArtPrefabManager]  Tiến độ: {completedFrames}/{totalFrames} frames ({elapsed:F1}s)");
                }
            }));
        }

        // Đợi tất cả frames load xong
        while (completedFrames < totalFrames)
        {
            yield return new WaitForSeconds(0.2f);
        }

        float totalTime = Time.time - startTime;
        if (showDebug) Debug.Log($"[ArtPrefabManager]   HOÀN TẤT tải {totalFrames} frames trong {totalTime:F2}s");
    }

    /// <summary>
    ///   Tải frame async với callback - KHÔNG GỌI ReloadArtwork()
    /// </summary>
    private IEnumerator LoadFrameAndImageAsync(int frameId, System.Action onComplete)
    {
        if (showDebug) Debug.Log($"[ArtPrefabManager] Đang tải thông tin cho frame {frameId}");

        // Tải thông tin ảnh từ server
        bool imageLoaded = false;
        ImageData imageData = null;

        APIArtManager.Instance.GetImageByFrame(frameId, (success, data, error) =>
        {
            imageLoaded = true;
            if (success && data != null)
            {
                imageData = data;
            }
            else
            {
                Debug.LogWarning($"[ArtPrefabManager] Không tìm thấy ảnh cho frame {frameId}: {error}");
            }
        });

        // Đợi cho đến khi tải xong
        yield return new WaitUntil(() => imageLoaded);

        // Nếu không có dữ liệu ảnh, bỏ qua
        if (imageData == null)
        {
            Debug.LogWarning($"[ArtPrefabManager] Bỏ qua frame {frameId} do không có dữ liệu ảnh");
            onComplete?.Invoke();
            yield break;
        }

        // Tạo frame mới từ prefab
        if (artFramePrefab == null)
        {
            Debug.LogError("[ArtPrefabManager] Không thể tạo frame: artFramePrefab is null");
            onComplete?.Invoke();
            yield break;
        }

        // Tạo vị trí từ dữ liệu
        Vector3 position = new Vector3(
            imageData.positionX,
            imageData.positionY,
            imageData.positionZ
        );

        // Tạo góc xoay từ dữ liệu
        Vector3 rotation = new Vector3(
            imageData.rotationX,
            imageData.rotationY,
            imageData.rotationZ
        );

        // Instantiate frame mới
        GameObject frameObject = Instantiate(artFramePrefab, position, Quaternion.Euler(rotation));
        frameObject.name = $"ArtFrame_{frameId}";

        // Gán frameId
        ArtFrame artFrame = frameObject.GetComponent<ArtFrame>();
        if (artFrame == null)
        {
            artFrame = frameObject.AddComponent<ArtFrame>();
        }

        // Thiết lập ID cho frame
        SetFrameId(artFrame, frameId);

        // Thêm vào dictionary
        frameInstances[frameId] = artFrame;

        // THAY ĐỔI: Load artwork trực tiếp thay vì gọi ReloadArtwork()
        yield return StartCoroutine(LoadArtworkDirectly(artFrame, imageData));

        if (showDebug) Debug.Log($"[ArtPrefabManager] Đã tạo frame {frameId} tại vị trí {position}");

        // Gọi callback
        onComplete?.Invoke();
    }

    /// <summary>
    /// THÊM MỚI: Load artwork trực tiếp từ ImageData - KHÔNG GỌI API
    /// </summary>
    private IEnumerator LoadArtworkDirectly(ArtFrame artFrame, ImageData imageData)
    {
        if (string.IsNullOrEmpty(imageData.url))
        {
            Debug.LogWarning($"[ArtPrefabManager] Frame {imageData.frameUse} không có URL");
            yield break;
        }

        Texture2D texture = null;

        if (ArtManager.Instance != null)
        {
            Sprite cachedSprite = ArtManager.Instance.GetCachedSprite(imageData.frameUse);
            if (cachedSprite != null && cachedSprite.texture != null)
            {
                texture = cachedSprite.texture;

                if (showDebug)
                    Debug.Log($"[ArtPrefabManager] 💾 Sử dụng cache cho frame {imageData.frameUse}");
            }
        }

        //   BƯỚC 2: Download nếu không có cache
        if (texture == null)
        {
            bool downloaded = false;

            APIArtManager.Instance.DownloadTexture(imageData.url, (success, tex, error) =>
            {
                downloaded = true;
                if (success && tex != null)
                {
                    texture = tex;
                }
                else
                {
                    Debug.LogError($"[ArtPrefabManager] ❌ Lỗi tải texture frame {imageData.frameUse}: {error}");
                }
            });

            // Đợi download xong
            yield return new WaitUntil(() => downloaded);
        }

        //   BƯỚC 3: Gọi ApplyTextureDirectly() của ArtFrame
        if (texture != null)
        {
            artFrame.ApplyTextureDirectly(texture, imageData);

            if (showDebug)
                Debug.Log($"[ArtPrefabManager]   Đã load texture cho frame {imageData.frameUse}");
        }
        else
        {
            Debug.LogError($"[ArtPrefabManager] ❌ Texture NULL cho frame {imageData.frameUse}");
        }
    }

    /// <summary>
    /// Tải thông tin frame và ảnh tương ứng, rồi tạo trong scene (CODE CŨ - GIỮ NGUYÊN)
    /// </summary>
    private IEnumerator LoadFrameAndImage(int frameId)
    {
        if (showDebug) Debug.Log($"[ArtPrefabManager] Đang tải thông tin cho frame {frameId}");

        // Tải thông tin ảnh từ server
        bool imageLoaded = false;
        ImageData imageData = null;

        APIArtManager.Instance.GetImageByFrame(frameId, (success, data, error) =>
        {
            imageLoaded = true;
            if (success && data != null)
            {
                imageData = data;
            }
            else
            {
                Debug.LogWarning($"[ArtPrefabManager] Không tìm thấy ảnh cho frame {frameId}: {error}");
            }
        });

        // Đợi cho đến khi tải xong
        yield return new WaitUntil(() => imageLoaded);

        // Nếu không có dữ liệu ảnh, bỏ qua
        if (imageData == null)
        {
            Debug.LogWarning($"[ArtPrefabManager] Bỏ qua frame {frameId} do không có dữ liệu ảnh");
            yield break;
        }

        // Tạo frame mới từ prefab
        if (artFramePrefab == null)
        {
            Debug.LogError("[ArtPrefabManager] Không thể tạo frame: artFramePrefab is null");
            yield break;
        }

        // Tạo vị trí từ dữ liệu
        Vector3 position = new Vector3(
            imageData.positionX,
            imageData.positionY,
            imageData.positionZ
        );

        // Tạo góc xoay từ dữ liệu
        Vector3 rotation = new Vector3(
            imageData.rotationX,
            imageData.rotationY,
            imageData.rotationZ
        );

        // Instantiate frame mới
        GameObject frameObject = Instantiate(artFramePrefab, position, Quaternion.Euler(rotation));
        frameObject.name = $"ArtFrame_{frameId}";

        // Gán frameId
        ArtFrame artFrame = frameObject.GetComponent<ArtFrame>();
        if (artFrame == null)
        {
            artFrame = frameObject.AddComponent<ArtFrame>();
        }

        // Thiết lập ID cho frame
        SetFrameId(artFrame, frameId);

        // Thêm vào dictionary
        frameInstances[frameId] = artFrame;

        //   THAY ĐỔI: Load artwork trực tiếp
        yield return StartCoroutine(LoadArtworkDirectly(artFrame, imageData));

        if (showDebug) Debug.Log($"[ArtPrefabManager] Đã tạo frame {frameId} tại vị trí {position}");
    }

    /// <summary>
    /// Thiết lập frame ID cho art frame
    /// </summary>
    private void SetFrameId(ArtFrame artFrame, int frameId)
    {
        System.Type type = artFrame.GetType();
        System.Reflection.FieldInfo field = type.GetField("frameId",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(artFrame, frameId);

            if (showDebug)
            {
                Debug.Log($"[ArtPrefabManager] Đã set frame ID = {frameId}");
            }
        }
        else
        {
            Debug.LogError("[ArtPrefabManager] Không thể tìm thấy field frameId!");
        }
    }

    /// <summary>
    /// Buộc làm mới frame có ID cụ thể
    /// </summary>
    public void ForceRefreshFrame(int frameId)
    {
        // Tìm trong dictionary trước
        if (frameInstances.TryGetValue(frameId, out ArtFrame frame))
        {
            if (frame != null)
            {
                frame.ReloadArtwork(true);
                if (showDebug) Debug.Log($"[ArtPrefabManager] Đã làm mới frame {frameId}");
                return;
            }
        }

        // Tìm trong scene nếu không có trong dictionary
        ArtFrame[] allFrames = FindObjectsByType<ArtFrame>(FindObjectsSortMode.None);
        foreach (var f in allFrames)
        {
            if (f != null && f.FrameId == frameId)
            {
                f.ReloadArtwork(true);
                frameInstances[frameId] = f;
                if (showDebug) Debug.Log($"[ArtPrefabManager] Đã làm mới frame {frameId}");
                return;
            }
        }

        if (showDebug) Debug.LogWarning($"[ArtPrefabManager] Không tìm thấy frame {frameId} để làm mới");
    }

    /// <summary>
    /// Làm mới tất cả frame
    /// </summary>
    public void RefreshAllFrames()
    {
        ArtFrame[] allFrames = FindObjectsByType<ArtFrame>(FindObjectsSortMode.None);
        int count = 0;

        foreach (var frame in allFrames)
        {
            if (frame != null)
            {
                frame.ReloadArtwork(true);
                count++;
            }
        }

        if (showDebug) Debug.Log($"[ArtPrefabManager] Đã làm mới {count} frame");
    }
}
