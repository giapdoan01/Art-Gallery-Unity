using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(MeshRenderer))]
public class ArtFrame : MonoBehaviour
{
    [Header("Frame Settings")]
    [SerializeField] private int frameId;
    [SerializeField] private string frameName;

    [Header("References")]
    [SerializeField] private MeshRenderer quadRenderer;
    [SerializeField] private Transform cubeFrame;

    [Header("Loading")]
    [SerializeField] private Texture2D loadingTexture;
    [SerializeField] private bool loadOnStart = true;

    [Header("Auto Resize")]
    [SerializeField] private bool autoResize = true;
    [SerializeField] private Vector2 widthLimits = new Vector2(0.5f, 10f);

    [Header("Transform Sync")]
    [SerializeField] private bool syncTransformFromServer = true;
    [SerializeField] private bool syncRotation = true;
    [SerializeField] private bool syncPosition = true;
    [SerializeField] private bool syncScale = false;
    [SerializeField] private bool autoSaveTransform = false;
    [SerializeField] private float autoSaveDelay = 2f;

    [Header("UI Buttons")]
    [SerializeField] private GameObject buttonContainer;
    [SerializeField] private Button infoButton;
    [SerializeField] private Button transformButton;
    [SerializeField] private float buttonDisplayDistance = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // Private variables
    private ArtFrameData frameData;
    private Material currentMaterial;
    private bool isSubscribed = false;
    private float lastSyncTime = 0f;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;
    private Camera mainCamera;

    // Properties
    public int FrameId => frameId;
    public string FrameName => frameName;
    public ArtFrameData FrameData => frameData;
    public bool IsLoaded => frameData != null && frameData.IsLoaded();
    public bool IsLoading => ArtManager.Instance != null && ArtManager.Instance.IsImageLoading(frameId);
    public GameObject ButtonContainer => buttonContainer;

    #region Unity Lifecycle

    private void Awake()
    {
        if (quadRenderer == null)
        {
            quadRenderer = GetComponent<MeshRenderer>();
        }

        if (cubeFrame == null)
        {
            cubeFrame = transform;
        }

        // Lưu transform hiện tại
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastScale = transform.localScale;

        // Ẩn container nút ban đầu
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(false);
        }
    }

    private void Start()
    {
        SubscribeToManager();

        if (loadOnStart)
        {
            LoadArtwork();
        }

        // Thiết lập sự kiện click cho các nút
        SetupButtonEvents();

        // Lấy tham chiếu đến camera
        mainCamera = Camera.main;

        // Đặt canvas sorting order cao hơn
        if (buttonContainer != null)
        {
            Canvas canvas = buttonContainer.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 100;
            }
        }
    }

    private void Update()
    {
        // Kiểm tra và đồng bộ transform lên server nếu đã thay đổi
        if (autoSaveTransform && Time.time - lastSyncTime >= autoSaveDelay)
        {
            bool hasChanges = false;

            if (syncPosition && Vector3.Distance(lastPosition, transform.position) > 0.01f)
                hasChanges = true;

            if (syncRotation && Quaternion.Angle(lastRotation, transform.rotation) > 0.1f)
                hasChanges = true;

            if (syncScale && Vector3.Distance(lastScale, transform.localScale) > 0.01f)
                hasChanges = true;

            if (hasChanges)
            {
                SaveTransformToServer();
                lastSyncTime = Time.time;
            }
        }

        // ✅ Kiểm tra khoảng cách để hiển thị/ẩn buttons
        CheckButtonVisibility();
    }

    private void OnEnable()
    {
        SubscribeToManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromManager();

        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
            currentMaterial = null;
        }

        // Remove button listeners
        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
        }

        if (transformButton != null)
        {
            transformButton.onClick.RemoveAllListeners();
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(frameName))
        {
            frameName = gameObject.name;
        }

        if (quadRenderer == null)
        {
            quadRenderer = GetComponent<MeshRenderer>();
        }

        if (cubeFrame == null)
        {
            cubeFrame = transform;
        }
    }

    #endregion

    #region Manager Subscription

    private void SubscribeToManager()
    {
        if (!isSubscribed && ArtManager.Instance != null)
        {
            ArtManager.Instance.OnImageUpdated += OnImageUpdatedFromManager;
            ArtManager.Instance.OnImageLoadError += OnImageLoadErrorFromManager;
            isSubscribed = true;
        }
    }

    private void UnsubscribeFromManager()
    {
        if (isSubscribed && ArtManager.Instance != null)
        {
            ArtManager.Instance.OnImageUpdated -= OnImageUpdatedFromManager;
            ArtManager.Instance.OnImageLoadError -= OnImageLoadErrorFromManager;
            isSubscribed = false;
        }
    }

    private void OnImageUpdatedFromManager(int updatedFrameId, Sprite newSprite)
    {
        if (updatedFrameId == frameId && newSprite != null)
        {
            if (showDebug) Debug.Log($"[ArtFrame] Auto update frame {frameId}", this);

            if (frameData == null)
            {
                frameData = new ArtFrameData(frameId, frameName);
            }
            frameData.sprite = newSprite;

            ApplyArtwork(newSprite);

            if (autoResize && newSprite.texture != null)
            {
                ResizeFrameByImageRatio(newSprite.texture);
            }

            // Khi có cập nhật ảnh từ server, kiểm tra xem có nên cập nhật transform không
            if (syncTransformFromServer)
            {
                SyncTransformFromServer();
            }
        }
    }

    private void OnImageLoadErrorFromManager(int errorFrameId, string errorMessage)
    {
        if (errorFrameId == frameId)
        {
            Debug.LogError($"[ArtFrame] Error frame {frameId}: {errorMessage}", this);
        }
    }

    #endregion

    #region Load Artwork

    public void LoadArtwork(bool forceRefresh = false)
    {
        if (frameId <= 0)
        {
            Debug.LogError($"[ArtFrame] Frame ID không hợp lệ: {frameId}", this);
            return;
        }

        ShowLoadingTexture();
        ArtManager.Instance.GetImageForFrame(frameId, OnArtworkLoaded, forceRefresh);

        // Đồng bộ transform từ server nếu đã bật
        if (syncTransformFromServer)
        {
            SyncTransformFromServer();
        }
    }

    private void OnArtworkLoaded(Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.LogError($"[ArtFrame] Không thể tải ảnh cho frame {frameId}", this);
            return;
        }

        if (frameData == null)
        {
            frameData = new ArtFrameData(frameId, frameName);
        }

        frameData.sprite = sprite;
        ApplyArtwork(sprite);

        if (autoResize && sprite.texture != null)
        {
            ResizeFrameByImageRatio(sprite.texture);
        }
    }

    private void ApplyArtwork(Sprite sprite)
    {
        if (sprite == null) return;

        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
        }

        currentMaterial = new Material(Shader.Find("Unlit/Texture"));
        currentMaterial.mainTexture = sprite.texture;

        if (frameData != null)
        {
            frameData.material = currentMaterial;
        }

        if (quadRenderer != null)
        {
            quadRenderer.material = currentMaterial;
        }
    }

    private void ShowLoadingTexture()
    {
        if (loadingTexture != null && quadRenderer != null)
        {
            Material loadingMat = new Material(Shader.Find("Unlit/Texture"));
            loadingMat.mainTexture = loadingTexture;
            quadRenderer.material = loadingMat;
        }
    }

    public void ClearArtwork()
    {
        ArtManager.Instance.ClearImageFromCache(frameId);

        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
            currentMaterial = null;
        }

        frameData = null;

        if (quadRenderer != null)
        {
            ShowLoadingTexture();
        }
    }

    public void ReloadArtwork(bool forceRefresh = false)
    {
        ClearArtwork();
        LoadArtwork(forceRefresh);
    }

    #endregion

    #region Auto Resize

    private void ResizeFrameByImageRatio(Texture2D texture)
    {
        if (texture == null || cubeFrame == null)
        {
            if (showDebug)
            {
                Debug.LogWarning($"[ArtFrame] Không thể resize frame {frameId}: " +
                               $"texture={texture != null}, cubeFrame={cubeFrame != null}", this);
            }
            return;
        }

        float currentHeight = cubeFrame.localScale.y;
        float imageRatio = (float)texture.width / (float)texture.height;
        float newWidth = currentHeight * imageRatio;
        newWidth = Mathf.Clamp(newWidth, widthLimits.x, widthLimits.y);

        Vector3 newScale = new Vector3(newWidth, currentHeight, cubeFrame.localScale.z);
        cubeFrame.localScale = newScale;

        if (showDebug)
        {
            Debug.Log($"[ArtFrame] Frame {frameId} đã resize:\n" +
                      $"  • Texture: {texture.width}x{texture.height}\n" +
                      $"  • Ratio: {imageRatio:F2}\n" +
                      $"  • Chiều cao cố định: {currentHeight:F2}\n" +
                      $"  • Chiều rộng mới: {newWidth:F2}\n" +
                      $"  • Scale cuối: {newScale}", this);
        }
    }

    public void ManualResize()
    {
        if (frameData != null && frameData.sprite != null && frameData.sprite.texture != null)
        {
            ResizeFrameByImageRatio(frameData.sprite.texture);
        }
        else
        {
            Debug.LogWarning($"[ArtFrame] Không thể resize frame {frameId}: chưa có sprite", this);
        }
    }

    #endregion

    #region Transform Sync

    public void SyncTransformFromServer()
    {
        ImageData imageData = ArtManager.Instance.GetImageDataForFrame(frameId);

        if (imageData == null)
        {
            if (showDebug) Debug.Log($"[ArtFrame] Không có dữ liệu transform từ server cho frame {frameId}", this);
            return;
        }

        if (showDebug)
        {
            string jsonString = JsonUtility.ToJson(imageData);
            Debug.Log($"[ArtFrame] Dữ liệu JSON từ server cho frame {frameId}: {jsonString}", this);

            if (imageData.position != null)
                Debug.Log($"[ArtFrame] Position từ server: ({imageData.position.x}, {imageData.position.y}, {imageData.position.z})", this);
            else
                Debug.Log($"[ArtFrame] Position từ server là null", this);

            if (imageData.rotation != null)
                Debug.Log($"[ArtFrame] Rotation từ server: ({imageData.rotation.x}, {imageData.rotation.y}, {imageData.rotation.z})", this);
            else
                Debug.Log($"[ArtFrame] Rotation từ server là null", this);
        }

        if (syncPosition && imageData.position != null)
        {
            Vector3 serverPosition = imageData.position.ToVector3();
            transform.position = serverPosition;
            lastPosition = serverPosition;

            if (showDebug) Debug.Log($"[ArtFrame] Đã đồng bộ vị trí frame {frameId} từ server: {serverPosition}", this);
        }

        if (syncRotation && imageData.rotation != null)
        {
            Vector3 serverRotation = imageData.rotation.ToVector3();
            transform.eulerAngles = serverRotation;
            lastRotation = transform.rotation;

            if (showDebug) Debug.Log($"[ArtFrame] Đã đồng bộ góc xoay frame {frameId} từ server: {serverRotation}", this);
        }

        lastScale = transform.localScale;
    }

    public void SaveTransformToServer()
    {
        if (showDebug) Debug.Log($"[ArtFrame] Đang lưu transform của frame {frameId} lên server", this);

        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastScale = transform.localScale;
    }

    public void ForceSyncTransformFromServer()
    {
        if (showDebug) Debug.Log($"[ArtFrame] Force sync transform from server for frame {frameId}", this);

        ArtManager.Instance.ForceRefreshFrame(frameId);
        StartCoroutine(DelayedSyncTransform());
    }

    private IEnumerator DelayedSyncTransform()
    {
        yield return new WaitForSeconds(0.5f);
        SyncTransformFromServer();
    }

    #endregion

    #region Button Management

    private void SetupButtonEvents()
    {
        // Đảm bảo buttons có raycast target
        EnsureButtonRaycastTarget();

        // Thiết lập sự kiện cho nút Info
        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(OnInfoButtonClicked);

            if (showDebug)
            {
                Debug.Log($"[ArtFrame] Info button setup for frame {frameId}");
            }
        }

        // Thiết lập sự kiện cho nút Transform
        if (transformButton != null)
        {
            transformButton.onClick.RemoveAllListeners();
            transformButton.onClick.AddListener(OnTransformButtonClicked);

            if (showDebug)
            {
                Debug.Log($"[ArtFrame] Transform button setup for frame {frameId}");
            }
        }
    }

    private void EnsureButtonRaycastTarget()
    {
        if (infoButton != null)
        {
            Image image = infoButton.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }
            infoButton.interactable = true;
        }

        if (transformButton != null)
        {
            Image image = transformButton.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }
            transformButton.interactable = true;
        }
    }

    private void OnInfoButtonClicked()
    {
        if (showDebug)
        {
            Debug.Log($"[ArtFrame] Info button clicked for frame {frameId}");
        }

        if (APIManager.Instance == null)
        {
            Debug.LogError("[ArtFrame] APIManager.Instance is null!");
            return;
        }

        APIManager.Instance.GetImageByFrame(frameId, (success, imageData, error) =>
        {
            if (success && imageData != null)
            {
                if (ImageEditPopup.Instance != null)
                {
                    ImageEditPopup.Instance.Show(imageData);
                }
                else
                {
                    Debug.LogError("[ArtFrame] ImageEditPopup.Instance is null!");
                }
            }
            else
            {
                Debug.LogError($"[ArtFrame] Không lấy được dữ liệu hình ảnh cho frame {frameId}: {error}");
            }
        });
    }

    private void OnTransformButtonClicked()
    {
        if (showDebug)
        {
            Debug.Log($"[ArtFrame] Transform button clicked for frame {frameId}");
        }

        if (APIManager.Instance == null)
        {
            Debug.LogError("[ArtFrame] APIManager.Instance is null!");
            return;
        }

        APIManager.Instance.GetImageByFrame(frameId, (success, imageData, error) =>
        {
            if (success && imageData != null)
            {
                if (TransformEditPopup.Instance != null)
                {
                    TransformEditPopup.Instance.Show(imageData, this);
                }
                else
                {
                    Debug.LogError("[ArtFrame] TransformEditPopup.Instance is null!");
                }
            }
            else
            {
                Debug.LogError($"[ArtFrame] Không lấy được dữ liệu hình ảnh cho frame {frameId}: {error}");
            }
        });
    }

    #endregion

    #region Button Visibility - ✅ ĐƠN GIẢN HÓA

    /// <summary>
    /// ✅ Kiểm tra khoảng cách và hiển thị/ẩn buttons
    /// </summary>
    private void CheckButtonVisibility()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        if (buttonContainer == null) return;

        // Tính khoảng cách từ camera đến frame
        float distanceToCamera = Vector3.Distance(transform.position, mainCamera.transform.position);

        // ✅ Hiển thị buttons nếu trong vùng distance
        if (distanceToCamera <= buttonDisplayDistance)
        {
            if (!buttonContainer.activeSelf)
            {
                buttonContainer.SetActive(true);

                if (showDebug)
                {
                    Debug.Log($"[ArtFrame] Hiển thị buttons cho frame {frameId} (distance: {distanceToCamera:F2})");
                }
            }
        }
        else
        {
            // ✅ Ẩn buttons nếu vượt quá distance
            if (buttonContainer.activeSelf)
            {
                buttonContainer.SetActive(false);

                if (showDebug)
                {
                    Debug.Log($"[ArtFrame] Ẩn buttons cho frame {frameId} (distance: {distanceToCamera:F2})");
                }
            }
        }
    }

    /// <summary>
    /// Kiểm tra pointer có đang trên button container của frame này không
    /// </summary>
    public bool IsPointerOverButtons()
    {
        if (EventSystem.current == null || buttonContainer == null || !buttonContainer.activeSelf)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject != null &&
                (result.gameObject.transform.IsChildOf(buttonContainer.transform) ||
                 result.gameObject == buttonContainer))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Force hiển thị buttons (gọi từ ArtFrameController)
    /// </summary>
    public void ForceShowButtons()
    {
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(true);

            if (showDebug)
            {
                Debug.Log($"[ArtFrame] Force show buttons cho frame {frameId}");
            }
        }
    }

    /// <summary>
    /// Force ẩn buttons (gọi từ ArtFrameController)
    /// </summary>
    public void ForceHideButtons()
    {
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(false);

            if (showDebug)
            {
                Debug.Log($"[ArtFrame] Force hide buttons cho frame {frameId}");
            }
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (showDebug && cubeFrame != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(cubeFrame.position, cubeFrame.localScale);
        }

        // Hiển thị vùng kích hoạt nút
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, buttonDisplayDistance);
    }

    #endregion
}
