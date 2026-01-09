using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Model3DTransformEditPopup - Popup để chỉnh sửa transform (position, rotation, scale) của Model3D
/// Sử dụng API3DModelManager để update transform lên server
/// Sử dụng Model3DManager để refresh cache sau khi update
/// ✅ FIXED: Uses gizmo from Model3DView (component on parent GameObject)
/// Gizmo component deactivation does NOT use GameObject.SetActive
/// KHÁC ImageTransformEditPopup: Có thêm SCALE (uniform scale cho x, y, z)
/// </summary>
public class Model3DTransformEditPopup : MonoBehaviour
{
    private static Model3DTransformEditPopup _instance;
    public static Model3DTransformEditPopup Instance => _instance;

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField modelIdInput; // Read-only
    [SerializeField] private TMP_InputField posXInput;
    [SerializeField] private TMP_InputField posYInput;
    [SerializeField] private TMP_InputField posZInput;
    [SerializeField] private TMP_InputField rotXInput;
    [SerializeField] private TMP_InputField rotYInput;
    [SerializeField] private TMP_InputField rotZInput;
    [SerializeField] private TMP_InputField scaleInput; // Uniform scale
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button resetButton; // Optional: reset to original
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Transform Preview Settings")]
    [SerializeField] private bool updateInRealtime = true;
    [SerializeField] private float updateDelay = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Data storage
    private string currentModelId;
    private Model3DData currentModelData;
    private Model3DView targetModel3DView;
    
    // ✅ Current gizmo (from target view - component on parent GameObject)
    private RuntimeTransformGizmo currentGizmo;

    // Transform tracking
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private float originalScale;
    private Vector3 newPosition;
    private Vector3 newRotation;
    private float newScale;
    private bool hasChanges = false;

    // Input handling
    private float nextUpdateTime;
    private PlayerController[] playerControllers;
    private System.Action onHideCallback;

    // Cờ để tránh cập nhật realtime khi đang khởi tạo
    private bool isPopulating = false;

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

        // Setup buttons
        SetupButtons();

        // Setup input fields
        SetupInputFields();

        // Hide initially
        Hide();

        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Initialized");
    }

    private void OnDestroy()
    {
        // Cleanup
        RemoveButtonListeners();
        EnablePlayerControllers();

        // ✅ Deactivate current gizmo if active
        DeactivateGizmo();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    #endregion

    #region Setup

    private void SetupButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetClicked);
        }
    }

    private void RemoveButtonListeners()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveAllListeners();
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveAllListeners();
        }
    }

    private void SetupInputFields()
    {
        // Set model ID input to read-only
        if (modelIdInput != null)
        {
            modelIdInput.readOnly = true;
            modelIdInput.interactable = false;
        }

        // Add listeners for realtime update
        if (updateInRealtime)
        {
            if (posXInput != null) posXInput.onValueChanged.AddListener(OnInputChanged);
            if (posYInput != null) posYInput.onValueChanged.AddListener(OnInputChanged);
            if (posZInput != null) posZInput.onValueChanged.AddListener(OnInputChanged);
            if (rotXInput != null) rotXInput.onValueChanged.AddListener(OnInputChanged);
            if (rotYInput != null) rotYInput.onValueChanged.AddListener(OnInputChanged);
            if (rotZInput != null) rotZInput.onValueChanged.AddListener(OnInputChanged);
            if (scaleInput != null) scaleInput.onValueChanged.AddListener(OnInputChanged);
        }
    }

    #endregion

    #region Public Methods - Show/Hide

    /// <summary>
    /// Hiển thị popup để edit transform của Model3D
    /// ✅ Gets gizmo component from Model3DView (component on parent GameObject)
    /// </summary>
    public void Show(Model3DView model3DView, System.Action onHide = null)
    {
        if (model3DView == null)
        {
            Debug.LogError("[Model3DTransformEditPopup] Model3DView is null!");
            return;
        }

        if (model3DView.ModelData == null)
        {
            Debug.LogError("[Model3DTransformEditPopup] Model3DData is null!");
            return;
        }

        targetModel3DView = model3DView;
        currentModelData = model3DView.ModelData;
        currentModelId = model3DView.ModelId;
        onHideCallback = onHide;

        // ✅ Get gizmo component from target view
        currentGizmo = targetModel3DView.Gizmo;

        // Store original transform
        originalPosition = currentModelData.position;
        originalRotation = currentModelData.rotation;
        originalScale = currentModelData.scale_x; // Assume uniform scale

        // Initialize new transform
        newPosition = originalPosition;
        newRotation = originalRotation;
        newScale = originalScale;
        hasChanges = false;

        // Populate input fields
        PopulateInputFields();

        // Show popup
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        // Disable player controllers
        DisablePlayerControllers();

        // ✅ Activate gizmo component from view
        if (currentGizmo != null)
        {
            if (showDebug)
                Debug.Log($"[Model3DTransformEditPopup] ✅ Found gizmo component on {targetModel3DView.name}");
            
            ActivateGizmo();
        }
        else
        {
            Debug.LogWarning($"[Model3DTransformEditPopup] ❌ No gizmo component found on {targetModel3DView.name}! Please add RuntimeTransformGizmo component to prefab.");
        }

        ClearStatus();

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] Showing popup for Model3D ID: {currentModelId}");
    }

    /// <summary>
    /// Ẩn popup
    /// </summary>
    public void Hide()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        // ✅ Deactivate gizmo component
        DeactivateGizmo();

        // Re-enable player controllers
        EnablePlayerControllers();

        // Invoke callback
        onHideCallback?.Invoke();
        onHideCallback = null;

        // Clear references
        targetModel3DView = null;
        currentModelData = null;
        currentModelId = "";
        currentGizmo = null; // ✅ Clear gizmo reference

        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Popup hidden");
    }

    #endregion

    #region Input Handling

    /// <summary>
    /// Populate input fields với data hiện tại
    /// </summary>
    private void PopulateInputFields()
    {
        isPopulating = true;

        if (modelIdInput != null)
        {
            modelIdInput.text = currentModelId;
        }

        if (posXInput != null) posXInput.text = originalPosition.x.ToString("F2");
        if (posYInput != null) posYInput.text = originalPosition.y.ToString("F2");
        if (posZInput != null) posZInput.text = originalPosition.z.ToString("F2");

        if (rotXInput != null) rotXInput.text = originalRotation.x.ToString("F2");
        if (rotYInput != null) rotYInput.text = originalRotation.y.ToString("F2");
        if (rotZInput != null) rotZInput.text = originalRotation.z.ToString("F2");

        if (scaleInput != null) scaleInput.text = originalScale.ToString("F2");

        isPopulating = false;
    }

    /// <summary>
    /// Callback khi input field thay đổi
    /// </summary>
    private void OnInputChanged(string value)
    {
        if (isPopulating) return;

        if (updateInRealtime && Time.time >= nextUpdateTime)
        {
            UpdateTransformFromInputs();
            nextUpdateTime = Time.time + updateDelay;
        }
    }

    /// <summary>
    /// Cập nhật transform từ input fields
    /// </summary>
    private void UpdateTransformFromInputs()
    {
        if (targetModel3DView == null) return;

        // Parse position
        if (float.TryParse(posXInput?.text, out float px) &&
            float.TryParse(posYInput?.text, out float py) &&
            float.TryParse(posZInput?.text, out float pz))
        {
            newPosition = new Vector3(px, py, pz);
        }

        // Parse rotation
        if (float.TryParse(rotXInput?.text, out float rx) &&
            float.TryParse(rotYInput?.text, out float ry) &&
            float.TryParse(rotZInput?.text, out float rz))
        {
            newRotation = new Vector3(rx, ry, rz);
        }

        // Parse scale (uniform)
        if (float.TryParse(scaleInput?.text, out float s))
        {
            newScale = Mathf.Max(0.01f, s); // Minimum scale 0.01
        }

        // Apply to GameObject
        targetModel3DView.transform.position = newPosition;
        targetModel3DView.transform.rotation = Quaternion.Euler(newRotation);
        targetModel3DView.transform.localScale = Vector3.one * newScale;

        // ✅ Update gizmo position (if active)
        UpdateGizmoPosition();

        // Check if has changes
        hasChanges = (newPosition != originalPosition) ||
                     (newRotation != originalRotation) ||
                     (Mathf.Abs(newScale - originalScale) > 0.001f);

        if (showDebug && hasChanges)
        {
            Debug.Log($"[Model3DTransformEditPopup] Transform updated - Pos: {newPosition}, Rot: {newRotation}, Scale: {newScale}");
        }
    }

    #endregion

    #region Gizmo Integration - FIXED

    /// <summary>
    /// ✅ FIXED: Activate gizmo COMPONENT only (not GameObject)
    /// Does NOT call gameObject.SetActive!
    /// </summary>
    private void ActivateGizmo()
    {
        if (currentGizmo == null || targetModel3DView == null)
        {
            Debug.LogWarning("[Model3DTransformEditPopup] Cannot activate gizmo - gizmo or target is null");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] ✅ Activating gizmo component for {targetModel3DView.name}");

        // ✅ REMOVED: Don't touch GameObject.SetActive!
        // currentGizmo.gameObject.SetActive(true);

        // 1. Set gizmo position to match target
        currentGizmo.transform.position = targetModel3DView.transform.position;
        currentGizmo.transform.rotation = targetModel3DView.transform.rotation;

        // 2. Activate gizmo COMPONENT (shows LineRenderers, enables interaction)
        currentGizmo.Activate();

        // 3. Subscribe to event
        currentGizmo.OnTransformChanged += OnGizmoTransformChanged;

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] ✅ Gizmo component activated (GameObject stays active)");
    }

    /// <summary>
    /// ✅ FIXED: Deactivate gizmo COMPONENT only (not GameObject)
    /// Does NOT call gameObject.SetActive!
    /// </summary>
    private void DeactivateGizmo()
    {
        if (currentGizmo == null) return;

        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Deactivating gizmo component");

        // 1. Unsubscribe event
        currentGizmo.OnTransformChanged -= OnGizmoTransformChanged;

        // 2. Deactivate gizmo COMPONENT (hides LineRenderers, disables interaction)
        currentGizmo.Deactivate();

        // ✅ REMOVED: Don't touch GameObject.SetActive!
        // currentGizmo.gameObject.SetActive(false);

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] ✅ Gizmo component deactivated (GameObject stays active)");
    }

    /// <summary>
    /// ✅ Update gizmo position to match target
    /// </summary>
    private void UpdateGizmoPosition()
    {
        if (currentGizmo == null || !currentGizmo.IsActive) return;
        if (targetModel3DView == null) return;

        currentGizmo.transform.position = targetModel3DView.transform.position;
        currentGizmo.transform.rotation = targetModel3DView.transform.rotation;
    }

    /// <summary>
    /// ✅ Callback khi gizmo thay đổi transform
    /// Signature: (Vector3 position, Vector3 rotation)
    /// </summary>
    private void OnGizmoTransformChanged(Vector3 position, Vector3 rotation)
    {
        if (targetModel3DView == null) return;

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] Gizmo changed - Pos: {position}, Rot: {rotation}");

        isPopulating = true;

        // Update position inputs
        if (posXInput != null) posXInput.text = position.x.ToString("F2");
        if (posYInput != null) posYInput.text = position.y.ToString("F2");
        if (posZInput != null) posZInput.text = position.z.ToString("F2");

        // Update rotation inputs
        if (rotXInput != null) rotXInput.text = rotation.x.ToString("F2");
        if (rotYInput != null) rotYInput.text = rotation.y.ToString("F2");
        if (rotZInput != null) rotZInput.text = rotation.z.ToString("F2");

        // Update scale input (uniform) - lấy từ transform
        float scale = targetModel3DView.transform.localScale.x;
        if (scaleInput != null) scaleInput.text = scale.ToString("F2");

        isPopulating = false;

        // Update internal values
        newPosition = position;
        newRotation = rotation;
        newScale = scale;

        // Apply to target
        targetModel3DView.transform.position = position;
        targetModel3DView.transform.rotation = Quaternion.Euler(rotation);

        // Check changes
        hasChanges = (newPosition != originalPosition) ||
                     (newRotation != originalRotation) ||
                     (Mathf.Abs(newScale - originalScale) > 0.001f);
    }

    #endregion

    #region Button Handlers

    /// <summary>
    /// Xử lý khi click Save button
    /// </summary>
    private void OnSaveClicked()
    {
        if (!hasChanges)
        {
            ShowStatus("No changes to save", Color.yellow);
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] Saving transform for Model3D ID: {currentModelId}");

        // Update transform từ inputs (nếu chưa update)
        UpdateTransformFromInputs();

        // Prepare updated data
        Model3DData updatedData = new Model3DData
        {
            id = currentModelId,
            name = currentModelData.name,
            author = currentModelData.author,
            description = currentModelData.description,
            url = currentModelData.url,
            position_x = newPosition.x,
            position_y = newPosition.y,
            position_z = newPosition.z,
            rotation_x = newRotation.x,
            rotation_y = newRotation.y,
            rotation_z = newRotation.z,
            scale_x = newScale,
            scale_y = newScale,
            scale_z = newScale
        };

        ShowStatus("Saving...", Color.white);

        // Call API to update transform
        API3DModelManager.Instance.UpdateModel(
            currentModelId,
            updatedData,
            null, // No file data
            (success, message) =>
            {
                if (success)
                {
                    ShowStatus("Transform saved successfully!", Color.green);

                    // Update current data
                    currentModelData.position = newPosition;
                    currentModelData.rotation = newRotation;
                    currentModelData.scale_x = newScale;
                    currentModelData.scale_y = newScale;
                    currentModelData.scale_z = newScale;

                    // Update original values
                    originalPosition = newPosition;
                    originalRotation = newRotation;
                    originalScale = newScale;
                    hasChanges = false;

                    // Clear cache in Model3DManager
                    if (Model3DManager.Instance != null)
                    {
                        Model3DManager.Instance.ClearCache();
                    }

                    if (showDebug)
                        Debug.Log($"[Model3DTransformEditPopup] Transform saved successfully for Model3D ID: {currentModelId}");

                    // Auto close after 1 second
                    StartCoroutine(HideAfterDelay(1f));
                }
                else
                {
                    ShowStatus($"Error: {message}", Color.red);
                    Debug.LogError($"[Model3DTransformEditPopup] Failed to save transform: {message}");
                }
            }
        );
    }

    /// <summary>
    /// Xử lý khi click Cancel button
    /// </summary>
    private void OnCancelClicked()
    {
        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Cancel clicked");

        // Reset to original transform
        if (targetModel3DView != null)
        {
            targetModel3DView.transform.position = originalPosition;
            targetModel3DView.transform.rotation = Quaternion.Euler(originalRotation);
            targetModel3DView.transform.localScale = Vector3.one * originalScale;
        }

        Hide();
    }

    /// <summary>
    /// Xử lý khi click Reset button
    /// </summary>
    private void OnResetClicked()
    {
        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Reset clicked");

        // Reset to original values
        newPosition = originalPosition;
        newRotation = originalRotation;
        newScale = originalScale;

        // Update inputs
        isPopulating = true;
        if (posXInput != null) posXInput.text = originalPosition.x.ToString("F2");
        if (posYInput != null) posYInput.text = originalPosition.y.ToString("F2");
        if (posZInput != null) posZInput.text = originalPosition.z.ToString("F2");
        if (rotXInput != null) rotXInput.text = originalRotation.x.ToString("F2");
        if (rotYInput != null) rotYInput.text = originalRotation.y.ToString("F2");
        if (rotZInput != null) rotZInput.text = originalRotation.z.ToString("F2");
        if (scaleInput != null) scaleInput.text = originalScale.ToString("F2");
        isPopulating = false;

        // Apply to GameObject
        if (targetModel3DView != null)
        {
            targetModel3DView.transform.position = originalPosition;
            targetModel3DView.transform.rotation = Quaternion.Euler(originalRotation);
            targetModel3DView.transform.localScale = Vector3.one * originalScale;
        }

        // Update gizmo position
        UpdateGizmoPosition();

        hasChanges = false;
        ShowStatus("Reset to original transform", Color.cyan);
    }

    #endregion

    #region Player Controller Management

    /// <summary>
    /// Disable player controllers khi popup mở
    /// </summary>
    private void DisablePlayerControllers()
    {
        playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var controller in playerControllers)
        {
            controller.enabled = false;
        }

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] Disabled {playerControllers.Length} player controllers");
    }

    /// <summary>
    /// Enable player controllers khi popup đóng
    /// </summary>
    private void EnablePlayerControllers()
    {
        if (playerControllers != null)
        {
            foreach (var controller in playerControllers)
            {
                if (controller != null)
                {
                    controller.enabled = true;
                }
            }

            if (showDebug)
                Debug.Log($"[Model3DTransformEditPopup] Enabled {playerControllers.Length} player controllers");
        }
    }

    #endregion

    #region UI Helpers

    /// <summary>
    /// Hiển thị status message
    /// </summary>
    private void ShowStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] Status: {message}");
    }

    /// <summary>
    /// Clear status message
    /// </summary>
    private void ClearStatus()
    {
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    /// <summary>
    /// Đóng popup sau delay
    /// </summary>
    private System.Collections.IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
    }

    #endregion
}
