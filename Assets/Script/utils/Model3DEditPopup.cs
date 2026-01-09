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
/// Model3DEditPopup - Popup để tạo mới hoặc chỉnh sửa thông tin 3D model
/// ✅ FIXED: Complete callback system + WebGL file picker integration
/// </summary>
public class Model3DEditPopup : MonoBehaviour
{
    private static Model3DEditPopup _instance;
    public static Model3DEditPopup Instance => _instance;

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField modelFileInput;
    [SerializeField] private TMP_InputField authorInput;
    [SerializeField] private TMP_InputField descriptionInput;
    [SerializeField] private Button browseButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button deleteButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private RawImage previewImage;

    [Header("Model Settings")]
    [SerializeField] private int maxModelSize = 100 * 1024 * 1024;
    [SerializeField] private bool showDebug = true;
    [SerializeField] private float spawnDistanceFromPlayer = 3f;

    // Private variables
    private bool isEditMode = false;
    private string currentModelId = "";
    private string selectedFilePath = "";
    private byte[] selectedFileData = null;
    private bool isProcessing = false;
    private Model3DData currentModelData = null;

    // ✅ Store reference to Model3DView for updates
    private Model3DView currentModelView = null;

    // Callbacks
    private Action<bool, Model3DData> onSaveCallback;

    #region Unity Lifecycle

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        gameObject.name = "Model3DEditPopup"; // ✅ Đặt tên để JS SendMessage tìm được

        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    private void Start()
    {
        SetupButtons();
        ClearStatusText();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    #endregion

    #region Setup

    private void SetupButtons()
    {
        if (browseButton != null)
        {
            browseButton.onClick.RemoveAllListeners();
            browseButton.onClick.AddListener(OnBrowseButtonClicked);
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(OnSaveButtonClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelButtonClicked);
        }

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
        }
    }

    #endregion

    #region Public Methods - Open Popup

    /// <summary>
    /// Mở popup để tạo model mới
    /// </summary>
    public void OpenForCreate(Action<bool, Model3DData> callback = null)
    {
        Debug.Log("[Model3DEditPopup] ✅ OpenForCreate() called - CREATE MODE");

        isEditMode = false;
        currentModelId = "";
        currentModelData = null;
        currentModelView = null;
        onSaveCallback = callback;

        ClearAllFields();

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(false);
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        ClearStatusText();

        if (showDebug)
        {
            Debug.Log($"[Model3DEditPopup] Mode: CREATE, isEditMode={isEditMode}");
        }
    }

    /// <summary>
    /// Mở popup để chỉnh sửa model hiện có
    /// ✅ Accept Model3DView reference for callbacks
    /// </summary>
    public void Show(Model3DData modelData, Model3DView modelView = null, Action<bool, Model3DData> callback = null)
    {
        if (modelData == null)
        {
            Debug.LogError("[Model3DEditPopup] Model3DData is null!");
            return;
        }

        Debug.Log($"[Model3DEditPopup] ✅ Show() called - EDIT MODE for ID: {modelData.id}");

        isEditMode = true;
        currentModelId = modelData.id;
        currentModelData = modelData;
        currentModelView = modelView;
        onSaveCallback = callback;

        // Fill data
        if (nameInput != null) nameInput.text = modelData.name ?? "";
        if (modelFileInput != null) modelFileInput.text = modelData.url ?? "";
        if (authorInput != null) authorInput.text = modelData.author ?? "";
        if (descriptionInput != null) descriptionInput.text = modelData.description ?? "";

        selectedFilePath = "";
        selectedFileData = null;

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(true);
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        ClearStatusText();

        if (!string.IsNullOrEmpty(modelData.url))
        {
            StartCoroutine(LoadPreviewFromURL(modelData.url));
        }

        if (showDebug)
        {
            Debug.Log($"[Model3DEditPopup] Mode: EDIT, isEditMode={isEditMode}, currentModelId='{currentModelId}'");
        }
    }

    /// <summary>
    /// Đóng popup
    /// </summary>
    public void Close()
    {
        if (showDebug)
        {
            Debug.Log("[Model3DEditPopup] Đóng popup");
        }

        Model3DItem.ClearSelection();

        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        ClearAllFields();
        ClearStatusText();
        ClearPreview();
        selectedFilePath = "";
        selectedFileData = null;
        currentModelData = null;
        currentModelView = null;
        onSaveCallback = null;

        isEditMode = false;
        currentModelId = "";
    }

    #endregion

    #region Button Handlers

    private void OnBrowseButtonClicked()
    {
        if (isProcessing)
        {
            ShowStatus("Đang xử lý, vui lòng đợi...", false);
            return;
        }

#if UNITY_EDITOR
        BrowseFileEditor();
#elif UNITY_WEBGL
        BrowseFileWebGL();
#else
        ShowStatus("Browse file chỉ hỗ trợ trong Editor và WebGL", false);
#endif
    }

    private void OnSaveButtonClicked()
    {
        if (isProcessing)
        {
            ShowStatus("Đang xử lý, vui lòng đợi...", false);
            return;
        }

        StartCoroutine(SaveModel());
    }

    private void OnCancelButtonClicked()
    {
        if (isProcessing)
        {
            ShowStatus("Đang xử lý, không thể hủy", false);
            return;
        }

        if (showDebug)
        {
            Debug.Log("[Model3DEditPopup] Cancel clicked");
        }

        Model3DItem.ClearSelection();

        onSaveCallback?.Invoke(false, null);
        onSaveCallback = null;

        Close();
    }

    private void OnDeleteButtonClicked()
    {
        if (isProcessing)
        {
            ShowStatus("Đang xử lý, vui lòng đợi...", false);
            return;
        }

        if (string.IsNullOrEmpty(currentModelId))
        {
            ShowStatus("Không có model để xóa", false);
            return;
        }

        if (showDebug)
        {
            Debug.Log($"[Model3DEditPopup] Xác nhận xóa model ID: {currentModelId}");
        }

        StartCoroutine(DeleteModel());
    }

    #endregion

    #region File Browse

#if UNITY_EDITOR
    private void BrowseFileEditor()
    {
        string path = EditorUtility.OpenFilePanel("Chọn file GLB", "", "glb");

        if (string.IsNullOrEmpty(path))
        {
            if (showDebug)
            {
                Debug.Log("[Model3DEditPopup] Người dùng hủy chọn file");
            }
            return;
        }

        if (!File.Exists(path))
        {
            ShowStatus("File không tồn tại", false);
            return;
        }

        FileInfo fileInfo = new FileInfo(path);

        if (fileInfo.Length > maxModelSize)
        {
            ShowStatus($"File quá lớn. Tối đa {maxModelSize / (1024 * 1024)} MB", false);
            return;
        }

        selectedFilePath = path;
        selectedFileData = File.ReadAllBytes(path);

        if (modelFileInput != null)
        {
            modelFileInput.text = Path.GetFileName(path);
        }

        ShowStatus($"Đã chọn file: {Path.GetFileName(path)}", true);

        if (showDebug)
        {
            Debug.Log($"[Model3DEditPopup] File selected: {path} ({fileInfo.Length} bytes)");
        }
    }
#endif

#if UNITY_WEBGL && !UNITY_EDITOR
    // ✅ Extern declaration cho OpenGLBFilePicker
    [DllImport("__Internal")]
    private static extern void OpenGLBFilePicker();

    private void BrowseFileWebGL()
    {
        try
        {
            if (showDebug)
            {
                Debug.Log("[Model3DEditPopup] Calling OpenGLBFilePicker()");
            }

            OpenGLBFilePicker();
            ShowStatus("Đang mở file picker...", true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Model3DEditPopup] Error opening file picker: {ex.Message}");
            ShowStatus("Lỗi: Không thể mở file picker", false);
        }
    }

    /// <summary>
    /// ✅ Callback từ JavaScript khi file GLB được chọn
    /// Format: "base64Data|fileName"
    /// </summary>
    public void OnGLBFileSelected(string data)
    {
        if (showDebug)
        {
            Debug.Log($"[Model3DEditPopup] OnGLBFileSelected called - Data length: {data?.Length ?? 0}");
        }

        if (string.IsNullOrEmpty(data))
        {
            ShowStatus("Lỗi: Dữ liệu file rỗng", false);
            Debug.LogError("[Model3DEditPopup] OnGLBFileSelected: data is null or empty");
            return;
        }

        try
        {
            // Parse data: base64|filename
            string[] parts = data.Split('|');
            
            if (parts.Length < 2)
            {
                Debug.LogError($"[Model3DEditPopup] Invalid data format. Expected 'base64|filename', got: {data.Substring(0, Math.Min(100, data.Length))}");
                ShowStatus("Lỗi: Định dạng dữ liệu không hợp lệ", false);
                return;
            }

            string base64Data = parts[0];
            string fileName = parts[1];

            if (showDebug)
            {
                Debug.Log($"[Model3DEditPopup] Parsing file - Name: {fileName}, Base64 length: {base64Data.Length}");
            }

            // Chuyển Base64 thành byte array
            selectedFileData = Convert.FromBase64String(base64Data);

            if (showDebug)
            {
                Debug.Log($"[Model3DEditPopup] File decoded - Size: {selectedFileData.Length} bytes");
            }

            // Kiểm tra kích thước
            if (selectedFileData.Length > maxModelSize)
            {
                ShowStatus($"File quá lớn. Tối đa {maxModelSize / (1024 * 1024)} MB", false);
                Debug.LogWarning($"[Model3DEditPopup] File too large: {selectedFileData.Length} bytes (max: {maxModelSize})");
                selectedFileData = null;
                return;
            }

            selectedFilePath = fileName;

            // Update UI
            if (modelFileInput != null)
            {
                modelFileInput.text = fileName;
            }

            ShowStatus($"Đã chọn file: {fileName} ({selectedFileData.Length / 1024} KB)", true);

            if (showDebug)
            {
                Debug.Log($"[Model3DEditPopup] ✅ GLB file loaded successfully: {fileName}, size: {selectedFileData.Length} bytes");
            }
        }
        catch (FormatException ex)
        {
            Debug.LogError($"[Model3DEditPopup] Base64 decode error: {ex.Message}");
            ShowStatus("Lỗi: Không thể giải mã dữ liệu file", false);
            selectedFileData = null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Model3DEditPopup] Error processing file: {ex.Message}\n{ex.StackTrace}");
            ShowStatus("Lỗi: Không thể xử lý file", false);
            selectedFileData = null;
        }
    }
#endif

    #endregion

    #region Save/Delete Operations

    /// <summary>
    /// Lưu model (create hoặc update)
    /// ✅ FIXED: Complete callback system
    /// </summary>
    private IEnumerator SaveModel()
    {
        Debug.Log($"[Model3DEditPopup] SaveModel() - Mode: {(isEditMode ? "EDIT" : "CREATE")}, ID: '{currentModelId}'");

        // Validate input
        if (string.IsNullOrEmpty(nameInput.text.Trim()))
        {
            ShowStatus("Vui lòng nhập tên model", false);
            yield break;
        }

        // Nếu là create mode, phải có file
        if (!isEditMode && selectedFileData == null)
        {
            ShowStatus("Vui lòng chọn file GLB", false);
            Debug.LogError("[Model3DEditPopup] CREATE mode requires file!");
            yield break;
        }

        isProcessing = true;
        ShowStatus("Đang lưu...", true);

        // Tạo Model3DData
        Model3DData modelData = new Model3DData
        {
            name = nameInput.text.Trim(),
            author = authorInput != null ? authorInput.text.Trim() : "",
            description = descriptionInput != null ? descriptionInput.text.Trim() : ""
        };

        // XỬ LÝ TRANSFORM
        if (isEditMode && currentModelData != null)
        {
            // EDIT MODE: Giữ nguyên transform
            modelData.position_x = currentModelData.position_x;
            modelData.position_y = currentModelData.position_y;
            modelData.position_z = currentModelData.position_z;
            modelData.rotation_x = currentModelData.rotation_x;
            modelData.rotation_y = currentModelData.rotation_y;
            modelData.rotation_z = currentModelData.rotation_z;
            modelData.scale_x = currentModelData.scale_x;
            modelData.scale_y = currentModelData.scale_y;
            modelData.scale_z = currentModelData.scale_z;

            Debug.Log($"[Model3DEditPopup] EDIT mode - Keeping original transform");
        }
        else
        {
            // CREATE MODE: Spawn trước mặt player
            Vector3 spawnPosition = GetSpawnPositionInFrontOfPlayer();

            modelData.position_x = spawnPosition.x;
            modelData.position_y = spawnPosition.y;
            modelData.position_z = spawnPosition.z;
            modelData.rotation_x = 0;
            modelData.rotation_y = 0;
            modelData.rotation_z = 0;
            modelData.scale_x = 1;
            modelData.scale_y = 1;
            modelData.scale_z = 1;

            Debug.Log($"[Model3DEditPopup] CREATE mode - Spawn position: {spawnPosition}");
        }

        bool success = false;
        string message = "";
        bool operationComplete = false;

        // ROUTE DECISION: CREATE vs UPDATE
        if (isEditMode && !string.IsNullOrEmpty(currentModelId))
        {
            // UPDATE
            Debug.Log($"[Model3DEditPopup] 🔄 Calling UpdateModel() for ID: {currentModelId}");

            API3DModelManager.Instance.UpdateModel(
                currentModelId,
                modelData,
                selectedFileData,
                (updateSuccess, updateMessage) =>
                {
                    success = updateSuccess;
                    message = updateMessage;
                    operationComplete = true;

                    Debug.Log($"[Model3DEditPopup] UpdateModel callback - Success: {success}");
                }
            );
        }
        else
        {
            // CREATE
            Debug.Log("[Model3DEditPopup] ✨ Calling CreateModel() - NEW MODEL");

            API3DModelManager.Instance.CreateModel(
                modelData,
                selectedFileData,
                (createSuccess, createMessage) =>
                {
                    success = createSuccess;
                    message = createMessage;
                    operationComplete = true;

                    Debug.Log($"[Model3DEditPopup] CreateModel callback - Success: {success}");
                }
            );
        }

        // Đợi operation hoàn thành
        yield return new WaitUntil(() => operationComplete);

        isProcessing = false;

        if (success)
        {
            ShowStatus(isEditMode ? "Cập nhật thành công!" : "Tạo mới thành công!", true);

            Debug.Log($"[Model3DEditPopup] ✅ Save successful: {modelData.name}");

            // If CREATE mode, fetch the newly created model to get its ID
            if (!isEditMode)
            {
                Debug.Log("[Model3DEditPopup] Fetching newly created model to get ID...");

                bool fetchComplete = false;
                Model3DData createdModel = null;

                API3DModelManager.Instance.GetAllModels((fetchSuccess, models, error) =>
                {
                    if (fetchSuccess && models != null && models.Count > 0)
                    {
                        createdModel = models.Find(m => m.name == modelData.name);

                        if (createdModel == null)
                        {
                            createdModel = models[0];
                        }

                        Debug.Log($"[Model3DEditPopup] Found created model with ID: {createdModel.id}");
                    }
                    else
                    {
                        Debug.LogError($"[Model3DEditPopup] Failed to fetch created model: {error}");
                    }

                    fetchComplete = true;
                });

                yield return new WaitUntil(() => fetchComplete);

                // Update modelData with generated ID
                if (createdModel != null)
                {
                    modelData.id = createdModel.id;
                    modelData.url = createdModel.url;
                    modelData.public_id = createdModel.public_id;
                    modelData.created_at = createdModel.created_at;

                    Debug.Log($"[Model3DEditPopup] Model created with ID: {modelData.id}");
                }
            }

            // ✅ REFRESH ALL COMPONENTS
            RefreshAllComponents(modelData, isEditMode);

            Model3DItem.ClearSelection();

            // Callback
            onSaveCallback?.Invoke(true, modelData);
            onSaveCallback = null;

            yield return new WaitForSeconds(1f);
            Close();
        }
        else
        {
            ShowStatus($"Lỗi: {message}", false);
            Debug.LogError($"[Model3DEditPopup] ❌ Save failed: {message}");
        }
    }

    /// <summary>
    /// Xóa model
    /// ✅ FIXED: Complete callback system
    /// </summary>
    private IEnumerator DeleteModel()
    {
        if (string.IsNullOrEmpty(currentModelId))
        {
            ShowStatus("Không có model để xóa", false);
            yield break;
        }

        isProcessing = true;
        ShowStatus("Đang xóa...", true);

        bool success = false;
        string message = "";
        bool operationComplete = false;

        API3DModelManager.Instance.DeleteModel(
            currentModelId,
            (deleteSuccess, deleteMessage) =>
            {
                success = deleteSuccess;
                message = deleteMessage;
                operationComplete = true;
            }
        );

        yield return new WaitUntil(() => operationComplete);

        isProcessing = false;

        if (success)
        {
            ShowStatus("Xóa thành công!", true);

            Debug.Log($"[Model3DEditPopup] Delete successful: {currentModelId}");

            // ✅ REFRESH ALL COMPONENTS (DELETE mode)
            RefreshAllComponents(null, false, true);

            Model3DItem.ClearSelection();

            // Callback với null (model đã bị xóa)
            onSaveCallback?.Invoke(true, null);
            onSaveCallback = null;

            yield return new WaitForSeconds(1f);
            Close();
        }
        else
        {
            ShowStatus($"Lỗi xóa: {message}", false);
            Debug.LogError($"[Model3DEditPopup] Delete failed: {message}");
        }
    }

    /// <summary>
    /// ✅ Refresh tất cả components sau khi CREATE/UPDATE/DELETE
    /// </summary>
    private void RefreshAllComponents(Model3DData modelData, bool wasEdit, bool wasDelete = false)
    {
        Debug.Log($"[Model3DEditPopup] 🔄 Refreshing all components - Edit: {wasEdit}, Delete: {wasDelete}");

        // 1. Clear Model3DManager cache
        if (Model3DManager.Instance != null)
        {
            Model3DManager.Instance.ClearCache();
            Debug.Log("[Model3DEditPopup] ✅ Model3DManager cache cleared");
        }

        // 2. Refresh Model3DView (if editing existing model in scene)
        if (wasEdit && currentModelView != null && modelData != null)
        {
            Debug.Log($"[Model3DEditPopup] 🔄 Refreshing Model3DView for ID: {modelData.id}");

            // Update view data
            currentModelView.UpdateModelData(modelData);

            // Reload model if file changed
            if (selectedFileData != null)
            {
                Debug.Log("[Model3DEditPopup] File changed, reloading model in view");
                currentModelView.LoadModel(modelData.url);
            }
        }

        // 3. Refresh Model3DGalleryContainer (always refresh gallery)
        if (Model3DGalleryContainer.Instance != null)
        {
            Debug.Log("[Model3DEditPopup] 🔄 Refreshing Model3DGalleryContainer");
            Model3DGalleryContainer.Instance.RefreshGallery();
        }

        // 4. Handle Model3DPrefabManager (efficient single model operations)
        if (Model3DPrefabManager.Instance != null)
        {
            if (wasDelete)
            {
                // DELETE: Remove model from scene
                Debug.Log($"[Model3DEditPopup] 🗑️ Removing model from scene: {currentModelId}");
                Model3DPrefabManager.Instance.RemoveModelInstance(currentModelId);
            }
            else if (!wasEdit && modelData != null)
            {
                // CREATE: Add new model to scene
                Debug.Log($"[Model3DEditPopup] ✨ Creating new model in scene: {modelData.name}");
                Model3DPrefabManager.Instance.CreateModelInstance(modelData);
            }
            else if (wasEdit && modelData != null && selectedFileData != null)
            {
                // UPDATE with new file: Reload model in scene
                if (currentModelView == null)
                {
                    Debug.Log($"[Model3DEditPopup] 🔄 Model edited from gallery, updating scene instance: {modelData.name}");

                    // Remove old instance and create new one with updated file
                    Model3DPrefabManager.Instance.RemoveModelInstance(modelData.id);
                    Model3DPrefabManager.Instance.CreateModelInstance(modelData);
                }
                else
                {
                    Debug.Log($"[Model3DEditPopup] Model edited from scene, Model3DView handles reload");
                }
            }
        }

        // 5. If DELETE, destroy the Model3DView GameObject (if exists)
        if (wasDelete && currentModelView != null)
        {
            Debug.Log($"[Model3DEditPopup] 🗑️ Destroying Model3DView GameObject");
            Destroy(currentModelView.gameObject);
            currentModelView = null;
        }

        Debug.Log("[Model3DEditPopup] ✅ All components refreshed");
    }

    private Vector3 GetSpawnPositionInFrontOfPlayer()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning("[Model3DEditPopup] Camera.main not found, using default position");
            return new Vector3(0, 1.5f, 3f);
        }

        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 cameraForward = mainCamera.transform.forward;
        cameraForward.y = 0;
        cameraForward.Normalize();

        Vector3 spawnPosition = cameraPosition + cameraForward * spawnDistanceFromPlayer;
        spawnPosition.y = 1.5f;

        return spawnPosition;
    }

    #endregion

    #region Preview

    private IEnumerator LoadPreviewFromURL(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            yield break;
        }

        if (showDebug)
        {
            Debug.Log($"[Model3DEditPopup] Loading preview from: {url}");
        }

        yield break;
    }

    private void ClearPreview()
    {
        if (previewImage != null)
        {
            previewImage.texture = null;
        }
    }

    #endregion

    #region UI Helpers

    private void ShowStatus(string text, bool isSuccess)
    {
        if (statusText != null)
        {
            statusText.text = text;
            statusText.color = isSuccess ? Color.green : Color.red;
        }

        if (showDebug)
        {
            Debug.Log($"[Model3DEditPopup] Status: {text} (Success: {isSuccess})");
        }
    }

    private void ClearStatusText()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    private void ClearAllFields()
    {
        if (nameInput != null) nameInput.text = "";
        if (modelFileInput != null) modelFileInput.text = "";
        if (authorInput != null) authorInput.text = "";
        if (descriptionInput != null) descriptionInput.text = "";

        selectedFilePath = "";
        selectedFileData = null;

        ClearPreview();
    }

    #endregion
}
