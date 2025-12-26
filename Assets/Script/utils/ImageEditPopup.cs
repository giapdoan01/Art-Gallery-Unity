using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// ImageEditPopup - Popup để tạo mới hoặc chỉnh sửa thông tin ảnh
/// Sử dụng APIManager để create/update image
/// Sử dụng ArtManager để refresh cache sau khi update
/// </summary>
public class ImageEditPopup : MonoBehaviour
{
    private static ImageEditPopup _instance;
    public static ImageEditPopup Instance => _instance;

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField frameInput;
    [SerializeField] private TMP_InputField imageFileInput;
    [SerializeField] private TMP_InputField authorInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private TMP_Dropdown imageTypeDropdown; 
    [SerializeField] private Button browseButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button deleteButton; 
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Image previewImage; 

    [Header("Image Settings")]
    [SerializeField] private int maxImageWidth = 2048;
    [SerializeField] private int maxImageHeight = 2048;
    [SerializeField] private int jpgQuality = 75;

    [Header("Delete Confirmation")]
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Button confirmDeleteButton;
    [SerializeField] private Button cancelDeleteButton;
    [SerializeField] private TextMeshProUGUI confirmationText;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Data storage
    private ImageData currentImageData;
    private string selectedImagePath;
    private Texture2D selectedImageTexture;
    private ImageItem currentSelectedImageItem;
    private bool isNewFrame = false;
    
    // Thêm biến để lưu reference đến ArtFrame
    private ArtFrame targetArtFrame;

    // Player controller management
    private PlayerController[] playerControllers;
    private System.Action onHideCallback;

    // WebGL file data structure
    [System.Serializable]
    public class WebGLFileData
    {
        public string fileName;
        public int fileSize;
        public string base64Data;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void OpenFilePicker();
#endif

    #region Unity Lifecycle

    private void Awake()
    {
        // Singleton pattern
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        gameObject.name = "ImageEditPopup";

        // Setup buttons
        SetupButtons();

        // Setup input fields
        SetupInputFields();

        // Setup dropdown
        SetupImageTypeDropdown();

        // Setup confirmation panel
        SetupConfirmationPanel();

        // Hide initially
        Hide();

        if (showDebug)
            Debug.Log("[ImageEditPopup] Initialized");
    }

    private void OnDestroy()
    {
        // Cleanup
        RemoveButtonListeners();
        CleanupTextures();
        EnablePlayerControllers();
        UnselectAllImageItems();
    }

    #endregion

    #region Setup

    private void SetupButtons()
    {
        if (browseButton != null)
            browseButton.onClick.AddListener(OnBrowseClicked);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        if (deleteButton != null)
            deleteButton.onClick.AddListener(OnDeleteClicked);
    }

    private void RemoveButtonListeners()
    {
        if (browseButton != null)
            browseButton.onClick.RemoveListener(OnBrowseClicked);

        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);

        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(OnDeleteClicked);
    }

    private void SetupInputFields()
    {
        // Frame input is read-only
        if (frameInput != null)
            frameInput.interactable = false;

        // Image file input is read-only
        if (imageFileInput != null)
            imageFileInput.interactable = false;
    }

    /// <summary>
    /// Setup dropdown với 2 options: "ngang" và "dọc"
    /// </summary>
    private void SetupImageTypeDropdown()
    {
        if (imageTypeDropdown == null)
        {
            Debug.LogWarning("[ImageEditPopup] Image Type Dropdown is not assigned!");
            return;
        }

        imageTypeDropdown.ClearOptions();

        List<string> options = new List<string>
        {
            "ngang",  // Landscape
            "dọc"     // Portrait
        };

        imageTypeDropdown.AddOptions(options);
        imageTypeDropdown.value = 0; // Default: ngang

        if (showDebug)
            Debug.Log("[ImageEditPopup] Image type dropdown setup complete");
    }
    
    /// <summary>
    /// Setup confirmation panel
    /// </summary>
    private void SetupConfirmationPanel()
    {
        if (confirmationPanel == null)
            return;
            
        // Hide initially
        confirmationPanel.SetActive(false);
        
        // Setup buttons
        if (confirmDeleteButton != null)
            confirmDeleteButton.onClick.AddListener(OnConfirmDeleteClicked);
            
        if (cancelDeleteButton != null)
            cancelDeleteButton.onClick.AddListener(OnCancelDeleteClicked);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Hiển thị popup với ImageData và ArtFrame (hoặc ImageItem)
    /// </summary>
    public void Show(ImageData imageData, ArtFrame artFrame = null, ImageItem sourceImageItem = null)
    {
        if (imageData == null)
        {
            Debug.LogError("[ImageEditPopup] Cannot show: ImageData is null");
            return;
        }

        currentImageData = imageData;
        currentSelectedImageItem = sourceImageItem;
        targetArtFrame = artFrame; // Lưu lại reference đến ArtFrame
        
        selectedImagePath = null;
        CleanupTextures();

        // Determine if this is a new frame
        isNewFrame = string.IsNullOrEmpty(imageData.url);

        // Populate UI
        PopulateUI();

        // Show popup
        if (popupPanel != null)
            popupPanel.SetActive(true);

        // Disable player controllers
        DisablePlayerControllers();

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Showing for frame {imageData.frameUse} (New: {isNewFrame}, ArtFrame: {(artFrame != null ? "Yes" : "No")})");
    }
    
    /// <summary>
    /// Phương thức Show tương thích với code cũ
    /// </summary>
    public void Show(ImageData imageData, ImageItem sourceImageItem = null)
    {
        Show(imageData, null, sourceImageItem);
    }

    /// <summary>
    /// Ẩn popup
    /// </summary>
    public void Hide()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);
            
        // Ẩn confirmation panel nếu đang hiển thị
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);

        // Cleanup
        CleanupTextures();
        selectedImagePath = null;
        targetArtFrame = null;

        // Enable player controllers
        EnablePlayerControllers();

        // Unselect image items
        UnselectAllImageItems();

        // Call callback
        onHideCallback?.Invoke();
        onHideCallback = null;

        if (showDebug)
            Debug.Log("[ImageEditPopup] Hidden");
    }

    /// <summary>
    /// Đăng ký callback khi popup đóng
    /// </summary>
    public void RegisterOnHideCallback(System.Action callback)
    {
        onHideCallback = callback;
    }

    #endregion

    #region UI Population

    private void PopulateUI()
    {
        // Name
        if (nameInput != null)
            nameInput.text = currentImageData.name ?? "";

        // Frame ID (read-only)
        if (frameInput != null)
            frameInput.text = currentImageData.frameUse.ToString();

        // Image file
        if (imageFileInput != null)
        {
            if (isNewFrame)
            {
                imageFileInput.text = "Select an image file";
            }
            else
            {
                string fileName = GetFileNameFromUrl(currentImageData.url);
                imageFileInput.text = string.IsNullOrEmpty(fileName) ? "Unknown file" : fileName;
            }
        }

        // Author
        if (authorInput != null)
            authorInput.text = currentImageData.author ?? "";

        // Description
        if (descriptionInput != null)
            descriptionInput.text = currentImageData.description ?? "";

        if (showDebug)
        {
            Debug.Log($"[ImageEditPopup] PopulateUI - ImageData.imageType: '{currentImageData.imageType}'");
            Debug.Log($"[ImageEditPopup] PopulateUI - Is null or empty: {string.IsNullOrEmpty(currentImageData.imageType)}");
        }

        SetImageTypeDropdown(currentImageData.imageType);

        // Delete button visibility
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(!isNewFrame);

        // Status
        UpdateStatus(isNewFrame ? "Creating new frame" : "Ready to edit");

        // Clear preview
        if (previewImage != null)
            previewImage.sprite = null;
    }

    /// <summary>
    /// Set giá trị dropdown từ imageType
    /// </summary>
    private void SetImageTypeDropdown(string imageType)
    {
        if (imageTypeDropdown == null)
        {
            Debug.LogWarning("[ImageEditPopup] Image Type Dropdown is null!");
            return;
        }

        if (string.IsNullOrEmpty(imageType))
        {
            imageTypeDropdown.value = 0; // Default: ngang

            if (showDebug)
                Debug.Log("[ImageEditPopup] ImageType is empty, set to default: ngang");

            return;
        }

        // Normalize và trim
        string normalizedType = imageType.Trim().ToLower();

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Setting dropdown for imageType: '{imageType}' (normalized: '{normalizedType}')");

        // So sánh với nhiều biến thể
        if (normalizedType == "landscape" ||
            normalizedType == "ngang" ||
            normalizedType.Contains("ngang") ||
            normalizedType.Contains("landscape"))
        {
            imageTypeDropdown.value = 0; // ngang

            if (showDebug)
                Debug.Log($"[ImageEditPopup] Set dropdown to: ngang (index 0)");
        }
        else if (normalizedType == "portrait" ||
                 normalizedType == "dọc" ||
                 normalizedType == "doc" || // Fallback không dấu
                 normalizedType.Contains("dọc") ||
                 normalizedType.Contains("doc") ||
                 normalizedType.Contains("portrait"))
        {
            imageTypeDropdown.value = 1; // dọc

            if (showDebug)
                Debug.Log($"[ImageEditPopup] Set dropdown to: dọc (index 1)");
        }
        else
        {
            // Unknown type - default to ngang
            imageTypeDropdown.value = 0;

            if (showDebug)
                Debug.LogWarning($"[ImageEditPopup] Unknown imageType: '{imageType}', defaulting to ngang");
        }

        // Verify selection
        if (showDebug)
        {
            string selectedText = imageTypeDropdown.options[imageTypeDropdown.value].text;
            Debug.Log($"[ImageEditPopup] Dropdown now shows: '{selectedText}' (index: {imageTypeDropdown.value})");
        }
    }

    /// <summary>
    /// Lấy giá trị imageType từ dropdown
    /// </summary>
    private string GetSelectedImageType()
    {
        if (imageTypeDropdown == null)
            return "ngang"; // Default

        return imageTypeDropdown.options[imageTypeDropdown.value].text;
    }

    private string GetFileNameFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;

        try
        {
            string[] parts = url.Split('/');
            string fileName = parts[parts.Length - 1];

            if (fileName.Contains("?"))
                fileName = fileName.Split('?')[0];

            fileName = Uri.UnescapeDataString(fileName);
            return fileName;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageEditPopup] Error extracting filename: {ex.Message}");
            return Path.GetFileName(url);
        }
    }

    #endregion

    #region Button Handlers

    private void OnBrowseClicked()
    {
        if (showDebug)
            Debug.Log("[ImageEditPopup] Browse clicked");

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            OpenFilePicker();
            UpdateStatus("Opening file picker...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageEditPopup] Error opening file picker: {ex.Message}");
            UpdateStatus("Error: Could not open file picker");
        }
#elif UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            LoadImageFromPath(path);
        }
        else
        {
            UpdateStatus("No file selected");
        }
#else
        UpdateStatus("File browser not available on this platform");
#endif
    }

    private void OnSaveClicked()
    {
        if (currentImageData == null)
        {
            UpdateStatus("Error: No image data");
            return;
        }

        // Validate input
        string newName = nameInput != null ? nameInput.text.Trim() : currentImageData.name;
        if (string.IsNullOrWhiteSpace(newName))
        {
            UpdateStatus("Error: Name cannot be empty");
            return;
        }

        // For new frames, image is required
        if (isNewFrame && selectedImageTexture == null)
        {
            UpdateStatus("Error: You must select an image for new frame");
            return;
        }

        // Get transform from ArtFrame
        Vector3 position = Vector3.zero;
        Vector3 rotation = Vector3.zero;
        
        // Ưu tiên lấy từ targetArtFrame
        if (targetArtFrame != null)
        {
            position = targetArtFrame.transform.position;
            rotation = targetArtFrame.transform.eulerAngles;

            if (showDebug)
                Debug.Log($"[ImageEditPopup] Using transform from targetArtFrame: Pos={position}, Rot={rotation}");
        }
        // Nếu không có targetArtFrame, tìm ArtFrame với frameId tương ứng
        else 
        {
            ArtFrame frameInScene = FindArtFrameByFrameId(currentImageData.frameUse);
            if (frameInScene != null)
            {
                position = frameInScene.transform.position;
                rotation = frameInScene.transform.eulerAngles;

                if (showDebug)
                    Debug.Log($"[ImageEditPopup] Using transform from found frame: Pos={position}, Rot={rotation}");
            }
            else
            {
                Debug.LogWarning($"[ImageEditPopup] Frame {currentImageData.frameUse} not found, using default transform");
            }
        }

        // Prepare data
        string author = authorInput != null ? authorInput.text.Trim() : "";
        string description = descriptionInput != null ? descriptionInput.text.Trim() : "";
        string imageType = GetSelectedImageType();

        // Create ImageData object
        ImageData dataToSend = new ImageData
        {
            frameUse = currentImageData.frameUse,
            name = newName,
            author = author,
            description = description,
            imageType = imageType,
            position = new Position { x = position.x, y = position.y, z = position.z },
            rotation = new Rotation { x = rotation.x, y = rotation.y, z = rotation.z }
        };

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Image type selected: {imageType}");

        UpdateStatus("Saving...");

        // Encode image to bytes if selected
        byte[] imageBytes = null;
        if (selectedImageTexture != null)
        {
            imageBytes = selectedImageTexture.EncodeToJPG(jpgQuality);
        }

        // Call appropriate API
        if (isNewFrame)
        {
            if (showDebug)
                Debug.Log($"[ImageEditPopup] Creating new image for frame {currentImageData.frameUse}");

            APIManager.Instance.CreateImage(dataToSend, imageBytes, OnSaveComplete);
        }
        else
        {
            if (showDebug)
                Debug.Log($"[ImageEditPopup] Updating image for frame {currentImageData.frameUse}");

            APIManager.Instance.UpdateImage(dataToSend, imageBytes, OnSaveComplete);
        }
    }

    private void OnCancelClicked()
    {
        if (showDebug)
            Debug.Log("[ImageEditPopup] Cancel clicked");

        // If new frame, notify ArtFrameCreator to clean up
        if (isNewFrame && ArtFrameCreator.Instance != null)
        {
            ArtFrameCreator.Instance.ClearLastCreatedFrame();
        }

        Hide();
    }

    private void OnDeleteClicked()
    {
        if (currentImageData == null || isNewFrame)
        {
            UpdateStatus("Error: Cannot delete new frame");
            return;
        }

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Delete clicked for frame {currentImageData.frameUse}");

        // Hiển thị hộp thoại xác nhận
        ShowDeleteConfirmation();
    }
    
    /// <summary>
    /// Hiển thị hộp thoại xác nhận xóa
    /// </summary>
    private void ShowDeleteConfirmation()
    {
        if (confirmationPanel == null)
        {
            // Không có hộp thoại xác nhận, xóa luôn
            PerformDelete();
            return;
        }
        
        // Thiết lập nội dung xác nhận
        if (confirmationText != null)
        {
            confirmationText.text = $"Bạn có chắc chắn muốn xóa ảnh này (Frame ID: {currentImageData.frameUse})?\n" +
                                    "Thao tác này không thể hoàn tác.";
        }
        
        // Hiển thị panel xác nhận
        confirmationPanel.SetActive(true);
    }
    
    /// <summary>
    /// Handler cho nút xác nhận xóa
    /// </summary>
    private void OnConfirmDeleteClicked()
    {
        // Ẩn panel xác nhận
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
            
        // Thực hiện xóa
        PerformDelete();
    }
    
    /// <summary>
    /// Handler cho nút hủy xóa
    /// </summary>
    private void OnCancelDeleteClicked()
    {
        // Chỉ ẩn panel xác nhận
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
            
        UpdateStatus("Delete canceled");
    }
    
    /// <summary>
    /// Thực hiện xóa ảnh
    /// </summary>
    private void PerformDelete()
    {
        UpdateStatus("Deleting...");
        
        if (showDebug)
            Debug.Log($"[ImageEditPopup] Deleting frame {currentImageData.frameUse}");
            
        APIManager.Instance.DeleteImage(currentImageData.frameUse, OnDeleteComplete);
    }

    #endregion

    #region WebGL File Handling

    /// <summary>
    /// Callback từ JavaScript khi file được chọn
    /// </summary>
    public void OnWebGLFileSelected(string jsonData)
    {
        if (showDebug)
            Debug.Log("[ImageEditPopup] OnWebGLFileSelected called");

        if (string.IsNullOrEmpty(jsonData))
        {
            UpdateStatus("No file selected");
            return;
        }

        try
        {
            // Parse JSON
            WebGLFileData fileData = JsonUtility.FromJson<WebGLFileData>(jsonData);

            if (fileData == null || string.IsNullOrEmpty(fileData.base64Data))
            {
                UpdateStatus("Invalid file data");
                return;
            }

            // Extract base64 string
            int commaIndex = fileData.base64Data.IndexOf(',');
            if (commaIndex < 0)
            {
                UpdateStatus("Invalid file format");
                return;
            }

            string base64String = fileData.base64Data.Substring(commaIndex + 1);
            byte[] fileBytes = Convert.FromBase64String(base64String);

            // Load and process image
            ProcessImageBytes(fileBytes, fileData.fileName, fileData.fileSize);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageEditPopup] Error processing file: {ex.Message}");
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    #endregion

    #region Image Processing

    private void LoadImageFromPath(string path)
    {
        if (!File.Exists(path))
        {
            UpdateStatus("Error: File not found");
            return;
        }

        try
        {
            byte[] fileBytes = File.ReadAllBytes(path);
            string fileName = Path.GetFileName(path);
            int fileSize = fileBytes.Length;

            ProcessImageBytes(fileBytes, fileName, fileSize);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageEditPopup] Error loading image: {ex.Message}");
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    private void ProcessImageBytes(byte[] fileBytes, string fileName, int originalSize)
    {
        if (showDebug)
            Debug.Log($"[ImageEditPopup] Processing: {fileName} ({originalSize / 1024}KB)");

        // Load original texture
        Texture2D originalTexture = new Texture2D(2, 2);
        if (!originalTexture.LoadImage(fileBytes))
        {
            UpdateStatus("Error: Invalid image format");
            Destroy(originalTexture);
            return;
        }

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Original: {originalTexture.width}x{originalTexture.height}");

        // Resize if too large
        Texture2D resizedTexture = ResizeTexture(originalTexture, maxImageWidth, maxImageHeight);
        Destroy(originalTexture);

        // Compress
        byte[] compressedBytes = resizedTexture.EncodeToJPG(jpgQuality);

        if (compressedBytes == null || compressedBytes.Length == 0)
        {
            UpdateStatus("Error: Failed to compress image");
            Destroy(resizedTexture);
            return;
        }

        // Load final texture
        Texture2D finalTexture = new Texture2D(2, 2);
        if (finalTexture.LoadImage(compressedBytes))
        {
            // Cleanup old texture
            CleanupTextures();

            // Store new texture
            selectedImageTexture = finalTexture;
            selectedImagePath = fileName;

            // Update UI
            if (imageFileInput != null)
                imageFileInput.text = fileName;

            // Update preview
            if (previewImage != null)
            {
                Sprite sprite = Sprite.Create(
                    finalTexture,
                    new Rect(0, 0, finalTexture.width, finalTexture.height),
                    new Vector2(0.5f, 0.5f)
                );
                previewImage.sprite = sprite;
            }

            // Update status
            int finalSize = compressedBytes.Length;
            float ratio = (float)finalSize / originalSize * 100;
            UpdateStatus($"✓ {fileName} ({finalSize / 1024}KB, {finalTexture.width}x{finalTexture.height}) - {ratio:F0}%");

            if (showDebug)
                Debug.Log($"[ImageEditPopup] Compressed: {originalSize / 1024}KB → {finalSize / 1024}KB");
        }
        else
        {
            UpdateStatus("Error: Failed to process image");
            Destroy(finalTexture);
        }

        Destroy(resizedTexture);
    }

    private Texture2D ResizeTexture(Texture2D source, int maxWidth, int maxHeight)
    {
        int width = source.width;
        int height = source.height;

        // Check if resize is needed
        if (width <= maxWidth && height <= maxHeight)
            return source;

        // Calculate new dimensions
        float ratio = Mathf.Min((float)maxWidth / width, (float)maxHeight / height);
        int newWidth = Mathf.RoundToInt(width * ratio);
        int newHeight = Mathf.RoundToInt(height * ratio);

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Resizing: {width}x{height} → {newWidth}x{newHeight}");

        // Create render texture
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        rt.filterMode = FilterMode.Bilinear;

        // Render to texture
        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        // Read pixels
        Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();

        // Cleanup
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    private void CleanupTextures()
    {
        if (selectedImageTexture != null)
        {
            Destroy(selectedImageTexture);
            selectedImageTexture = null;
        }

        if (previewImage != null && previewImage.sprite != null)
        {
            Destroy(previewImage.sprite);
            previewImage.sprite = null;
        }
    }

    #endregion

    #region API Callbacks

    private void OnSaveComplete(bool success, string message)
    {
        if (success)
        {
            UpdateStatus("✓ Saved successfully!");

            if (showDebug)
                Debug.Log("[ImageEditPopup] Save successful");

            // Refresh cache in ArtManager
            if (ArtManager.Instance != null)
            {
                ArtManager.Instance.RefreshFrame(currentImageData.frameUse, (sprite, data) =>
                {
                    if (showDebug)
                        Debug.Log($"[ImageEditPopup] Frame {currentImageData.frameUse} refreshed");
                });
            }

            // Refresh gallery
            var gallery = FindObjectOfType<ImageGalleryContainer>();
            if (gallery != null)
            {
                gallery.RefreshGallery();
            }

            // Refresh all ArtFrames with this ID
            RefreshAllArtFrames(currentImageData.frameUse);

            // Notify ArtFrameCreator if new frame
            if (isNewFrame && ArtFrameCreator.Instance != null)
            {
                ArtFrameCreator.Instance.OnFrameSaved(currentImageData.frameUse);
            }

            // Unselect image item
            if (currentSelectedImageItem != null)
            {
                currentSelectedImageItem.SetSelected(false);
            }

            // Close popup after delay
            Invoke(nameof(Hide), 1f);
        }
        else
        {
            UpdateStatus($"✗ Error: {message}");
            Debug.LogError($"[ImageEditPopup] Save failed: {message}");

            // If new frame failed, clean up
            if (isNewFrame && ArtFrameCreator.Instance != null)
            {
                ArtFrameCreator.Instance.ClearLastCreatedFrame();
            }
        }
    }

    private void OnDeleteComplete(bool success, string message)
    {
        if (success)
        {
            UpdateStatus("✓ Deleted successfully!");

            if (showDebug)
                Debug.Log("[ImageEditPopup] Delete successful");

            // Clear cache
            if (ArtManager.Instance != null)
            {
                ArtManager.Instance.ClearFrameCache(currentImageData.frameUse);
            }

            // Refresh gallery
            var gallery = FindObjectOfType<ImageGalleryContainer>();
            if (gallery != null)
            {
                gallery.RefreshGallery();
            }

            // Nếu có reference đến ArtFrame cụ thể, xóa GameObject đó
            if (targetArtFrame != null)
            {
                if (showDebug)
                    Debug.Log($"[ImageEditPopup] Destroying ArtFrame GameObject: {targetArtFrame.gameObject.name}");
                
                Destroy(targetArtFrame.gameObject);
            }
            else
            {
                // Nếu không có reference cụ thể, xóa tất cả ArtFrame có frameId này
                DestroyAllArtFrames(currentImageData.frameUse);
            }

            // Close popup after delay
            Invoke(nameof(Hide), 1f);
        }
        else
        {
            UpdateStatus($"✗ Error: {message}");
            Debug.LogError($"[ImageEditPopup] Delete failed: {message}");
        }
    }

    #endregion

    #region ArtFrame Management

    private ArtFrame FindArtFrameByFrameId(int frameId)
    {
        ArtFrame[] allFrames = FindObjectsOfType<ArtFrame>();
        foreach (var frame in allFrames)
        {
            if (frame != null && frame.FrameId == frameId)
                return frame;
        }
        return null;
    }

    private void RefreshAllArtFrames(int frameId)
    {
        ArtFrame[] allFrames = FindObjectsOfType<ArtFrame>();
        int count = 0;

        foreach (ArtFrame frame in allFrames)
        {
            if (frame != null && frame.FrameId == frameId)
            {
                frame.ReloadArtwork(true);
                count++;
            }
        }

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Refreshed {count} ArtFrame(s) with ID {frameId}");
    }

    /// <summary>
    /// Xóa nội dung của tất cả ArtFrame có ID tương ứng
    /// </summary>
    private void ClearAllArtFrames(int frameId)
    {
        ArtFrame[] allFrames = FindObjectsOfType<ArtFrame>();
        int count = 0;

        foreach (ArtFrame frame in allFrames)
        {
            if (frame != null && frame.FrameId == frameId)
            {
                frame.ClearArtwork();
                count++;
            }
        }

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Cleared {count} ArtFrame(s) with ID {frameId}");
    }
    
    /// <summary>
    /// Xóa tất cả GameObject ArtFrame có ID tương ứng
    /// </summary>
    private void DestroyAllArtFrames(int frameId)
    {
        ArtFrame[] allFrames = FindObjectsOfType<ArtFrame>();
        int count = 0;

        foreach (ArtFrame frame in allFrames)
        {
            if (frame != null && frame.FrameId == frameId)
            {
                if (showDebug)
                    Debug.Log($"[ImageEditPopup] Destroying ArtFrame: {frame.gameObject.name}");
                
                Destroy(frame.gameObject);
                count++;
            }
        }

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Destroyed {count} ArtFrame(s) with ID {frameId}");
    }

    #endregion

    #region Player Controller Management

    private void DisablePlayerControllers()
    {
        playerControllers = FindObjectsOfType<PlayerController>();

        foreach (var controller in playerControllers)
        {
            if (controller != null && controller.enabled)
            {
                controller.enabled = false;

                if (showDebug)
                    Debug.Log($"[ImageEditPopup] Disabled PlayerController: {controller.gameObject.name}");
            }
        }
    }

    private void EnablePlayerControllers()
    {
        if (playerControllers == null)
            return;

        foreach (var controller in playerControllers)
        {
            if (controller != null)
            {
                controller.enabled = true;

                if (showDebug)
                    Debug.Log($"[ImageEditPopup] Enabled PlayerController: {controller.gameObject.name}");
            }
        }
    }

    #endregion

    #region Image Item Management

    private void UnselectAllImageItems()
    {
        if (currentSelectedImageItem != null)
        {
            currentSelectedImageItem.SetSelected(false);
            currentSelectedImageItem = null;
        }

        ImageItem[] allImageItems = FindObjectsOfType<ImageItem>();
        foreach (var item in allImageItems)
        {
            if (item != null && item.IsSelected())
            {
                item.SetSelected(false);
            }
        }
    }

    #endregion

    #region UI Updates

    private void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;

        if (showDebug)
            Debug.Log($"[ImageEditPopup] Status: {message}");
    }

    #endregion
}