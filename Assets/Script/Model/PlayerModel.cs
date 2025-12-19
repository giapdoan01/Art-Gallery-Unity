using UnityEngine;
using System;

public class PlayerModel
{
    // Dữ liệu cơ bản của người chơi
    public string SessionId { get; private set; }
    public string Username { get; private set; }
    public bool IsLocalPlayer { get; private set; }
    public Player NetworkState { get; private set; }

    // Dữ liệu chuyển động
    public Vector3 Position { get; set; }
    public float RotationY { get; set; }
    public float MoveSpeed { get; set; } = 5f;
    public float RotationSpeed { get; set; } = 10f;

    // Dữ liệu mạng
    public Vector3 NetworkPosition { get; set; }
    public Quaternion NetworkRotation { get; set; }
    public float SendInterval { get; set; } = 0.1f;
    public float LerpSpeed { get; set; } = 10f;
    public float LastSendTime { get; set; }

    // Dữ liệu animation
    public float MovementSpeed { get; set; } // Tốc độ chuyển động để điều khiển animation

    // Sự kiện thông báo cập nhật
    public event Action<Vector3, float> OnPositionUpdated;
    public event Action<float> OnSpeedChanged;

    public PlayerModel(string sessionId, Player state, bool isLocalPlayer, float moveSpeed = 5f, float rotationSpeed = 10f)
    {
        SessionId = sessionId;
        NetworkState = state;
        IsLocalPlayer = isLocalPlayer;
        Username = state.username;
        
        // Thiết lập các tham số chuyển động
        MoveSpeed = moveSpeed;
        RotationSpeed = rotationSpeed;
        
        // Khởi tạo vị trí ban đầu
        Position = new Vector3(state.x, state.y, state.z);
        RotationY = state.rotationY;
        NetworkPosition = Position;
        NetworkRotation = Quaternion.Euler(0, RotationY, 0);
    }

    // Cập nhật vị trí từ mạng (cho remote player)
    public void UpdateFromNetwork(float x, float y, float z, float rotY)
    {
        if (IsLocalPlayer) return;

        NetworkState.x = x;
        NetworkState.y = y;
        NetworkState.z = z;
        NetworkState.rotationY = rotY;
        
        NetworkPosition = new Vector3(x, y, z);
        NetworkRotation = Quaternion.Euler(0, rotY, 0);
        
        OnPositionUpdated?.Invoke(NetworkPosition, rotY);
    }

    // Cập nhật vị trí từ input (cho local player)
    public void UpdateLocalPosition(Vector3 newPosition, float newRotationY)
    {
        if (!IsLocalPlayer) return;

        Position = newPosition;
        RotationY = newRotationY;
        
        OnPositionUpdated?.Invoke(Position, RotationY);
    }

    // Cập nhật tốc độ di chuyển cho animation
    public void SetMovementSpeed(float speed)
    {
        MovementSpeed = speed;
        OnSpeedChanged?.Invoke(speed);
    }
}