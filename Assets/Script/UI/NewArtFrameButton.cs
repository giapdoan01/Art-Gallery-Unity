using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class NewArtFrameButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Start()
    {
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        if (ArtFrameCreator.Instance != null)
        {
            ArtFrameCreator.Instance.CreateNewArtFrame();
        }
        else
        {
            Debug.LogError("[NewArtFrameButton] ArtFrameCreator.Instance không tồn tại!");
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }
}