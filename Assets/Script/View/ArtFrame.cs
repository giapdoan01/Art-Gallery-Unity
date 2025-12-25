using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// ArtFrame - Component đại diện cho một khung tranh trong scene
/// Sử dụng ArtManager để load và cache ảnh
/// Sử dụng APIManager để lấy metadata và update transform
/// Hỗ trợ 2 loại khung: ngang (landscape) và dọc (portrait)
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class ArtFrame : MonoBehaviour
{
    [Header("Frame Settings")]
    [SerializeField] private int frameId;
    [SerializeField] private string frameName;

    [Header("References")]
    [SerializeField] private MeshRenderer quadRenderer;
    [SerializeField] private Transform cubeFrame;

    [Header("Frame Types - Assign Both")]
    [SerializeField] private Transform landscapeFrame; // ✅ Khung ngang (KHÔNG scale)
    [SerializeField] private Transform portraitFrame;  // ✅ Khung dọc (KHÔNG scale)

    [Header("Loading")]
    [SerializeField] private Texture2D loadingTexture;
    [SerializeField] private bool loadOnStart = true;

    [Header("Auto Resize")]
    [SerializeField] private bool autoResize = true;

    [Header("Quad Resize Settings")]
    [Tooltip("Khi resize, giữ nguyên chiều ngang của quad cho ảnh dọc")]
    [SerializeField] private bool useQuadOriginalSize = true;
    
    [Header("Manual Override (nếu không dùng original size)")]
    [SerializeField] private float landscapeFixedHeight = 2f; // Cho ảnh ngang
    [SerializeField] private float portraitFixedWidth = 1.5f; // Cho ảnh dọc
    [SerializeField] private Vector2 widthLimits = new Vector2(0.5f, 10f);
    [SerializeField] private Vector2 heightLimits = new Vector2(0.5f, 10f);

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
    private ImageData currentImageData;
    private Sprite currentSprite;
    private Material currentMaterial;
    private bool isSubscribed = false;
    private float lastSyncTime = 0f;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Vector3 lastScale;
    private Camera mainCamera;
    private bool isLoading = false;
    private string currentFrameType = ""; // ✅ "ngang" hoặc "dọc"
    
    // ✅ Lưu kích thước ban đầu của quad từ prefab
    private Vector3 quadOriginalScale;
    private bool hasStoredOriginalScale = false;

    // Properties
    public int FrameId => frameId;
    public string FrameName => frameName;
    public ImageData ImageData => currentImageData;
    public Sprite CurrentSprite => currentSprite;
    public bool IsLoaded => currentSprite != null && currentImageData != null;
    public bool IsLoading => isLoading || (ArtManager.Instance != null && ArtManager.Instance.IsImageLoading(frameId));
    public GameObject ButtonContainer => buttonContainer;
    public string CurrentFrameType => currentFrameType;

    #region Unity Lifecycle

    private void Awake()
    {
        // Auto-assign references
        if (quadRenderer == null)
        {
            quadRenderer = GetComponent<MeshRenderer>();
        }

        if (cubeFrame == null)
        {
            cubeFrame = transform;
        }

        // ✅ Lưu kích thước ban đầu của quad
        if (quadRenderer != null && !hasStoredOriginalScale)
        {
            quadOriginalScale = quadRenderer.transform.localScale;
            hasStoredOriginalScale = true;

            if (showDebug)
                Debug.Log($"[ArtFrame] Stored quad original scale: {quadOriginalScale}", this);
        }

        // Lưu transform hiện tại
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        lastScale = transform.localScale;

        // ✅ Ẩn cả 2 khung ban đầu
        if (landscapeFrame != null)
            landscapeFrame.gameObject.SetActive(false);

        if (portraitFrame != null)
            portraitFrame.gameObject.SetActive(false);

        // Ẩn button container ban đầu
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(false);
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] Awake: Frame {frameId} initialized", this);
    }

    private void Start()
    {
        // Subscribe to ArtManager events
        SubscribeToManager();

        // Setup button events
        SetupButtonEvents();

        // Get main camera reference
        mainCamera = Camera.main;

        // Setup canvas sorting order
        SetupCanvasSorting();

        // Load artwork if enabled
        if (loadOnStart)
        {
            LoadArtwork();
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] Start: Frame {frameId} ready", this);
    }

    private void Update()
    {
        // Auto save transform if enabled
        if (autoSaveTransform && Time.time - lastSyncTime >= autoSaveDelay)
        {
            CheckAndSaveTransform();
        }

        // Check button visibility based on distance
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
        CleanupResources();
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

        // ✅ Validate frame references
        if (landscapeFrame == null)
            Debug.LogWarning("[ArtFrame] Landscape frame not assigned!", this);

        if (portraitFrame == null)
            Debug.LogWarning("[ArtFrame] Portrait frame not assigned!", this);

        // ✅ Lưu original scale trong editor
        if (quadRenderer != null && !hasStoredOriginalScale && !Application.isPlaying)
        {
            quadOriginalScale = quadRenderer.transform.localScale;
            hasStoredOriginalScale = true;
        }
    }

    #endregion

    #region Manager Subscription

    private void SubscribeToManager()
    {
        if (isSubscribed || ArtManager.Instance == null)
            return;

        ArtManager.Instance.OnImageLoaded += OnImageLoadedFromManager;
        ArtManager.Instance.OnImageLoadFailed += OnImageLoadFailedFromManager;
        isSubscribed = true;

        if (showDebug)
            Debug.Log($"[ArtFrame] Subscribed to ArtManager events", this);
    }

    private void UnsubscribeFromManager()
    {
        if (!isSubscribed || ArtManager.Instance == null)
            return;

        ArtManager.Instance.OnImageLoaded -= OnImageLoadedFromManager;
        ArtManager.Instance.OnImageLoadFailed -= OnImageLoadFailedFromManager;
        isSubscribed = false;

        if (showDebug)
            Debug.Log($"[ArtFrame] Unsubscribed from ArtManager events", this);
    }

    private void OnImageLoadedFromManager(int loadedFrameId, Sprite sprite, ImageData data)
    {
        if (loadedFrameId != frameId)
            return;

        if (showDebug)
            Debug.Log($"[ArtFrame] Image loaded from manager: Frame {frameId}", this);

        currentSprite = sprite;
        currentImageData = data;
        isLoading = false;

        // ✅ Xử lý frame type và hiển thị
        string frameType = GetFrameType(data, sprite);
        SetFrameVisibility(frameType);

        ApplyArtwork(sprite);

        // ✅ Resize quad theo tỷ lệ ảnh
        if (autoResize && sprite != null && sprite.texture != null)
        {
            ResizeQuadByImageRatio(sprite.texture, frameType);
        }

        // Sync transform from server if enabled
        if (syncTransformFromServer && data != null)
        {
            ApplyTransformFromImageData(data);
        }
    }

    private void OnImageLoadFailedFromManager(int errorFrameId, string errorMessage)
    {
        if (errorFrameId != frameId)
            return;

        isLoading = false;

        Debug.LogError($"[ArtFrame] Failed to load image for frame {frameId}: {errorMessage}", this);
    }

    #endregion

    #region Load Artwork

    /// <summary>
    /// Load artwork từ ArtManager (có cache)
    /// </summary>
    public void LoadArtwork(bool forceRefresh = false)
    {
        if (frameId <= 0)
        {
            Debug.LogError($"[ArtFrame] Invalid frame ID: {frameId}", this);
            return;
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] Loading artwork for frame {frameId} (forceRefresh: {forceRefresh})", this);

        isLoading = true;
        ShowLoadingTexture();

        if (forceRefresh)
        {
            // Force refresh - clear cache và reload
            ArtManager.Instance.RefreshFrame(frameId, OnArtworkLoaded);
        }
        else
        {
            // Normal load - sử dụng cache nếu có
            ArtManager.Instance.LoadImage(frameId, OnArtworkLoaded);
        }
    }

    /// <summary>
    /// Callback khi artwork load xong
    /// </summary>
    private void OnArtworkLoaded(Sprite sprite, ImageData data)
    {
        isLoading = false;

        if (sprite == null)
        {
            Debug.LogWarning($"[ArtFrame] No sprite loaded for frame {frameId}", this);
            return;
        }

        currentSprite = sprite;
        currentImageData = data;

        // ✅ Xử lý frame type
        string frameType = GetFrameType(data, sprite);
        SetFrameVisibility(frameType);

        ApplyArtwork(sprite);

        // ✅ Resize quad theo tỷ lệ ảnh
        if (autoResize && sprite.texture != null)
        {
            ResizeQuadByImageRatio(sprite.texture, frameType);
        }

        // Apply transform from server data
        if (syncTransformFromServer && data != null)
        {
            ApplyTransformFromImageData(data);
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] Artwork loaded successfully for frame {frameId} (Type: {frameType})", this);
    }

    /// <summary>
    /// Apply sprite lên material
    /// </summary>
    private void ApplyArtwork(Sprite sprite)
    {
        if (sprite == null || quadRenderer == null)
            return;

        // Cleanup old material
        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
        }

        // Create new material
        currentMaterial = new Material(Shader.Find("Unlit/Texture"));
        currentMaterial.mainTexture = sprite.texture;

        // Apply to renderer
        quadRenderer.material = currentMaterial;

        if (showDebug)
            Debug.Log($"[ArtFrame] Applied artwork to frame {frameId}", this);
    }

    /// <summary>
    /// Hiển thị loading texture
    /// </summary>
    private void ShowLoadingTexture()
    {
        if (loadingTexture == null || quadRenderer == null)
            return;

        Material loadingMat = new Material(Shader.Find("Unlit/Texture"));
        loadingMat.mainTexture = loadingTexture;
        quadRenderer.material = loadingMat;

        if (showDebug)
            Debug.Log($"[ArtFrame] Showing loading texture for frame {frameId}", this);
    }

    /// <summary>
    /// Clear artwork và cache
    /// </summary>
    public void ClearArtwork()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Clearing artwork for frame {frameId}", this);

        // Clear cache in ArtManager
        ArtManager.Instance.ClearFrameCache(frameId);

        // Clear local references
        currentSprite = null;
        currentImageData = null;
        currentFrameType = "";

        // Cleanup material
        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
            currentMaterial = null;
        }

        // ✅ Ẩn cả 2 khung
        if (landscapeFrame != null)
            landscapeFrame.gameObject.SetActive(false);

        if (portraitFrame != null)
            portraitFrame.gameObject.SetActive(false);

        // ✅ Reset quad về kích thước ban đầu
        if (quadRenderer != null && hasStoredOriginalScale)
        {
            quadRenderer.transform.localScale = quadOriginalScale;
        }

        // Show loading texture
        ShowLoadingTexture();
    }

    /// <summary>
    /// Reload artwork (clear + load)
    /// </summary>
    public void ReloadArtwork(bool forceRefresh = true)
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Reloading artwork for frame {frameId}", this);

        ClearArtwork();
        LoadArtwork(forceRefresh);
    }

    #endregion

    #region Frame Type Handling

    /// <summary>
    /// ✅ Lấy frame type từ ImageData hoặc tự động detect
    /// </summary>
    private string GetFrameType(ImageData data, Sprite sprite)
    {
        string frameType = "";

        // Ưu tiên lấy từ server
        if (data != null && !string.IsNullOrEmpty(data.imageType))
        {
            frameType = data.imageType;
        }
        // Fallback: Tự động detect từ tỷ lệ ảnh
        else if (sprite != null && sprite.texture != null)
        {
            float aspectRatio = (float)sprite.texture.width / sprite.texture.height;
            frameType = aspectRatio >= 1f ? "ngang" : "dọc";

            if (showDebug)
                Debug.Log($"[ArtFrame] Auto-detected frame type: {frameType} (aspect: {aspectRatio:F2})", this);
        }
        else
        {
            frameType = "ngang"; // Default
        }

        currentFrameType = frameType;
        return frameType;
    }

    /// <summary>
    /// ✅ Hiện/ẩn khung dựa trên frame type (KHÔNG scale frame)
    /// </summary>
    private void SetFrameVisibility(string frameType)
    {
        bool isLandscape = frameType.ToLower() == "ngang" || frameType.ToLower() == "landscape";
        bool isPortrait = frameType.ToLower() == "dọc" || frameType.ToLower() == "portrait";

        // Hiện khung ngang, ẩn khung dọc
        if (isLandscape)
        {
            if (landscapeFrame != null)
                landscapeFrame.gameObject.SetActive(true);

            if (portraitFrame != null)
                portraitFrame.gameObject.SetActive(false);

            if (showDebug)
                Debug.Log($"[ArtFrame] Showing landscape frame, hiding portrait frame", this);
        }
        // Hiện khung dọc, ẩn khung ngang
        else if (isPortrait)
        {
            if (landscapeFrame != null)
                landscapeFrame.gameObject.SetActive(false);

            if (portraitFrame != null)
                portraitFrame.gameObject.SetActive(true);

            if (showDebug)
                Debug.Log($"[ArtFrame] Showing portrait frame, hiding landscape frame", this);
        }
        // Unknown type - hiện landscape mặc định
        else
        {
            if (showDebug)
                Debug.LogWarning($"[ArtFrame] Unknown frame type: {frameType}, showing landscape by default", this);

            if (landscapeFrame != null)
                landscapeFrame.gameObject.SetActive(true);

            if (portraitFrame != null)
                portraitFrame.gameObject.SetActive(false);
        }

        // ✅ Frame KHÔNG bao giờ scale, giữ nguyên kích thước ban đầu
    }

    #endregion

    #region Auto Resize Quad

    /// <summary>
    /// ✅ Resize QUAD theo tỷ lệ ảnh, dựa trên kích thước ban đầu của quad
    /// - Ảnh NGANG: Giữ nguyên CHIỀU CAO ban đầu, scale chiều rộng
    /// - Ảnh DỌC: Giữ nguyên CHIỀU NGANG ban đầu, scale chiều cao
    /// - Frame KHÔNG scale
    /// </summary>
    private void ResizeQuadByImageRatio(Texture2D texture, string frameType)
    {
        if (texture == null || quadRenderer == null)
        {
            if (showDebug)
                Debug.LogWarning($"[ArtFrame] Cannot resize: texture or quadRenderer is null", this);
            return;
        }

        if (!hasStoredOriginalScale)
        {
            Debug.LogWarning($"[ArtFrame] Original quad scale not stored!", this);
            return;
        }

        float aspectRatio = (float)texture.width / (float)texture.height;
        Vector3 newScale = quadRenderer.transform.localScale;

        bool isLandscape = frameType.ToLower() == "ngang" || frameType.ToLower() == "landscape";

        if (isLandscape)
        {
            // ✅ ẢNH NGANG: Giữ nguyên CHIỀU CAO ban đầu của quad, scale chiều rộng
            if (useQuadOriginalSize)
            {
                newScale.y = quadOriginalScale.y; // Giữ nguyên chiều cao ban đầu
                newScale.x = quadOriginalScale.y * aspectRatio; // Scale chiều rộng theo tỷ lệ
            }
            else
            {
                newScale.y = landscapeFixedHeight;
                newScale.x = landscapeFixedHeight * aspectRatio;
            }

            // Clamp width
            newScale.x = Mathf.Clamp(newScale.x, widthLimits.x, widthLimits.y);

            if (showDebug)
            {
                Debug.Log($"[ArtFrame] Landscape quad resize:");
                Debug.Log($"  Original quad scale: {quadOriginalScale}");
                Debug.Log($"  Fixed height: {newScale.y:F2}");
                Debug.Log($"  Calculated width: {newScale.x:F2}");
                Debug.Log($"  Aspect ratio: {aspectRatio:F2}");
            }
        }
        else
        {
            // ✅ ẢNH DỌC: Giữ nguyên CHIỀU NGANG ban đầu của quad, scale chiều cao
            if (useQuadOriginalSize)
            {
                newScale.x = quadOriginalScale.x; // Giữ nguyên chiều ngang ban đầu
                newScale.y = quadOriginalScale.x / aspectRatio; // Scale chiều cao theo tỷ lệ
            }
            else
            {
                newScale.x = portraitFixedWidth;
                newScale.y = portraitFixedWidth / aspectRatio;
            }

            // Clamp height
            newScale.y = Mathf.Clamp(newScale.y, heightLimits.x, heightLimits.y);

            if (showDebug)
            {
                Debug.Log($"[ArtFrame] Portrait quad resize:");
                Debug.Log($"  Original quad scale: {quadOriginalScale}");
                Debug.Log($"  Fixed width: {newScale.x:F2}");
                Debug.Log($"  Calculated height: {newScale.y:F2}");
                Debug.Log($"  Aspect ratio: {aspectRatio:F2}");
            }
        }

        // Keep Z scale unchanged
        newScale.z = quadRenderer.transform.localScale.z;

        // ✅ CHỈ scale QUAD, KHÔNG scale frame
        quadRenderer.transform.localScale = newScale;

        if (showDebug)
        {
            Debug.Log($"[ArtFrame] Quad resized to: {newScale}");
            Debug.Log($"[ArtFrame] Frame scale unchanged (landscape: {(landscapeFrame != null ? landscapeFrame.localScale.ToString() : "null")}, portrait: {(portraitFrame != null ? portraitFrame.localScale.ToString() : "null")})");
        }
    }

    /// <summary>
    /// Manual resize (gọi từ editor hoặc code)
    /// </summary>
    public void ManualResize()
    {
        if (currentSprite != null && currentSprite.texture != null)
        {
            string frameType = GetFrameType(currentImageData, currentSprite);
            ResizeQuadByImageRatio(currentSprite.texture, frameType);
        }
        else
        {
            Debug.LogWarning($"[ArtFrame] Cannot resize: No sprite loaded", this);
        }
    }

    /// <summary>
    /// ✅ Reset quad về kích thước ban đầu
    /// </summary>
    public void ResetQuadToOriginalSize()
    {
        if (quadRenderer != null && hasStoredOriginalScale)
        {
            quadRenderer.transform.localScale = quadOriginalScale;

            if (showDebug)
                Debug.Log($"[ArtFrame] Reset quad to original size: {quadOriginalScale}", this);
        }
    }

    #endregion

    #region Transform Sync

    /// <summary>
    /// Apply transform từ ImageData
    /// </summary>
    private void ApplyTransformFromImageData(ImageData data)
    {
        if (data == null)
            return;

        bool hasChanges = false;

        // Apply position
        if (syncPosition && data.position != null)
        {
            Vector3 serverPosition = data.position.ToVector3();
            transform.position = serverPosition;
            lastPosition = serverPosition;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[ArtFrame] Applied position from server: {serverPosition}", this);
        }

        // Apply rotation
        if (syncRotation && data.rotation != null)
        {
            Vector3 serverRotation = data.rotation.ToVector3();
            transform.eulerAngles = serverRotation;
            lastRotation = transform.rotation;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[ArtFrame] Applied rotation from server: {serverRotation}", this);
        }

        // Update last scale
        lastScale = transform.localScale;

        if (hasChanges && showDebug)
        {
            Debug.Log($"[ArtFrame] Transform synced from server for frame {frameId}", this);
        }
    }

    /// <summary>
    /// Sync transform từ server (force reload data)
    /// </summary>
    public void SyncTransformFromServer()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Syncing transform from server for frame {frameId}", this);

        // Lấy ImageData từ cache hoặc server
        ImageData cachedData = ArtManager.Instance.GetCachedImageData(frameId);

        if (cachedData != null)
        {
            ApplyTransformFromImageData(cachedData);
        }
        else
        {
            // Load from server
            APIManager.Instance.GetImageByFrame(frameId, (success, data, error) =>
            {
                if (success && data != null)
                {
                    ApplyTransformFromImageData(data);
                }
                else
                {
                    Debug.LogWarning($"[ArtFrame] Failed to sync transform: {error}", this);
                }
            });
        }
    }

    /// <summary>
    /// Kiểm tra và lưu transform nếu có thay đổi
    /// </summary>
    private void CheckAndSaveTransform()
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

    /// <summary>
    /// Lưu transform lên server qua APIManager
    /// </summary>
    public void SaveTransformToServer()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Saving transform to server for frame {frameId}", this);

        Vector3 position = transform.position;
        Vector3 rotation = transform.eulerAngles;

        APIManager.Instance.UpdateTransform(frameId, position, rotation, (success, message) =>
        {
            if (success)
            {
                lastPosition = position;
                lastRotation = transform.rotation;
                lastScale = transform.localScale;

                if (showDebug)
                    Debug.Log($"[ArtFrame] Transform saved successfully for frame {frameId}", this);
            }
            else
            {
                Debug.LogError($"[ArtFrame] Failed to save transform: {message}", this);
            }
        });
    }

    /// <summary>
    /// Force sync transform (refresh data từ server)
    /// </summary>
    public void ForceSyncTransformFromServer()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Force syncing transform for frame {frameId}", this);

        StartCoroutine(ForceSyncCoroutine());
    }

    private IEnumerator ForceSyncCoroutine()
    {
        // Refresh frame data
        ArtManager.Instance.RefreshFrame(frameId, (sprite, data) =>
        {
            if (data != null)
            {
                ApplyTransformFromImageData(data);
            }
        });

        yield return new WaitForSeconds(0.5f);
    }

    #endregion

    #region Button Management

    /// <summary>
    /// Setup button events
    /// </summary>
    private void SetupButtonEvents()
    {
        // Setup Info button
        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(OnInfoButtonClicked);
            EnsureButtonInteractable(infoButton);

            if (showDebug)
                Debug.Log($"[ArtFrame] Info button setup for frame {frameId}", this);
        }

        // Setup Transform button
        if (transformButton != null)
        {
            transformButton.onClick.RemoveAllListeners();
            transformButton.onClick.AddListener(OnTransformButtonClicked);
            EnsureButtonInteractable(transformButton);

            if (showDebug)
                Debug.Log($"[ArtFrame] Transform button setup for frame {frameId}", this);
        }
    }

    /// <summary>
    /// Đảm bảo button có thể click được
    /// </summary>
    private void EnsureButtonInteractable(Button button)
    {
        if (button == null)
            return;

        button.interactable = true;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.raycastTarget = true;
        }
    }

    /// <summary>
    /// Setup canvas sorting order
    /// </summary>
    private void SetupCanvasSorting()
    {
        if (buttonContainer == null)
            return;

        Canvas canvas = buttonContainer.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.sortingOrder = 100;
        }
    }

    /// <summary>
    /// Handler cho Info button
    /// </summary>
    private void OnInfoButtonClicked()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Info button clicked for frame {frameId}", this);

        // Lấy data từ cache hoặc server
        ImageData data = currentImageData ?? ArtManager.Instance.GetCachedImageData(frameId);

        if (data != null)
        {
            ShowImageEditPopup(data);
        }
        else
        {
            // Load from server
            APIManager.Instance.GetImageByFrame(frameId, (success, imageData, error) =>
            {
                if (success && imageData != null)
                {
                    ShowImageEditPopup(imageData);
                }
                else
                {
                    Debug.LogError($"[ArtFrame] Failed to get image data: {error}", this);
                }
            });
        }
    }

    /// <summary>
    /// Handler cho Transform button
    /// </summary>
    private void OnTransformButtonClicked()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Transform button clicked for frame {frameId}", this);

        // Lấy data từ cache hoặc server
        ImageData data = currentImageData ?? ArtManager.Instance.GetCachedImageData(frameId);

        if (data != null)
        {
            ShowTransformEditPopup(data);
        }
        else
        {
            // Load from server
            APIManager.Instance.GetImageByFrame(frameId, (success, imageData, error) =>
            {
                if (success && imageData != null)
                {
                    ShowTransformEditPopup(imageData);
                }
                else
                {
                    Debug.LogError($"[ArtFrame] Failed to get image data: {error}", this);
                }
            });
        }
    }

    /// <summary>
    /// Hiển thị ImageEditPopup
    /// </summary>
    private void ShowImageEditPopup(ImageData data)
    {
        if (ImageEditPopup.Instance != null)
        {
            ImageEditPopup.Instance.Show(data, null);
        }
        else
        {
            Debug.LogError("[ArtFrame] ImageEditPopup.Instance is null!", this);
        }
    }

    /// <summary>
    /// Hiển thị TransformEditPopup
    /// </summary>
    private void ShowTransformEditPopup(ImageData data)
    {
        if (TransformEditPopup.Instance != null)
        {
            TransformEditPopup.Instance.Show(data, this);
        }
        else
        {
            Debug.LogError("[ArtFrame] TransformEditPopup.Instance is null!", this);
        }
    }

    #endregion

    #region Button Visibility

    /// <summary>
    /// Kiểm tra và cập nhật visibility của buttons dựa trên khoảng cách
    /// </summary>
    private void CheckButtonVisibility()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        if (buttonContainer == null)
            return;

        // Tính khoảng cách từ camera đến frame
        float distanceToCamera = Vector3.Distance(transform.position, mainCamera.transform.position);

        // Hiển thị/ẩn buttons dựa trên khoảng cách
        bool shouldShow = distanceToCamera <= buttonDisplayDistance;

        if (buttonContainer.activeSelf != shouldShow)
        {
            buttonContainer.SetActive(shouldShow);

            if (showDebug)
            {
                Debug.Log($"[ArtFrame] Buttons {(shouldShow ? "shown" : "hidden")} for frame {frameId} (distance: {distanceToCamera:F2})", this);
            }
        }
    }

    /// <summary>
    /// Kiểm tra pointer có đang trên buttons không
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
    /// Force hiển thị buttons
    /// </summary>
    public void ForceShowButtons()
    {
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(true);

            if (showDebug)
                Debug.Log($"[ArtFrame] Force showing buttons for frame {frameId}", this);
        }
    }

    /// <summary>
    /// Force ẩn buttons
    /// </summary>
    public void ForceHideButtons()
    {
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(false);

            if (showDebug)
                Debug.Log($"[ArtFrame] Force hiding buttons for frame {frameId}", this);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set frame ID từ bên ngoài
    /// </summary>
    public void SetFrameId(int id)
    {
        frameId = id;
        if (showDebug)
            Debug.Log($"[ArtFrame] Frame ID set to: {id}", this);
    }

    /// <summary>
    /// Set frame name từ bên ngoài
    /// </summary>
    public void SetFrameName(string name)
    {
        frameName = name;
        gameObject.name = $"ArtFrame_{frameId}_{name}";
        if (showDebug)
            Debug.Log($"[ArtFrame] Frame name set to: {name}", this);
    }

    #endregion

    #region Cleanup

    /// <summary>
    /// Cleanup resources
    /// </summary>
    private void CleanupResources()
    {
        // Remove button listeners
        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
        }

        if (transformButton != null)
        {
            transformButton.onClick.RemoveAllListeners();
        }

        // Destroy material
        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
            currentMaterial = null;
        }

        // Clear references
        currentSprite = null;
        currentImageData = null;
        currentFrameType = "";

        if (showDebug)
            Debug.Log($"[ArtFrame] Resources cleaned up for frame {frameId}", this);
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Draw quad bounds
        if (showDebug && quadRenderer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(quadRenderer.transform.position, quadRenderer.transform.localScale);
        }

        // Draw frame bounds (landscape)
        if (showDebug && landscapeFrame != null && landscapeFrame.gameObject.activeSelf)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(landscapeFrame.position, landscapeFrame.localScale);
        }

        // Draw frame bounds (portrait)
        if (showDebug && portraitFrame != null && portraitFrame.gameObject.activeSelf)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(portraitFrame.position, portraitFrame.localScale);
        }

        // Draw button visibility range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, buttonDisplayDistance);
    }

    #endregion
}
