using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// ArtFrame - Component đại diện cho một khung tranh trong scene
/// Sử dụng ArtManager để load và cache ảnh
/// Sử dụng APIArtManager để lấy metadata và update transform
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
    [SerializeField] private Transform landscapeFrame;
    [SerializeField] private Transform portraitFrame;

    [Header("Loading")]
    [SerializeField] private Texture2D loadingTexture;
    [SerializeField] private bool loadOnStart = false; 

    [Header("Auto Resize")]
    [SerializeField] private bool autoResize = true;

    [Header("Quad Resize Settings")]
    [Tooltip("Khi resize, giữ nguyên chiều ngang của quad cho ảnh dọc")]
    [SerializeField] private bool useQuadOriginalSize = true;

    [Header("Manual Override (nếu không dùng original size)")]
    [SerializeField] private float landscapeFixedHeight = 2f;
    [SerializeField] private float portraitFixedWidth = 1.5f;
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

    [Header("Runtime Gizmo")]
    [SerializeField] private RuntimeTransformGizmo gizmo;

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
    private string currentFrameType = "";

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

        // Lưu kích thước ban đầu của quad
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

        // Ẩn cả 2 khung ban đầu
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
            Debug.Log($"[ArtFrame] Awake: Frame {frameId} initialized (loadOnStart={loadOnStart})", this);
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

        // ✅ CHỈ LOAD NẾU loadOnStart = true (mặc định là false)
        if (loadOnStart)
        {
            if (showDebug)
                Debug.Log($"[ArtFrame] loadOnStart=true, loading artwork for frame {frameId}", this);
            
            LoadArtwork();
        }
        else
        {
            if (showDebug)
                Debug.Log($"[ArtFrame] loadOnStart=false, skipping auto-load for frame {frameId}", this);
        }
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

        // Validate frame references
        if (landscapeFrame == null)
            Debug.LogWarning("[ArtFrame] Landscape frame not assigned!", this);

        if (portraitFrame == null)
            Debug.LogWarning("[ArtFrame] Portrait frame not assigned!", this);

        // Lưu original scale trong editor
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

        string frameType = GetFrameType(data, sprite);
        SetFrameVisibility(frameType);

        ApplyArtwork(sprite);

        if (autoResize && sprite != null && sprite.texture != null)
        {
            ResizeQuadByImageRatio(sprite.texture, frameType);
        }

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
    /// ✅ Load artwork từ ArtManager (CHỈ GỌI KHI CẦN)
    /// </summary>
    public void LoadArtwork(bool forceRefresh = false)
    {
        if (frameId <= 0)
        {
            Debug.LogError($"[ArtFrame] Invalid frame ID: {frameId}", this);
            return;
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] LoadArtwork called for frame {frameId} (forceRefresh: {forceRefresh})", this);

        isLoading = true;
        ShowLoadingTexture();

        if (forceRefresh)
        {
            ArtManager.Instance.RefreshFrame(frameId, OnArtworkLoaded);
        }
        else
        {
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

        string frameType = GetFrameType(data, sprite);
        SetFrameVisibility(frameType);

        ApplyArtwork(sprite);

        if (autoResize && sprite.texture != null)
        {
            ResizeQuadByImageRatio(sprite.texture, frameType);
        }

        if (syncTransformFromServer && data != null)
        {
            ApplyTransformFromImageData(data);
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] Artwork loaded successfully for frame {frameId} (Type: {frameType})", this);
    }

    /// <summary>
    /// ✅ Apply sprite lên material - CHỈ GỌI TỪ CALLBACK
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
    /// ✅ THÊM MỚI: Apply texture trực tiếp (GỌI TỪ ArtPrefabManager)
    /// </summary>
    public void ApplyTextureDirectly(Texture2D texture, ImageData data)
    {
        if (texture == null || quadRenderer == null)
        {
            Debug.LogWarning($"[ArtFrame] Cannot apply texture: texture or renderer is null", this);
            return;
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] Applying texture directly for frame {frameId}", this);

        // Cleanup old material
        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
        }

        // Create new material
        currentMaterial = new Material(Shader.Find("Unlit/Texture"));
        currentMaterial.mainTexture = texture;

        // Apply to renderer
        quadRenderer.material = currentMaterial;

        // Store data
        currentImageData = data;
        isLoading = false;

        // Handle frame type
        string frameType = GetFrameType(data, null);
        SetFrameVisibility(frameType);

        // Resize quad
        if (autoResize)
        {
            ResizeQuadByImageRatio(texture, frameType);
        }

        // Apply transform
        if (syncTransformFromServer && data != null)
        {
            ApplyTransformFromImageData(data);
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] Texture applied directly for frame {frameId}", this);
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
        if (ArtManager.Instance != null)
        {
            ArtManager.Instance.ClearFrameCache(frameId);
        }

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

        // Ẩn cả 2 khung
        if (landscapeFrame != null)
            landscapeFrame.gameObject.SetActive(false);

        if (portraitFrame != null)
            portraitFrame.gameObject.SetActive(false);

        // Reset quad về kích thước ban đầu
        if (quadRenderer != null && hasStoredOriginalScale)
        {
            quadRenderer.transform.localScale = quadOriginalScale;
        }

        // Show loading texture
        ShowLoadingTexture();
    }

    private void OnMouseDown()
    {
        if (EventSystem.current.IsPointerOverGameObject())
            return;

        if (currentImageData == null)
        {
            if (showDebug)
                Debug.Log($"[ArtFrame] No image data to edit for frame {frameId}");
            return;
        }

        if (showDebug)
            Debug.Log($"[ArtFrame] Clicked on frame {frameId}, opening edit popup");

        // Show popup
        if (TransformEditPopup.Instance != null)
        {
            TransformEditPopup.Instance.Show(currentImageData, this);
        }
    }

    public void DeactivateGizmo()
    {
        if (gizmo != null)
        {
            gizmo.Deactivate();
        }
    }

    /// <summary>
    /// Reload artwork (clear + load)
    /// </summary>
    public void ReloadArtwork(bool forceRefresh = true)
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] ReloadArtwork called for frame {frameId}", this);

        ClearArtwork();
        LoadArtwork(forceRefresh);
    }

    #endregion

    #region Frame Type Handling

    /// <summary>
    /// Lấy frame type từ ImageData hoặc tự động detect
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
    /// Hiện/ẩn khung dựa trên frame type
    /// </summary>
    private void SetFrameVisibility(string frameType)
    {
        bool isLandscape = frameType.ToLower() == "ngang" || frameType.ToLower() == "landscape";
        bool isPortrait = frameType.ToLower() == "dọc" || frameType.ToLower() == "portrait";

        if (isLandscape)
        {
            if (landscapeFrame != null)
                landscapeFrame.gameObject.SetActive(true);

            if (portraitFrame != null)
                portraitFrame.gameObject.SetActive(false);

            if (showDebug)
                Debug.Log($"[ArtFrame] Showing landscape frame, hiding portrait frame", this);
        }
        else if (isPortrait)
        {
            if (landscapeFrame != null)
                landscapeFrame.gameObject.SetActive(false);

            if (portraitFrame != null)
                portraitFrame.gameObject.SetActive(true);

            if (showDebug)
                Debug.Log($"[ArtFrame] Showing portrait frame, hiding landscape frame", this);
        }
        else
        {
            if (showDebug)
                Debug.LogWarning($"[ArtFrame] Unknown frame type: {frameType}, showing landscape by default", this);

            if (landscapeFrame != null)
                landscapeFrame.gameObject.SetActive(true);

            if (portraitFrame != null)
                portraitFrame.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Auto Resize Quad

    /// <summary>
    /// Resize QUAD theo tỷ lệ ảnh
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
            if (useQuadOriginalSize)
            {
                newScale.y = quadOriginalScale.y;
                newScale.x = quadOriginalScale.y * aspectRatio;
            }
            else
            {
                newScale.y = landscapeFixedHeight;
                newScale.x = landscapeFixedHeight * aspectRatio;
            }

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
            if (useQuadOriginalSize)
            {
                newScale.x = quadOriginalScale.x;
                newScale.y = quadOriginalScale.x / aspectRatio;
            }
            else
            {
                newScale.x = portraitFixedWidth;
                newScale.y = portraitFixedWidth / aspectRatio;
            }

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

        newScale.z = quadRenderer.transform.localScale.z;

        quadRenderer.transform.localScale = newScale;

        if (showDebug)
        {
            Debug.Log($"[ArtFrame] Quad resized to: {newScale}");
        }
    }

    /// <summary>
    /// Manual resize
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
    /// Reset quad về kích thước ban đầu
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

        if (syncPosition && data.position != null)
        {
            Vector3 serverPosition = data.position.ToVector3();
            transform.position = serverPosition;
            lastPosition = serverPosition;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[ArtFrame] Applied position from server: {serverPosition}", this);
        }

        if (syncRotation && data.rotation != null)
        {
            Vector3 serverRotation = data.rotation.ToVector3();
            transform.eulerAngles = serverRotation;
            lastRotation = transform.rotation;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[ArtFrame] Applied rotation from server: {serverRotation}", this);
        }

        lastScale = transform.localScale;

        if (hasChanges && showDebug)
        {
            Debug.Log($"[ArtFrame] Transform synced from server for frame {frameId}", this);
        }
    }

    /// <summary>
    /// Sync transform từ server
    /// </summary>
    public void SyncTransformFromServer()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Syncing transform from server for frame {frameId}", this);

        if (ArtManager.Instance != null)
        {
            ImageData cachedData = ArtManager.Instance.GetCachedImageData(frameId);

            if (cachedData != null)
            {
                ApplyTransformFromImageData(cachedData);
            }
            else
            {
                APIArtManager.Instance.GetImageByFrame(frameId, (success, data, error) =>
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
    /// Lưu transform lên server
    /// </summary>
    public void SaveTransformToServer()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Saving transform to server for frame {frameId}", this);

        Vector3 position = transform.position;
        Vector3 rotation = transform.eulerAngles;

        APIArtManager.Instance.UpdateTransform(frameId, position, rotation, (success, message) =>
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
    /// Force sync transform
    /// </summary>
    public void ForceSyncTransformFromServer()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Force syncing transform for frame {frameId}", this);

        StartCoroutine(ForceSyncCoroutine());
    }

    private IEnumerator ForceSyncCoroutine()
    {
        if (ArtManager.Instance != null)
        {
            ArtManager.Instance.RefreshFrame(frameId, (sprite, data) =>
            {
                if (data != null)
                {
                    ApplyTransformFromImageData(data);
                }
            });
        }

        yield return new WaitForSeconds(0.5f);
    }

    #endregion

    #region Button Management

    private void SetupButtonEvents()
    {
        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
            infoButton.onClick.AddListener(OnInfoButtonClicked);
            EnsureButtonInteractable(infoButton);

            if (showDebug)
                Debug.Log($"[ArtFrame] Info button setup for frame {frameId}", this);
        }

        if (transformButton != null)
        {
            transformButton.onClick.RemoveAllListeners();
            transformButton.onClick.AddListener(OnTransformButtonClicked);
            EnsureButtonInteractable(transformButton);

            if (showDebug)
                Debug.Log($"[ArtFrame] Transform button setup for frame {frameId}", this);
        }
    }

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

    private void OnInfoButtonClicked()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Info button clicked for frame {frameId}", this);

        ImageData data = currentImageData;
        
        if (ArtManager.Instance != null && data == null)
        {
            data = ArtManager.Instance.GetCachedImageData(frameId);
        }

        if (data != null)
        {
            ShowImageEditPopup(data);
        }
        else
        {
            APIArtManager.Instance.GetImageByFrame(frameId, (success, imageData, error) =>
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

    private void OnTransformButtonClicked()
    {
        if (showDebug)
            Debug.Log($"[ArtFrame] Transform button clicked for frame {frameId}", this);

        ImageData data = currentImageData;
        
        if (ArtManager.Instance != null && data == null)
        {
            data = ArtManager.Instance.GetCachedImageData(frameId);
        }

        if (data != null)
        {
            ShowTransformEditPopup(data);
        }
        else
        {
            APIArtManager.Instance.GetImageByFrame(frameId, (success, imageData, error) =>
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

    private void ShowImageEditPopup(ImageData data)
    {
        if (ImageEditPopup.Instance != null)
        {
            ImageEditPopup.Instance.Show(data, this);
        }
        else
        {
            Debug.LogError("[ArtFrame] ImageEditPopup.Instance is null!", this);
        }
    }

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

    private void CheckButtonVisibility()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        if (buttonContainer == null)
            return;

        float distanceToCamera = Vector3.Distance(transform.position, mainCamera.transform.position);

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

    public void ForceShowButtons()
    {
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(true);

            if (showDebug)
                Debug.Log($"[ArtFrame] Force showing buttons for frame {frameId}", this);
        }
    }

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

    public void SetFrameId(int id)
    {
        frameId = id;
        if (showDebug)
            Debug.Log($"[ArtFrame] Frame ID set to: {id}", this);
    }

    public void SetFrameName(string name)
    {
        frameName = name;
        gameObject.name = $"ArtFrame_{frameId}_{name}";
        if (showDebug)
            Debug.Log($"[ArtFrame] Frame name set to: {name}", this);
    }

    #endregion

    #region Cleanup

    private void CleanupResources()
    {
        if (infoButton != null)
        {
            infoButton.onClick.RemoveAllListeners();
        }

        if (transformButton != null)
        {
            transformButton.onClick.RemoveAllListeners();
        }

        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
            currentMaterial = null;
        }

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
        if (showDebug && quadRenderer != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(quadRenderer.transform.position, quadRenderer.transform.localScale);
        }

        if (showDebug && landscapeFrame != null && landscapeFrame.gameObject.activeSelf)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(landscapeFrame.position, landscapeFrame.localScale);
        }

        if (showDebug && portraitFrame != null && portraitFrame.gameObject.activeSelf)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(portraitFrame.position, portraitFrame.localScale);
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, buttonDisplayDistance);
    }

    #endregion
}
