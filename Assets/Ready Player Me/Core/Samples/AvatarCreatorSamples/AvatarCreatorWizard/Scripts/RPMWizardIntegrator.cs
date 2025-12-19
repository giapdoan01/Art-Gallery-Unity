using UnityEngine;
using ReadyPlayerMe.Core;
using ReadyPlayerMe.Samples.AvatarCreatorWizard;
using UnityEngine.SceneManagement;

/// <summary>
/// Script trung gian kết nối Ready Player Me Avatar Creator Wizard với hệ thống menu
/// </summary>
public class RPMWizardIntegrator : MonoBehaviour
{
    // URL cơ sở cho các model ReadyPlayerMe
    private const string RPM_MODELS_BASE_URL = "https://models.readyplayer.me";
    
    // Singleton pattern
    private static RPMWizardIntegrator _instance;
    public static RPMWizardIntegrator Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("RPMWizardIntegrator");
                _instance = go.AddComponent<RPMWizardIntegrator>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    // URL của avatar được tạo gần nhất
    private string lastCreatedAvatarUrl;
    public string LastCreatedAvatarUrl => lastCreatedAvatarUrl;

    // Scene để quay lại sau khi tạo avatar
    private string menuSceneName = "MenuScene";

    // Tham chiếu đến AvatarCreatorStateMachine
    private AvatarCreatorStateMachine avatarCreatorStateMachine;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Tìm và đăng ký với AvatarCreatorStateMachine khi vào scene tạo avatar
        if (scene.name.Contains("AvatarCreatorWizard") || scene.name.Contains("AvatarCreatorWizard"))
        {
            FindAndRegisterWithAvatarCreator();
        }
    }

    private void FindAndRegisterWithAvatarCreator()
    {
        // Thay thế FindObjectOfType bằng FindAnyObjectByType (phương thức mới không bị lỗi thời)
        avatarCreatorStateMachine = FindAnyObjectByType<AvatarCreatorStateMachine>();

        if (avatarCreatorStateMachine != null)
        {
            Debug.Log("RPMWizardIntegrator: Found AvatarCreatorStateMachine");
            avatarCreatorStateMachine.AvatarSaved += OnAvatarSaved;
        }
        else
        {
            Debug.LogWarning("RPMWizardIntegrator: AvatarCreatorStateMachine not found in scene");
        }
    }

    private void OnAvatarSaved(string avatarId)
    {
        Debug.Log($"RPMWizardIntegrator: Avatar saved with ID {avatarId}");
        // Sử dụng RPM_MODELS_BASE_URL thay vì Env.RPM_MODELS_BASE_URL
        lastCreatedAvatarUrl = $"{RPM_MODELS_BASE_URL}/{avatarId}.glb";

        // Tự động quay lại menu scene sau khi tạo avatar
        StartCoroutine(ReturnToMenuScene());
    }

    private System.Collections.IEnumerator ReturnToMenuScene()
    {
        yield return new WaitForSeconds(1.5f); // Chờ một chút để hiệu ứng hoàn thành
        SceneManager.LoadScene(menuSceneName);
    }

    public bool HasCreatedAvatar()
    {
        return !string.IsNullOrEmpty(lastCreatedAvatarUrl);
    }

    public string GetAndClearCreatedAvatarUrl()
    {
        string url = lastCreatedAvatarUrl;
        lastCreatedAvatarUrl = null;
        return url;
    }

    // Phương thức để mở Avatar Creator
    public void OpenAvatarCreator()
    {
        // Lưu tên scene hiện tại làm scene menu
        menuSceneName = SceneManager.GetActiveScene().name;

        // Tìm scene Avatar Creator trong build settings
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

            if (sceneName.Contains("AvatarCreator") || sceneName.Contains("ReadyPlayerMe"))
            {
                SceneManager.LoadScene(sceneName);
                return;
            }
        }

        Debug.LogError("Không tìm thấy scene Avatar Creator trong build settings");
    }
}