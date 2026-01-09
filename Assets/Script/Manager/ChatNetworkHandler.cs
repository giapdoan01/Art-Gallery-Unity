using System;
using UnityEngine;

public class ChatNetworkHandler : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private int maxMessageLength = 500;
    [SerializeField] private float rateLimitSeconds = 1f;

    // Events
    public event Action<string, string, long> OnChatMessageReceived;
    public event Action<string> OnChatError;

    // Singleton
    public static ChatNetworkHandler Instance { get; private set; }

    // Rate limiting
    private float lastMessageTime = 0f;

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
        // Subscribe to adapter - FIX: Sử dụng tên event mới
        if (ChatNetworkAdapter.Instance != null)
        {
            ChatNetworkAdapter.Instance.OnChatReceived += OnMessageReceived;
        }
        else
        {
            LogError("ChatNetworkAdapter not found!");
        }
    }

    private void OnDestroy()
    {
        if (ChatNetworkAdapter.Instance != null)
        {
            ChatNetworkAdapter.Instance.OnChatReceived -= OnMessageReceived;
        }
    }

    #region Receive Messages

    private void OnMessageReceived(string username, string message, long timestamp)
    {
        Log($"💬 [{username}]: {message}");
        OnChatMessageReceived?.Invoke(username, message, timestamp);
    }

    #endregion

    #region Send Messages

    public void SendChatMessage(string message)
    {
        // Validate
        if (!ValidateMessage(message, out string error))
        {
            LogError(error);
            OnChatError?.Invoke(error);
            return;
        }

        // Rate limiting
        if (Time.time - lastMessageTime < rateLimitSeconds)
        {
            string rateLimitError = $"Please wait {rateLimitSeconds} seconds between messages";
            LogError(rateLimitError);
            OnChatError?.Invoke(rateLimitError);
            return;
        }

        try
        {
            if (ChatNetworkAdapter.Instance != null)
            {
                ChatNetworkAdapter.Instance.SendChatMessage(message);
                lastMessageTime = Time.time;
                Log($"✅ Sent: {message}");
            }
            else
            {
                throw new Exception("ChatNetworkAdapter not found");
            }
        }
        catch (Exception ex)
        {
            LogError($"Error sending message: {ex.Message}");
            OnChatError?.Invoke($"Error: {ex.Message}");
        }
    }

    #endregion

    #region Validation

    private bool ValidateMessage(string message, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(message))
        {
            error = "Empty message";
            return false;
        }

        string trimmed = message.Trim();

        if (trimmed.Length > maxMessageLength)
        {
            error = $"Message too long (max {maxMessageLength} characters)";
            return false;
        }

        return true;
    }

    #endregion

    #region Logging

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ChatNetworkHandler] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[ChatNetworkHandler] {message}");
    }

    #endregion
}
