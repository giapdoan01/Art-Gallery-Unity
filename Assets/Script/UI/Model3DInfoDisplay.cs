using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class Model3DInfoDisplay : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject infoPanel; // Panel chứa thông tin
    [SerializeField] private TextMeshProUGUI nameText; // Text hiển thị tên
    [SerializeField] private TextMeshProUGUI authorText; // Text hiển thị tác giả
    [SerializeField] private TextMeshProUGUI descriptionText; // Text hiển thị mô tả
    [SerializeField] private Button closeButton; // Button đóng panel

    private static Model3DInfoDisplay _instance;
    public static Model3DInfoDisplay Instance => _instance;

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
    /// Hiển thị thông tin của model 3D
    /// </summary>
    public void ShowInfo(Model3DData modelData)
    {
        if (modelData == null)
        {
            Debug.LogError("Model3DData is null!");
            return;
        }

        // Fill thông tin vào text
        if (nameText != null) nameText.text = modelData.name ?? "Không có tên";
        if (authorText != null) authorText.text = modelData.author ?? "Không có tác giả";
        if (descriptionText != null) descriptionText.text = modelData.description ?? "Không có mô tả";

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
