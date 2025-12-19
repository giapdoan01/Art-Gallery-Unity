using UnityEngine;
using System;

public class RPMMenuModel
{
    // Các thuộc tính dữ liệu
    public string PlayerName { get; set; }
    public string AvatarURL { get; set; }
    public string DefaultAvatarURL { get; private set; }
    public bool IsAvatarLoaded { get; set; }
    public GameObject PreviewAvatar { get; set; }
    public RuntimeAnimatorController AnimatorController { get; set; }

    // Events để thông báo khi dữ liệu thay đổi
    public event Action<string> OnStatusChanged;
    public event Action OnAvatarChanged;
    public event Action<bool> OnJoinStateChanged;

    public RPMMenuModel(
        string defaultAvatarURL = "https://models.readyplayer.me/6942207f4a15f239b0965d1f.glb",
        RuntimeAnimatorController animatorController = null)
    {
        DefaultAvatarURL = defaultAvatarURL;
        AnimatorController = animatorController;
        PlayerName = $"Player{UnityEngine.Random.Range(1000, 9999)}";
        IsAvatarLoaded = false;
    }

    // Logic nghiệp vụ
    public bool IsValidAvatarURL(string url)
    {
        return url.Contains("readyplayer.me") || url.EndsWith(".glb");
    }

    public bool IsValidPlayerName(string name, out string errorMessage)
    {
        if (string.IsNullOrEmpty(name))
        {
            errorMessage = "Vui lòng nhập tên!";
            return false;
        }

        if (name.Length < 3)
        {
            errorMessage = "Tên phải dài hơn 3 ký tự!";
            return false;
        }

        if (name.Length > 20)
        {
            errorMessage = "Tên quá dài! (tối đa 20 ký tự)";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public void SavePlayerData()
    {
        PlayerPrefs.SetString("AvatarURL", AvatarURL);
        PlayerPrefs.SetString("PlayerName", PlayerName);
        PlayerPrefs.Save();
    }

    // Phương thức để thông báo thay đổi
    public void SetStatus(string message)
    {
        OnStatusChanged?.Invoke(message);
    }

    public void NotifyAvatarChanged()
    {
        OnAvatarChanged?.Invoke();
    }

    public void SetJoinButtonState(bool enabled)
    {
        OnJoinStateChanged?.Invoke(enabled);
    }
}