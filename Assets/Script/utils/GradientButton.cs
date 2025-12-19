using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(Image))]
public class SimpleHoverButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Components")]
    private Image buttonImage;
    
    [Header("Scale Settings")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float scaleDuration = 0.3f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Brightness Settings")]
    [SerializeField] private float brightnessFactor = 1.2f;
    [SerializeField] private float brightnessDuration = 0.3f;
    [SerializeField] private AnimationCurve brightnessCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private Vector3 originalScale;
    private Color originalColor;
    private Coroutine scaleCoroutine;
    private Coroutine brightnessCoroutine;
    private bool isHovering = false;
    
    void Awake()
    {
        buttonImage = GetComponent<Image>();
        originalScale = transform.localScale;
        originalColor = buttonImage.color;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovering = true;
        
        // Scale up
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(originalScale * hoverScale, scaleDuration));
        
        // Brighten
        if (brightnessCoroutine != null) StopCoroutine(brightnessCoroutine);
        brightnessCoroutine = StartCoroutine(AnimateBrightness(brightnessFactor, brightnessDuration));
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        isHovering = false;
        
        // Scale back
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(originalScale, scaleDuration));
        
        // Darken back
        if (brightnessCoroutine != null) StopCoroutine(brightnessCoroutine);
        brightnessCoroutine = StartCoroutine(AnimateBrightness(1f, brightnessDuration));
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        // Scale down slightly
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(originalScale * 0.97f, 0.1f));
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        // Scale back to hover or normal
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        
        if (isHovering)
        {
            scaleCoroutine = StartCoroutine(AnimateScale(originalScale * hoverScale, 0.1f));
        }
        else
        {
            scaleCoroutine = StartCoroutine(AnimateScale(originalScale, 0.1f));
        }
    }
    
    private IEnumerator AnimateScale(Vector3 targetScale, float duration)
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = scaleCurve.Evaluate(t);
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, easedT);
            
            yield return null;
        }
        
        transform.localScale = targetScale;
    }
    
    private IEnumerator AnimateBrightness(float targetBrightness, float duration)
    {
        float elapsed = 0f;
        Color startColor = buttonImage.color;
        Color targetColor = originalColor * targetBrightness;
        
        // Giữ alpha gốc
        targetColor.a = originalColor.a;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = brightnessCurve.Evaluate(t);
            
            buttonImage.color = Color.Lerp(startColor, targetColor, easedT);
            
            yield return null;
        }
        
        buttonImage.color = targetColor;
    }
}
