using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a short green check when a StatBook question is answered correctly,
/// and a short red cross when it is answered incorrectly.
/// Add it to a Canvas or to an empty GameObject in each playable scene.
/// It can create the UI automatically if references are not assigned.
/// </summary>
public class StatBookAnswerFeedbackUI2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup feedbackCanvasGroup;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private Image feedbackImage;

    [Header("Optional sprites")]
    [Tooltip("Si lo asignas, se mostrará esta imagen al acertar. Si está vacío, se usará el texto ✓.")]
    [SerializeField] private Sprite checkSprite;
    [Tooltip("Si lo asignas, se mostrará esta imagen al fallar. Si está vacío, se usará el texto ✕.")]
    [SerializeField] private Sprite crossSprite;

    [Header("Text fallback")]
    [SerializeField] private string correctSymbol = "✓";
    [SerializeField] private string wrongSymbol = "✕";
    [SerializeField] private Color correctColor = new Color(0.1f, 1f, 0.25f, 1f);
    [SerializeField] private Color wrongColor = new Color(1f, 0.05f, 0.05f, 1f);
    [SerializeField, Min(20f)] private float symbolFontSize = 180f;

    [Header("Animation")]
    [SerializeField] private bool createUIIfMissing = true;
    [SerializeField] private float visibleDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.25f;
    [SerializeField] private float startScale = 0.75f;
    [SerializeField] private float endScale = 1.15f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Filtering")]
    [Tooltip("Si está activo, solo reacciona al manager de StatBooks de esta escena. Normalmente debe estar activado.")]
    [SerializeField] private bool onlyReactToActiveSceneManagers = true;

    [Header("Sorting")]
    [SerializeField] private int autoCanvasSortingOrder = 980;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private RectTransform feedbackRect;
    private Coroutine feedbackRoutine;

    private void Awake()
    {
        if (createUIIfMissing)
            EnsureUIExists();

        HideInstantly();
    }

    private void OnEnable()
    {
        StatBookEncounterManager2D.AnyAnswerResolved += HandleStatBookAnswerResolved;
    }

    private void OnDisable()
    {
        StatBookEncounterManager2D.AnyAnswerResolved -= HandleStatBookAnswerResolved;
    }

    private void HandleStatBookAnswerResolved(StatBookEncounterManager2D manager, bool answeredCorrectly)
    {
        if (manager == null)
            return;

        if (onlyReactToActiveSceneManagers && !manager.gameObject.scene.isLoaded)
            return;

        ShowFeedback(answeredCorrectly);
    }

    [ContextMenu("Test Correct Feedback")]
    public void TestCorrectFeedback()
    {
        ShowFeedback(true);
    }

    [ContextMenu("Test Wrong Feedback")]
    public void TestWrongFeedback()
    {
        ShowFeedback(false);
    }

    public void ShowFeedback(bool answeredCorrectly)
    {
        if (createUIIfMissing)
            EnsureUIExists();

        if (feedbackCanvasGroup == null)
            return;

        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);

        ConfigureVisual(answeredCorrectly);
        feedbackRoutine = StartCoroutine(FeedbackRoutine());

        if (debugLogs)
            Debug.Log($"StatBookAnswerFeedbackUI2D: resultado mostrado -> {(answeredCorrectly ? "correcto" : "fallo")}", this);
    }

    private void ConfigureVisual(bool answeredCorrectly)
    {
        Sprite spriteToUse = answeredCorrectly ? checkSprite : crossSprite;
        Color colorToUse = answeredCorrectly ? correctColor : wrongColor;
        string symbolToUse = answeredCorrectly ? correctSymbol : wrongSymbol;

        if (feedbackImage != null)
        {
            feedbackImage.gameObject.SetActive(spriteToUse != null);
            feedbackImage.sprite = spriteToUse;
            feedbackImage.color = colorToUse;
            feedbackImage.raycastTarget = false;
            feedbackImage.preserveAspect = true;
        }

        if (feedbackText != null)
        {
            bool useText = spriteToUse == null;
            feedbackText.gameObject.SetActive(useText);
            feedbackText.text = symbolToUse;
            feedbackText.color = colorToUse;
            feedbackText.fontSize = symbolFontSize;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.raycastTarget = false;
        }
    }

    private IEnumerator FeedbackRoutine()
    {
        feedbackCanvasGroup.gameObject.SetActive(true);
        feedbackCanvasGroup.interactable = false;
        feedbackCanvasGroup.blocksRaycasts = false;
        feedbackCanvasGroup.alpha = 1f;

        if (feedbackRect != null)
            feedbackRect.localScale = Vector3.one * startScale;

        float growDuration = 0.12f;
        float elapsed = 0f;

        while (elapsed < growDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / growDuration);

            if (feedbackRect != null)
                feedbackRect.localScale = Vector3.one * Mathf.Lerp(startScale, 1f, t);

            yield return null;
        }

        float wait = Mathf.Max(0f, visibleDuration);
        elapsed = 0f;
        while (elapsed < wait)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        float fadeDuration = Mathf.Max(0.01f, fadeOutDuration);
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            feedbackCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t);

            if (feedbackRect != null)
                feedbackRect.localScale = Vector3.one * Mathf.Lerp(1f, endScale, t);

            yield return null;
        }

        HideInstantly();
        feedbackRoutine = null;
    }

    private void HideInstantly()
    {
        if (feedbackCanvasGroup != null)
        {
            feedbackCanvasGroup.alpha = 0f;
            feedbackCanvasGroup.interactable = false;
            feedbackCanvasGroup.blocksRaycasts = false;
            feedbackCanvasGroup.gameObject.SetActive(false);
        }
    }

    private void EnsureUIExists()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
            {
#if UNITY_2023_1_OR_NEWER
                canvas = FindFirstObjectByType<Canvas>();
#else
                canvas = FindObjectOfType<Canvas>();
#endif
            }
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("StatBookAnswerFeedbackCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = autoCanvasSortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();
        }
        else
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, autoCanvasSortingOrder);
        }

        if (feedbackCanvasGroup == null)
        {
            GameObject feedbackObject = new GameObject("StatBookAnswerFeedback");
            feedbackObject.transform.SetParent(canvas.transform, false);

            feedbackRect = feedbackObject.AddComponent<RectTransform>();
            feedbackRect.anchorMin = new Vector2(0.5f, 0.5f);
            feedbackRect.anchorMax = new Vector2(0.5f, 0.5f);
            feedbackRect.pivot = new Vector2(0.5f, 0.5f);
            feedbackRect.anchoredPosition = Vector2.zero;
            feedbackRect.sizeDelta = new Vector2(220f, 220f);

            feedbackCanvasGroup = feedbackObject.AddComponent<CanvasGroup>();
            feedbackCanvasGroup.interactable = false;
            feedbackCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            feedbackRect = feedbackCanvasGroup.GetComponent<RectTransform>();
        }

        if (feedbackText == null)
        {
            GameObject textObject = new GameObject("FeedbackSymbolText");
            textObject.transform.SetParent(feedbackCanvasGroup.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            feedbackText = textObject.AddComponent<TextMeshProUGUI>();
            feedbackText.text = correctSymbol;
            feedbackText.fontSize = symbolFontSize;
            feedbackText.alignment = TextAlignmentOptions.Center;
            feedbackText.raycastTarget = false;
        }

        if (feedbackImage == null)
        {
            GameObject imageObject = new GameObject("FeedbackSpriteImage");
            imageObject.transform.SetParent(feedbackCanvasGroup.transform, false);

            RectTransform imageRect = imageObject.AddComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            feedbackImage = imageObject.AddComponent<Image>();
            feedbackImage.raycastTarget = false;
            feedbackImage.preserveAspect = true;
            feedbackImage.gameObject.SetActive(false);
        }
    }
}
