using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Button để tạo mới Model3D
/// Tương tự NewArtFrameButton nhưng cho Model3D
/// </summary>
[RequireComponent(typeof(Button))]
public class NewModel3DButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (Model3DCreator.Instance != null)
        {
            Model3DCreator.Instance.CreateNewModel3D();
        }
        else
        {
            Debug.LogError("[NewModel3DButton] Model3DCreator.Instance is null!");
        }
    }
}
