using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ImageEditPopup : MonoBehaviour
{
    private static ImageEditPopup _instance;
    public static ImageEditPopup Instance => _instance;

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField frameInput; // Read-only
    [SerializeField] private TMP_InputField imageFileInput; // Hiển thị tên file
    [SerializeField] private Button browseButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private ImageData currentImageData;
    private string selectedImagePath;
    private PlayerController[] playerControllers;
    private System.Action onHideCallback;
    private ImageItem currentSelectedImageItem; 

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // Setup buttons
        if (browseButton != null)
        {
            browseButton.onClick.AddListener(OnBrowseClicked);
        }

        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Hide);
        }

        // Disable frame input (read-only)
        if (frameInput != null)
        {
            frameInput.interactable = false;
        }

        // Disable image file input (read-only, chỉ hiển thị)
        if (imageFileInput != null)
        {
            imageFileInput.interactable = false;
        }

        Hide();
    }

    // Phương thức trích xuất tên file từ URL
    private string GetFileNameFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url))
            return string.Empty;
        
        try
        {
            // Tách URL theo dấu "/" và lấy phần cuối cùng
            string[] parts = url.Split('/');
            string fileName = parts[parts.Length - 1];
            
            // Xử lý thêm nếu có tham số query trong URL
            if (fileName.Contains("?"))
            {
                fileName = fileName.Split('?')[0];
            }
            
            // Giải mã URL nếu cần
            fileName = Uri.UnescapeDataString(fileName);
            
            return fileName;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageEditPopup] Error extracting file name from URL: {ex.Message}");
            return Path.GetFileName(url); // Fallback, dùng Path.GetFileName
        }
    }

    public void Show(ImageData imageData, ImageItem sourceImageItem = null)
    {
        currentImageData = imageData;
        selectedImagePath = null;
        
        // Lưu lại ImageItem đang được chọn
        currentSelectedImageItem = sourceImageItem;

        // Set name
        if (nameInput != null)
        {
            nameInput.text = imageData.name;
        }

        // Set frame (read-only)
        if (frameInput != null)
        {
            frameInput.text = imageData.frameUse.ToString();
        }

        // Hiển thị tên file ảnh từ URL
        if (imageFileInput != null)
        {
            string fileName = GetFileNameFromUrl(imageData.url);
            imageFileInput.text = string.IsNullOrEmpty(fileName) ? "Unknown file" : fileName;
        }

        // Reset status
        UpdateStatus("Ready");

        // Show popup
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        // Disable all PlayerControllers to prevent movement while popup is open
        DisablePlayerControllers();

        if (showDebug) Debug.Log($"[ImageEditPopup] Showing popup for: {imageData.name}");
    }

    public void RegisterOnHideCallback(System.Action callback)
    {
        onHideCallback = callback;
    }

    public void Hide()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        selectedImagePath = null;
        
        // Enable lại PlayerControllers khi đóng popup
        EnablePlayerControllers();
        
        // Bỏ chọn tất cả các ImageItem
        UnselectAllImageItems();
        
        // Gọi callback nếu có
        if (onHideCallback != null)
        {
            onHideCallback.Invoke();
            onHideCallback = null; // Reset callback sau khi gọi
        }
    }

    // Phương thức mới để bỏ chọn tất cả ImageItem
    private void UnselectAllImageItems()
    {
        // Cách 1: Nếu lưu tham chiếu đến ImageItem đang được chọn
        if (currentSelectedImageItem != null)
        {
            currentSelectedImageItem.SetSelected(false);
            currentSelectedImageItem = null;
        }
        
        // Cách 2: Tìm tất cả ImageItem trong scene và bỏ chọn
        // Phòng trường hợp có nhiều ImageItem được chọn do lỗi nào đó
        ImageItem[] allImageItems = FindObjectsOfType<ImageItem>();
        foreach (var item in allImageItems)
        {
            if (item != null && item.IsSelected())
            {
                item.SetSelected(false);
            }
        }
    }

    private void DisablePlayerControllers()
    {
        // Tìm tất cả PlayerController trong scene và disable chúng
        playerControllers = FindObjectsOfType<PlayerController>();
        foreach (var controller in playerControllers)
        {
            if (controller != null && controller.enabled)
            {
                controller.enabled = false;
                if (showDebug) Debug.Log("[ImageEditPopup] Disabled PlayerController: " + controller.gameObject.name);
            }
        }
    }

    private void EnablePlayerControllers()
    {
        // Enable lại tất cả PlayerController đã lưu
        if (playerControllers != null)
        {
            foreach (var controller in playerControllers)
            {
                if (controller != null)
                {
                    controller.enabled = true;
                    if (showDebug) Debug.Log("[ImageEditPopup] Enabled PlayerController: " + controller.gameObject.name);
                }
            }
        }
    }

    private void OnBrowseClicked()
    {
        if (showDebug) Debug.Log("[ImageEditPopup] Browse clicked");

        string path = OpenFilePicker();

        if (!string.IsNullOrEmpty(path))
        {
            selectedImagePath = path;

            // Hiển thị tên file
            if (imageFileInput != null)
            {
                imageFileInput.text = Path.GetFileName(path);
            }

            UpdateStatus($"Selected: {Path.GetFileName(path)}");

            if (showDebug) Debug.Log($"[ImageEditPopup] Selected file: {path}");
        }
    }

    private string OpenFilePicker()
    {
#if UNITY_EDITOR
        // Unity Editor: Dùng EditorUtility
        string path = EditorUtility.OpenFilePanel(
            "Select Image",
            "",
            "png,jpg,jpeg"
        );

        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }
#elif UNITY_STANDALONE_WIN
        // Windows Standalone: Dùng SimpleFileBrowser (see below)
        // Hoặc dùng native plugin
        Debug.LogWarning("[ImageEditPopup] File browser not implemented for standalone build");
        UpdateStatus("File browser not available in standalone build");
        
        // WORKAROUND: Bạn có thể hardcode path để test
        // return "C:/Users/YourName/Pictures/test.png";
#else
        Debug.LogWarning("[ImageEditPopup] File picker not supported on this platform");
        UpdateStatus("File picker not supported");
#endif
        return null;
    }

    private void OnSaveClicked()
    {
        if (currentImageData == null)
        {
            UpdateStatus("Error: No image data");
            return;
        }

        string newName = nameInput != null ? nameInput.text.Trim() : currentImageData.name;

        if (string.IsNullOrWhiteSpace(newName))
        {
            UpdateStatus("Error: Name cannot be empty");
            return;
        }

        UpdateStatus("Saving...");

        if (showDebug) Debug.Log($"[ImageEditPopup] Saving - Name: {newName}, Frame: {currentImageData.frameUse}");

        // Nếu có chọn ảnh mới
        if (!string.IsNullOrEmpty(selectedImagePath))
        {
            if (showDebug) Debug.Log($"[ImageEditPopup] Updating with new image: {selectedImagePath}");

            APIManager.Instance.UpdateImageByFrameFromPath(
                currentImageData.frameUse,
                newName,
                selectedImagePath,
                OnUpdateComplete
            );
        }
        else
        {
            // Chỉ update tên
            if (showDebug) Debug.Log("[ImageEditPopup] Updating name only");

            APIManager.Instance.UpdateImageByFrame(
                currentImageData.frameUse,
                newName,
                null,
                OnUpdateComplete
            );
        }
    }

    private void OnUpdateComplete(bool success, ImageData updatedData, string error)
    {
        if (success)
        {
            UpdateStatus("✅ Saved!");

            if (showDebug) Debug.Log("[ImageEditPopup] Update successful");

            // Refresh gallery
            var gallery = FindFirstObjectByType<ImageGalleryContainer>();
            if (gallery != null)
            {
                gallery.RefreshGallery();
            }
            
            // Refresh tất cả các ArtFrame hiển thị ảnh này
            if (updatedData != null)
            {
                // 1. Làm mới cache trong ArtManager
                if (ArtManager.Instance != null)
                {
                    if (showDebug) Debug.Log($"[ImageEditPopup] Force refresh frame {updatedData.frameUse} trong ArtManager");
                    ArtManager.Instance.ForceRefreshFrame(updatedData.frameUse);
                }
                
                // 2. Cập nhật tất cả ArtFrame trong scene sử dụng frameId này
                RefreshAllArtFrames(updatedData.frameUse);
            }

            // Close popup sau 1 giây
            Invoke(nameof(Hide), 1f);
        }
        else
        {
            UpdateStatus($"❌ Error: {error}");
            Debug.LogError($"[ImageEditPopup] Update failed: {error}");
        }
    }
    
    // Phương thức mới để tìm và refresh tất cả ArtFrame sử dụng frameId
    private void RefreshAllArtFrames(int frameId)
    {
        ArtFrame[] allFrames = FindObjectsOfType<ArtFrame>();
        int count = 0;
        
        foreach (ArtFrame frame in allFrames)
        {
            if (frame != null && frame.FrameId == frameId)
            {
                if (showDebug) Debug.Log($"[ImageEditPopup] Reloading ArtFrame {frame.name} với ID {frameId}");
                frame.ReloadArtwork(true); // Force refresh
                count++;
            }
        }
        
        if (showDebug) Debug.Log($"[ImageEditPopup] Đã refresh {count} ArtFrame với ID {frameId}");
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void OnDestroy()
    {
        if (browseButton != null)
        {
            browseButton.onClick.RemoveListener(OnBrowseClicked);
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnSaveClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(Hide);
        }
        
        // Ensure player controllers are enabled when destroying this object
        EnablePlayerControllers();
        
        // Make sure to unselect items when destroyed
        UnselectAllImageItems();
    }
}