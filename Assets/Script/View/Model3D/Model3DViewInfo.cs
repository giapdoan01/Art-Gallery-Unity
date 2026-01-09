using UnityEngine;
using UnityEngine.UI;

public class Model3DViewInfo : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button infoButton;
    [SerializeField] private float displayDistance = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private Model3DView model3DView;
    private Camera mainCamera;

    private void Awake()
    {
        model3DView = GetComponent<Model3DView>();
        mainCamera = Camera.main;

        if (infoButton != null)
        {
            infoButton.onClick.AddListener(OnInfoButtonClicked);
            infoButton.gameObject.SetActive(false); // Ẩn ban đầu
        }
    }

    private void Update()
    {
        // Hiển thị/ẩn button theo khoảng cách
        if (infoButton != null && mainCamera != null)
        {
            float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
            bool shouldShow = distance <= displayDistance;
            
            if (infoButton.gameObject.activeSelf != shouldShow)
            {
                infoButton.gameObject.SetActive(shouldShow);
            }
        }
    }

    private void OnInfoButtonClicked()
    {
        if (model3DView == null || model3DView.ModelData == null)
        {
            if (showDebug)
            {
                Debug.LogWarning("[Model3DViewInfo] No model data to display");
            }
            return;
        }

        // Hiển thị thông tin qua Model3DInfoDisplay
        if (Model3DInfoDisplay.Instance != null)
        {
            Model3DInfoDisplay.Instance.ShowInfo(model3DView.ModelData);
            
            if (showDebug)
            {
                Debug.Log($"[Model3DViewInfo] Showing info for: {model3DView.ModelData.name}");
            }
        }
        else
        {
            Debug.LogError("[Model3DViewInfo] Model3DInfoDisplay instance not found!");
        }
    }
}
