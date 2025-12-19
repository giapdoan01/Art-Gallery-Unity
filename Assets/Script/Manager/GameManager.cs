using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class GameManager : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string loginSceneName = "MenuScene";
    
    [Header("Debug")]
    [SerializeField] private bool showDebug = true;
    
    [Header("Settings")]
    [SerializeField] private float disconnectTimeout = 3f;
    [SerializeField] private GameObject moveButtonGuide;
    [SerializeField] private float moveButtonGuideDisplayTime = 10f; // Thời gian hiển thị hướng dẫn

    public static GameManager Instance { get; private set; }
    
    public event Action OnGameLeft;
    
    private NetworkManager networkManager;
    private bool isLeavingGame = false;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Get reference to NetworkManager
        networkManager = FindObjectOfType<NetworkManager>();
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager not found in scene!");
        }
    }
    
    private void Start()
    {
        // Bắt đầu coroutine để xử lý hiển thị moveButtonGuide
        StartCoroutine(HideMoveButtonGuideAfterDelay());
    }
    
    private IEnumerator HideMoveButtonGuideAfterDelay()
    {
        if (moveButtonGuide != null)
        {
            // Đảm bảo moveButtonGuide hiển thị ngay từ đầu
            moveButtonGuide.SetActive(true);
            
            if (showDebug) Debug.Log($"Hiển thị hướng dẫn nút di chuyển trong {moveButtonGuideDisplayTime} giây");
            
            // Đợi trong khoảng thời gian cài đặt
            yield return new WaitForSeconds(moveButtonGuideDisplayTime);
            
            // Ẩn moveButtonGuide sau khi hết thời gian
            moveButtonGuide.SetActive(false);
            
            if (showDebug) Debug.Log("Đã ẩn hướng dẫn nút di chuyển");
        }
    }
    
    public void LeaveGame()
    {
        // Ngăn chặn việc gọi LeaveGame nhiều lần
        if (isLeavingGame)
        {
            if (showDebug) Debug.Log("Đang trong quá trình rời game, vui lòng đợi...");
            return;
        }
        
        isLeavingGame = true;
        StartCoroutine(LeaveGameCoroutine());
    }
    
    private IEnumerator LeaveGameCoroutine()
    {
        if (showDebug) Debug.Log("Bắt đầu quá trình rời game...");
        
        if (networkManager != null && networkManager.IsConnected)
        {
            // Lưu sessionId hiện tại
            string sessionId = networkManager.SessionId;
            if (showDebug) Debug.Log($"SessionId hiện tại: {sessionId}");
            
            // Đánh dấu player đã bị xóa khỏi danh sách theo dõi
            if (!string.IsNullOrEmpty(sessionId))
            {
                if (showDebug) Debug.Log($"Đánh dấu player {sessionId} đã bị xóa");
                networkManager.MarkPlayerRemoved(sessionId);
            }
            
            // Thiết lập biến để theo dõi sự kiện ngắt kết nối
            bool disconnectCompleted = false;
            Action onDisconnectHandler = null;
            
            onDisconnectHandler = () => {
                if (showDebug) Debug.Log("Sự kiện ngắt kết nối đã được kích hoạt");
                disconnectCompleted = true;
                
                // Hủy đăng ký sự kiện sau khi đã nhận được
                if (networkManager != null)
                {
                    networkManager.OnDisconnected -= onDisconnectHandler;
                }
            };
            
            // Đăng ký để theo dõi sự kiện ngắt kết nối
            networkManager.OnDisconnected += onDisconnectHandler;
            
            // Gọi ngắt kết nối
            if (showDebug) Debug.Log("Gọi NetworkManager.Disconnect()");
            networkManager.Disconnect();
            
            // Đợi cho đến khi ngắt kết nối hoàn tất hoặc timeout
            float startTime = Time.time;
            while (!disconnectCompleted && Time.time - startTime < disconnectTimeout)
            {
                if (showDebug && (int)((Time.time - startTime) * 2) % 2 == 0)
                {
                    Debug.Log($"Đang đợi ngắt kết nối... ({(Time.time - startTime):F1}s)");
                }
                yield return null;
            }
            
            // Nếu hết timeout mà chưa nhận được sự kiện
            if (!disconnectCompleted)
            {
                Debug.LogWarning($"Ngắt kết nối timeout sau {disconnectTimeout} giây!");
                
                // Hủy đăng ký sự kiện nếu timeout
                if (networkManager != null)
                {
                    networkManager.OnDisconnected -= onDisconnectHandler;
                }
            }
            else
            {
                if (showDebug) Debug.Log("Ngắt kết nối hoàn tất");
            }
            
            // Xóa dữ liệu phiên làm việc
            ClearPlayerSession();
        }
        else
        {
            if (showDebug) Debug.Log("NetworkManager không tồn tại hoặc không kết nối");
        }
        
        // Phát sự kiện đã rời game
        if (showDebug) Debug.Log("Gọi sự kiện OnGameLeft");
        OnGameLeft?.Invoke();
        
        // Đợi một frame để đảm bảo mọi việc đã hoàn tất
        yield return null;
        
        // Chuyển về màn hình đăng nhập
        if (showDebug) Debug.Log($"Chuyển đến scene {loginSceneName}");
        SceneManager.LoadScene(loginSceneName);
        
        // Đảm bảo đợi cho scene mới load xong
        yield return new WaitForEndOfFrame();
        
        // Reset flag
        isLeavingGame = false;
        if (showDebug) Debug.Log("Quá trình rời game hoàn tất");
    }
    
    private void ClearPlayerSession()
    {
        if (showDebug) Debug.Log("Xóa dữ liệu phiên làm việc...");
        
        // Xóa dữ liệu phiên làm việc từ PlayerPrefs
        PlayerPrefs.DeleteKey("CurrentSessionId");
        PlayerPrefs.DeleteKey("LastPosition");
        // Không xóa AvatarURL vì đó là tùy chọn của người dùng
        
        // Đảm bảo lưu các thay đổi
        PlayerPrefs.Save();
        
        if (showDebug) Debug.Log("Đã xóa dữ liệu phiên làm việc");
    }
    
    public void OnLeaveButtonClicked()
    {
        if (showDebug) Debug.Log("Nút Leave được nhấn");
        LeaveGame();
    }
}