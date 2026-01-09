using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

/// <summary>
/// Model3DCreator - Quản lý việc tạo mới Model3D trong scene
/// Tương tự ArtFrameCreator nhưng cho Model3D
/// ✅ FIXED: Correctly calls OpenForCreate() for new models
/// </summary>
public class Model3DCreator : MonoBehaviour
{
    private static Model3DCreator _instance;
    public static Model3DCreator Instance => _instance;

    [Header("Prefab References")]
    [SerializeField] private GameObject model3DPrefab;

    [Header("Settings")]
    [SerializeField] private Vector3 defaultSpawnPosition = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector3 defaultRotation = Vector3.zero;
    [SerializeField] private float defaultScale = 1f;
    [SerializeField] private float spawnOffsetDistance = 3f;
    [SerializeField] private bool spawnInFrontOfCamera = true;

    [Header("Auto-naming")]
    [SerializeField] private string defaultNamePrefix = "Model3D_";
    [SerializeField] private bool autoIncrementName = true;

    [Header("Parent Container")]
    [SerializeField] private Transform model3DContainer;

    [Header("Events")]
    public UnityEvent<GameObject> OnModel3DCreated;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private int modelCounter = 0;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    private void Start()
    {
        // Find container if not assigned
        if (model3DContainer == null)
        {
            GameObject containerObj = GameObject.Find("Model3DContainer");
            if (containerObj != null)
            {
                model3DContainer = containerObj.transform;
            }
            else
            {
                // Create container
                containerObj = new GameObject("Model3DContainer");
                model3DContainer = containerObj.transform;
            }
        }

        // Count existing models
        modelCounter = model3DContainer.childCount;
    }

    /// <summary>
    /// Tạo Model3D mới và mở popup để nhập thông tin
    /// ✅ FIXED: Calls OpenForCreate() instead of Show()
    /// </summary>
    public void CreateNewModel3D()
    {
        if (model3DPrefab == null)
        {
            Debug.LogError("[Model3DCreator] Model3D prefab is not assigned!");
            return;
        }

        // Generate name
        string modelName = autoIncrementName 
            ? $"{defaultNamePrefix}{modelCounter:000}" 
            : defaultNamePrefix;

        // Calculate spawn position
        Vector3 spawnPosition = CalculateSpawnPosition();

        // Instantiate prefab
        GameObject newModel = Instantiate(model3DPrefab, spawnPosition, Quaternion.Euler(defaultRotation));
        newModel.name = modelName;
        newModel.transform.localScale = Vector3.one * defaultScale;

        // Set parent
        if (model3DContainer != null)
        {
            newModel.transform.SetParent(model3DContainer);
        }

        modelCounter++;

        if (showDebug)
        {
            Debug.Log($"[Model3DCreator] Created Model3D: {modelName} at position {spawnPosition}");
        }

        // Invoke event
        OnModel3DCreated?.Invoke(newModel);

        // ✅ FIX: Open popup in CREATE mode (NOT EDIT mode!)
        OpenEditPopupForNewModel(newModel);
    }

    /// <summary>
    /// Mở popup để nhập thông tin cho model mới
    /// ✅ FIXED: Uses OpenForCreate() for new models
    /// ✅ FIXED: Removed non-existent method calls
    /// </summary>
    private void OpenEditPopupForNewModel(GameObject modelObject)
    {
        if (Model3DEditPopup.Instance == null)
        {
            Debug.LogError("[Model3DCreator] Model3DEditPopup.Instance is null!");
            return;
        }

        if (showDebug)
        {
            Debug.Log($"[Model3DCreator] ✅ Opening CREATE popup for new model: {modelObject.name}");
        }

        // ✅ FIX: Call OpenForCreate() (NOT Show()!)
        Model3DEditPopup.Instance.OpenForCreate((success, modelData) =>
        {
            if (success && modelData != null)
            {
                if (showDebug)
                {
                    Debug.Log($"[Model3DCreator] ✅ Model created successfully with ID: {modelData.id}");
                    Debug.Log($"[Model3DCreator] Model URL: {modelData.url}");
                }

                // Update GameObject name with actual name from server
                modelObject.name = modelData.name;

                // ✅ REMOVED: SetModelData() - doesn't exist
                // If you need to store data, do it differently:
                // Option 1: Add a simple component to store ID
                // Option 2: Use GameObject.name to store ID
                // Option 3: Keep a Dictionary<GameObject, string> mapping

                // Store model ID in GameObject name for reference
                // Format: "ModelName_ID"
                modelObject.name = $"{modelData.name}_{modelData.id}";

                // ✅ Clear cache only (LoadAllModels doesn't exist)
                if (Model3DManager.Instance != null)
                {
                    Model3DManager.Instance.ClearCache();
                    // Cache will be refreshed automatically on next access
                }

                if (showDebug)
                {
                    Debug.Log($"[Model3DCreator] GameObject renamed to: {modelObject.name}");
                }
            }
            else
            {
                // User cancelled or failed - destroy the temporary GameObject
                if (showDebug)
                {
                    Debug.Log($"[Model3DCreator] ❌ Model creation cancelled or failed, destroying GameObject");
                }

                Destroy(modelObject);
                modelCounter--; // Rollback counter
            }
        });
    }

    /// <summary>
    /// Tính toán vị trí spawn
    /// </summary>
    private Vector3 CalculateSpawnPosition()
    {
        if (!spawnInFrontOfCamera)
        {
            return defaultSpawnPosition;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[Model3DCreator] Camera.main not found, using default position");
            return defaultSpawnPosition;
        }

        // Get camera position and forward direction
        Vector3 cameraPosition = mainCamera.transform.position;
        Vector3 cameraForward = mainCamera.transform.forward;

        // Flatten forward vector (ignore Y)
        cameraForward.y = 0;
        cameraForward.Normalize();

        // Calculate spawn position
        Vector3 spawnPosition = cameraPosition + cameraForward * spawnOffsetDistance;
        spawnPosition.y = defaultSpawnPosition.y; // Use default Y height

        return spawnPosition;
    }

    /// <summary>
    /// Tạo Model3D tại vị trí cụ thể
    /// </summary>
    public GameObject CreateModel3DAtPosition(Vector3 position, string customName = null)
    {
        if (model3DPrefab == null)
        {
            Debug.LogError("[Model3DCreator] Model3D prefab is not assigned!");
            return null;
        }

        string modelName = string.IsNullOrEmpty(customName)
            ? (autoIncrementName ? $"{defaultNamePrefix}{modelCounter:000}" : defaultNamePrefix)
            : customName;

        GameObject newModel = Instantiate(model3DPrefab, position, Quaternion.Euler(defaultRotation));
        newModel.name = modelName;
        newModel.transform.localScale = Vector3.one * defaultScale;

        if (model3DContainer != null)
        {
            newModel.transform.SetParent(model3DContainer);
        }

        modelCounter++;

        if (showDebug)
        {
            Debug.Log($"[Model3DCreator] Created Model3D: {modelName} at position {position}");
        }

        OnModel3DCreated?.Invoke(newModel);

        return newModel;
    }

    /// <summary>
    /// Reset counter
    /// </summary>
    public void ResetCounter()
    {
        modelCounter = 0;
        if (showDebug)
        {
            Debug.Log("[Model3DCreator] Counter reset to 0");
        }
    }

    /// <summary>
    /// Get current counter value
    /// </summary>
    public int GetCurrentCounter()
    {
        return modelCounter;
    }
}
