using UnityEngine;
using ReadyPlayerMe.Core;

public class RPMAvatarLoader : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool loadOnStart = false;
    [SerializeField] private string defaultAvatarURL = "https://models.readyplayer.me/6942207f4a15f239b0965d1f.glb";

    [Header("Animator")]
    [SerializeField] private RuntimeAnimatorController animatorController;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private GameObject avatarObject;
    private AvatarObjectLoader avatarLoader;
    private Animator avatarAnimator;
    private bool isLoaded = false;
    private bool isLoading = false;

    public bool IsLoaded => isLoaded;
    public bool IsLoading => isLoading;
    public GameObject AvatarObject => avatarObject;
    public Animator AvatarAnimator => avatarAnimator;

    private void Awake()
    {
        avatarLoader = new AvatarObjectLoader();
        avatarLoader.OnCompleted += OnAvatarLoadCompleted;
        avatarLoader.OnFailed += OnAvatarLoadFailed;
    }
    public void LoadAvatar(string avatarURL)
    {
        // ✅ KIỂM TRA ĐÃ LOAD HOẶC ĐANG LOAD
        if (isLoading)
        {
            if (showDebug) Debug.LogWarning($"⚠️ Avatar is already loading, skipping...");
            return;
        }

        if (isLoaded)
        {
            if (showDebug) Debug.LogWarning($"⚠️ Avatar already loaded, skipping...");
            return;
        }

        if (string.IsNullOrEmpty(avatarURL))
        {
            Debug.LogError("❌ Avatar URL is empty!");
            return;
        }

        if (showDebug) Debug.Log($"🔄 Loading avatar: {avatarURL}");

        // ✅ Clear previous avatar (nếu có)
        if (avatarObject != null)
        {
            if (showDebug) Debug.Log("🗑️ Destroying previous avatar");
            Destroy(avatarObject);
            avatarObject = null;
            avatarAnimator = null;
        }

        isLoaded = false;
        isLoading = true;

        // Load avatar
        avatarLoader.LoadAvatar(avatarURL);
    }

    private void OnAvatarLoadCompleted(object sender, CompletionEventArgs args)
    {
        avatarObject = args.Avatar;

        if (avatarObject == null)
        {
            Debug.LogError("Avatar object is null after load!");
            isLoading = false;
            return;
        }

        avatarObject.transform.SetParent(transform);
        avatarObject.transform.localPosition = Vector3.zero;
        avatarObject.transform.localRotation = Quaternion.identity;
        avatarObject.transform.localScale = Vector3.one;

        avatarAnimator = avatarObject.GetComponentInChildren<Animator>();

        if (avatarAnimator == null)
        {
            Debug.LogError("No Animator found on avatar!");
            isLoading = false;
            return;
        }

        isLoaded = true;
        isLoading = false;

        SetupAnimator();

        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.OnAvatarLoaded(avatarAnimator);
        }
        else
        {
            Debug.LogWarning("PlayerController not found on this GameObject");
        }
    }

    private void OnAvatarLoadFailed(object sender, FailureEventArgs args)
    {
        Debug.LogError($"Avatar load failed: {args.Type} - {args.Message}");
        isLoading = false;
        isLoaded = false;

        if (!string.IsNullOrEmpty(defaultAvatarURL))
        {
            LoadAvatar(defaultAvatarURL);
        }
    }

    private void SetupAnimator()
    {
        if (avatarAnimator == null)
        {
            Debug.LogError("No Animator found on avatar!");
            return;
        }

        if (animatorController != null)
        {
            avatarAnimator.runtimeAnimatorController = animatorController;
        }
        else
        {
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("Animations/PlayerAnimator");

            if (controller != null)
            {
                avatarAnimator.runtimeAnimatorController = controller;
            }
            else
            {
                Debug.LogError("Animator controller not found! Please assign in Inspector or create at Resources/Animations/PlayerAnimator");
                return;
            }
        }
    }

    private void OnDestroy()
    {
        if (avatarLoader != null)
        {
            avatarLoader.OnCompleted -= OnAvatarLoadCompleted;
            avatarLoader.OnFailed -= OnAvatarLoadFailed;
        }
    }
}
