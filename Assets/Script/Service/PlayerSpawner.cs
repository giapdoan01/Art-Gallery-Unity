using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Prefab Settings")]
    [SerializeField] private GameObject rpmPlayerPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Update Settings")]
    [SerializeField] private float checkInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    private Dictionary<string, string> playerAvatarURLs = new Dictionary<string, string>();
    private int nextSpawnIndex = 0;
    private float lastCheckTime = 0f;

    private void Start()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("NetworkManager not found!");
            return;
        }

        if (showDebug) Debug.Log("PlayerSpawner.Start() called");

        NetworkManager.Instance.OnPlayerJoined += OnPlayerJoined;
        NetworkManager.Instance.OnPlayerLeft += OnPlayerLeft;

        if (NetworkManager.Instance.IsConnected)
        {
            if (showDebug) Debug.Log("PlayerSpawner: Already connected, checking for players immediately");
            
            StartCoroutine(ForceCheckPlayers());
        }
        else
        {
            if (showDebug) Debug.Log("PlayerSpawner: Not connected yet, waiting...");
            NetworkManager.Instance.OnConnected += OnConnected;
        }
    }

    private IEnumerator ForceCheckPlayers()
    {
        yield return new WaitForEndOfFrame();
        
        if (showDebug) Debug.Log("Force checking for existing players...");
        
        var state = NetworkManager.Instance.State;
        if (state?.players == null)
        {
            if (showDebug) Debug.LogWarning("State or players is null!");
            yield break;
        }

        if (showDebug) Debug.Log($"Found {state.players.Count} players in state");

        state.players.ForEach((sessionId, player) =>
        {
            if (showDebug) Debug.Log($"Checking player: {player.username} ({sessionId})");
            
            if (!players.ContainsKey(sessionId))
            {
                if (showDebug) Debug.Log($"Spawning player: {player.username}");
                SpawnPlayer(sessionId, player);
            }
            else
            {
                if (showDebug) Debug.LogWarning($"Player already spawned: {player.username}");
            }
        });
    }

    private void Update()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            if (Time.time - lastCheckTime > checkInterval)
            {
                CheckForPlayers();
                UpdatePlayerPositions();
                lastCheckTime = Time.time;
            }
        }
    }

    private void OnConnected()
    {
        if (showDebug) Debug.Log("PlayerSpawner: Connected to room");
        CheckForPlayers();
    }

    private void CheckForPlayers()
    {
        var state = NetworkManager.Instance.State;
        if (state == null) return;

        NetworkManager.Instance.CheckForNewPlayers();

        var playersToRemove = new List<string>();
        foreach (var entry in players)
        {
            bool stillExists = false;
            state.players.ForEach((key, player) =>
            {
                if (key.Equals(entry.Key))
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
            OnPlayerLeft(sessionId, null);
        }
    }

    private void UpdatePlayerPositions()
    {
        var state = NetworkManager.Instance.State;
        if (state == null) return;

        state.players.ForEach((sessionId, player) =>
        {
            // Skip local player
            if (sessionId.Equals(NetworkManager.Instance.SessionId))
                return;

            // Update remote player position
            if (players.TryGetValue(sessionId, out GameObject playerObj))
            {
                PlayerController controller = playerObj.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.UpdateNetworkPosition(player.x, player.y, player.z, player.rotationY);
                }
            }
        });
    }

    private void OnPlayerJoined(string sessionId, Player player)
    {
        if (showDebug)
        {
            Debug.Log($"========================================");
            Debug.Log($"PlayerSpawner.OnPlayerJoined CALLED!");
            Debug.Log($"   SessionId: {sessionId}");
            Debug.Log($"   Username: {player.username}");
            Debug.Log($"   Avatar URL: {player.avatarURL}");
            Debug.Log($"========================================");
        }
        
        SpawnPlayer(sessionId, player);
    }

    private void OnPlayerLeft(string sessionId, Player player)
    {
        if (players.TryGetValue(sessionId, out GameObject playerObj))
        {
            if (showDebug) Debug.Log($"Destroying player: {sessionId}");
            Destroy(playerObj);
            players.Remove(sessionId);
            playerAvatarURLs.Remove(sessionId);
            NetworkManager.Instance.MarkPlayerRemoved(sessionId);
        }
    }

    private void SpawnPlayer(string sessionId, Player player)
    {
        if (players.ContainsKey(sessionId))
        {
            if (showDebug) 
            {
                Debug.LogWarning($"========================================");
                Debug.LogWarning($"   DUPLICATE SPAWN ATTEMPT:");
                Debug.LogWarning($"   SessionId: {sessionId}");
                Debug.LogWarning($"   Username: {player.username}");
                Debug.LogWarning($"   Already exists in players dictionary!");
                Debug.LogWarning($"   SKIPPING SPAWN");
                Debug.LogWarning($"========================================");
            }
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition();

        // Spawn RPM player prefab
        GameObject playerObj = Instantiate(rpmPlayerPrefab, spawnPosition, Quaternion.identity);
        
        bool isLocal = sessionId.Equals(NetworkManager.Instance.SessionId);
        
        playerObj.name = $"Player_{player.username}_{sessionId.Substring(0, 5)}_{(isLocal ? "LOCAL" : "REMOTE")}";

        if (showDebug)
        {
            Debug.Log($"========================================");
            Debug.Log($"   SPAWNING PLAYER:");
            Debug.Log($"   Username: {player.username}");
            Debug.Log($"   SessionId: {sessionId}");
            Debug.Log($"   My SessionId: {NetworkManager.Instance.SessionId}");
            Debug.Log($"   Is Local: {isLocal}");
            Debug.Log($"   Avatar URL from server: {player.avatarURL}");
            Debug.Log($"   Current players count: {players.Count}");
        }

        string avatarURL = player.avatarURL;

        if (string.IsNullOrEmpty(avatarURL))
        {
            avatarURL = "https://models.readyplayer.me/6942207f4a15f239b0965d1f.glb";
            Debug.LogWarning($"Avatar URL empty from server, using default");
        }

        playerAvatarURLs[sessionId] = avatarURL;

        if (showDebug)
        {
            Debug.Log($"Final avatar URL: {avatarURL}");
        }

        // Initialize PlayerController
        PlayerController controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.Initialize(sessionId, player, isLocal);
        }
        else
        {
            Debug.LogError("PlayerController not found on prefab!");
        }

        RPMAvatarLoader avatarLoader = playerObj.GetComponent<RPMAvatarLoader>();
        if (avatarLoader != null)
        {
            if (!avatarLoader.IsLoaded && !avatarLoader.IsLoading)
            {
                if (showDebug) 
                    Debug.Log($"Loading avatar for {player.username}: {avatarURL}");

                avatarLoader.LoadAvatar(avatarURL);
            }
            else
            {
                if (showDebug) 
                    Debug.LogWarning($"Avatar already loaded/loading for {player.username}");
            }
        }
        else
        {
            Debug.LogError("RPMAvatarLoader not found on prefab!");
        }

        players[sessionId] = playerObj;

        if (showDebug)
        {
            Debug.Log($"Player spawned successfully. Total players: {players.Count}");
        }
    }

    private Vector3 GetSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Vector3 pos = spawnPoints[nextSpawnIndex].position;
            nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Length;
            return pos;
        }

        // Random spawn nếu không có spawn points
        return new Vector3(
            UnityEngine.Random.Range(-5f, 5f),
            0,
            UnityEngine.Random.Range(-5f, 5f)
        );
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnPlayerJoined -= OnPlayerJoined;
            NetworkManager.Instance.OnPlayerLeft -= OnPlayerLeft;
            NetworkManager.Instance.OnConnected -= OnConnected;
        }
    }
}
