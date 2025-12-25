using UnityEngine;
using ReadyPlayerMe.Core;
using System.Collections;
using ReadyPlayerMe.Samples.AvatarCreatorWizard;

public class RPMMenuController : MonoBehaviour
{
    [SerializeField] private RPMMenuView view;
    
    [Header("Avatar Settings")]
    [SerializeField] private RuntimeAnimatorController animatorController;
    
    private RPMMenuModel model;
    private AvatarObjectLoader avatarLoader;
    private bool isLoadingAvatar = false;
    
    private void Awake()
    {
        // Khởi tạo model với default avatar URL và animator controller
        model = new RPMMenuModel(animatorController: animatorController);
        
        // Kiểm tra và tạo view nếu chưa được gán
        if (view == null)
            view = GetComponent<RPMMenuView>();
            
        if (view == null)
        {
            Debug.LogError("RPMMenuView not assigned to RPMMenuController");
            enabled = false;
            return;
        }
    }
    
    private void Start()
    {
        // Khởi tạo avatar loader
        avatarLoader = new AvatarObjectLoader();
        avatarLoader.OnCompleted += OnAvatarLoadCompleted;
        avatarLoader.OnFailed += OnAvatarLoadFailed;
        
        // Đăng ký listeners cho các sự kiện từ view
        view.OnNameChanged += HandleNameChanged;
        view.OnMultiPlayerButtonClicked += HandleMultiPlayerButtonClicked; // Đổi tên từ OnJoinButtonClicked
        view.OnSinglePlayerButtonClicked += HandleSinglePlayerButtonClicked; // Thêm handler mới
        view.OnCreateAvatarButtonClicked += HandleCreateAvatarButtonClicked;
        
        // Đăng ký listeners cho các sự kiện từ model
        model.OnStatusChanged += view.UpdateStatusText;
        model.OnJoinStateChanged += view.SetJoinButtonInteractable;
        
        // Khởi tạo view với dữ liệu từ model
        view.SetPlayerName(model.PlayerName);
        view.SetJoinButtonInteractable(false);
        
        // Kiểm tra nếu có avatar đã tạo từ Ready Player Me Wizard
        bool avatarFound = CheckForCreatedAvatar();
        
        // Nếu không có avatar mới, thử tải avatar đã lưu từ trước
        if (!avatarFound)
        {
            avatarFound = LoadSavedAvatar();
        }
        
        // Hiển thị trạng thái ban đầu CHỈ khi không có avatar đang được tải
        if (!model.IsAvatarLoaded && !isLoadingAvatar && !avatarFound)
        {
            model.SetStatus("Tạo avatar để bắt đầu");
        }
    }
    
    // Phương thức mới để tải avatar đã lưu trước đó
    private bool LoadSavedAvatar()
    {
        string savedAvatarURL = PlayerPrefs.GetString("AvatarURL", "");
        if (!string.IsNullOrEmpty(savedAvatarURL))
        {
            Debug.Log($"Tìm thấy avatar đã lưu trước đó: {savedAvatarURL}");
            model.AvatarURL = savedAvatarURL;
            LoadAvatarPreview(savedAvatarURL);
            model.SetStatus("Đang tải avatar đã lưu trước đó...");
            return true;
        }
        return false;
    }
    
    // Phương thức để kiểm tra avatar đã tạo từ RPM Wizard
    private bool CheckForCreatedAvatar()
    {
        if (RPMWizardIntegrator.Instance != null && RPMWizardIntegrator.Instance.HasCreatedAvatar())
        {
            string avatarUrl = RPMWizardIntegrator.Instance.GetAndClearCreatedAvatarUrl();
            Debug.Log($"Tìm thấy avatar đã tạo: {avatarUrl}");
            
            // Tải avatar
            model.AvatarURL = avatarUrl;
            LoadAvatarPreview(avatarUrl);
            
            // Lưu avatar URL mới vào PlayerPrefs
            PlayerPrefs.SetString("AvatarURL", avatarUrl);
            PlayerPrefs.Save();
            
            model.SetStatus("Avatar đã được tạo thành công và đang được tải...");
            return true;
        }
        return false;
    }
    
    // Xử lý các sự kiện từ view
    private void HandleNameChanged(string name)
    {
        model.PlayerName = name;
    }
    
    private void HandleAvatarURLChanged(string url)
    {
        // Có thể thêm logic kiểm tra URL hợp lệ ở đây
        if (!string.IsNullOrEmpty(url) && model.IsValidAvatarURL(url))
        {
            model.AvatarURL = url;
        }
    }
    
    // Đổi tên từ HandleJoinButtonClicked
    private void HandleMultiPlayerButtonClicked()
    {
        string playerName = view.GetPlayerName();
        
        string errorMessage;
        if (!model.IsValidPlayerName(playerName, out errorMessage))
        {
            model.SetStatus(errorMessage);
            return;
        }

        if (!model.IsAvatarLoaded)
        {
            model.SetStatus("Vui lòng chọn avatar trước!");
            return;
        }
        
        model.PlayerName = playerName;
        model.SetJoinButtonState(false);
        model.SetStatus("Đang kết nối...");
        
        // Lưu dữ liệu người chơi
        model.SavePlayerData();
        
        // Kết nối vào phòng
        NetworkManager.Instance.ConnectAndJoinRoom(
            model.PlayerName,
            OnConnectionResult
        );
    }
    
    // Phương thức mới xử lý chế độ single player
    private void HandleSinglePlayerButtonClicked()
    {
        string playerName = view.GetPlayerName();
        
        string errorMessage;
        if (!model.IsValidPlayerName(playerName, out errorMessage))
        {
            model.SetStatus(errorMessage);
            return;
        }

        if (!model.IsAvatarLoaded)
        {
            model.SetStatus("Vui lòng chọn avatar trước!");
            return;
        }
        
        model.PlayerName = playerName;
        model.SetStatus("Đang tải chế độ chơi đơn...");
        
        // Lưu dữ liệu người chơi
        model.SavePlayerData();
        
        // Chuyển sang chế độ single player
        NetworkManager.Instance.LoadSinglePlayerMode(model.PlayerName);
    }
    
    private void HandleCreateAvatarButtonClicked()
    {
        // Sử dụng RPMWizardIntegrator để mở trình tạo avatar
        if (RPMWizardIntegrator.Instance != null)
        {
            model.SetStatus("Đang mở trình tạo avatar...");
            RPMWizardIntegrator.Instance.OpenAvatarCreator();
        }
        else
        {
            // Fallback sang cách cũ nếu không tìm thấy RPMWizardIntegrator
            string rpmURL = "https://readyplayer.me/avatar";
            Application.OpenURL(rpmURL);
            model.SetStatus("Đã mở trình tạo avatar. Copy URL avatar và paste vào ô bên dưới.");
        }
    }
    
    // Phương thức xử lý avatar
    private void LoadAvatarPreview(string url)
    {
        isLoadingAvatar = true;
        model.SetStatus("Đang tải avatar...");
        
        if (model.PreviewAvatar != null)
        {
            Destroy(model.PreviewAvatar);
            model.PreviewAvatar = null;
        }
        
        avatarLoader.LoadAvatar(url);
    }
    
    private void OnAvatarLoadCompleted(object sender, CompletionEventArgs args)
    {
        model.PreviewAvatar = args.Avatar;
        view.PlaceAvatarInScene(model.PreviewAvatar);
        
        // Thiết lập animator cho avatar preview
        view.SetAvatarAnimator(model.PreviewAvatar, model.AnimatorController);
        
        model.IsAvatarLoaded = true;
        isLoadingAvatar = false;
        model.SetJoinButtonState(true);
        
        // Kích hoạt cả hai nút khi avatar đã sẵn sàng
        view.SetSinglePlayerButtonInteractable(true);
        
        model.SetStatus("Avatar đã sẵn sàng! Chọn chế độ chơi để bắt đầu.");
        
        // Lưu URL avatar hiện tại vào PlayerPrefs
        PlayerPrefs.SetString("AvatarURL", model.AvatarURL);
        PlayerPrefs.Save();
    }
    
    private void OnAvatarLoadFailed(object sender, FailureEventArgs args)
    {
        isLoadingAvatar = false;
        model.SetStatus($"Không thể tải avatar: {args.Message}");
        Debug.LogError($"Avatar load failed: {args.Type} - {args.Message}");
        
        // Nếu không tải được avatar đã lưu, xóa URL cũ
        if (args.Message.Contains("404") || args.Message.Contains("Not Found"))
        {
            Debug.Log("Xóa URL avatar không hợp lệ khỏi PlayerPrefs");
            PlayerPrefs.DeleteKey("AvatarURL");
            PlayerPrefs.Save();
        }
    }
    
    private void OnConnectionResult(bool success, string message)
    {
        if (!success)
        {
            model.SetStatus($" {message}");
            model.SetJoinButtonState(true);
        }
        else
        {
            model.SetStatus("Kết nối thành công!");
        }
    }
    
    // Phương thức cập nhật RPMAvatarLoader
    public void UpdateAvatarLoader(RPMAvatarLoader avatarLoader, string avatarURL = null)
    {
        if (avatarLoader == null)
            return;
            
        string urlToLoad = avatarURL ?? model.AvatarURL;
        
        if (!string.IsNullOrEmpty(urlToLoad))
        {
            avatarLoader.LoadAvatar(urlToLoad);
        }
    }
    
    // Thêm phương thức để lấy avatar URL hiện tại
    public string GetCurrentAvatarURL()
    {
        return model.AvatarURL;
    }
    
    private void OnDestroy()
    {
        // Hủy đăng ký tất cả sự kiện
        if (avatarLoader != null)
        {
            avatarLoader.OnCompleted -= OnAvatarLoadCompleted;
            avatarLoader.OnFailed -= OnAvatarLoadFailed;
        }
        
        if (model != null)
        {
            model.OnStatusChanged -= view.UpdateStatusText;
            model.OnJoinStateChanged -= view.SetJoinButtonInteractable;
        }
        
        if (view != null)
        {
            view.OnNameChanged -= HandleNameChanged;
            view.OnMultiPlayerButtonClicked -= HandleMultiPlayerButtonClicked;
            view.OnSinglePlayerButtonClicked -= HandleSinglePlayerButtonClicked;
            view.OnCreateAvatarButtonClicked -= HandleCreateAvatarButtonClicked;
        }
        
        // Dọn dẹp tài nguyên
        if (model != null && model.PreviewAvatar != null)
        {
            Destroy(model.PreviewAvatar);
        }
    }
}