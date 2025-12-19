using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Settings")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float height = 2f;
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Mouse Control")]
    [SerializeField] private float mouseSensitivity = 3f;
    
    [Header("Camera Mode")]
    [SerializeField] private bool firstPersonMode = false;
    [SerializeField] private float firstPersonHeightOffset = 1.6f; // Chiều cao của camera từ gốc người chơi
    
    private float currentYaw = 0f;
    private float currentPitch = 20f;

    private bool isDragging = false;
    private Vector2 lastMousePosition;

    private void Start()
    {
        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
        }
    }

    public void ToggleCameraMode()
    {
        firstPersonMode = !firstPersonMode;
        
        if (firstPersonMode)
        {
            currentPitch = 0f;
        }
        else
        {
            currentPitch = 20f;
        }
    }

    public void SetCameraMode(bool isFirstPerson)
    {
        if (firstPersonMode != isFirstPerson)
        {
            firstPersonMode = isFirstPerson;
            
            if (firstPersonMode)
            {
                currentPitch = 0f;
            }
            else
            {
                currentPitch = 20f;
            }
        }
    }

    public bool IsFirstPersonMode()
    {
        return firstPersonMode;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        HandleInput();
        UpdateCamera();
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector2 currentMousePosition = Input.mousePosition;
            Vector2 delta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;

            currentYaw += delta.x * mouseSensitivity * Time.deltaTime * 10f;
            currentPitch -= delta.y * mouseSensitivity * Time.deltaTime * 10f;

            // Giới hạn pitch khác nhau cho first-person và third-person
            if (firstPersonMode)
            {
                // First-person pitch limits - cho phép nhìn lên/xuống nhiều hơn
                currentPitch = Mathf.Clamp(currentPitch, -80f, 80f);
            }
            else
            {
                // Third-person pitch limits - như cũ
                currentPitch = Mathf.Clamp(currentPitch, -20f, 60f);
            }
        }
    }

    private void UpdateCamera()
    {
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);

        if (firstPersonMode)
        {
            
            // First-person camera position (at player's head)
            Vector3 firstPersonPosition = target.position + Vector3.up * firstPersonHeightOffset;
            
            // Đặt vị trí camera trực tiếp - không sử dụng Lerp để đồng bộ với người chơi
            transform.position = firstPersonPosition;
            
            // Chỉ làm mượt góc quay của camera, không làm mượt vị trí
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, smoothSpeed * Time.deltaTime);
        }
        else
        {
            // Third-person camera position
            Vector3 targetPosition = target.position + Vector3.up * height;
            Vector3 direction = rotation * Vector3.back;
            Vector3 desiredPosition = targetPosition + direction * distance;

            // Giữ lại smoothing cho góc nhìn thứ ba
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.LookAt(targetPosition);
        }
    }
}