using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TransformEditPopup - Popup để chỉnh sửa transform (position, rotation) của ArtFrame
/// Sử dụng APIManager để update transform lên server
/// Sử dụng ArtManager để refresh cache sau khi update
/// </summary>
public class TransformEditPopup : MonoBehaviour
{
    private static TransformEditPopup _instance;
    public static TransformEditPopup Instance => _instance;

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField frameIdInput; // Read-only
    [SerializeField] private TMP_InputField posXInput;
    [SerializeField] private TMP_InputField posYInput;
    [SerializeField] private TMP_InputField posZInput;
    [SerializeField] private TMP_InputField rotXInput;
    [SerializeField] private TMP_InputField rotYInput;
    [SerializeField] private TMP_InputField rotZInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button resetButton; // Optional: reset to original
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Transform Preview Settings")]
    [SerializeField] private bool updateInRealtime = true;
    [SerializeField] private float updateDelay = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    [Header("Gizmo Integration")]
    [SerializeField] private bool enableGizmo = true;
    [SerializeField] private RuntimeTransformGizmo gizmo;

    // Data storage
    private int currentFrameId;
    private ImageData currentImageData;
    private ArtFrame targetArtFrame;

    // Transform tracking
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private Vector3 newPosition;
    private Vector3 newRotation;
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
            Debug.Log("[TransformEditPopup] Initialized");
    }

    private void OnDestroy()
    {
        // Cleanup
        RemoveButtonListeners();
        EnablePlayerControllers();
    }

    #endregion

    #region Setup

    private void SetupButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveClicked);
            saveButton.interactable = false; // Disable initially
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
            saveButton.onClick.RemoveListener(OnSaveClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(OnResetClicked);
    }

    private void SetupInputFields()
    {
        // Setup position inputs
        SetupTransformInputField(posXInput, OnPositionInputChanged);
        SetupTransformInputField(posYInput, OnPositionInputChanged);
        SetupTransformInputField(posZInput, OnPositionInputChanged);

        // Setup rotation inputs
        SetupTransformInputField(rotXInput, OnRotationInputChanged);
        SetupTransformInputField(rotYInput, OnRotationInputChanged);
        SetupTransformInputField(rotZInput, OnRotationInputChanged);

        // Disable frame ID input (read-only)
        if (frameIdInput != null)
        {
            frameIdInput.interactable = false;
        }
    }

    private void SetupTransformInputField(TMP_InputField inputField, Action<string> onChangeAction)
    {
        if (inputField == null)
            return;

        // Add listener for value change
        inputField.onValueChanged.AddListener((value) => onChangeAction(value));

        // Setup formatting
        inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Hiển thị popup với ImageData và ArtFrame
    /// </summary>
    public void Show(ImageData imageData, ArtFrame artFrame)
    {
        if (imageData == null || artFrame == null)
        {
            Debug.LogError("[TransformEditPopup] Cannot show: Invalid data or frame");
            return;
        }

        currentImageData = imageData;
        currentFrameId = imageData.frameUse;
        targetArtFrame = artFrame;

        // Store original transform
        originalPosition = artFrame.transform.position;
        originalRotation = artFrame.transform.eulerAngles;

        // Set current values as new values
        newPosition = originalPosition;
        newRotation = originalRotation;

        // Populate UI
        PopulateUI();

        // Show popup
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        // ✅ GET GIZMO COMPONENT
        RuntimeTransformGizmo gizmo = artFrame.GetComponent<RuntimeTransformGizmo>();

        if (gizmo != null)
        {
            Debug.Log($"[TransformEditPopup] ✅ Found gizmo component on {artFrame.gameObject.name}");

            // ✅ SUBSCRIBE TO EVENTS
            gizmo.OnTransformChanged += OnGizmoTransformChanged;
            Debug.Log($"[TransformEditPopup] ✅ Subscribed to gizmo events");

            // ✅ ACTIVATE GIZMO (QUAN TRỌNG!)
            gizmo.Activate();
            Debug.Log($"[TransformEditPopup] ✅ Gizmo activated");
        }
        else
        {
            Debug.LogError($"[TransformEditPopup] ❌ Gizmo component NOT FOUND on {artFrame.gameObject.name}!");
        }

        // Disable player controllers
        DisablePlayerControllers();

        if (showDebug)
            Debug.Log($"[TransformEditPopup] Showing for frame {currentFrameId}");
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

        // ✅ Deactivate gizmo và unsubscribe
        if (targetArtFrame != null)
        {
            RuntimeTransformGizmo gizmo = targetArtFrame.GetComponent<RuntimeTransformGizmo>();
            if (gizmo != null)
            {
                gizmo.OnTransformChanged -= OnGizmoTransformChanged;
                gizmo.Deactivate();

                if (showDebug)
                    Debug.Log($"[TransformEditPopup] ✅ Gizmo deactivated and unsubscribed");
            }
        }

        // Enable player controllers
        EnablePlayerControllers();

        // Call callback if registered
        onHideCallback?.Invoke();
        onHideCallback = null;

        if (showDebug)
            Debug.Log("[TransformEditPopup] Hidden");
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
        // Đặt cờ để tránh cập nhật transform khi khởi tạo
        isPopulating = true;

        // Set frame ID (read-only)
        if (frameIdInput != null)
        {
            frameIdInput.text = currentFrameId.ToString();
        }

        // Populate input fields
        UpdateInputFieldsFromTransform(originalPosition, originalRotation);

        // Reset status
        UpdateStatus("Ready to edit transform");

        // Reset changes flag
        hasChanges = false;
        UpdateSaveButtonState(false);

        // Tắt cờ để cho phép cập nhật transform
        isPopulating = false;
    }

    private void UpdateInputFieldsFromTransform(Vector3 position, Vector3 rotation)
    {
        // Update position input fields
        if (posXInput != null) posXInput.text = position.x.ToString("F2");
        if (posYInput != null) posYInput.text = position.y.ToString("F2");
        if (posZInput != null) posZInput.text = position.z.ToString("F2");

        // Update rotation input fields
        if (rotXInput != null) rotXInput.text = rotation.x.ToString("F2");
        if (rotYInput != null) rotYInput.text = rotation.y.ToString("F2");
        if (rotZInput != null) rotZInput.text = rotation.z.ToString("F2");
    }

    #endregion

    #region Input Handlers

    private void OnPositionInputChanged(string value)
    {
        if (posXInput == null || posYInput == null || posZInput == null || targetArtFrame == null)
            return;

        // Không xử lý khi đang khởi tạo giá trị
        if (isPopulating)
            return;

        // Try to parse all position values
        if (float.TryParse(posXInput.text, out float x) &&
            float.TryParse(posYInput.text, out float y) &&
            float.TryParse(posZInput.text, out float z))
        {
            newPosition = new Vector3(x, y, z);

            // Check for changes
            bool positionChanged = Vector3.Distance(newPosition, originalPosition) > 0.001f;
            bool rotationChanged = Vector3.Distance(newRotation, originalRotation) > 0.001f;

            UpdateSaveButtonState(positionChanged || rotationChanged);

            // Update transform in realtime if enabled
            if (updateInRealtime && Time.time > nextUpdateTime)
            {
                targetArtFrame.transform.position = newPosition;
                nextUpdateTime = Time.time + updateDelay;

                if (showDebug)
                    Debug.Log($"[TransformEditPopup] Realtime position update: {newPosition}");
            }
        }
    }

    private void OnRotationInputChanged(string value)
    {
        if (rotXInput == null || rotYInput == null || rotZInput == null || targetArtFrame == null)
            return;

        // Không xử lý khi đang khởi tạo giá trị
        if (isPopulating)
            return;

        // Try to parse all rotation values
        if (float.TryParse(rotXInput.text, out float x) &&
            float.TryParse(rotYInput.text, out float y) &&
            float.TryParse(rotZInput.text, out float z))
        {
            newRotation = new Vector3(x, y, z);

            // Check for changes
            bool positionChanged = Vector3.Distance(newPosition, originalPosition) > 0.001f;
            bool rotationChanged = Vector3.Distance(newRotation, originalRotation) > 0.001f;

            UpdateSaveButtonState(positionChanged || rotationChanged);

            // Update transform in realtime if enabled
            if (updateInRealtime && Time.time > nextUpdateTime)
            {
                targetArtFrame.transform.eulerAngles = newRotation;
                nextUpdateTime = Time.time + updateDelay;

                if (showDebug)
                    Debug.Log($"[TransformEditPopup] Realtime rotation update: {newRotation}");
            }
        }
    }

    #endregion

    #region Button Handlers

    private void OnSaveClicked()
    {
        if (currentImageData == null || targetArtFrame == null)
        {
            UpdateStatus("Error: No image data or frame reference");
            return;
        }

        if (!hasChanges)
        {
            UpdateStatus("No changes to save");
            return;
        }

        UpdateStatus("Saving transform...");

        if (showDebug)
        {
            Debug.Log($"[TransformEditPopup] Saving transform:\n" +
                      $"  Frame: {currentFrameId}\n" +
                      $"  Position: {newPosition}\n" +
                      $"  Rotation: {newRotation}");
        }

        // Disable save button while saving
        if (saveButton != null)
            saveButton.interactable = false;

        // Save transform to server via APIManager
        APIManager.Instance.UpdateTransform(
            currentFrameId,
            newPosition,
            newRotation,
            OnSaveComplete
        );
    }

    private void OnCancelClicked()
    {
        if (targetArtFrame != null)
        {
            // Restore original transform
            targetArtFrame.transform.position = originalPosition;
            targetArtFrame.transform.eulerAngles = originalRotation;

            if (showDebug)
            {
                Debug.Log($"[TransformEditPopup] Cancelled changes, restored transform");
            }
        }

        // Close popup
        Hide();
    }

    private void OnResetClicked()
    {
        if (targetArtFrame == null)
            return;

        // Đặt cờ để tránh cập nhật transform khi đặt lại giá trị
        isPopulating = true;

        // Reset to original transform
        targetArtFrame.transform.position = originalPosition;
        targetArtFrame.transform.eulerAngles = originalRotation;

        // Update input fields
        newPosition = originalPosition;
        newRotation = originalRotation;
        UpdateInputFieldsFromTransform(originalPosition, originalRotation);

        // Reset changes flag
        UpdateSaveButtonState(false);

        // Tắt cờ để cho phép cập nhật transform
        isPopulating = false;

        UpdateStatus("Reset to original transform");

        if (showDebug)
            Debug.Log("[TransformEditPopup] Reset to original transform");
    }

    #endregion

    #region Save Callback

    private void OnSaveComplete(bool success, string message)
    {
        if (success)
        {
            UpdateStatus("Transform saved successfully!");

            if (showDebug)
                Debug.Log("[TransformEditPopup] Transform saved successfully");

            // Update original values
            originalPosition = newPosition;
            originalRotation = newRotation;

            // Clear cache và refresh frame trong ArtManager
            if (ArtManager.Instance != null)
            {
                ArtManager.Instance.RefreshFrame(currentFrameId, (sprite, data) =>
                {
                    if (showDebug)
                        Debug.Log($"[TransformEditPopup] Frame {currentFrameId} refreshed in cache");
                });
            }

            // Close popup after delay
            Invoke(nameof(Hide), 1f);
        }
        else
        {
            UpdateStatus($"Error: {message}");
            Debug.LogError($"[TransformEditPopup] Save failed: {message}");

            // Re-enable save button
            if (saveButton != null)
                saveButton.interactable = true;
        }
    }

    #endregion

    #region UI Updates

    private void UpdateSaveButtonState(bool hasChanges)
    {
        this.hasChanges = hasChanges;

        if (saveButton != null)
        {
            saveButton.interactable = hasChanges;
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (showDebug)
            Debug.Log($"[TransformEditPopup] Status: {message}");
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
                    Debug.Log($"[TransformEditPopup] Disabled PlayerController: {controller.gameObject.name}");
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
                    Debug.Log($"[TransformEditPopup] Enabled PlayerController: {controller.gameObject.name}");
            }
        }
    }

    #endregion
    private void OnGizmoTransformChanged(Vector3 position, Vector3 rotation)
    {
        // Đặt cờ để tránh trigger input change events
        isPopulating = true;

        // Update new values
        newPosition = position;
        newRotation = rotation;

        // Update input fields
        UpdateInputFieldsFromTransform(position, rotation);

        // Check for changes
        bool positionChanged = Vector3.Distance(newPosition, originalPosition) > 0.001f;
        bool rotationChanged = Vector3.Distance(newRotation, originalRotation) > 0.001f;

        UpdateSaveButtonState(positionChanged || rotationChanged);

        // Tắt cờ
        isPopulating = false;

        if (showDebug)
            Debug.Log($"[TransformEditPopup] Gizmo changed transform - Pos: {position}, Rot: {rotation}");
    }
}