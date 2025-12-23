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
    [SerializeField] private Button browseButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private ImageData currentImageData;
    private string selectedImagePath;
    private Texture2D selectedImageTexture;
    private PlayerController[] playerControllers;
    private System.Action onHideCallback;
    private ImageItem currentSelectedImageItem;

    // ✅ Class để parse JSON từ JavaScript
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

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        gameObject.name = "ImageEditPopup";

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

        if (frameInput != null)
        {
            frameInput.interactable = false;
        }

        if (imageFileInput != null)
        {
            imageFileInput.interactable = false;
        }

        Hide();
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
            {
                fileName = fileName.Split('?')[0];
            }

            fileName = Uri.UnescapeDataString(fileName);
            return fileName;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageEditPopup] Error extracting file name from URL: {ex.Message}");
            return Path.GetFileName(url);
        }
    }

    public void Show(ImageData imageData, ImageItem sourceImageItem = null)
    {
        currentImageData = imageData;
        selectedImagePath = null;
        
        if (selectedImageTexture != null)
        {
            Destroy(selectedImageTexture);
            selectedImageTexture = null;
        }

        currentSelectedImageItem = sourceImageItem;

        if (nameInput != null)
        {
            nameInput.text = imageData.name;
        }

        if (frameInput != null)
        {
            frameInput.text = imageData.frameUse.ToString();
        }

        if (imageFileInput != null)
        {
            string fileName = GetFileNameFromUrl(imageData.url);
            imageFileInput.text = string.IsNullOrEmpty(fileName) ? "Unknown file" : fileName;
        }

        if (authorInput != null)
        {
            authorInput.text = imageData.author ?? "";
        }

        if (descriptionInput != null)
        {
            descriptionInput.text = imageData.description ?? "";
        }

        UpdateStatus("Ready");

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

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
        
        if (selectedImageTexture != null)
        {
            Destroy(selectedImageTexture);
            selectedImageTexture = null;
        }

        EnablePlayerControllers();
        UnselectAllImageItems();

        if (onHideCallback != null)
        {
            onHideCallback.Invoke();
            onHideCallback = null;
        }
    }

    private void UnselectAllImageItems()
    {
        if (currentSelectedImageItem != null)
        {
            currentSelectedImageItem.SetSelected(false);
            currentSelectedImageItem = null;
        }

        ImageItem[] allImageItems = FindObjectsByType<ImageItem>(FindObjectsSortMode.None);
        foreach (var item in allImageItems)
        {
            if (item != null && item.IsSelected())
            {
                item.SetSelected(false);
            }
        }
    }

    private ArtFrame FindArtFrameByFrameId(int frameId)
    {
        ArtFrame[] allFrames = FindObjectsByType<ArtFrame>(FindObjectsSortMode.None);
        foreach (var f in allFrames)
        {
            if (f != null && f.FrameId == frameId)
                return f;
        }
        return null;
    }

    private void DisablePlayerControllers()
    {
        playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
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

#if UNITY_WEBGL && !UNITY_EDITOR
        try
        {
            OpenFilePicker();
            UpdateStatus("Opening file picker...");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageEditPopup] Error calling OpenFilePicker: {ex.Message}");
            UpdateStatus("Error: Could not open file picker");
        }
#elif UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            selectedImagePath = path;
            
            if (imageFileInput != null)
            {
                imageFileInput.text = Path.GetFileName(path);
            }

            UpdateStatus($"Selected: {Path.GetFileName(path)}");
            if (showDebug) Debug.Log($"[ImageEditPopup] Selected file: {path}");
        }
        else
        {
            UpdateStatus("No file selected");
        }
#else
        UpdateStatus("File browser not available on this platform");
#endif
    }

    // ✅ Method nhận JSON từ JavaScript
    public void OnWebGLFileSelected(string jsonData)
    {
        if (showDebug) Debug.Log("[ImageEditPopup] OnWebGLFileSelected called");

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

            string fileName = fileData.fileName;
            int originalSize = fileData.fileSize;
            
            if (showDebug) Debug.Log($"[ImageEditPopup] Processing: {fileName} ({originalSize / 1024}KB)");

            // Extract base64 string
            int commaIndex = fileData.base64Data.IndexOf(',');
            if (commaIndex < 0)
            {
                UpdateStatus("Invalid file format");
                return;
            }

            string base64String = fileData.base64Data.Substring(commaIndex + 1);
            byte[] fileBytes = Convert.FromBase64String(base64String);

            // Load ảnh gốc
            Texture2D originalTexture = new Texture2D(2, 2);
            if (!originalTexture.LoadImage(fileBytes))
            {
                UpdateStatus("✗ Invalid image format");
                Destroy(originalTexture);
                return;
            }

            if (showDebug) Debug.Log($"[ImageEditPopup] Original: {originalTexture.width}x{originalTexture.height}");

            // Resize nếu quá lớn
            Texture2D resizedTexture = ResizeTexture(originalTexture, 2048, 2048);
            Destroy(originalTexture);

            // Nén ảnh
            byte[] compressedBytes = resizedTexture.EncodeToJPG(75);
            
            if (compressedBytes == null || compressedBytes.Length == 0)
            {
                UpdateStatus("✗ Failed to compress");
                Destroy(resizedTexture);
                return;
            }

            // Load lại
            Texture2D finalTexture = new Texture2D(2, 2);
            if (finalTexture.LoadImage(compressedBytes))
            {
                if (selectedImageTexture != null)
                {
                    Destroy(selectedImageTexture);
                }
                
                selectedImageTexture = finalTexture;
                selectedImagePath = fileName;

                if (imageFileInput != null)
                {
                    imageFileInput.text = fileName;
                }

                int finalSize = compressedBytes.Length;
                float ratio = (float)finalSize / originalSize * 100;
                
                UpdateStatus($"✓ {fileName} ({finalSize / 1024}KB, {finalTexture.width}x{finalTexture.height}) - {ratio:F0}%");
                
                if (showDebug) 
                {
                    Debug.Log($"[ImageEditPopup] Compressed: {originalSize / 1024}KB → {finalSize / 1024}KB");
                }
            }
            else
            {
                UpdateStatus("✗ Failed to process");
                Destroy(finalTexture);
            }
            
            Destroy(resizedTexture);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ImageEditPopup] Error: {ex.Message}");
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    // ✅ Method resize texture
    private Texture2D ResizeTexture(Texture2D source, int maxWidth, int maxHeight)
    {
        int width = source.width;
        int height = source.height;

        if (width <= maxWidth && height <= maxHeight)
        {
            return source;
        }

        float ratio = Mathf.Min((float)maxWidth / width, (float)maxHeight / height);
        int newWidth = Mathf.RoundToInt(width * ratio);
        int newHeight = Mathf.RoundToInt(height * ratio);

        if (showDebug) Debug.Log($"[ImageEditPopup] Resizing: {width}x{height} → {newWidth}x{newHeight}");

        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        rt.filterMode = FilterMode.Bilinear;

        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
        result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return result;
    }

    private void OnSaveClicked()
    {
        if (currentImageData == null)
        {
            UpdateStatus("Error: No image data");
            return;
        }

        string newName = nameInput != null ? nameInput.text.Trim() : currentImageData.name;
        string author = authorInput != null ? authorInput.text.Trim() : "";
        string description = descriptionInput != null ? descriptionInput.text.Trim() : "";

        if (string.IsNullOrWhiteSpace(newName))
        {
            UpdateStatus("Error: Name cannot be empty");
            return;
        }

        Vector3 position = Vector3.zero;
        Vector3 rotation = Vector3.zero;

        ArtFrame targetFrame = FindArtFrameByFrameId(currentImageData.frameUse);

        if (targetFrame != null)
        {
            position = targetFrame.transform.position;
            rotation = targetFrame.transform.eulerAngles;
        }

        UpdateStatus("Saving...");

        if (!string.IsNullOrEmpty(selectedImagePath))
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (selectedImageTexture != null)
            {
                if (showDebug) Debug.Log("[ImageEditPopup] WebGL: Updating with Texture2D");
                
                // ✅ SỬA: Thêm delegate wrapper
                APIManager.Instance.UpdateImageByFrame(
                    currentImageData.frameUse,
                    newName,
                    author,
                    description,
                    position,
                    rotation,
                    selectedImageTexture,
                    (success, data, error) => OnUpdateComplete(success, data, error)
                );
            }
            else
            {
                UpdateStatus("Error: No texture loaded");
            }
#else
            if (showDebug) Debug.Log($"[ImageEditPopup] Editor: Updating with path: {selectedImagePath}");
            
            // ✅ SỬA: Thêm delegate wrapper
            APIManager.Instance.UpdateImageByFrameFromPath(
                currentImageData.frameUse,
                newName,
                author,
                description,
                position,
                rotation,
                selectedImagePath,
                (success, data, error) => OnUpdateComplete(success, data, error)
            );
#endif
        }
        else
        {
            if (showDebug) Debug.Log("[ImageEditPopup] Updating info only");

            // ✅ SỬA: Thêm delegate wrapper
            APIManager.Instance.UpdateImageByFrame(
                currentImageData.frameUse,
                newName,
                author,
                description,
                position,
                rotation,
                null,
                (success, data, error) => OnUpdateComplete(success, data, error)
            );
        }
    }

    private void OnUpdateComplete(bool success, ImageData updatedData, string error)
    {
        if (success)
        {
            UpdateStatus("✓ Saved!");

            if (showDebug) Debug.Log("[ImageEditPopup] Update successful");

            var gallery = FindAnyObjectByType<ImageGalleryContainer>();
            if (gallery != null)
            {
                if (showDebug) Debug.Log("[ImageEditPopup] Refreshing gallery");
                gallery.RefreshGallery();
            }

            if (updatedData != null)
            {
                if (ArtManager.Instance != null)
                {
                    if (showDebug) Debug.Log($"[ImageEditPopup] Force refresh frame {updatedData.frameUse}");
                    ArtManager.Instance.ForceRefreshFrame(updatedData.frameUse);
                }

                RefreshAllArtFrames(updatedData.frameUse);
            }

            // ✅ SỬA: Unselect trước khi hide
            if (currentSelectedImageItem != null)
            {
                currentSelectedImageItem.SetSelected(false);
            }

            Invoke(nameof(Hide), 1f);
        }
        else
        {
            UpdateStatus($"✗ Error: {error}");
            Debug.LogError($"[ImageEditPopup] Update failed: {error}");
        }
    }

    private void RefreshAllArtFrames(int frameId)
    {
        ArtFrame[] allFrames = FindObjectsByType<ArtFrame>(FindObjectsSortMode.None);
        int count = 0;

        foreach (ArtFrame frame in allFrames)
        {
            if (frame != null && frame.FrameId == frameId)
            {
                if (showDebug) Debug.Log($"[ImageEditPopup] Reloading ArtFrame {frame.name} với ID {frameId}");
                frame.ReloadArtwork(true);
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

        if (selectedImageTexture != null)
        {
            Destroy(selectedImageTexture);
            selectedImageTexture = null;
        }

        EnablePlayerControllers();
        UnselectAllImageItems();
    }
}
