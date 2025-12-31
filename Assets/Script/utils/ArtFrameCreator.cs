using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

public class ArtFrameCreator : MonoBehaviour
{
    [Header("Prefab References")]
    [SerializeField] private GameObject artFramePrefab;

    [Header("Settings")]
    [SerializeField] private float spawnDistance = 2f;
    [SerializeField] private float heightOffset = 2f;

    [Header("Frame Size")]
    [SerializeField] private bool useCustomScale = false;
    [SerializeField] private Vector3 frameSize = new Vector3(1.5f, 1f, 0.1f);

    [Header("References")]
    [SerializeField] private ImageGalleryContainer galleryContainer;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private GameObject lastCreatedFrame;
    private int lastCreatedFrameId;
    private System.Action onCreationCanceled;

    // Singleton pattern
    private static ArtFrameCreator _instance;
    public static ArtFrameCreator Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // Tìm gallery container nếu chưa được gán
        if (galleryContainer == null)
        {
            galleryContainer = FindObjectOfType<ImageGalleryContainer>();
        }
    }

    /// <summary>
    /// Tạo mới một art frame
    /// </summary>
    public void CreateNewArtFrame()
    {
        if (artFramePrefab == null)
        {
            Debug.LogError("[ArtFrameCreator] Art Frame Prefab không được thiết lập!");
            return;
        }

        // Lấy frame ID tiếp theo từ server
        GetNextAvailableFrameId((success, frameId) =>
        {
            if (!success)
            {
                Debug.LogError("[ArtFrameCreator] Không thể lấy frame ID tiếp theo!");
                return;
            }

            // Tìm player transform (để tạo frame trước mặt người chơi)
            Transform playerTransform = FindPlayerTransform();
            if (playerTransform == null)
            {
                Debug.LogError("[ArtFrameCreator] Không tìm thấy Player Transform!");
                return;
            }

            // Tạo frame mới trước mặt người chơi
            GameObject newFrameObj = SpawnFrameInFrontOfPlayer(playerTransform);

            if (newFrameObj == null)
            {
                Debug.LogError("[ArtFrameCreator] Không thể tạo art frame!");
                return;
            }

            // Lưu reference đến frame mới tạo
            lastCreatedFrame = newFrameObj;
            lastCreatedFrameId = frameId;

            // Thiết lập ID và thông tin cho frame
            ArtFrame artFrame = newFrameObj.GetComponent<ArtFrame>();
            if (artFrame != null)
            {
                // Set frame ID
                SetFrameId(artFrame, frameId);

                if (showDebug)
                {
                    Debug.Log($"[ArtFrameCreator] Đã tạo frame mới với ID: {frameId} tại vị trí {newFrameObj.transform.position}");
                }

                // Hiển thị gallery và popup để edit
                ShowEditPopup(frameId);
            }
            else
            {
                Debug.LogError("[ArtFrameCreator] Art Frame không có component ArtFrame!");
                Destroy(newFrameObj);
                lastCreatedFrame = null;
            }
        });
    }

    /// <summary>
    /// Tìm transform của player
    /// </summary>
    private Transform FindPlayerTransform()
    {
        // Thử tìm qua PlayerController
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            return playerController.transform;
        }

        // Nếu không tìm được ngay lúc đó, thử tìm lại
        playerController = GameObject.FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            return playerController.transform;
        }

        // Hoặc tìm qua Camera
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform;
        }

        return null;
    }

    /// <summary>
    /// Lấy ID tiếp theo cho frame mới (tìm ID nhỏ nhất chưa được sử dụng)
    /// </summary>
    private void GetNextAvailableFrameId(System.Action<bool, int> callback)
    {
        if (APIArtManager.Instance == null)
        {
            Debug.LogError("[ArtFrameCreator] APIManager.Instance is null!");
            callback?.Invoke(false, 0);
            return;
        }

        APIArtManager.Instance.GetAllFrames((success, frames, error) =>
        {
            if (success && frames != null)
            {
                // Tạo danh sách các ID đã được sử dụng
                HashSet<int> usedIds = new HashSet<int>();
                int maxFrameId = 0;

                // Thu thập tất cả ID đã được sử dụng
                foreach (var frame in frames)
                {
                    usedIds.Add(frame.frameUse);
                    if (frame.frameUse > maxFrameId)
                    {
                        maxFrameId = frame.frameUse;
                    }
                }

                // Tìm ID nhỏ nhất chưa được sử dụng, bắt đầu từ 1
                int nextId = 1;
                while (usedIds.Contains(nextId))
                {
                    nextId++;
                }

                if (showDebug)
                {
                    Debug.Log($"[ArtFrameCreator] Frame ID lớn nhất: {maxFrameId}, ID nhỏ nhất chưa sử dụng: {nextId}");
                }

                callback?.Invoke(true, nextId);
            }
            else
            {
                Debug.LogError($"[ArtFrameCreator] Lỗi lấy frames: {error}");
                callback?.Invoke(false, 0);
            }
        });
    }

    /// <summary>
    /// Tạo frame trước mặt người chơi với độ cao tùy chỉnh
    /// </summary>
    private GameObject SpawnFrameInFrontOfPlayer(Transform playerTransform)
    {
        // Tính toán vị trí đặt frame
        Vector3 playerPosition = playerTransform.position;
        Vector3 playerForward = playerTransform.forward;

        // Đặt frame cách người chơi một khoảng
        Vector3 spawnPosition = playerPosition + playerForward * spawnDistance;

        // Điều chỉnh độ cao - thêm heightOffset vào tọa độ Y
        spawnPosition.y += heightOffset;

        // Đảm bảo frame đối mặt với người chơi
        Quaternion spawnRotation = Quaternion.LookRotation(-playerForward);

        // Tạo frame mới
        GameObject newFrame = Instantiate(artFramePrefab, spawnPosition, spawnRotation);
        newFrame.name = $"ArtFrame_New";

        // CHỈ áp dụng scale tùy chỉnh nếu flag useCustomScale = true
        if (useCustomScale)
        {
            if (showDebug) Debug.Log($"[ArtFrameCreator] Đang sử dụng scale tùy chỉnh: {frameSize}");

            if (newFrame.transform.Find("CubeFrame") is Transform cubeFrame)
            {
                cubeFrame.localScale = frameSize;
            }
            else
            {
                newFrame.transform.localScale = frameSize;
            }
        }
        else
        {
            if (showDebug) Debug.Log($"[ArtFrameCreator] Giữ nguyên scale của prefab gốc");
        }

        if (showDebug)
        {
            Debug.Log($"[ArtFrameCreator] Đã tạo frame tại vị trí: {spawnPosition}, cao hơn mặt đất: {heightOffset} đơn vị");
            Debug.Log($"[ArtFrameCreator] Scale của frame: {newFrame.transform.localScale}");
        }

        return newFrame;
    }

    /// <summary>
    /// Thiết lập frame ID cho art frame
    /// </summary>
    private void SetFrameId(ArtFrame artFrame, int frameId)
    {
        // Vì frameId là SerializeField, có thể cần dùng reflection để set giá trị
        // Hoặc nếu ArtFrame có public setter, dùng trực tiếp

        System.Type type = artFrame.GetType();
        System.Reflection.FieldInfo field = type.GetField("frameId",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (field != null)
        {
            field.SetValue(artFrame, frameId);

            if (showDebug)
            {
                Debug.Log($"[ArtFrameCreator] Đã set frame ID = {frameId}");
            }
        }
        else
        {
            Debug.LogError("[ArtFrameCreator] Không thể tìm thấy field frameId!");
        }
    }

    /// <summary>
    /// Hiển thị popup để edit thông tin ảnh
    /// </summary>
    private void ShowEditPopup(int frameId)
    {
        // Hiển thị gallery
        if (galleryContainer != null)
        {
            galleryContainer.gameObject.SetActive(true);
            galleryContainer.RefreshGallery();
        }

        // Tạo dữ liệu ảnh mới
        ImageData newImageData = new ImageData
        {
            frameUse = frameId,
            name = $"New Image {frameId}",
            description = "",
            author = "",
            url = ""
        };

        // Hiển thị popup để edit
        if (ImageEditPopup.Instance != null)
        {
            // Đăng ký callback khi đóng popup
            onCreationCanceled = () =>
            {
                // Nếu popup đóng mà không save, xóa frame đã tạo
                if (lastCreatedFrame != null && lastCreatedFrameId == frameId)
                {
                    Destroy(lastCreatedFrame);
                    lastCreatedFrame = null;

                    if (showDebug)
                    {
                        Debug.Log($"[ArtFrameCreator] Đã xóa frame mới tạo với ID: {frameId}");
                    }
                }
            };

            ImageEditPopup.Instance.RegisterOnHideCallback(() =>
            {
                if (onCreationCanceled != null)
                {
                    onCreationCanceled.Invoke();
                    onCreationCanceled = null;
                }

                // Refresh gallery khi đóng popup
                if (galleryContainer != null)
                {
                    galleryContainer.RefreshGallery();
                }
            });

            // Lấy ArtFrame component từ frame vừa tạo
            ArtFrame newArtFrame = GetLastCreatedFrame();
            // Sửa lời gọi để truyền rõ ArtFrame
            ImageEditPopup.Instance.Show(newImageData, newArtFrame);
        }
        else
        {
            Debug.LogError("[ArtFrameCreator] ImageEditPopup.Instance is null!");

            // Xóa frame nếu không thể hiển thị popup
            if (lastCreatedFrame != null)
            {
                Destroy(lastCreatedFrame);
                lastCreatedFrame = null;
            }
        }
    }

    /// <summary>
    /// Button handler để tạo frame mới
    /// </summary>
    public void OnCreateNewFrameButtonClicked()
    {
        CreateNewArtFrame();
    }

    /// <summary>
    /// Xóa frame mới tạo (gọi khi cancel)
    /// </summary>
    public void ClearLastCreatedFrame()
    {
        if (lastCreatedFrame != null)
        {
            Destroy(lastCreatedFrame);
            lastCreatedFrame = null;

            if (showDebug)
            {
                Debug.Log($"[ArtFrameCreator] Đã xóa frame mới tạo với ID: {lastCreatedFrameId}");
            }
        }
    }

    /// <summary>
    /// Phương thức để thông báo frame đã được lưu thành công
    /// </summary>
    public void OnFrameSaved(int frameId)
    {
        if (lastCreatedFrameId == frameId)
        {
            // Đã lưu thành công, không cần xóa frame nữa
            onCreationCanceled = null;

            if (lastCreatedFrame != null)
            {
                lastCreatedFrame.name = $"ArtFrame_{frameId}";
            }

            if (showDebug)
            {
                Debug.Log($"[ArtFrameCreator] Frame với ID: {frameId} đã được lưu thành công");
            }
        }
    }

    /// <summary>
    /// Lấy ArtFrame component của frame mới nhất được tạo
    /// </summary>
    public ArtFrame GetLastCreatedFrame()
    {
        if (lastCreatedFrame != null)
        {
            return lastCreatedFrame.GetComponent<ArtFrame>();
        }
        return null;
    }
}