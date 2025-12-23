using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Transform Preview Settings")]
    [SerializeField] private bool updateInRealtime = true;
    [SerializeField] private float updateDelay = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Data storage
    private int currentFrameId;
    private ImageData currentImageData;
    private ArtFrame targetArtFrame;
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private Vector3 newPosition;
    private Vector3 newRotation;
    private bool hasChanges = false;

    // Input handling
    private float nextUpdateTime;
    private PlayerController[] playerControllers;
    private System.Action onHideCallback;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        // Setup buttons
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveClicked);
            saveButton.interactable = false; // Disable initially
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        // Setup input fields
        SetupTransformInputField(posXInput, OnPositionInputChanged);
        SetupTransformInputField(posYInput, OnPositionInputChanged);
        SetupTransformInputField(posZInput, OnPositionInputChanged);
        SetupTransformInputField(rotXInput, OnRotationInputChanged);
        SetupTransformInputField(rotYInput, OnRotationInputChanged);
        SetupTransformInputField(rotZInput, OnRotationInputChanged);

        // Disable frame ID input (read-only)
        if (frameIdInput != null)
        {
            frameIdInput.interactable = false;
        }

        Hide();
    }

    private void SetupTransformInputField(TMP_InputField inputField, Action<string> onChangeAction)
    {
        if (inputField != null)
        {
            // Add listener for value change
            inputField.onValueChanged.AddListener((value) => onChangeAction(value));
            
            // Setup formatting to always show 2 decimal places
            inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        }
    }

    public void Show(ImageData imageData, ArtFrame artFrame)
    {
        if (imageData == null || artFrame == null)
        {
            Debug.LogError("[TransformEditPopup] Cannot show popup: Invalid image data or art frame");
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
        if (saveButton != null)
        {
            saveButton.interactable = false;
        }

        // Show popup
        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        // Disable all PlayerControllers to prevent movement while popup is open
        DisablePlayerControllers();

        if (showDebug) Debug.Log($"[TransformEditPopup] Showing popup for frame ID: {currentFrameId}");
    }

    public void Hide()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        // Enable player controllers
        EnablePlayerControllers();

        // Call callback if registered
        if (onHideCallback != null)
        {
            onHideCallback.Invoke();
            onHideCallback = null;
        }
    }

    public void RegisterOnHideCallback(System.Action callback)
    {
        onHideCallback = callback;
    }

    private void OnPositionInputChanged(string value)
    {
        // Only update if we have all input fields and a target frame
        if (posXInput == null || posYInput == null || posZInput == null || targetArtFrame == null) return;

        // Try to parse all position values
        if (float.TryParse(posXInput.text, out float x) &&
            float.TryParse(posYInput.text, out float y) &&
            float.TryParse(posZInput.text, out float z))
        {
            newPosition = new Vector3(x, y, z);
            
            // Check for changes
            bool positionChanged = Vector3.Distance(newPosition, originalPosition) > 0.001f;
            
            // Update save button state based on whether there are changes
            UpdateSaveButtonState(positionChanged || Vector3.Distance(newRotation, originalRotation) > 0.001f);

            // Update transform in realtime if enabled
            if (updateInRealtime && Time.time > nextUpdateTime)
            {
                targetArtFrame.transform.position = newPosition;
                nextUpdateTime = Time.time + updateDelay;
            }
        }
    }

    private void OnRotationInputChanged(string value)
    {
        // Only update if we have all input fields and a target frame
        if (rotXInput == null || rotYInput == null || rotZInput == null || targetArtFrame == null) return;

        // Try to parse all rotation values
        if (float.TryParse(rotXInput.text, out float x) &&
            float.TryParse(rotYInput.text, out float y) &&
            float.TryParse(rotZInput.text, out float z))
        {
            newRotation = new Vector3(x, y, z);
            
            // Check for changes
            bool rotationChanged = Vector3.Distance(newRotation, originalRotation) > 0.001f;
            
            // Update save button state based on whether there are changes
            UpdateSaveButtonState(rotationChanged || Vector3.Distance(newPosition, originalPosition) > 0.001f);

            // Update transform in realtime if enabled
            if (updateInRealtime && Time.time > nextUpdateTime)
            {
                targetArtFrame.transform.eulerAngles = newRotation;
                nextUpdateTime = Time.time + updateDelay;
            }
        }
    }

    private void UpdateSaveButtonState(bool hasChanges)
    {
        this.hasChanges = hasChanges;
        if (saveButton != null)
        {
            saveButton.interactable = hasChanges;
        }
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
            Debug.Log($"[TransformEditPopup] Saving transform - Frame: {currentFrameId}, " +
                     $"Position: {newPosition}, Rotation: {newRotation}");
        }

        // Get other data from current ImageData to preserve it
        string name = currentImageData.name ?? "Unnamed";
        string author = currentImageData.author ?? "";
        string description = currentImageData.description ?? "";

        // Save transform changes to server
        APIManager.Instance.UpdateImageByFrame(
            currentFrameId,
            name,
            author,
            description,
            newPosition,
            newRotation,
            null, // No image change
            OnUpdateComplete
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
                Debug.Log($"[TransformEditPopup] Cancelled changes, restored position to {originalPosition} " +
                         $"and rotation to {originalRotation}");
            }
        }
        
        // Close the popup
        Hide();
    }

    private void OnUpdateComplete(bool success, ImageData updatedData, string error)
    {
        if (success)
        {
            UpdateStatus("Transform saved successfully!");

            if (showDebug) Debug.Log("[TransformEditPopup] Transform update successful");

            // Update ArtManager cache
            if (ArtManager.Instance != null && updatedData != null)
            {
                ArtManager.Instance.ForceRefreshFrame(currentFrameId);
            }

            // Close popup after a short delay
            Invoke(nameof(Hide), 1f);
        }
        else
        {
            UpdateStatus($"Error: {error}");
            Debug.LogError($"[TransformEditPopup] Transform update failed: {error}");
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private void DisablePlayerControllers()
    {
        playerControllers = FindObjectsOfType<PlayerController>();
        foreach (var controller in playerControllers)
        {
            if (controller != null && controller.enabled)
            {
                controller.enabled = false;
                if (showDebug) Debug.Log("[TransformEditPopup] Disabled PlayerController: " + controller.gameObject.name);
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
                    if (showDebug) Debug.Log("[TransformEditPopup] Enabled PlayerController: " + controller.gameObject.name);
                }
            }
        }
    }

    private void OnDestroy()
    {
        // Remove listeners
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnSaveClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        // Enable player controllers
        EnablePlayerControllers();
    }
}