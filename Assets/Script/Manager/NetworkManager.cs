using UnityEngine;
using UnityEngine.SceneManagement;
using Colyseus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class NetworkManager : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "wss://gallery-server.onrender.com";
    [SerializeField] private string roomName = "gallery";

    [Header("Scene Settings")]
    [SerializeField] private string galleryMultiSceneName = "ArtGallery";
    [SerializeField] private string gallerySingleSceneName = "ArtGallerySingle";
    
    [Header("Sync Settings")]
    [SerializeField] private float networkUpdateRate = 0.1f; 
    [SerializeField] private float positionLerpSpeed = 10f;  
    [SerializeField] private float rotationLerpSpeed = 15f;  

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Singleton
    public static NetworkManager Instance { get; private set; }

    // Colyseus
    private ColyseusClient client;
    private ColyseusRoom<GalleryState> room;

    // Player Data
    public string PlayerName { get; set; }
    public int AvatarIndex { get; set; }

    // Events
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;
    public event Action<string, Player> OnPlayerJoined;
    public event Action<string, Player> OnPlayerLeft;
    
    // Events for smooth movement
    public event Action<string, Vector3, float> OnPlayerPositionUpdated; // sessionId, position, rotationY
    public event Action<string, string> OnPlayerAnimationUpdated; // sessionId, animationState

    // Properties
    public bool IsConnected => room != null;
    public string SessionId => room?.SessionId;
    public GalleryState State => room?.State;

    // Track spawned players
    private HashSet<string> spawnedPlayers = new HashSet<string>();
    private Dictionary<string, Player> previousPlayers = new Dictionary<string, Player>();
    
    // Track player movement for interpolation
    private Dictionary<string, Vector3> targetPositions = new Dictionary<string, Vector3>();
    private Dictionary<string, float> targetRotations = new Dictionary<string, float>();
    private Dictionary<string, long> lastUpdateTime = new Dictionary<string, long>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Phương thức mới để vào chế độ single player mà không cần kết nối
    public void LoadSinglePlayerMode(string playerName)
    {
        PlayerName = playerName;
        
        if (showDebug) Debug.Log($"Loading single player mode with name: {playerName}");
        
        // Lưu dữ liệu người chơi (nếu cần)
        PlayerPrefs.SetString("PlayerName", PlayerName);
        PlayerPrefs.Save();
        
        // Chuyển đến scene single player
        SceneManager.LoadScene(gallerySingleSceneName);
    }

    public void ConnectAndJoinRoom(string playerName, Action<bool, string> callback)
    {
        PlayerName = playerName;
        StartCoroutine(ConnectCoroutine(callback));
    }

    public void ConnectAndJoinRoom(string playerName, string avatarURL, Action<bool, string> callback)
    {
        PlayerName = playerName;
        
        if (!string.IsNullOrEmpty(avatarURL))
        {
            PlayerPrefs.SetString("AvatarURL", avatarURL);
            PlayerPrefs.Save();
        }
        
        StartCoroutine(ConnectCoroutine(callback));
    }

    private IEnumerator ConnectCoroutine(Action<bool, string> callback)
    {
        client = new ColyseusClient(serverUrl);

        string avatarURL = PlayerPrefs.GetString("AvatarURL", "");
        
        if (string.IsNullOrEmpty(avatarURL))
        {
            avatarURL = "https://models.readyplayer.me/6942207f4a15f239b0965d1f.glb";
        }

        if (showDebug) Debug.Log($"Connecting with avatar: {avatarURL}");

        var options = new Dictionary<string, object>
        {
            { "username", PlayerName },
            { "avatarURL", avatarURL } 
        };

        Task<ColyseusRoom<GalleryState>> connectTask = client.JoinOrCreate<GalleryState>(roomName, options);

        while (!connectTask.IsCompleted)
        {
            yield return null;
        }

        if (connectTask.IsFaulted)
        {
            string errorMsg = connectTask.Exception?.Message ?? "Unknown error";
            Debug.LogError($"Connection failed: {errorMsg}");
            OnError?.Invoke(errorMsg);
            callback?.Invoke(false, errorMsg);
            yield break;
        }

        room = connectTask.Result;

        if (showDebug) Debug.Log($"Connected! SessionId: {room.SessionId}");

        SetupRoomListeners();

        OnConnected?.Invoke();
        callback?.Invoke(true, "Connected successfully");

        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene(galleryMultiSceneName);
    }

    private void SetupRoomListeners()
    {
        room.OnLeave += (code) =>
        {
            if (showDebug) Debug.Log($"Left room: {code}");
            OnDisconnected?.Invoke();
        };

        room.OnError += (code, message) =>
        {
            Debug.LogError($"Room error: {code} - {message}");
            OnError?.Invoke(message);
        };
        
        // Đăng ký nhận sự kiện thay đổi vị trí từ server
        room.OnStateChange += (state, isFirstState) => {
            if (state.players != null) {
                state.players.ForEach((sessionId, player) => {
                    // Cập nhật thông tin mục tiêu di chuyển
                    Vector3 position = new Vector3(player.x, player.y, player.z);
                    targetPositions[sessionId] = position;
                    targetRotations[sessionId] = player.rotationY;
                    lastUpdateTime[sessionId] = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    
                    // Thông báo cho các listener về vị trí mới
                    OnPlayerPositionUpdated?.Invoke(sessionId, position, player.rotationY);
                    
                    // Thông báo về trạng thái animation
                    OnPlayerAnimationUpdated?.Invoke(sessionId, player.animationState);
                });
            }
        };

        StartCoroutine(MonitorPlayerChanges());
    }

    private IEnumerator MonitorPlayerChanges()
    {
        yield return new WaitForEndOfFrame();

        while (room != null && room.State != null)
        {
            if (room.State.players == null)
            {
                yield return new WaitForSeconds(networkUpdateRate);
                continue;
            }

            room.State.players.ForEach((sessionId, player) =>
            {
                if (!previousPlayers.ContainsKey(sessionId))
                {
                    // New player joined
                    if (showDebug)
                    {
                        Debug.Log($"========================================");
                        Debug.Log($"   New player detected:");
                        Debug.Log($"   SessionId: {sessionId}");
                        Debug.Log($"   Username: {player.username}");
                        Debug.Log($"   Avatar URL: {player.avatarURL}");
                        Debug.Log($"   My SessionId: {room.SessionId}");
                        Debug.Log($"   Is Me: {sessionId.Equals(room.SessionId)}");
                        Debug.Log($"   Already spawned: {spawnedPlayers.Contains(sessionId)}");
                        Debug.Log($"========================================");
                    }

                    previousPlayers[sessionId] = player;
                    
                    // Khởi tạo vị trí mục tiêu
                    Vector3 position = new Vector3(player.x, player.y, player.z);
                    targetPositions[sessionId] = position;
                    targetRotations[sessionId] = player.rotationY;
                    lastUpdateTime[sessionId] = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                    if (!spawnedPlayers.Contains(sessionId))
                    {
                        spawnedPlayers.Add(sessionId);
                        OnPlayerJoined?.Invoke(sessionId, player);
                    }
                    else
                    {
                        if (showDebug) Debug.Log($"Player {sessionId} already in spawnedPlayers, skipping");
                    }
                }
                else
                {
                    // Cập nhật vị trí mục tiêu cho người chơi đã tồn tại
                    Vector3 position = new Vector3(player.x, player.y, player.z);
                    targetPositions[sessionId] = position;
                    targetRotations[sessionId] = player.rotationY;
                    lastUpdateTime[sessionId] = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                }
            });

            var playersToRemove = new List<string>();
            foreach (var entry in previousPlayers)
            {
                bool stillExists = false;
                room.State.players.ForEach((sessionId, player) =>
                {
                    if (sessionId.Equals(entry.Key))
                    {
                        stillExists = true;
                    }
                });

                if (!stillExists)
                {
                    playersToRemove.Add(entry.Key);
                }
            }

            foreach (var sessionId in playersToRemove)
            {
                if (showDebug) Debug.Log($"Player removed: {sessionId}");
                
                Player removedPlayer = previousPlayers[sessionId];
                previousPlayers.Remove(sessionId);
                spawnedPlayers.Remove(sessionId);
                targetPositions.Remove(sessionId);
                targetRotations.Remove(sessionId);
                lastUpdateTime.Remove(sessionId);
                
                OnPlayerLeft?.Invoke(sessionId, removedPlayer);
            }

            yield return new WaitForSeconds(networkUpdateRate);
        }
    }
    
    // Lấy vị trí nội suy cho một player
    public Vector3 GetInterpolatedPosition(string sessionId, Vector3 currentPosition)
    {
        if (!targetPositions.ContainsKey(sessionId))
            return currentPosition;
            
        // Nội suy từ vị trí hiện tại đến vị trí mục tiêu
        return Vector3.Lerp(currentPosition, targetPositions[sessionId], Time.deltaTime * positionLerpSpeed);
    }
    
    // Lấy góc quay nội suy cho một player
    public float GetInterpolatedRotation(string sessionId, float currentRotation)
    {
        if (!targetRotations.ContainsKey(sessionId))
            return currentRotation;
            
        // Nội suy từ góc quay hiện tại đến góc quay mục tiêu
        return Mathf.LerpAngle(currentRotation, targetRotations[sessionId], Time.deltaTime * rotationLerpSpeed);
    }

    public void CheckForNewPlayers()
    {
        if (room?.State?.players == null) return;

        if (showDebug)
        {
            Debug.Log($"   CheckForNewPlayers:");
            Debug.Log($"   My SessionId: {room.SessionId}");
            Debug.Log($"   Players in state: {room.State.players.Count}");
            Debug.Log($"   Already spawned: {string.Join(", ", spawnedPlayers)}");
        }

        room.State.players.ForEach((sessionId, player) =>
        {
            if (!spawnedPlayers.Contains(sessionId))
            {
                if (showDebug) Debug.Log($"New player detected in CheckForNewPlayers: {player.username} ({sessionId})");
                
                spawnedPlayers.Add(sessionId);
                previousPlayers[sessionId] = player;
                
                // Khởi tạo vị trí mục tiêu
                Vector3 position = new Vector3(player.x, player.y, player.z);
                targetPositions[sessionId] = position;
                targetRotations[sessionId] = player.rotationY;
                lastUpdateTime[sessionId] = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                
                OnPlayerJoined?.Invoke(sessionId, player);
            }
        });
    }

    public void MarkPlayerRemoved(string sessionId)
    {
        spawnedPlayers.Remove(sessionId);
        previousPlayers.Remove(sessionId);
        targetPositions.Remove(sessionId);
        targetRotations.Remove(sessionId);
        lastUpdateTime.Remove(sessionId);
        if (showDebug) Debug.Log($"Player marked as removed: {sessionId}");
    }

    public void SendPosition(Vector3 position, float rotationY)
    {
        SendMove(position.x, position.y, position.z, rotationY);
    }

    public void SendPosition(float x, float y, float z, float rotationY)
    {
        SendMove(x, y, z, rotationY);
    }

    public void SendMove(float x, float y, float z, float rotationY)
    {
        if (room != null)
        {
            // Gửi thông tin vị trí cho gallery room
            var message = new Dictionary<string, object>
            {
                { "x", x },
                { "y", y },
                { "z", z },
                { "rotationY", rotationY }
            };

            room.Send("move", message);
        }
    }

    public void SendAnimation(string animationState)
    {
        if (room != null)
        {
            // Gửi animation cho gallery room
            var message = new Dictionary<string, object>
            {
                { "animationState", animationState }
            };

            room.Send("animation", message);
        }
    }

    public void SendChat(string message)
    {
        if (room != null)
        {
            // Gửi chat message cho gallery room
            var chatMessage = new Dictionary<string, object>
            {
                { "message", message }
            };

            room.Send("chat", chatMessage);
        }
    }

    public void Disconnect()
    {
        if (room != null)
        {
            room.Leave();
            room = null;
        }
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}