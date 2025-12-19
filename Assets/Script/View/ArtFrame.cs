using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class ArtFrame : MonoBehaviour
{
    [Header("Frame Settings")]
    [SerializeField] private int frameId;
    [SerializeField] private string frameName;

    [Header("References")]
    [SerializeField] private MeshRenderer quadRenderer;

    [Header("Loading")]
    [SerializeField] private Texture2D loadingTexture;
    [SerializeField] private bool loadOnStart = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false; // Tắt debug để giảm log spam

    private ArtFrameData frameData;
    private Material currentMaterial;
    private bool isSubscribed = false;

    private void Awake()
    {
        if (quadRenderer == null)
        {
            quadRenderer = GetComponent<MeshRenderer>();
        }
    }

    private void Start()
    {
        SubscribeToManager();

        if (loadOnStart)
        {
            LoadArtwork();
        }
    }

    private void SubscribeToManager()
    {
        if (!isSubscribed && ArtManager.Instance != null)
        {
            ArtManager.Instance.OnImageUpdated += OnImageUpdatedFromManager;
            ArtManager.Instance.OnImageLoadError += OnImageLoadErrorFromManager;
            isSubscribed = true;
        }
    }

    private void UnsubscribeFromManager()
    {
        if (isSubscribed && ArtManager.Instance != null)
        {
            ArtManager.Instance.OnImageUpdated -= OnImageUpdatedFromManager;
            ArtManager.Instance.OnImageLoadError -= OnImageLoadErrorFromManager;
            isSubscribed = false;
        }
    }

    public void LoadArtwork(bool forceRefresh = false)
    {
        if (frameId <= 0)
        {
            Debug.LogError($"[ArtFrame] Frame ID không hợp lệ: {frameId}", this);
            return;
        }

        ShowLoadingTexture();
        ArtManager.Instance.GetImageForFrame(frameId, OnArtworkLoaded, forceRefresh);
    }

    private void OnArtworkLoaded(Sprite sprite)
    {
        if (sprite == null)
        {
            Debug.LogError($"[ArtFrame] Không thể tải ảnh cho frame {frameId}", this);
            return;
        }

        if (frameData == null)
        {
            frameData = new ArtFrameData(frameId, frameName);
        }
        
        frameData.sprite = sprite;
        ApplyArtwork(sprite);
    }

    private void ApplyArtwork(Sprite sprite)
    {
        if (sprite == null) return;

        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
        }

        currentMaterial = new Material(Shader.Find("Unlit/Texture"));
        currentMaterial.mainTexture = sprite.texture;

        if (frameData != null)
        {
            frameData.material = currentMaterial;
        }

        if (quadRenderer != null)
        {
            quadRenderer.material = currentMaterial;
        }
    }

    private void ShowLoadingTexture()
    {
        if (loadingTexture != null && quadRenderer != null)
        {
            Material loadingMat = new Material(Shader.Find("Unlit/Texture"));
            loadingMat.mainTexture = loadingTexture;
            quadRenderer.material = loadingMat;
        }
    }

    public void ClearArtwork()
    {
        ArtManager.Instance.ClearImageFromCache(frameId);

        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
            currentMaterial = null;
        }

        frameData = null;

        if (quadRenderer != null)
        {
            ShowLoadingTexture();
        }
    }

    public void ReloadArtwork(bool forceRefresh = false)
    {
        ClearArtwork();
        LoadArtwork(forceRefresh);
    }

    private void OnImageUpdatedFromManager(int updatedFrameId, Sprite newSprite)
    {
        if (updatedFrameId == frameId && newSprite != null)
        {
            if (showDebug) Debug.Log($"[ArtFrame] Auto update frame {frameId}", this);
            
            if (frameData == null)
            {
                frameData = new ArtFrameData(frameId, frameName);
            }
            frameData.sprite = newSprite;
            
            ApplyArtwork(newSprite);
        }
    }

    private void OnImageLoadErrorFromManager(int errorFrameId, string errorMessage)
    {
        if (errorFrameId == frameId)
        {
            Debug.LogError($"[ArtFrame] Error frame {frameId}: {errorMessage}", this);
        }
    }

    public int FrameId => frameId;
    public string FrameName => frameName;
    public ArtFrameData FrameData => frameData;
    public bool IsLoaded => frameData != null && frameData.IsLoaded();
    public bool IsLoading => ArtManager.Instance.IsImageLoading(frameId);

    private void OnDestroy()
    {
        UnsubscribeFromManager();

        if (currentMaterial != null)
        {
            Destroy(currentMaterial);
            currentMaterial = null;
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(frameName))
        {
            frameName = gameObject.name;
        }

        if (quadRenderer == null)
        {
            quadRenderer = GetComponent<MeshRenderer>();
        }
    }

    private void OnEnable()
    {
        SubscribeToManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromManager();
    }
}