using UnityEngine;

/// <summary>
/// UIBillboard - UI trượt trên bề mặt hình cầu, luôn ở vị trí gần player nhất
/// và xoay về phía player
/// </summary>
public class UIBillboard : MonoBehaviour
{
    [Header("Billboard Settings")]
    [SerializeField] private Transform uiTransform; // UI object cần di chuyển và xoay
    [SerializeField] private float sphereRadius = 1f; // Bán kính hình cầu
    [SerializeField] private bool hideWhenPlayerFar = true; // Ẩn UI khi player quá xa
    [SerializeField] private float maxVisibleDistance = 10f; // Khoảng cách tối đa để hiện UI

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f; // Tốc độ di chuyển (smooth)
    [SerializeField] private float rotationSpeed = 10f; // Tốc độ xoay (smooth)

    [Header("Rotation Settings")]
    [SerializeField] private Vector3 rotationOffset = Vector3.zero; // Offset góc xoay nếu cần

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;
    [SerializeField] private bool showGizmos = true;

    private Camera mainCamera;
    private GameObject uiGameObject;
    private Vector3 targetPosition; // Vị trí mục tiêu trên mặt cầu
    private Quaternion targetRotation; // Rotation mục tiêu

    #region Unity Lifecycle

    private void Awake()
    {
        if (uiTransform != null)
        {
            uiGameObject = uiTransform.gameObject;
        }
        else
        {
            Debug.LogWarning("[UIBillboard] UI Transform is not assigned!", this);
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError("[UIBillboard] Camera.main not found!", this);
        }

        // Set vị trí ban đầu
        if (uiTransform != null)
        {
            uiTransform.position = transform.position + Vector3.up * sphereRadius;
        }
    }

    private void LateUpdate()
    {
        if (mainCamera == null || uiTransform == null)
            return;

        // Tính khoảng cách đến player
        float distanceToPlayer = Vector3.Distance(transform.position, mainCamera.transform.position);

        // Kiểm tra có hiển thị UI không
        if (hideWhenPlayerFar)
        {
            bool shouldShow = distanceToPlayer <= maxVisibleDistance;
            if (uiGameObject.activeSelf != shouldShow)
            {
                uiGameObject.SetActive(shouldShow);
                
                if (showDebug)
                {
                    Debug.Log($"[UIBillboard] UI {(shouldShow ? "shown" : "hidden")}. Distance: {distanceToPlayer:F2}m", this);
                }
            }

            if (!shouldShow)
                return;
        }

        // Tính vị trí trên mặt cầu gần player nhất
        CalculateTargetPositionOnSphere();

        // Tính rotation về phía player
        CalculateTargetRotation();

        // Smooth move và rotate
        uiTransform.position = Vector3.Lerp(
            uiTransform.position, 
            targetPosition, 
            Time.deltaTime * moveSpeed
        );

        uiTransform.rotation = Quaternion.Slerp(
            uiTransform.rotation, 
            targetRotation, 
            Time.deltaTime * rotationSpeed
        );
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Tính vị trí trên mặt cầu gần player nhất
    /// </summary>
    private void CalculateTargetPositionOnSphere()
    {
        // Vector từ tâm (object này) đến player
        Vector3 directionToPlayer = (mainCamera.transform.position - transform.position).normalized;

        // Vị trí trên mặt cầu theo hướng player
        targetPosition = transform.position + directionToPlayer * sphereRadius;

        if (showDebug)
        {
            Debug.DrawLine(transform.position, targetPosition, Color.green);
            Debug.DrawLine(targetPosition, mainCamera.transform.position, Color.cyan);
        }
    }

    /// <summary>
    /// Tính rotation để UI quay về phía player
    /// </summary>
    private void CalculateTargetRotation()
    {
        // Vector từ UI đến player
        Vector3 directionToCamera = (mainCamera.transform.position - targetPosition).normalized;

        // Tạo rotation nhìn về player
        targetRotation = Quaternion.LookRotation(directionToCamera);

        // Apply rotation offset nếu có
        if (rotationOffset != Vector3.zero)
        {
            targetRotation *= Quaternion.Euler(rotationOffset);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Set UI transform runtime
    /// </summary>
    public void SetUITransform(Transform ui)
    {
        uiTransform = ui;
        if (ui != null)
        {
            uiGameObject = ui.gameObject;
        }
    }

    /// <summary>
    /// Set sphere radius
    /// </summary>
    public void SetSphereRadius(float radius)
    {
        sphereRadius = radius;
    }

    /// <summary>
    /// Force show/hide UI
    /// </summary>
    public void SetUIVisible(bool visible)
    {
        if (uiGameObject != null)
        {
            uiGameObject.SetActive(visible);
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showGizmos)
            return;

        // Vẽ hình cầu (bề mặt UI trượt)
        Gizmos.color = new Color(0f, 1f, 1f, 0.2f); // Cyan transparent
        Gizmos.DrawWireSphere(transform.position, sphereRadius);

        // Vẽ max visible distance
        if (hideWhenPlayerFar)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.1f); // Yellow transparent
            Gizmos.DrawWireSphere(transform.position, maxVisibleDistance);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
            return;

        // Vẽ hình cầu solid
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f); // Cyan transparent
        Gizmos.DrawSphere(transform.position, sphereRadius);

        // Vẽ UI position hiện tại
        if (uiTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(uiTransform.position, Vector3.one * 0.1f);

            // Vẽ line từ tâm đến UI
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, uiTransform.position);
        }

        // Vẽ target position khi playing
        if (Application.isPlaying && mainCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPosition, 0.05f);
            
            // Vẽ line từ tâm đến target
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, targetPosition);
        }
    }

    #endregion
}
