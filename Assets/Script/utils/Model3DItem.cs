using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Model3DItem - Đại diện cho một 3D model item trong gallery list
/// Hiển thị thông tin: Name, Author, URL (chỉ đọc)
/// ✅ FIXED: Complete callback system with Model3DEditPopup
/// ✅ FIXED: Pass null for Model3DView (gallery items don't have views)
/// </summary>
public class Model3DItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI authorText;
    [SerializeField] private TextMeshProUGUI urlText;
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject selectionFrame;

    [Header("Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.8f, 1f, 0.8f, 1f); // Light green
    [SerializeField] private Image backgroundImage; // Optional: để đổi màu background

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // Data
    private Model3DData modelData;

    // Static reference để track item đang được chọn
    private static Model3DItem currentSelectedItem;

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

    #region Public Methods - Setup

    /// <summary>
    /// Setup item với Model3DData
    /// </summary>
    public void Setup(Model3DData data)
    {
        if (data == null)
        {
            Debug.LogError("[Model3DItem] Setup called with null Model3DData!");
            return;
        }

        modelData = data;

        // Set text values
        if (nameText != null)
        {
            nameText.text = !string.IsNullOrEmpty(data.name) ? data.name : "Unnamed Model";
        }

        if (authorText != null)
        {
            authorText.text = !string.IsNullOrEmpty(data.author) ? data.author : "Unknown";
        }

        if (urlText != null)
        {
            // Rút gọn URL nếu quá dài
            string displayUrl = !string.IsNullOrEmpty(data.url) ? data.url : "No URL";
            if (displayUrl.Length > 50)
            {
                displayUrl = displayUrl.Substring(0, 47) + "...";
            }
            urlText.text = displayUrl;
        }

        if (showDebug)
            Debug.Log($"[Model3DItem] Setup: {data.name} by {data.author}");
    }

    /// <summary>
    /// Refresh data - update UI với data mới
    /// </summary>
    public void RefreshData(Model3DData newData)
    {
        if (newData == null)
        {
            Debug.LogWarning("[Model3DItem] Cannot refresh: Model3DData is null");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DItem] Refreshing data for: {newData.name}");

        Setup(newData);
    }

    #endregion

    #region Public Methods - Selection

    /// <summary>
    /// Set trạng thái selected
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectionFrame != null)
        {
            selectionFrame.SetActive(selected);
        }

        // Đổi màu background nếu có
        if (backgroundImage != null)
        {
            backgroundImage.color = selected ? selectedColor : normalColor;
        }

        if (showDebug)
            Debug.Log($"[Model3DItem] {modelData?.name} selected: {selected}");
    }

    /// <summary>
    /// Kiểm tra xem item có đang được chọn không
    /// </summary>
    public bool IsSelected()
    {
        return this == currentSelectedItem;
    }

    #endregion

    #region Public Methods - Data Access

    /// <summary>
    /// Lấy Model3DData
    /// </summary>
    public Model3DData GetModelData()
    {
        return modelData;
    }

    /// <summary>
    /// Lấy Model ID
    /// </summary>
    public string GetModelId()
    {
        return modelData?.id;
    }

    /// <summary>
    /// Lấy Model Name
    /// </summary>
    public string GetModelName()
    {
        return modelData?.name;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Xử lý khi click vào item
    /// </summary>
    private void OnSelectClicked()
    {
        if (modelData == null)
        {
            Debug.LogError("[Model3DItem] Cannot select: Model3DData is null!");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DItem] Clicked: {modelData.name} by {modelData.author}");

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
    /// Mở Model3DEditPopup
    /// ✅ FIXED: Pass null for Model3DView (gallery items don't have 3D views)
    /// </summary>
    private void OpenEditPopup()
    {
        if (Model3DEditPopup.Instance == null)
        {
            Debug.LogError("[Model3DItem] Model3DEditPopup.Instance is null! Make sure Model3DEditPopup exists in scene.");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DItem] Opening edit popup for: {modelData.name}");

        // ✅ Pass null for Model3DView parameter (gallery items don't have 3D views)
        // Only Model3DView objects in the scene have actual 3D model views
        Model3DEditPopup.Instance.Show(modelData, null, OnEditComplete);
    }

    /// <summary>
    /// Callback khi edit hoàn tất
    /// ✅ FIXED: Handle all cases (update, delete, cancel)
    /// </summary>
    private void OnEditComplete(bool success, Model3DData updatedData)
    {
        if (!success)
        {
            if (showDebug)
                Debug.Log($"[Model3DItem] Edit cancelled");
            return;
        }

        if (updatedData == null)
        {
            // Model bị xóa
            if (showDebug)
                Debug.Log($"[Model3DItem] Model was deleted, destroying item");

            // Clear selection if this item was selected
            if (currentSelectedItem == this)
            {
                currentSelectedItem = null;
            }

            // Destroy this item
            Destroy(gameObject);
            return;
        }

        // Model được update - refresh UI
        if (showDebug)
            Debug.Log($"[Model3DItem] Model updated: {updatedData.name}, refreshing UI");

        RefreshData(updatedData);

        // Note: Model3DEditPopup.RefreshAllComponents() will also refresh the gallery
        // So this item might be destroyed and recreated by the gallery refresh
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Lấy item đang được chọn
    /// </summary>
    public static Model3DItem GetCurrentSelectedItem()
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

    /// <summary>
    /// ✅ NEW: Check if any item is currently selected
    /// </summary>
    public static bool HasSelection()
    {
        return currentSelectedItem != null;
    }

    /// <summary>
    /// ✅ NEW: Get selected model data
    /// </summary>
    public static Model3DData GetSelectedModelData()
    {
        return currentSelectedItem?.GetModelData();
    }

    #endregion
}
