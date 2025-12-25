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
            // Thêm delay nhỏ để đảm bảo các thành phần khác đã khởi tạo xong
            Invoke(nameof(LoadAllFramesFromServer), loadDelay);
        }
    }

    /// <summary>
    /// Tải và hiển thị tất cả frame từ server
    /// </summary>
    public void LoadAllFramesFromServer()
    {
        if (APIManager.Instance == null)
        {
            Debug.LogError("[ArtPrefabManager] Không thể tải frame: APIManager.Instance is null");
            return;
        }

        if (showDebug) Debug.Log("[ArtPrefabManager] Đang tải danh sách frame từ server...");

        // Lấy tất cả frame từ server
        APIManager.Instance.GetAllFrames((success, frames, error) =>
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
                    // Thêm vào dictionary để quản lý
                    if (!frameInstances.ContainsKey(frame.FrameId))
                    {
                        frameInstances[frame.FrameId] = frame;
                    }
                }
            }

            if (showDebug) Debug.Log($"[ArtPrefabManager] Đã tìm thấy {existingFrameIds.Count} frame trong scene hiện tại");

            // Xử lý từng frame
            foreach (var frameData in frames)
            {
                // Nếu frame không có trong scene, tạo mới
                if (!existingFrameIds.Contains(frameData.frameUse))
                {
                    StartCoroutine(LoadFrameAndImage(frameData.frameUse));
                }
            }
        });
    }

    /// <summary>
    /// Tải thông tin frame và ảnh tương ứng, rồi tạo trong scene
    /// </summary>
    private IEnumerator LoadFrameAndImage(int frameId)
    {
        if (showDebug) Debug.Log($"[ArtPrefabManager] Đang tải thông tin cho frame {frameId}");

        // Tải thông tin ảnh từ server
        bool imageLoaded = false;
        ImageData imageData = null;

        APIManager.Instance.GetImageByFrame(frameId, (success, data, error) =>
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

        // Tạo vị trí từ dữ liệu - Đơn giản hóa, dùng trực tiếp từ positionX, positionY, positionZ
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

        // Tải và hiển thị ảnh
        artFrame.ReloadArtwork(true);

        if (showDebug) Debug.Log($"[ArtPrefabManager] Đã tạo frame {frameId} tại vị trí {position}");
    }

    /// <summary>
    /// Thiết lập frame ID cho art frame
    /// </summary>
    private void SetFrameId(ArtFrame artFrame, int frameId)
    {
        // Vì frameId là SerializeField, có thể cần dùng reflection để set giá trị
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
                // Thêm vào dictionary nếu chưa có
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