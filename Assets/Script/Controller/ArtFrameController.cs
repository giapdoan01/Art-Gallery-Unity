using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

[RequireComponent(typeof(ArtFrame), typeof(Collider))]
public class ArtFrameController : MonoBehaviour
{
    [Header("Mouse Interaction")]
    [SerializeField] private float interactionDistance = 10f;
    [SerializeField] private GameObject selectionIndicator;
    [SerializeField] private bool includeChildColliders = true;
    [SerializeField] private bool ignoreUIClicks = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private ArtFrame artFrame;
    private Collider artFrameCollider;
    private bool isSelected = false;
    private static ArtFrameController currentSelectedFrame;

    // Properties
    public bool IsSelected => isSelected;
    public ArtFrame ArtFrameComponent => artFrame;

    private void Awake()
    {
        artFrame = GetComponent<ArtFrame>();
        artFrameCollider = GetComponent<Collider>();

        // Đảm bảo có collider cho tương tác
        if (artFrameCollider == null)
        {
            Debug.LogError("[ArtFrameController] Thiếu component Collider. Thêm BoxCollider.", this);
            artFrameCollider = gameObject.AddComponent<BoxCollider>();
        }

        // Thiết lập chỉ báo lựa chọn nếu có
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(false);
        }
    }

    private void Update()
    {
        // Kiểm tra tương tác bằng chuột
        CheckMouseInteraction();
    }

    private void CheckMouseInteraction()
    {
        // Chỉ xử lý khi click chuột trái
        if (!Input.GetMouseButtonDown(0)) return;

        // Kiểm tra xem có đang click vào UI 
        if (ignoreUIClicks && IsPointerOverUI())
        {
            if (showDebug)
            {
                Debug.Log($"[ArtFrameController] Bỏ qua click vì đang trên UI");
            }
            return;
        }

        // Kiểm tra xem có đang click vào buttons của ArtFrame 
        if (artFrame != null && artFrame.IsPointerOverButtons())
        {
            if (showDebug)
            {
                Debug.Log($"[ArtFrameController] Bỏ qua click vì đang trên buttons của ArtFrame");
            }
            return;
        }

        // Raycast để kiểm tra click vào frame
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Bỏ qua UI layer khi raycast
        int layerMask = ~LayerMask.GetMask("UI");

        if (Physics.Raycast(ray, out hit, interactionDistance, layerMask))
        {
            // Kiểm tra xem có hit vào frame này không
            bool hitThisFrame = false;

            if (hit.collider.gameObject == gameObject)
            {
                hitThisFrame = true;
            }
            else if (includeChildColliders && hit.collider.transform.IsChildOf(transform))
            {
                hitThisFrame = true;
            }

            if (hitThisFrame)
            {
                if (showDebug)
                {
                    Debug.Log($"[ArtFrameController] Click vào frame {artFrame.FrameId}");
                }

                OnFrameClicked();
            }
            else
            {
                // Click vào object khác, bỏ chọn frame này
                if (isSelected)
                {
                    Deselect();
                }
            }
        }
        else
        {
            // Click vào không gian trống, bỏ chọn
            if (isSelected)
            {
                Deselect();
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem pointer có đang trên UI không (ngoại trừ buttons của ArtFrame)
    /// </summary>
    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        // Kiểm tra cơ bản
        if (!EventSystem.current.IsPointerOverGameObject()) return false;

        // Kiểm tra chi tiết để loại trừ buttons của ArtFrame
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null) continue;

            // Nếu là button container của ArtFrame này, không tính là UI
            if (artFrame != null && artFrame.ButtonContainer != null)
            {
                if (result.gameObject.transform.IsChildOf(artFrame.ButtonContainer.transform) ||
                    result.gameObject == artFrame.ButtonContainer)
                {
                    continue;
                }
            }

            // Nếu là UI khác, return true
            if (result.gameObject.layer == LayerMask.NameToLayer("UI"))
            {
                return true;
            }

            // Kiểm tra có Canvas component không
            Canvas canvas = result.gameObject.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                // Nếu là canvas của ArtFrame, bỏ qua
                if (artFrame != null && artFrame.ButtonContainer != null)
                {
                    Canvas artFrameCanvas = artFrame.ButtonContainer.GetComponentInParent<Canvas>();
                    if (artFrameCanvas != null && canvas == artFrameCanvas)
                    {
                        continue;
                    }
                }

                return true;
            }
        }

        return false;
    }

    private void OnFrameClicked()
    {
        // Toggle selection
        if (isSelected)
        {
            Deselect();
        }
        else
        {
            Select();
        }
    }

    public void Select()
    {
        // Bỏ chọn frame đang được chọn trước đó
        if (currentSelectedFrame != null && currentSelectedFrame != this)
        {
            currentSelectedFrame.Deselect();
        }

        isSelected = true;
        currentSelectedFrame = this;

        // Hiển thị indicator
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(true);
        }

        // Hiển thị buttons của ArtFrame
        if (artFrame != null)
        {
            artFrame.ForceShowButtons();
        }

        if (showDebug)
        {
            Debug.Log($"[ArtFrameController] Frame {artFrame.FrameId} được chọn");
        }

        // Gọi event hoặc callback nếu cần
        OnFrameSelected();
    }

    public void Deselect()
    {
        isSelected = false;

        if (currentSelectedFrame == this)
        {
            currentSelectedFrame = null;
        }

        // Ẩn indicator
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(false);
        }

        if (showDebug)
        {
            Debug.Log($"[ArtFrameController] Frame {artFrame.FrameId} bỏ chọn");
        }

        // Gọi event hoặc callback nếu cần
        OnFrameDeselected();
    }

    public void ToggleSelection()
    {
        if (isSelected)
        {
            Deselect();
        }
        else
        {
            Select();
        }
    }

    /// <summary>
    /// Được gọi khi frame được chọn
    /// </summary>
    protected virtual void OnFrameSelected()
    {
        // Override trong subclass nếu cần
    }

    /// <summary>
    /// Được gọi khi frame bỏ chọn
    /// </summary>
    protected virtual void OnFrameDeselected()
    {
        // Override trong subclass nếu cần
    }

    #region Public Methods

    /// <summary>
    /// Lấy frame đang được chọn hiện tại
    /// </summary>
    public static ArtFrameController GetCurrentSelectedFrame()
    {
        return currentSelectedFrame;
    }

    /// <summary>
    /// Bỏ chọn tất cả frames
    /// </summary>
    public static void DeselectAll()
    {
        if (currentSelectedFrame != null)
        {
            currentSelectedFrame.Deselect();
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Hiển thị vùng tương tác
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);

        // Hiển thị trạng thái
        if (Application.isPlaying && isSelected)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, transform.localScale * 1.1f);
        }
    }

    #endregion
}
