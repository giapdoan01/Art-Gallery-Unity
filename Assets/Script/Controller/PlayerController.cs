using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerView))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Network")]
    [SerializeField] private float sendInterval = 0.1f;
    [SerializeField] private float lerpSpeed = 10f;
    
    private CharacterController characterController;
    private PlayerView playerView;
    private PlayerModel playerModel;
    private NetworkManager networkManager;
    private string playerSessionId;
    private Vector3 previousPosition;
    
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerView = GetComponent<PlayerView>();
        networkManager = NetworkManager.Instance;
    }
    
    public void Initialize(string sessionId, Player state, bool isLocal)
    {
        // Lưu lại sessionId để dùng cho nội suy
        this.playerSessionId = sessionId;
        
        // Tạo model với dữ liệu khởi tạo
        playerModel = new PlayerModel(sessionId, state, isLocal, moveSpeed, rotationSpeed)
        {
            SendInterval = sendInterval,
            LerpSpeed = lerpSpeed
        };
        
        // Khởi tạo view
        playerView.Initialize(state.username, isLocal);
        
        // Đặt vị trí ban đầu
        transform.position = new Vector3(state.x, state.y, state.z);
        transform.rotation = Quaternion.Euler(0, state.rotationY, 0);
        previousPosition = transform.position;
        
        // Đăng ký các sự kiện từ model
        playerModel.OnSpeedChanged += playerView.UpdateAnimationSpeed;
    }
    
    private void Update()
    {
        if (playerModel == null) return;
        
        if (playerModel.IsLocalPlayer)
        {
            HandleLocalPlayerMovement();
        }
        else
        {
            HandleRemotePlayerMovement();
        }
    }
    
    private void HandleLocalPlayerMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;
        
        // Tính toán hướng di chuyển theo camera
        Vector3 cameraForward = mainCamera.transform.forward;
        Vector3 cameraRight = mainCamera.transform.right;
        
        cameraForward.y = 0;
        cameraRight.y = 0;
        cameraForward.Normalize();
        cameraRight.Normalize();
        
        Vector3 moveDirection = cameraForward * vertical + cameraRight * horizontal;
        
        if (moveDirection.magnitude > 0.1f)
        {
            // Xoay nhân vật theo hướng di chuyển
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Lerp(
                transform.rotation, 
                targetRotation, 
                playerModel.RotationSpeed * Time.deltaTime
            );
            
            // Di chuyển nhân vật
            characterController.Move(moveDirection * playerModel.MoveSpeed * Time.deltaTime);
            
            // Cập nhật animation
            playerModel.SetMovementSpeed(1.0f);
            
            // Gửi animation nếu đang di chuyển
            if (Time.time - playerModel.LastSendTime > playerModel.SendInterval * 0.5f)
            {
                NetworkManager.Instance.SendAnimation("walk");
            }
        }
        else
        {
            // Dừng animation khi không di chuyển
            playerModel.SetMovementSpeed(0.0f);
            
            // Gửi animation khi đứng yên
            if (Time.time - playerModel.LastSendTime > playerModel.SendInterval * 0.5f)
            {
                NetworkManager.Instance.SendAnimation("idle");
            }
        }
        
        // Áp dụng trọng lực
        characterController.Move(Vector3.down * 9.81f * Time.deltaTime);
        
        // Gửi vị trí lên mạng nếu đã đến thời điểm
        if (Time.time - playerModel.LastSendTime > playerModel.SendInterval)
        {
            // Cập nhật vị trí trong model
            playerModel.UpdateLocalPosition(transform.position, transform.eulerAngles.y);
            
            // Gửi vị trí lên mạng
            NetworkManager.Instance.SendPosition(transform.position, transform.eulerAngles.y);
            
            // Cập nhật thời gian gửi cuối
            playerModel.LastSendTime = Time.time;
        }
    }
    
    private void HandleRemotePlayerMovement()
    {
        if (playerModel == null || networkManager == null) return;
        
        // Lưu vị trí trước khi nội suy
        previousPosition = transform.position;
        
        // Sử dụng nội suy vị trí từ NetworkManager thay vì trực tiếp từ PlayerModel
        if (!string.IsNullOrEmpty(playerSessionId) && networkManager != null)
        {
            // Nội suy vị trí sử dụng NetworkManager mới
            transform.position = networkManager.GetInterpolatedPosition(playerSessionId, transform.position);
            
            // Nội suy góc quay sử dụng NetworkManager mới
            float currentRotationY = transform.eulerAngles.y;
            float newRotationY = networkManager.GetInterpolatedRotation(playerSessionId, currentRotationY);
            transform.rotation = Quaternion.Euler(0, newRotationY, 0);
            
            // Tính toán tốc độ di chuyển dựa trên thay đổi vị trí
            Vector3 movement = transform.position - previousPosition;
            float horizontalMovement = new Vector2(movement.x, movement.z).magnitude;
            
            // Đặt ngưỡng nhỏ hơn để phát hiện chuyển động chính xác hơn
            float movementThreshold = 0.001f;
            float targetSpeed = horizontalMovement > movementThreshold ? 1f : 0f;
            
            // Làm mịn chuyển đổi giữa trạng thái animation
            float currentSpeed = playerModel.MovementSpeed;
            float smoothSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime * 8f);
            
            if (Mathf.Abs(smoothSpeed - currentSpeed) > 0.01f)
            {
                playerModel.SetMovementSpeed(smoothSpeed);
            }
        }
        else
        {
            // Fallback nếu không có NetworkManager hoặc sessionId
            playerView.LerpToPosition(playerModel.NetworkPosition, playerModel.LerpSpeed);
            playerView.LerpToRotation(playerModel.NetworkRotation, playerModel.LerpSpeed);
            
            float speed = Vector3.Distance(transform.position, playerModel.NetworkPosition) > 0.1f ? 1f : 0f;
            playerModel.SetMovementSpeed(speed);
        }
    }
    
    public void OnAvatarLoaded(Animator avatarAnimator)
    {
        playerView.OnAvatarLoaded(avatarAnimator);
    }
    
    public void UpdateNetworkPosition(float x, float y, float z, float rotY)
    {
        if (playerModel != null)
        {
            playerModel.UpdateFromNetwork(x, y, z, rotY);
        }
    }
    
    private void OnDestroy()
    {
        // Hủy đăng ký các sự kiện
        if (playerModel != null)
        {
            playerModel.OnSpeedChanged -= playerView.UpdateAnimationSpeed;
        }
    }
}