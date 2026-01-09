using UnityEngine;
using UnityEngine.UI;

public class ArtFrameInfo : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button infoButton;
    [SerializeField] private float displayDistance = 5f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private ArtFrame artFrame;
    private Camera mainCamera;

    private void Awake()
    {
        artFrame = GetComponent<ArtFrame>();
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
        if (artFrame == null || artFrame.ImageData == null)
        {
            if (showDebug)
            {
                Debug.LogWarning("[ArtFrameInfo] No image data to display");
            }
            return;
        }

        // Hiển thị thông tin qua ImageInfoDisplay
        if (ImageInfoDisplay.Instance != null)
        {
            ImageInfoDisplay.Instance.ShowInfo(artFrame.ImageData);
            
            if (showDebug)
            {
                Debug.Log($"[ArtFrameInfo] Showing info for: {artFrame.ImageData.name}");
            }
        }
        else
        {
            Debug.LogError("[ArtFrameInfo] ImageInfoDisplay instance not found!");
        }
    }
}
