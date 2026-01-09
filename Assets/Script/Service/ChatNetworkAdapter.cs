using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class ChatNetworkAdapter : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Events
    public event Action<string, string, long> OnChatReceived;

    // Singleton
    public static ChatNetworkAdapter Instance { get; private set; }

    // Reference to NetworkManager room
    private object currentRoom;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnConnected += OnNetworkConnected;
            NetworkManager.Instance.OnDisconnected += OnNetworkDisconnected;

            if (NetworkManager.Instance.IsConnected)
            {
                OnNetworkConnected();
            }
        }
        else
        {
            LogError("NetworkManager not found!");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnConnected -= OnNetworkConnected;
            NetworkManager.Instance.OnDisconnected -= OnNetworkDisconnected;
        }
    }

    #region Network Events

    private void OnNetworkConnected()
    {
        Log("✅ Network connected - Setting up chat listener");
        SetupChatListener();
    }

    private void OnNetworkDisconnected()
    {
        Log("❌ Network disconnected");
        currentRoom = null;
    }

    #endregion

    #region Chat Listener Setup

    private void SetupChatListener()
    {
        try
        {
            // Get room từ NetworkManager
            var roomField = typeof(NetworkManager).GetField("room", 
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (roomField == null)
            {
                LogError("Room field not found");
                return;
            }

            currentRoom = roomField.GetValue(NetworkManager.Instance);

            if (currentRoom == null)
            {
                LogError("Room is null");
                return;
            }

            Log($"Room type: {currentRoom.GetType().Name}");

            // ✅ Tìm OnMessage method với signature đúng
            var methods = currentRoom.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            MethodInfo targetMethod = null;
            
            foreach (var method in methods)
            {
                if (method.Name == "OnMessage" && method.IsGenericMethod)
                {
                    var parameters = method.GetParameters();
                    
                    if (parameters.Length == 2 && 
                        parameters[0].ParameterType == typeof(string))
                    {
                        targetMethod = method;
                        Log($"Found OnMessage method with {parameters.Length} parameters");
                        break;
                    }
                }
            }

            if (targetMethod == null)
            {
                LogError("OnMessage<T>(string, Action<T>) not found");
                return;
            }

            // Make generic method
            var genericMethod = targetMethod.MakeGenericMethod(typeof(Dictionary<string, object>));

            // Create callback
            Action<Dictionary<string, object>> callback = HandleChatMessageFromServer;

            // Invoke
            genericMethod.Invoke(currentRoom, new object[] { "chatMessage", callback });

            Log("✅ Chat listener setup complete");
        }
        catch (Exception ex)
        {
            LogError($"Failed to setup chat listener: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void HandleChatMessageFromServer(Dictionary<string, object> data)
    {
        try
        {
            string username = data.ContainsKey("username") ? data["username"].ToString() : "Unknown";
            string message = data.ContainsKey("message") ? data["message"].ToString() : "";
            long timestamp = data.ContainsKey("timestamp") 
                ? Convert.ToInt64(data["timestamp"]) 
                : DateTimeOffset.Now.ToUnixTimeMilliseconds();

            Log($"💬 [{username}]: {message}");

            OnChatReceived?.Invoke(username, message, timestamp);
        }
        catch (Exception ex)
        {
            LogError($"Error processing chat message: {ex.Message}");
        }
    }

    #endregion

    #region Send Chat Message

    public async void SendChatMessage(string message)
    {
        if (currentRoom == null)
        {
            LogError("Room not connected");
            throw new Exception("Not connected to room");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            LogError("Empty message");
            return;
        }

        try
        {
            // ✅ Tìm Send method với signature đúng
            var methods = currentRoom.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            MethodInfo sendMethod = null;
            
            foreach (var method in methods)
            {
                if (method.Name == "Send" && !method.IsGenericMethod)
                {
                    var parameters = method.GetParameters();
                    
                    // Tìm Send(string, object)
                    if (parameters.Length == 2 && 
                        parameters[0].ParameterType == typeof(string) &&
                        parameters[1].ParameterType == typeof(object))
                    {
                        sendMethod = method;
                        Log($"Found Send method with {parameters.Length} parameters");
                        break;
                    }
                }
            }

            if (sendMethod == null)
            {
                LogError("Send(string, object) method not found");
                return;
            }

            var messageData = new Dictionary<string, object>
            {
                { "message", message.Trim() }
            };

            // Call room.Send("chat", messageData)
            var task = (System.Threading.Tasks.Task)sendMethod.Invoke(currentRoom, 
                new object[] { "chat", messageData });

            await task;

            Log($"✅ Sent: {message}");
        }
        catch (Exception ex)
        {
            LogError($"Error sending chat: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    #endregion

    #region Logging

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ChatNetworkAdapter] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[ChatNetworkAdapter] {message}");
    }

    #endregion
}
