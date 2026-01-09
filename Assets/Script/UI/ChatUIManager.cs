using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChatUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField chatInput;
    [SerializeField] private Button sendButton;
    [SerializeField] private Transform chatContainer;
    [SerializeField] private GameObject chatMessagePrefab;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Settings")]
    [SerializeField] private int maxMessages = 50;

    [Header("Colors")]
    [SerializeField] private Color myMessageColor = new Color(0.3f, 0.8f, 1f);
    [SerializeField] private Color otherMessageColor = Color.white;
    [SerializeField] private Color systemMessageColor = new Color(1f, 1f, 0.3f);
    [SerializeField] private Color errorMessageColor = new Color(1f, 0.3f, 0.3f);

    private List<GameObject> messageObjects = new List<GameObject>();

    private void Start()
    {
        // Validate references
        if (chatInput == null) Debug.LogError("[ChatUIManager] ChatInput not assigned!");
        if (sendButton == null) Debug.LogError("[ChatUIManager] SendButton not assigned!");
        if (chatContainer == null) Debug.LogError("[ChatUIManager] ChatContainer not assigned!");
        if (chatMessagePrefab == null) Debug.LogError("[ChatUIManager] ChatMessagePrefab not assigned!");
        if (scrollRect == null) Debug.LogWarning("[ChatUIManager] ScrollRect not assigned - auto scroll disabled");

        // Setup buttons
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendMessage);
        }

        if (chatInput != null)
        {
            chatInput.onSubmit.AddListener((text) => SendMessage());
        }

        // Subscribe to chat events
        if (ChatNetworkHandler.Instance != null)
        {
            ChatNetworkHandler.Instance.OnChatMessageReceived += OnChatMessageReceived;
            ChatNetworkHandler.Instance.OnChatError += OnChatError;
        }
        else
        {
            Debug.LogError("[ChatUIManager] ChatNetworkHandler not found!");
        }

        // Welcome message
        DisplaySystemMessage("Chat connected!");
    }

    private void OnDestroy()
    {
        if (ChatNetworkHandler.Instance != null)
        {
            ChatNetworkHandler.Instance.OnChatMessageReceived -= OnChatMessageReceived;
            ChatNetworkHandler.Instance.OnChatError -= OnChatError;
        }
    }

    #region Send Message

    private void SendMessage()
    {
        if (chatInput == null || string.IsNullOrWhiteSpace(chatInput.text))
        {
            return;
        }

        string message = chatInput.text.Trim();

        if (ChatNetworkHandler.Instance != null)
        {
            ChatNetworkHandler.Instance.SendChatMessage(message);
        }
        else
        {
            Debug.LogError("[ChatUIManager] ChatNetworkHandler not found!");
        }

        chatInput.text = "";
        chatInput.ActivateInputField();
    }

    #endregion

    #region Receive Messages

    private void OnChatMessageReceived(string username, string message, long timestamp)
    {
        Debug.Log($"[ChatUIManager] Received: [{username}] {message}");

        bool isMyMessage = (username == NetworkManager.Instance?.PlayerName);
        DisplayChatMessage(username, message, isMyMessage);
    }

    private void OnChatError(string error)
    {
        Debug.LogError($"[ChatUIManager] Error: {error}");
        DisplayErrorMessage(error);
    }

    #endregion

    #region Display Messages

    private void DisplayChatMessage(string username, string message, bool isMyMessage)
    {
        if (chatMessagePrefab == null || chatContainer == null)
        {
            Debug.LogError("[ChatUIManager] Prefab or container is null!");
            return;
        }

        GameObject messageObj = Instantiate(chatMessagePrefab, chatContainer);
        messageObjects.Add(messageObj);

        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        
        if (messageText != null)
        {
            messageText.text = $"<b>{username}:</b> {message}";
            messageText.color = isMyMessage ? myMessageColor : otherMessageColor;
        }

        LimitMessages();
        ScrollToBottom();
    }

    public void DisplaySystemMessage(string message)
    {
        if (chatMessagePrefab == null || chatContainer == null)
        {
            return;
        }

        GameObject messageObj = Instantiate(chatMessagePrefab, chatContainer);
        messageObjects.Add(messageObj);

        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        
        if (messageText != null)
        {
            messageText.text = $"<i>[System] {message}</i>";
            messageText.color = systemMessageColor;
        }

        LimitMessages();
        ScrollToBottom();
    }

    public void DisplayErrorMessage(string error)
    {
        if (chatMessagePrefab == null || chatContainer == null)
        {
            return;
        }

        GameObject messageObj = Instantiate(chatMessagePrefab, chatContainer);
        messageObjects.Add(messageObj);

        TextMeshProUGUI messageText = messageObj.GetComponentInChildren<TextMeshProUGUI>();
        
        if (messageText != null)
        {
            messageText.text = $"<i>⚠ {error}</i>";
            messageText.color = errorMessageColor;
        }

        LimitMessages();
        ScrollToBottom();
    }

    #endregion

    #region Helpers

    private void LimitMessages()
    {
        while (messageObjects.Count > maxMessages)
        {
            if (messageObjects[0] != null)
            {
                Destroy(messageObjects[0]);
            }
            messageObjects.RemoveAt(0);
        }
    }

    private void ScrollToBottom()
    {
        // ✅ NULL CHECK
        if (scrollRect == null)
        {
            return;
        }

        // ✅ CHECK content assigned
        if (scrollRect.content == null)
        {
            Debug.LogWarning("[ChatUIManager] ScrollRect.content not assigned!");
            return;
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
    }

    #endregion
}
