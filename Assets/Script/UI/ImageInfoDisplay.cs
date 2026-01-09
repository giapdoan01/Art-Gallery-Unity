using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ImageInfoDisplay : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject infoPanel; // Panel chứa thông tin
    [SerializeField] private TextMeshProUGUI nameText; // Text hiển thị tên
    [SerializeField] private TextMeshProUGUI authorText; // Text hiển thị tác giả
    [SerializeField] private TextMeshProUGUI descriptionText; // Text hiển thị mô tả
    [SerializeField] private Button closeButton; // Button đóng panel

    private static ImageInfoDisplay _instance;
    public static ImageInfoDisplay Instance => _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Ẩn panel khi khởi động
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }

        // Setup close button
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideInfo);
        }
    }

    /// <summary>
    /// Hiển thị thông tin của ảnh
    /// </summary>
    public void ShowInfo(ImageData imageData)
    {
        if (imageData == null)
        {
            Debug.LogError("ImageData is null!");
            return;
        }

        // Fill thông tin vào text
        if (nameText != null) nameText.text = imageData.name ?? "Không có tên";
        if (authorText != null) authorText.text = imageData.author ?? "Không có tác giả";
        if (descriptionText != null) descriptionText.text = imageData.description ?? "Không có mô tả";

        // Hiển thị panel
        if (infoPanel != null)
        {
            infoPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Ẩn panel thông tin
    /// </summary>
    public void HideInfo()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(false);
        }
    }
}
