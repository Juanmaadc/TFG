using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Convierte un Button estándar de Unity en una línea de texto clicable para integrarlo visualmente en una página/libro.
/// El botón sigue recibiendo clicks, pero el feedback visual se aplica sobre el texto.
/// </summary>
[RequireComponent(typeof(Button))]
public class BookAnswerLineButton2D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color normalColor = new Color(0.23f, 0.12f, 0.035f, 1f);
    [SerializeField] private Color hoverColor = new Color(0.78f, 0.52f, 0.16f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.48f, 0.24f, 0.07f, 1f);
    [SerializeField] private Color disabledColor = new Color(0.35f, 0.28f, 0.2f, 0.55f);
    [SerializeField] private bool underlineOnHover = true;
    [SerializeField] private bool scaleOnHover = false;
    [SerializeField] private float hoverScale = 1.035f;

    private Button button;
    private bool pointerInside;
    private bool pointerPressed;
    private FontStyles baseFontStyle;
    private bool hasBaseFontStyle;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
        CaptureBaseFontStyleIfNeeded();
    }

    private void OnEnable()
    {
        pointerInside = false;
        pointerPressed = false;
        CaptureBaseFontStyleIfNeeded();
        ApplyVisualState();
    }

    private void OnDisable()
    {
        pointerInside = false;
        pointerPressed = false;
        ResetScale();
        RestoreBaseFontStyle();
    }

    private void LateUpdate()
    {
        // Si otro script/animator cambia el texto o el estado interactable, mantenemos el color correcto.
        ApplyVisualState();
    }

    public void Configure(
        TMP_Text newLabel,
        Color newNormalColor,
        Color newHoverColor,
        Color newPressedColor,
        Color newDisabledColor,
        bool newUnderlineOnHover,
        bool newScaleOnHover,
        float newHoverScale)
    {
        label = newLabel;
        normalColor = newNormalColor;
        hoverColor = newHoverColor;
        pressedColor = newPressedColor;
        disabledColor = newDisabledColor;
        underlineOnHover = newUnderlineOnHover;
        scaleOnHover = newScaleOnHover;
        hoverScale = Mathf.Max(1f, newHoverScale);

        if (button == null)
            button = GetComponent<Button>();

        baseScale = transform.localScale;
        hasBaseFontStyle = false;
        CaptureBaseFontStyleIfNeeded();
        ApplyVisualState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        ApplyVisualState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        pointerPressed = false;
        ApplyVisualState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerPressed = true;
        ApplyVisualState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pointerPressed = false;
        ApplyVisualState();
    }

    private void CaptureBaseFontStyleIfNeeded()
    {
        if (label == null || hasBaseFontStyle)
            return;

        baseFontStyle = label.fontStyle;
        hasBaseFontStyle = true;
    }

    private void ApplyVisualState()
    {
        if (label == null)
            return;

        CaptureBaseFontStyleIfNeeded();

        bool interactable = button == null || button.interactable;

        if (!interactable)
        {
            label.color = disabledColor;
            RestoreBaseFontStyle();
            ResetScale();
            return;
        }

        if (pointerPressed)
            label.color = pressedColor;
        else if (pointerInside)
            label.color = hoverColor;
        else
            label.color = normalColor;

        if (underlineOnHover && pointerInside)
            label.fontStyle = baseFontStyle | FontStyles.Underline;
        else
            RestoreBaseFontStyle();

        if (scaleOnHover && pointerInside)
            transform.localScale = baseScale * hoverScale;
        else
            ResetScale();
    }

    private void RestoreBaseFontStyle()
    {
        if (label != null && hasBaseFontStyle)
            label.fontStyle = baseFontStyle;
    }

    private void ResetScale()
    {
        transform.localScale = baseScale;
    }
}
