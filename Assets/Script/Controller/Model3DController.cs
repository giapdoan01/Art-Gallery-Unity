using UnityEngine;

/// <summary>
/// Controller - Quản lý dữ liệu và logic của Model3D
/// </summary>
public class Model3DController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Model3DView view;

    [Header("Transform Sync")]
    [SerializeField] private bool syncTransformFromServer = true;
    [SerializeField] private bool syncRotation = true;
    [SerializeField] private bool syncPosition = true;
    [SerializeField] private bool syncScale = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private Model3DData modelData;

    private void Awake()
    {
        // Auto-assign view
        if (view == null)
        {
            view = GetComponent<Model3DView>();
        }
    }

    /// <summary>
    /// Initialize với model data từ API
    /// </summary>
    public void Initialize(Model3DData data)
    {
        modelData = data;
        
        // Set name
        gameObject.name = $"Model3D_{data.name}_{data.id}";

        // Apply transform từ server nếu enabled
        if (syncTransformFromServer)
        {
            ApplyTransformFromData();
        }

        // Set model ID cho view
        if (view != null)
        {
            view.SetModelId(data.id, autoLoad: false); // Không auto load, sẽ load thủ công
        }

        if (showDebug)
            Debug.Log($"[Model3DController] Initialized: {data.name} (ID: {data.id})");
    }

    /// <summary>
    /// Áp dụng transform từ model data
    /// </summary>
    private void ApplyTransformFromData()
    {
        if (modelData == null) return;

        if (syncPosition)
            transform.position = modelData.position;

        if (syncRotation)
            transform.rotation = Quaternion.Euler(modelData.rotation);

        if (syncScale)
            transform.localScale = modelData.scale;

        if (showDebug)
            Debug.Log($"[Model3DController] Applied transform from data: pos={modelData.position}, rot={modelData.rotation}, scale={modelData.scale}");
    }

    /// <summary>
    /// Cập nhật model data
    /// </summary>
    public void UpdateModelData(Model3DData newData)
    {
        modelData = newData;
        
        if (syncTransformFromServer)
        {
            ApplyTransformFromData();
        }
    }

    /// <summary>
    /// Lấy model data
    /// </summary>
    public Model3DData GetModelData()
    {
        return modelData;
    }

    /// <summary>
    /// Lấy view
    /// </summary>
    public Model3DView GetView()
    {
        return view;
    }

    /// <summary>
    /// Load model thông qua view
    /// </summary>
    public void LoadModel()
    {
        if (view != null)
        {
            view.LoadModel();
        }
    }
}
