using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class ImageItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI frameIdText;
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject selectionFrame;

    private ImageData imageData;
    private static ImageItem currentSelectedItem;

    private void Awake()
    {
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnSelectClicked);
        }

        // Khởi tạo: ẩn frame selection
        if (selectionFrame != null)
        {
            selectionFrame.SetActive(false);
        }
    }

    public void Setup(ImageData data)
    {
        imageData = data;

        // Set text
        if (nameText != null)
        {
            nameText.text = data.name;
        }

        if (frameIdText != null)
        {
            frameIdText.text = $"Frame: {data.frameUse}";
        }

        // Load thumbnail
        LoadThumbnail(data.url);
    }

    private void LoadThumbnail(string url)
    {
        // Tạm thời disable image
        if (thumbnailImage != null)
        {
            thumbnailImage.enabled = false;
        }

        // Dùng APIManager để tải ảnh
        APIManager.Instance.LoadTextureFromUrl(url, (texture) =>
        {
            if (texture != null && thumbnailImage != null)
            {
                // Convert Texture2D to Sprite
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f)
                );

                thumbnailImage.sprite = sprite;
                thumbnailImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[ImageItem] Failed to load thumbnail for {imageData.name}");
            }
        });
    }

    private void OnSelectClicked()
    {
        Debug.Log($"[ImageItem] Clicked: {imageData.name}");

        // Tắt frame của item đang được chọn trước đó (nếu có)
        if (currentSelectedItem != null && currentSelectedItem != this)
        {
            currentSelectedItem.SetSelected(false);
        }

        // Thiết lập item hiện tại là được chọn
        currentSelectedItem = this;
        SetSelected(true);

        // Hiển thị popup và truyền tham chiếu đến chính ImageItem này
        if (ImageEditPopup.Instance != null)
        {
            // Truyền thêm tham chiếu đến this (ImageItem hiện tại)
            ImageEditPopup.Instance.Show(imageData, this);
        }
        else
        {
            Debug.LogError("[ImageItem] ImageEditPopup.Instance is null! Make sure ImageEditPopup exists in scene.");
        }
    }

    // Phương thức mới để bật/tắt frame selection
    public void SetSelected(bool selected)
    {
        if (selectionFrame != null)
        {
            selectionFrame.SetActive(selected);
        }
    }

    public ImageData GetImageData()
    {
        return imageData;
    }

    // Kiểm tra xem item có đang được chọn không
    public bool IsSelected()
    {
        return this == currentSelectedItem;
    }

    private void OnDestroy()
    {
        // Nếu item này đang được chọn và bị hủy, reset biến static
        if (currentSelectedItem == this)
        {
            currentSelectedItem = null;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(OnSelectClicked);
        }
    }
}