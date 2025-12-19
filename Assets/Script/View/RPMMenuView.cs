using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class RPMMenuView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button createAvatarButton;
    [SerializeField] private TextMeshProUGUI statusText;
    
    [Header("Avatar Preview")]
    [SerializeField] private Transform avatarSpawnPoint;

    // Events để Controller xử lý
    public event Action<string> OnNameChanged;
    public event Action OnJoinButtonClicked;
    public event Action OnCreateAvatarButtonClicked;
    public event Action OnUseDefaultButtonClicked;

    private void Awake()
    {
        // Đăng ký listeners cho các sự kiện UI
        if (nameInput != null)
            nameInput.onValueChanged.AddListener(OnNameInputChanged);
            
        if (joinButton != null)
            joinButton.onClick.AddListener(() => OnJoinButtonClicked?.Invoke());
            
        if (createAvatarButton != null)
            createAvatarButton.onClick.AddListener(() => OnCreateAvatarButtonClicked?.Invoke());
    }
    
    // Phương thức xử lý sự kiện UI
    private void OnNameInputChanged(string value)
    {
        OnNameChanged?.Invoke(value);
    }
    
    // Phương thức cập nhật UI
    public void SetPlayerName(string name)
    {
        if (nameInput != null)
            nameInput.text = name;
    }
    
    public string GetPlayerName()
    {
        return nameInput != null ? nameInput.text.Trim() : string.Empty;
    }
    
    public void UpdateStatusText(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
    
    public void SetJoinButtonInteractable(bool interactable)
    {
        if (joinButton != null)
            joinButton.interactable = interactable;
    }
    public void PlaceAvatarInScene(GameObject avatar)
    {
        if (avatar != null)
        {
            if (avatarSpawnPoint != null)
            {
                avatar.transform.position = avatarSpawnPoint.position;
                avatar.transform.rotation = avatarSpawnPoint.rotation;
            }
            else
            {
                avatar.transform.position = new Vector3(163f, -0.7f, -1235.94f);
                avatar.transform.rotation = Quaternion.Euler(0f, -180f, 0f);
            }

            avatar.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        }
    }
    
    // Phương thức mới để thiết lập Animator cho avatar
    public void SetAvatarAnimator(GameObject avatar, RuntimeAnimatorController animatorController)
    {
        if (avatar != null && animatorController != null)
        {
            // Tìm Animator trong avatar (thường nằm trong child objects)
            Animator animator = avatar.GetComponentInChildren<Animator>();
            
            if (animator != null)
            {
                animator.runtimeAnimatorController = animatorController;
                
                // Có thể thêm các animation mặc định nếu cần
                animator.SetFloat("Speed", 0f); // Avatar đứng yên trong menu
            }
            else
            {
                Debug.LogWarning("Không tìm thấy Animator trong avatar preview");
            }
        }
    }
    
    private void OnDestroy()
    {
        // Hủy đăng ký tất cả listeners
        if (nameInput != null)
            nameInput.onValueChanged.RemoveAllListeners();
            
        if (joinButton != null)
            joinButton.onClick.RemoveAllListeners();
            
        if (createAvatarButton != null)
            createAvatarButton.onClick.RemoveAllListeners();
    }
}