using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ImageGalleryContainer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform contentContainer; 
    [SerializeField] private GameObject imageItemPrefab; 
    [SerializeField] private Button refreshButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingPanel;

    [Header("Settings")]
    [SerializeField] private bool loadOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private List<ImageItem> imageItems = new List<ImageItem>();
    private List<ImageData> currentImages = new List<ImageData>();

    private void Start()
    {
        // Setup refresh button
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshGallery);
        }

        if (loadOnStart)
        {
            LoadAllImages();
        }
        gameObject.SetActive(false);
    }

    public void LoadAllImages()
    {
        if (showDebug) Debug.Log("[ImageGallery] Loading all images...");

        ShowLoading(true);
        UpdateStatus("Loading images...");

        APIManager.Instance.GetAllImages((success, images, error) =>
        {
            ShowLoading(false);

            if (success && images != null)
            {
                if (showDebug) Debug.Log($"[ImageGallery] Loaded {images.Count} images");

                currentImages = images;
                DisplayImages(images);
                UpdateStatus($"Loaded {images.Count} images");
            }
            else
            {
                Debug.LogError($"[ImageGallery] Failed to load images: {error}");
                UpdateStatus($"Error: {error}");
            }
        });
    }

    /// <summary>
    /// Hiển thị danh sách ảnh
    /// </summary>
    private void DisplayImages(List<ImageData> images)
    {
        // Clear existing items
        ClearItems();

        if (images == null || images.Count == 0)
        {
            if (showDebug) Debug.Log("[ImageGallery] No images to display");
            UpdateStatus("No images found");
            return;
        }

        // Create new items
        foreach (ImageData imageData in images)
        {
            CreateImageItem(imageData);
        }

        // Force rebuild layout
        StartCoroutine(RebuildLayoutNextFrame());
    }

    /// <summary>
    /// Tạo một image item
    /// </summary>
    private void CreateImageItem(ImageData imageData)
    {
        if (imageItemPrefab == null || contentContainer == null)
        {
            Debug.LogError("[ImageGallery] Missing prefab or container!");
            return;
        }

        // Instantiate prefab
        GameObject itemObj = Instantiate(imageItemPrefab, contentContainer);
        ImageItem item = itemObj.GetComponent<ImageItem>();

        if (item != null)
        {
            // Setup item
            item.Setup(imageData);
            imageItems.Add(item);

            if (showDebug) Debug.Log($"[ImageGallery] Created item for: {imageData.name}");
        }
        else
        {
            Debug.LogError("[ImageGallery] ImageItem component not found on prefab!");
            Destroy(itemObj);
        }
    }

    /// <summary>
    /// Clear tất cả items
    /// </summary>
    private void ClearItems()
    {
        foreach (ImageItem item in imageItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }

        imageItems.Clear();
    }

    /// <summary>
    /// Refresh gallery
    /// </summary>
    public void RefreshGallery()
    {
        if (showDebug) Debug.Log("[ImageGallery] Refreshing...");
        LoadAllImages();
    }

    /// <summary>
    /// Show/hide loading panel
    /// </summary>
    private void ShowLoading(bool show)
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(show);
        }
    }

    /// <summary>
    /// Update status text
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    /// <summary>
    /// Rebuild layout sau 1 frame
    /// </summary>
    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return null;

        // Force rebuild
        if (contentContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);
        }
    }

    /// <summary>
    /// Get current images
    /// </summary>
    public List<ImageData> GetCurrentImages()
    {
        return new List<ImageData>(currentImages);
    }

    /// <summary>
    /// Get image count
    /// </summary>
    public int GetImageCount()
    {
        return currentImages.Count;
    }

    private void OnDestroy()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshGallery);
        }
    }
}
