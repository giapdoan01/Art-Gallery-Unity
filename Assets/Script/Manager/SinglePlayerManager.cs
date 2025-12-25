using UnityEngine;
using ReadyPlayerMe.Core;
using System.Collections;

public class SinglePlayerManager : MonoBehaviour
{
    [Header("Avatar Settings")]
    [SerializeField] private GameObject playerPrefab; // Prefab chứa RPMAvatarLoader
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float loadDelayTime = 0.5f; // Đợi 0.5 giây trước khi tải avatar

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private GameObject playerObject;
    private bool isPlayerSpawned = false;
    private AvatarObjectLoader directAvatarLoader; // Loader trực tiếp nếu cần

    private void Awake()
    {
        // Kiểm tra các tham số cần thiết
        if (playerPrefab == null)
        {
            Debug.LogError("[SinglePlayerManager] CRITICAL: Player prefab không được gán! Vui lòng gán prefab vào SinglePlayerManager.");
        }

        if (spawnPoint == null)
        {
            Debug.LogWarning("[SinglePlayerManager] Spawn point không được gán, sử dụng vị trí mặc định.");
        }

        // Tạo direct loader dự phòng
        directAvatarLoader = new AvatarObjectLoader();
        directAvatarLoader.OnCompleted += OnDirectAvatarLoadCompleted;
        directAvatarLoader.OnFailed += OnDirectAvatarLoadFailed;
    }

    private void Start()
    {
        Debug.Log("[SinglePlayerManager] Khởi tạo SinglePlayerManager...");
        
        // Chờ một chút trước khi tải avatar
        StartCoroutine(DelayedAvatarLoad());
    }

    private IEnumerator DelayedAvatarLoad()
    {
        if (showDebug) Debug.Log("[SinglePlayerManager] Đang chờ " + loadDelayTime + " giây trước khi tải avatar...");
        
        // Đợi một khoảng thời gian ngắn để đảm bảo scene đã tải hoàn toàn
        yield return new WaitForSeconds(loadDelayTime);

        if (showDebug) Debug.Log("[SinglePlayerManager] Bắt đầu tạo avatar player...");
        
        // Tải và tạo avatar
        SpawnPlayerAvatar();
    }

    private void SpawnPlayerAvatar()
    {
        // Đã tạo player rồi thì không tạo nữa
        if (isPlayerSpawned && playerObject != null)
        {
            if (showDebug) Debug.Log("[SinglePlayerManager] Player đã được tạo trước đó, bỏ qua.");
            return;
        }

        // Kiểm tra prefab
        if (playerPrefab == null)
        {
            Debug.LogError("[SinglePlayerManager] CRITICAL: Không thể tạo player vì prefab không được gán!");
            return;
        }

        // Lấy tên và avatar URL từ PlayerPrefs
        string playerName = PlayerPrefs.GetString("PlayerName", "Player");
        string avatarURL = PlayerPrefs.GetString("AvatarURL", "");

        if (string.IsNullOrEmpty(avatarURL))
        {
            Debug.LogWarning("[SinglePlayerManager] Không tìm thấy URL avatar trong PlayerPrefs! Sử dụng URL mặc định.");
            avatarURL = "https://models.readyplayer.me/6942207f4a15f239b0965d1f.glb"; // URL mặc định
        }

        if (showDebug)
        {
            Debug.Log("====== SINGLE PLAYER MODE ======");
            Debug.Log($"Tên người chơi: {playerName}");
            Debug.Log($"URL Avatar: {avatarURL}");
        }

        // Lấy vị trí spawn
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : new Vector3(0, 0, 0);
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        if (showDebug) Debug.Log($"[SinglePlayerManager] Tạo player tại vị trí: {spawnPosition}");

        // Tạo player từ prefab
        playerObject = Instantiate(playerPrefab, spawnPosition, spawnRotation);
        playerObject.name = $"LocalPlayer_{playerName}";

        // Gán tag Player cho object để dễ tìm
        playerObject.tag = "Player";

        // PHƯƠNG PHÁP 1: Sử dụng RPMAvatarLoader trên prefab
        RPMAvatarLoader avatarLoader = playerObject.GetComponent<RPMAvatarLoader>();
        if (avatarLoader != null)
        {
            if (showDebug) Debug.Log($"[SinglePlayerManager] Đã tìm thấy RPMAvatarLoader trên prefab. Tải avatar: {avatarURL}");

            // Thiết lập animator controller cho RPMAvatarLoader
            if (animatorController != null)
            {
                // Kiểm tra nếu animatorController có thể truy cập trực tiếp
                var field = typeof(RPMAvatarLoader).GetField("animatorController", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.NonPublic);
                
                if (field != null)
                {
                    field.SetValue(avatarLoader, animatorController);
                    if (showDebug) Debug.Log("[SinglePlayerManager] Đã gán Animator Controller thông qua reflection.");
                }
                else
                {
                    // Thử thiết lập thông qua SerializedObject
                    try
                    {
                        #if UNITY_EDITOR
                        UnityEditor.SerializedObject serializedObject = new UnityEditor.SerializedObject(avatarLoader);
                        UnityEditor.SerializedProperty property = serializedObject.FindProperty("animatorController");
                        if (property != null)
                        {
                            property.objectReferenceValue = animatorController;
                            serializedObject.ApplyModifiedProperties();
                            if (showDebug) Debug.Log("[SinglePlayerManager] Đã gán Animator Controller thông qua SerializedObject.");
                        }
                        #endif
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[SinglePlayerManager] Không thể gán Animator Controller qua SerializedObject: {ex.Message}");
                    }
                }
            }

            // Tải avatar
            StartCoroutine(LoadAvatarWithRetry(avatarLoader, avatarURL, 3));
            isPlayerSpawned = true;
        }
        else
        {
            Debug.LogError("[SinglePlayerManager] CRITICAL: RPMAvatarLoader không tìm thấy trên prefab! Sử dụng phương pháp tải trực tiếp.");
            
            // PHƯƠNG PHÁP 2: Tải trực tiếp avatar nếu không tìm thấy RPMAvatarLoader
            if (!string.IsNullOrEmpty(avatarURL))
            {
                if (showDebug) Debug.Log($"[SinglePlayerManager] Tải trực tiếp avatar từ URL: {avatarURL}");
                directAvatarLoader.LoadAvatar(avatarURL);
            }
        }

        // Thiết lập các component khác
        SetupPlayerComponents(playerObject);
    }

    // Phương thức thử tải avatar với số lần thử lại
    private IEnumerator LoadAvatarWithRetry(RPMAvatarLoader loader, string avatarUrl, int retryCount)
    {
        if (showDebug) Debug.Log($"[SinglePlayerManager] Bắt đầu tải avatar với {retryCount} lần thử...");
        
        int attempts = 0;
        bool success = false;

        while (!success && attempts < retryCount)
        {
            attempts++;
            if (showDebug) Debug.Log($"[SinglePlayerManager] Lần thử tải avatar thứ {attempts}...");
            
            // Kiểm tra trạng thái của loader
            if (loader.IsLoaded)
            {
                if (showDebug) Debug.Log("[SinglePlayerManager] Avatar đã được tải trước đó!");
                success = true;
                break;
            }
            
            if (loader.IsLoading)
            {
                if (showDebug) Debug.Log("[SinglePlayerManager] Avatar đang được tải, đợi...");
                yield return new WaitForSeconds(1.0f);
                continue;
            }

            // Tải avatar
            loader.LoadAvatar(avatarUrl);
            
            // Đợi một khoảng thời gian
            float waitTime = 0f;
            float maxWaitTime = 5f;
            
            while (waitTime < maxWaitTime)
            {
                if (loader.IsLoaded)
                {
                    if (showDebug) Debug.Log("[SinglePlayerManager] Avatar đã được tải thành công!");
                    success = true;
                    break;
                }
                
                if (!loader.IsLoading)
                {
                    if (showDebug) Debug.Log("[SinglePlayerManager] Avatar tải thất bại hoặc đã kết thúc.");
                    break;
                }
                
                waitTime += 0.5f;
                yield return new WaitForSeconds(0.5f);
            }
            
            if (success) break;
            
            if (showDebug) Debug.LogWarning($"[SinglePlayerManager] Lần thử {attempts} thất bại, đợi trước khi thử lại...");
            yield return new WaitForSeconds(1.0f);
        }
        
        if (!success)
        {
            Debug.LogError("[SinglePlayerManager] Không thể tải avatar sau nhiều lần thử. Thử phương pháp khác.");
            
            // Thử phương pháp tải trực tiếp nếu RPMAvatarLoader thất bại
            if (!string.IsNullOrEmpty(avatarUrl))
            {
                if (showDebug) Debug.Log($"[SinglePlayerManager] Tải trực tiếp avatar từ URL: {avatarUrl}");
                directAvatarLoader.LoadAvatar(avatarUrl);
            }
        }
    }

    // Callback khi tải avatar trực tiếp thành công
    private void OnDirectAvatarLoadCompleted(object sender, CompletionEventArgs args)
    {
        if (showDebug) Debug.Log("[SinglePlayerManager] Tải trực tiếp avatar thành công!");
        
        if (playerObject != null && args.Avatar != null)
        {
            // Đặt avatar vào đúng vị trí trên player
            args.Avatar.transform.SetParent(playerObject.transform);
            args.Avatar.transform.localPosition = Vector3.zero;
            args.Avatar.transform.localRotation = Quaternion.identity;
            args.Avatar.transform.localScale = Vector3.one;
            
            // Thiết lập Animator nếu có
            Animator avatarAnimator = args.Avatar.GetComponentInChildren<Animator>();
            if (avatarAnimator != null && animatorController != null)
            {
                avatarAnimator.runtimeAnimatorController = animatorController;
                if (showDebug) Debug.Log("[SinglePlayerManager] Đã gán Animator Controller cho avatar trực tiếp.");
            }
            
            // Thông báo cho PlayerController về avatar đã tải xong
            PlayerController controller = playerObject.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.OnAvatarLoaded(avatarAnimator);
                if (showDebug) Debug.Log("[SinglePlayerManager] Đã thông báo cho PlayerController về avatar mới.");
            }
        }
    }

    // Callback khi tải avatar trực tiếp thất bại
    private void OnDirectAvatarLoadFailed(object sender, FailureEventArgs args)
    {
        Debug.LogError($"[SinglePlayerManager] Tải trực tiếp avatar thất bại: {args.Type} - {args.Message}");
    }

    private void SetupPlayerComponents(GameObject player)
    {
        if (player == null) return;
        
        // Kiểm tra PlayerController và thiết lập
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            try
            {
                // Tìm phương thức InitializeSinglePlayer bằng reflection
                var method = controller.GetType().GetMethod("InitializeSinglePlayer", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.Public);
                
                if (method != null)
                {
                    if (showDebug) Debug.Log("[SinglePlayerManager] Gọi InitializeSinglePlayer bằng reflection");
                    method.Invoke(controller, null);
                }
                else
                {
                    // Fallback: sử dụng Initialize với thông số dành cho single player
                    if (showDebug) Debug.Log("[SinglePlayerManager] Không tìm thấy InitializeSinglePlayer, thử Initialize");
                    
                    // Tạo player dummy để khởi tạo
                    Player dummyPlayer = new Player
                    {
                        username = PlayerPrefs.GetString("PlayerName", "Player"),
                        avatarURL = PlayerPrefs.GetString("AvatarURL", "")
                    };
                    
                    // Gọi Initialize với tham số cho single player
                    controller.Initialize("local_player", dummyPlayer, true);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SinglePlayerManager] Lỗi khi khởi tạo PlayerController: {ex.Message}");
                
                // Nếu không thể dùng reflection, thử khởi tạo thông qua Interface
                try 
                {
                    // Gửi message để khởi tạo (phòng trường hợp controller đang implement method này)
                    controller.SendMessage("InitializeSinglePlayer", null, SendMessageOptions.DontRequireReceiver);
                    
                    if (showDebug) Debug.Log("[SinglePlayerManager] Đã gửi message InitializeSinglePlayer");
                }
                catch (System.Exception msgEx)
                {
                    Debug.LogError($"[SinglePlayerManager] Không thể gửi message InitializeSinglePlayer: {msgEx.Message}");
                }
            }
        }
        else
        {
            Debug.LogWarning("[SinglePlayerManager] PlayerController không tìm thấy trên player prefab!");
        }
        
        // Thiết lập camera theo dõi nếu cần
        SetupCamera(player.transform);
    }

    private void SetupCamera(Transform playerTransform)
    {
        // Tìm main camera
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // Kiểm tra các camera follow script phổ biến
            bool cameraSetup = false;
            
            // Kiểm tra CameraFollow component
            var cameraFollow = mainCamera.GetComponent("CameraFollow");
            if (cameraFollow != null)
            {
                // Sử dụng reflection để gọi SetTarget hoặc tương tự
                var setTargetMethod = cameraFollow.GetType().GetMethod("SetTarget");
                if (setTargetMethod != null)
                {
                    setTargetMethod.Invoke(cameraFollow, new object[] { playerTransform });
                    if (showDebug) Debug.Log("[SinglePlayerManager] Đã thiết lập camera follow target.");
                    cameraSetup = true;
                }
            }
            
            // Nếu không tìm thấy script theo dõi, tạo một basic script
            if (!cameraSetup)
            {
                if (showDebug) Debug.Log("[SinglePlayerManager] Không tìm thấy CameraFollow script, tạo mới...");
                
                // Tạo một GameObject mới để gắn script follow camera
                GameObject cameraRig = new GameObject("CameraRig");
                cameraRig.transform.position = mainCamera.transform.position;
                cameraRig.transform.rotation = mainCamera.transform.rotation;
                
                // Thiết lập camera là con của camera rig
                mainCamera.transform.SetParent(cameraRig.transform);
                
                // Tạo và thiết lập BasicCameraFollow
                BasicCameraFollow basicFollow = cameraRig.AddComponent<BasicCameraFollow>();
                basicFollow.target = playerTransform;
                basicFollow.offset = mainCamera.transform.position - playerTransform.position;
                basicFollow.smoothSpeed = 0.125f;
            }
        }
    }

    // Phương thức để lấy player object từ bên ngoài nếu cần
    public GameObject GetPlayerObject()
    {
        return playerObject;
    }
    
    private void OnDestroy()
    {
        if (directAvatarLoader != null)
        {
            directAvatarLoader.OnCompleted -= OnDirectAvatarLoadCompleted;
            directAvatarLoader.OnFailed -= OnDirectAvatarLoadFailed;
        }
    }
}

// Class hỗ trợ camera follow đơn giản
public class BasicCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 2, -5);
    public float smoothSpeed = 0.125f;

    void LateUpdate()
    {
        if (target == null) return;
        
        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
        
        transform.LookAt(target);
    }
}