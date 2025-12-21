using UnityEngine;

[RequireComponent(typeof(ArtFrame), typeof(Collider))]
public class ArtFrameController : MonoBehaviour
{
    [Header("Tùy chọn tương tác")]
    [SerializeField] private bool interactableInGame = true;
    [SerializeField] private float interactionDistance = 10f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private GameObject selectionIndicator;
    [SerializeField] private bool includeChildColliders = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private ArtFrame artFrame;
    private Collider artFrameCollider;
    private bool isSelected = false;
    private bool isPlayerNearby = false;
    private static ArtFrameController currentSelectedFrame;

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
        if (!interactableInGame) return;

        // Kiểm tra người chơi có gần không
        CheckPlayerProximity();

        // Xử lý tương tác click/touch
        HandleInteractions();
    }

    private void CheckPlayerProximity()
    {
        // Tìm người chơi trong scene
        PlayerController localPlayer = FindObjectOfType<PlayerController>();
        
        if (localPlayer != null)
        {
            float distance = Vector3.Distance(transform.position, localPlayer.transform.position);
            isPlayerNearby = distance <= interactionDistance;

            if (showDebug && isSelected)
            {
                Debug.DrawLine(transform.position, localPlayer.transform.position, isPlayerNearby ? Color.green : Color.red);
            }
        }
    }

    private void HandleInteractions()
    {
        // Tương tác click chuột
        if (Input.GetMouseButtonDown(0)) // Left click
        {
            TryInteractWithRaycast();
        }

        // Tương tác bấm phím khi gần
        if (isPlayerNearby && Input.GetKeyDown(interactionKey))
        {
            OnInteract();
        }
    }

    private void TryInteractWithRaycast()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            bool isHit = false;
            
            if (hit.collider == artFrameCollider)
            {
                // Hit trực tiếp vào collider của ArtFrame
                isHit = true;
            }
            else if (includeChildColliders)
            {
                // Kiểm tra xem collider bị hit có phải là con của GameObject này không
                Transform hitTransform = hit.collider.transform;
                if (hitTransform.IsChildOf(transform) && hitTransform != transform)
                {
                    // Hit vào collider của một trong các object con
                    isHit = true;
                    
                    if (showDebug)
                        Debug.Log($"[ArtFrameController] Hit vào collider con: {hit.collider.name}");
                }
            }

            if (isHit)
            {
                OnInteract();
                return;
            }
        }
        
        // Nếu không hit hoặc hit vào collider khác
        if (isSelected && currentSelectedFrame == this)
        {
            // Click vào chỗ khác, bỏ chọn frame này
            SetSelected(false);
            currentSelectedFrame = null;
        }
    }

    private void OnInteract()
    {
        if (showDebug) 
            Debug.Log($"[ArtFrameController] Tương tác với khung tranh: {artFrame.FrameName} (ID: {artFrame.FrameId})");

        // Bỏ chọn frame đã chọn trước đó
        if (currentSelectedFrame != null && currentSelectedFrame != this)
        {
            currentSelectedFrame.SetSelected(false);
        }

        // Đặt frame này là lựa chọn hiện tại
        currentSelectedFrame = this;
        SetSelected(true);

        // Lấy dữ liệu hình ảnh cho frame
        if (artFrame != null && artFrame.FrameId > 0)
        {
            // Yêu cầu dữ liệu hình ảnh từ APIManager
            APIManager.Instance.GetImageByFrame(artFrame.FrameId, OnGetImageByFrameComplete);
        }
    }

    private void OnGetImageByFrameComplete(bool success, ImageData imageData, string error)
    {
        if (!success || imageData == null)
        {
            Debug.LogError($"[ArtFrameController] Không lấy được dữ liệu hình ảnh cho frame {artFrame.FrameId}: {error}", this);
            SetSelected(false);
            return;
        }

        // Hiển thị popup chỉnh sửa
        if (ImageEditPopup.Instance != null)
        {
            // Hiển thị popup với dữ liệu hình ảnh
            ImageEditPopup.Instance.Show(imageData);
            
            // Đăng ký callback khi đóng popup
            ImageEditPopup.Instance.RegisterOnHideCallback(OnPopupClosed);
        }
        else
        {
            Debug.LogError("[ArtFrameController] ImageEditPopup.Instance là null! Đảm bảo nó tồn tại trong scene.", this);
            SetSelected(false);
        }
    }

    private void OnPopupClosed()
    {
        // Bỏ chọn frame này khi đóng popup
        SetSelected(false);
        
        // Xóa tham chiếu tĩnh nếu đây là frame đã được chọn
        if (currentSelectedFrame == this)
        {
            currentSelectedFrame = null;
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        // Cập nhật hiển thị chỉ báo lựa chọn
        if (selectionIndicator != null)
        {
            selectionIndicator.SetActive(selected);
        }

        // Có thể thêm hiệu ứng lựa chọn khác ở đây
    }

    public bool IsSelected()
    {
        return isSelected;
    }

    private void OnDrawGizmosSelected()
    {
        if (interactableInGame)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionDistance);
        }
    }

    // Hỗ trợ dọn dẹp khi đối tượng bị hủy
    private void OnDestroy()
    {
        // Xóa tham chiếu tĩnh nếu đây là frame đang được chọn
        if (currentSelectedFrame == this)
        {
            currentSelectedFrame = null;
        }
    }
}