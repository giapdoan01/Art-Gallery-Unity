using UnityEngine;

public class UIButtonHandler : MonoBehaviour
{
    public GameObject targetUI; // Kéo thả UI cần hiệu ứng vào đây

    public void ShowUI()
    {
        if (targetUI != null)
            UIEffectUtils.ShowUI(targetUI);
    }

    public void HideUI()
    {
        if (targetUI != null)
            UIEffectUtils.HideUI(targetUI);
    }
}