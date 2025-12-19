using UnityEngine;
using TMPro;

public class PlayerView : MonoBehaviour
{
    [SerializeField] private Canvas nameTagCanvas;
    [SerializeField] private TextMeshProUGUI nameTagText;
    
    private Animator animator;
    private Transform cameraTransform;
    private bool isLocalPlayer;
    
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void Initialize(string username, bool isLocal)
    {
        this.isLocalPlayer = isLocal;
        
        // Thiết lập name tag
        if (nameTagText != null)
        {
            nameTagText.text = username;
        }
        
        // Xử lý hiển thị name tag dựa vào loại người chơi
        if (nameTagCanvas != null)
        {
            nameTagCanvas.gameObject.SetActive(!isLocal);
        }
        
        // Cài đặt camera follow nếu là local player
        if (isLocal)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
                CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
                if (cameraFollow == null)
                {
                    cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
                }
                cameraFollow.SetTarget(transform);
            }
        }
    }
    
    public void UpdateAnimationSpeed(float speed)
    {
        if (animator != null)
        {
            animator.SetFloat(SpeedHash, speed);
        }
    }
    
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
    }
    
    public void SetRotation(float rotationY)
    {
        transform.rotation = Quaternion.Euler(0, rotationY, 0);
    }
    
    public void LerpToPosition(Vector3 targetPosition, float lerpSpeed)
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * lerpSpeed);
    }
    
    public void LerpToRotation(Quaternion targetRotation, float lerpSpeed)
    {
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);
    }

    public void UpdateNameTagRotation()
    {
        if (nameTagCanvas != null && !isLocalPlayer && cameraTransform == null)
        {
            cameraTransform = Camera.main?.transform;
        }
        
        if (nameTagCanvas != null && !isLocalPlayer && cameraTransform != null)
        {
            nameTagCanvas.transform.LookAt(cameraTransform);
            nameTagCanvas.transform.Rotate(0, 180, 0);
        }
    }
    
    public void OnAvatarLoaded(Animator avatarAnimator)
    {
        this.animator = avatarAnimator;
    }
    
    public void Update()
    {
        UpdateNameTagRotation();
    }
}