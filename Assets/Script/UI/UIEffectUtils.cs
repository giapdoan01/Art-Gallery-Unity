using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIEffectUtils : MonoBehaviour
{
    public enum EffectType
    {
        Fade,
        Scale,
        Slide
    }

    public enum SlideDirection
    {
        FromLeft,
        FromRight,
        FromTop,
        FromBottom
    }

    [Serializable]
    public class UIEffectSettings
    {
        public EffectType effectType = EffectType.Fade;
        public SlideDirection slideDirection = SlideDirection.FromBottom;
        public float duration = 0.3f;
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }

    [Header("Show Effect Settings")]
    public UIEffectSettings showEffect = new UIEffectSettings();

    [Header("Hide Effect Settings")]
    public UIEffectSettings hideEffect = new UIEffectSettings { duration = 0.2f };

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;
    private Vector2 originalPosition;
    private Vector3 originalScale;
    private Coroutine activeEffectCoroutine;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            Debug.LogError($"[UIEffectUtils] RectTransform component not found on {gameObject.name}");
            enabled = false;
            return;
        }

        // Lưu lại vị trí và kích thước ban đầu
        originalPosition = rectTransform.anchoredPosition;
        originalScale = rectTransform.localScale;

        // Tự động thêm CanvasGroup nếu cần
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Hiển thị GameObject với hiệu ứng được cấu hình
    /// </summary>
    public void ShowWithEffect(Action onComplete = null)
    {
        // Dừng coroutine đang chạy nếu có
        if (activeEffectCoroutine != null)
        {
            StopCoroutine(activeEffectCoroutine);
        }

        // Đảm bảo đối tượng đã được bật
        gameObject.SetActive(true);

        // Thiết lập trạng thái bắt đầu dựa trên loại hiệu ứng
        SetupInitialStateForShow();

        // Bắt đầu hiệu ứng
        activeEffectCoroutine = StartCoroutine(PlayShowEffect(onComplete));
    }

    /// <summary>
    /// Ẩn GameObject với hiệu ứng được cấu hình
    /// </summary>
    public void HideWithEffect(Action onComplete = null)
    {
        // Dừng coroutine đang chạy nếu có
        if (activeEffectCoroutine != null)
        {
            StopCoroutine(activeEffectCoroutine);
        }

        // Đảm bảo đối tượng đã được bật
        if (!gameObject.activeSelf)
        {
            if (onComplete != null) onComplete.Invoke();
            return;
        }

        // Bắt đầu hiệu ứng
        activeEffectCoroutine = StartCoroutine(PlayHideEffect(onComplete));
    }

    private void SetupInitialStateForShow()
    {
        switch (showEffect.effectType)
        {
            case EffectType.Fade:
                canvasGroup.alpha = 0;
                break;

            case EffectType.Scale:
                rectTransform.localScale = Vector3.zero;
                break;

            case EffectType.Slide:
                Vector2 startPos = GetSlideStartPosition(showEffect.slideDirection);
                rectTransform.anchoredPosition = startPos;
                break;
        }
    }

    private Vector2 GetSlideStartPosition(SlideDirection direction)
    {
        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas == null)
        {
            Debug.LogWarning("[UIEffectUtils] Root canvas not found. Using default offscreen position.");
            return direction switch
            {
                SlideDirection.FromLeft => new Vector2(-1000, originalPosition.y),
                SlideDirection.FromRight => new Vector2(1000, originalPosition.y),
                SlideDirection.FromTop => new Vector2(originalPosition.x, 1000),
                SlideDirection.FromBottom => new Vector2(originalPosition.x, -1000),
                _ => originalPosition
            };
        }

        RectTransform canvasRect = rootCanvas.GetComponent<RectTransform>();
        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;

        return direction switch
        {
            SlideDirection.FromLeft => new Vector2(-canvasWidth, originalPosition.y),
            SlideDirection.FromRight => new Vector2(canvasWidth, originalPosition.y),
            SlideDirection.FromTop => new Vector2(originalPosition.x, canvasHeight),
            SlideDirection.FromBottom => new Vector2(originalPosition.x, -canvasHeight),
            _ => originalPosition
        };
    }

    private IEnumerator PlayShowEffect(Action onComplete)
    {
        float elapsedTime = 0;
        float duration = showEffect.duration;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            float curveValue = showEffect.animationCurve.Evaluate(t);

            ApplyEffect(showEffect.effectType, showEffect.slideDirection, curveValue, true);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Đảm bảo hiệu ứng kết thúc với trạng thái cuối cùng chính xác
        ApplyEffect(showEffect.effectType, showEffect.slideDirection, 1f, true);
        
        // Gọi callback hoàn thành
        onComplete?.Invoke();
        
        activeEffectCoroutine = null;
    }

    private IEnumerator PlayHideEffect(Action onComplete)
    {
        float elapsedTime = 0;
        float duration = hideEffect.duration;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            float curveValue = hideEffect.animationCurve.Evaluate(t);

            ApplyEffect(hideEffect.effectType, hideEffect.slideDirection, curveValue, false);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Đảm bảo hiệu ứng kết thúc với trạng thái cuối cùng chính xác
        ApplyEffect(hideEffect.effectType, hideEffect.slideDirection, 1f, false);
        
        // Ẩn GameObject sau khi hoàn thành hiệu ứng
        gameObject.SetActive(false);
        
        // Reset về trạng thái ban đầu
        rectTransform.anchoredPosition = originalPosition;
        rectTransform.localScale = originalScale;
        canvasGroup.alpha = 1f;
        
        // Gọi callback hoàn thành
        onComplete?.Invoke();
        
        activeEffectCoroutine = null;
    }

    private void ApplyEffect(EffectType effectType, SlideDirection slideDirection, float t, bool isShowEffect)
    {
        // Đảo ngược giá trị t cho hiệu ứng ẩn
        if (!isShowEffect) t = 1f - t;

        switch (effectType)
        {
            case EffectType.Fade:
                canvasGroup.alpha = t;
                break;

            case EffectType.Scale:
                rectTransform.localScale = Vector3.Lerp(Vector3.zero, originalScale, t);
                break;

            case EffectType.Slide:
                Vector2 startPos = GetSlideStartPosition(slideDirection);
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, originalPosition, t);
                break;
        }
    }

    /// <summary>
    /// Áp dụng hiệu ứng cho một UI element bất kỳ
    /// </summary>
    public static void ShowUI(GameObject uiObject, EffectType effectType = EffectType.Fade, float duration = 0.3f, Action onComplete = null)
    {
        if (uiObject == null) return;
        
        UIEffectUtils effectUtils = uiObject.GetComponent<UIEffectUtils>();
        if (effectUtils == null)
        {
            effectUtils = uiObject.AddComponent<UIEffectUtils>();
        }
        
        effectUtils.showEffect.effectType = effectType;
        effectUtils.showEffect.duration = duration;
        effectUtils.ShowWithEffect(onComplete);
    }

    /// <summary>
    /// Ẩn một UI element với hiệu ứng
    /// </summary>
    public static void HideUI(GameObject uiObject, EffectType effectType = EffectType.Fade, float duration = 0.2f, Action onComplete = null)
    {
        if (uiObject == null) return;
        
        UIEffectUtils effectUtils = uiObject.GetComponent<UIEffectUtils>();
        if (effectUtils == null)
        {
            effectUtils = uiObject.AddComponent<UIEffectUtils>();
        }
        
        effectUtils.hideEffect.effectType = effectType;
        effectUtils.hideEffect.duration = duration;
        effectUtils.HideWithEffect(onComplete);
    }
}