using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ImageItem - Đại diện cho một item trong gallery list
/// Sử dụng ArtManager để load thumbnail (có cache)
/// </summary>
public class ImageItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI frameIdText;
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject selectionFrame;
    [SerializeField] private GameObject loadingIndicator; // Optional: loading spinner

    [Header("Thumbnail Settings")]
    [SerializeField] private Sprite defaultThumbnail; // Ảnh mặc định khi chưa load
    [SerializeField] private Color loadingColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // Data
    private ImageData imageData;
    private int frameId;
    private bool isLoading = false;

    // Static reference để track item đang được chọn
    private static ImageItem currentSelectedItem;

    #region Unity Lifecycle

    private void Awake()
    {
        // Setup button listener
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnSelectClicked);
        }

        // Khởi tạo: ẩn selection frame
        if (selectionFrame != null)
        {
            selectionFrame.SetActive(false);
        }

        // Ẩn loading indicator
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }

        // Set default thumbnail
        if (thumbnailImage != null && defaultThumbnail != null)
        {
            thumbnailImage.sprite = defaultThumbnail;
            thumbnailImage.color = Color.white;
        }
    }

    private void OnDestroy()
    {
        // Cleanup button listener
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(OnSelectClicked);
        }

        // Nếu item này đang được chọn và bị hủy, reset biến static
        if (currentSelectedItem == this)
        {
            currentSelectedItem = null;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Setup item với ImageData
    /// </summary>
    public void Setup(ImageData data)
    {
        if (data == null)
        {
            Debug.LogError("[ImageItem] Setup called with null ImageData!");
            return;
        }

        imageData = data;
        frameId = data.frameUse;

        // Set text information
        if (nameText != null)
        {
            nameText.text = !string.IsNullOrEmpty(data.name) ? data.name : "Unnamed";
        }

        if (frameIdText != null)
        {
            frameIdText.text = $"Frame {data.frameUse}";
        }

        if (showDebug)
            Debug.Log($"[ImageItem] Setup: {data.name} (Frame {data.frameUse})");

        // Load thumbnail từ ArtManager (có cache)
        LoadThumbnail();
    }

    /// <summary>
    /// Refresh thumbnail - reload từ server
    /// </summary>
    public void RefreshThumbnail()
    {
        if (imageData == null)
        {
            Debug.LogWarning("[ImageItem] Cannot refresh: ImageData is null");
            return;
        }

        if (showDebug)
            Debug.Log($"[ImageItem] Refreshing thumbnail for frame {frameId}");

        // Clear cache và reload
        ArtManager.Instance.ClearFrameCache(frameId);
        LoadThumbnail();
    }

    /// <summary>
    /// Set trạng thái selected
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectionFrame != null)
        {
            selectionFrame.SetActive(selected);
        }

        if (showDebug)
            Debug.Log($"[ImageItem] {imageData?.name} selected: {selected}");
    }

    /// <summary>
    /// Kiểm tra xem item có đang được chọn không
    /// </summary>
    public bool IsSelected()
    {
        return this == currentSelectedItem;
    }

    /// <summary>
    /// Lấy ImageData
    /// </summary>
    public ImageData GetImageData()
    {
        return imageData;
    }

    /// <summary>
    /// Lấy Frame ID
    /// </summary>
    public int GetFrameId()
    {
        return frameId;
    }

    /// <summary>
    /// Kiểm tra xem thumbnail có đang load không
    /// </summary>
    public bool IsLoading()
    {
        return isLoading;
    }

    #endregion

    #region Private Methods - Thumbnail Loading

    /// <summary>
    /// Load thumbnail sử dụng ArtManager (có cache)
    /// </summary>
    private void LoadThumbnail()
    {
        if (imageData == null || string.IsNullOrEmpty(imageData.url))
        {
            if (showDebug)
                Debug.LogWarning($"[ImageItem] Cannot load thumbnail: Invalid data or URL");
            
            SetDefaultThumbnail();
            return;
        }

        // Kiểm tra cache trước
        Sprite cachedSprite = ArtManager.Instance.GetCachedSprite(frameId);
        if (cachedSprite != null)
        {
            if (showDebug)
                Debug.Log($"[ImageItem] Loading thumbnail from cache: Frame {frameId}");
            
            SetThumbnail(cachedSprite);
            return;
        }

        // Chưa có cache - load mới
        SetLoadingState(true);

        if (showDebug)
            Debug.Log($"[ImageItem] Loading thumbnail from server: {imageData.url}");

        // Sử dụng ArtManager để load (có background loading và cache)
        ArtManager.Instance.LoadImage(frameId, OnThumbnailLoaded);
    }

    /// <summary>
    /// Callback khi thumbnail load xong
    /// </summary>
    private void OnThumbnailLoaded(Sprite sprite, ImageData data)
    {
        SetLoadingState(false);

        if (sprite != null)
        {
            SetThumbnail(sprite);

            if (showDebug)
                Debug.Log($"[ImageItem] Thumbnail loaded successfully: Frame {frameId}");
        }
        else
        {
            if (showDebug)
                Debug.LogWarning($"[ImageItem] Failed to load thumbnail: Frame {frameId}");
            
            SetDefaultThumbnail();
        }

        // Update data nếu có thay đổi
        if (data != null)
        {
            imageData = data;
        }
    }

    /// <summary>
    /// Set thumbnail sprite
    /// </summary>
    private void SetThumbnail(Sprite sprite)
    {
        if (thumbnailImage != null)
        {
            thumbnailImage.sprite = sprite;
            thumbnailImage.color = Color.white;
            thumbnailImage.enabled = true;
        }
    }

    /// <summary>
    /// Set default thumbnail khi không load được
    /// </summary>
    private void SetDefaultThumbnail()
    {
        if (thumbnailImage != null)
        {
            if (defaultThumbnail != null)
            {
                thumbnailImage.sprite = defaultThumbnail;
            }
            thumbnailImage.color = Color.white;
            thumbnailImage.enabled = true;
        }
    }

    /// <summary>
    /// Set loading state
    /// </summary>
    private void SetLoadingState(bool loading)
    {
        isLoading = loading;

        // Show/hide loading indicator
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(loading);
        }

        // Dim thumbnail khi đang load
        if (thumbnailImage != null && loading)
        {
            thumbnailImage.color = loadingColor;
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Xử lý khi click vào item
    /// </summary>
    private void OnSelectClicked()
    {
        if (imageData == null)
        {
            Debug.LogError("[ImageItem] Cannot select: ImageData is null!");
            return;
        }

        if (showDebug)
            Debug.Log($"[ImageItem] Clicked: {imageData.name} (Frame {frameId})");

        // Deselect item trước đó
        if (currentSelectedItem != null && currentSelectedItem != this)
        {
            currentSelectedItem.SetSelected(false);
        }

        // Select item hiện tại
        currentSelectedItem = this;
        SetSelected(true);

        // Mở popup edit
        OpenEditPopup();
    }

    /// <summary>
    /// Mở ImageEditPopup
    /// </summary>
    private void OpenEditPopup()
    {
        if (ImageEditPopup.Instance != null)
        {
            ImageEditPopup.Instance.Show(imageData, this);
        }
        else
        {
            Debug.LogError("[ImageItem] ImageEditPopup.Instance is null! Make sure ImageEditPopup exists in scene.");
        }
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Lấy item đang được chọn
    /// </summary>
    public static ImageItem GetCurrentSelectedItem()
    {
        return currentSelectedItem;
    }

    /// <summary>
    /// Clear selection
    /// </summary>
    public static void ClearSelection()
    {
        if (currentSelectedItem != null)
        {
            currentSelectedItem.SetSelected(false);
            currentSelectedItem = null;
        }
    }

    #endregion
}
