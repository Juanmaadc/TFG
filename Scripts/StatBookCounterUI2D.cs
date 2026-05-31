using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StatBookCounterUI2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StatBookEncounterManager2D statBookEncounter;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private Image statBookIconImage;
    [SerializeField] private Sprite statBookIconSprite;

    [Header("Auto setup")]
    [SerializeField] private bool autoFindStatBookEncounter = true;
    [SerializeField] private bool createUIIfMissing = true;
    [SerializeField] private bool autoUseSpriteFromStatBookPrefab = true;
    [SerializeField] private bool retryFindManagerDuringFirstFrames = true;
    [SerializeField, Min(1)] private int retryFrames = 60;

    [Header("Display")]
    [SerializeField] private string counterFormat = "{0}/{1}";
    [SerializeField] private bool hideIfNoStatBookManager = true;
    [SerializeField] private Vector2 anchoredPosition = new Vector2(28f, -90f);
    [SerializeField] private Vector2 iconSize = new Vector2(42f, 42f);
    [SerializeField] private float textFontSize = 34f;
    [SerializeField] private Color textColor = Color.white;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Canvas canvas;
    private RectTransform generatedRoot;
    private int lastCurrent = int.MinValue;
    private int lastRequired = int.MinValue;
    private Coroutine retryCoroutine;

    void Reset()
    {
        autoFindStatBookEncounter = true;
        createUIIfMissing = true;
        autoUseSpriteFromStatBookPrefab = true;
        retryFindManagerDuringFirstFrames = true;
        retryFrames = 60;
        counterFormat = "{0}/{1}";
        hideIfNoStatBookManager = true;
        anchoredPosition = new Vector2(28f, -90f);
        iconSize = new Vector2(42f, 42f);
        textFontSize = 34f;
        textColor = Color.white;
    }

    void Awake()
    {
        ResolveReferences();
        EnsureUI();
        UpdateFromManager();
    }

    void OnEnable()
    {
        StatBookEncounterManager2D.AnyProgressChanged += HandleAnyProgressChanged;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        ResolveReferences();
        EnsureUI();
        UpdateFromManager();

        if (retryFindManagerDuringFirstFrames)
            StartRetryCoroutine();
    }

    void OnDisable()
    {
        StatBookEncounterManager2D.AnyProgressChanged -= HandleAnyProgressChanged;
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (retryCoroutine != null)
        {
            StopCoroutine(retryCoroutine);
            retryCoroutine = null;
        }
    }

    void Update()
    {
        // Respaldo para casos en los que el manager se reconstruye por generación procedural
        // antes de que esta UI se haya podido suscribir a un evento.
        if (statBookEncounter == null && autoFindStatBookEncounter)
            ResolveReferences();

        UpdateFromManager();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        statBookEncounter = null;
        lastCurrent = int.MinValue;
        lastRequired = int.MinValue;

        ResolveReferences();
        EnsureUI();
        UpdateFromManager();

        if (retryFindManagerDuringFirstFrames)
            StartRetryCoroutine();
    }

    private void HandleAnyProgressChanged(StatBookEncounterManager2D manager, int current, int required)
    {
        if (manager == null)
            return;

        if (statBookEncounter == null || statBookEncounter == manager)
        {
            statBookEncounter = manager;
            EnsureUI();
            SetCounter(current, required);
        }
    }

    private void StartRetryCoroutine()
    {
        if (retryCoroutine != null)
            StopCoroutine(retryCoroutine);

        retryCoroutine = StartCoroutine(RetryFindManager());
    }

    private IEnumerator RetryFindManager()
    {
        int frames = Mathf.Max(1, retryFrames);

        for (int i = 0; i < frames; i++)
        {
            ResolveReferences();
            EnsureUI();
            UpdateFromManager();
            yield return null;
        }

        retryCoroutine = null;
    }

    private void ResolveReferences()
    {
        if (autoFindStatBookEncounter && statBookEncounter == null)
            statBookEncounter = FindFirstObjectByType<StatBookEncounterManager2D>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
    }

    private void EnsureUI()
    {
        if (!createUIIfMissing)
        {
            TryResolveIconSprite();
            ApplyIconSprite();
            return;
        }

        if (counterText != null && statBookIconImage != null)
        {
            TryResolveIconSprite();
            ApplyIconSprite();
            return;
        }

        EnsureCanvas();

        if (generatedRoot == null)
            generatedRoot = CreateGeneratedRoot();

        if (statBookIconImage == null)
            statBookIconImage = CreateIcon(generatedRoot);

        if (counterText == null)
            counterText = CreateText(generatedRoot);

        TryResolveIconSprite();
        ApplyIconSprite();
    }

    private void EnsureCanvas()
    {
        if (canvas != null)
            return;

        GameObject canvasGO = new GameObject("HUDCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
    }

    private RectTransform CreateGeneratedRoot()
    {
        GameObject rootGO = new GameObject("StatBookCounterUI_Generated");
        rootGO.transform.SetParent(canvas.transform, false);

        RectTransform rt = rootGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(180f, 56f);

        HorizontalLayoutGroup layout = rootGO.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rootGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return rt;
    }

    private Image CreateIcon(RectTransform parent)
    {
        GameObject iconGO = new GameObject("StatBookIcon");
        iconGO.transform.SetParent(parent, false);

        Image image = iconGO.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = true;

        RectTransform rt = iconGO.GetComponent<RectTransform>();
        rt.sizeDelta = iconSize;

        LayoutElement layout = iconGO.AddComponent<LayoutElement>();
        layout.preferredWidth = iconSize.x;
        layout.preferredHeight = iconSize.y;

        return image;
    }

    private TMP_Text CreateText(RectTransform parent)
    {
        GameObject textGO = new GameObject("StatBookCounterText");
        textGO.transform.SetParent(parent, false);

        TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.fontSize = textFontSize;
        text.color = textColor;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.text = string.Format(counterFormat, 0, 2);

        RectTransform rt = textGO.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100f, iconSize.y);

        LayoutElement layout = textGO.AddComponent<LayoutElement>();
        layout.preferredWidth = 100f;
        layout.preferredHeight = iconSize.y;

        return text;
    }

    private void TryResolveIconSprite()
    {
        if (statBookIconSprite != null || !autoUseSpriteFromStatBookPrefab || statBookEncounter == null)
            return;

        GameObject prefab = statBookEncounter.StatBookPrefab;
        if (prefab == null)
            return;

        SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);

        if (renderer != null && renderer.sprite != null)
        {
            statBookIconSprite = renderer.sprite;
            if (debugLogs)
                Debug.Log($"StatBookCounterUI2D: usando automáticamente el sprite '{renderer.sprite.name}' del prefab StatBook.", this);
        }
    }

    private void ApplyIconSprite()
    {
        if (statBookIconImage == null)
            return;

        if (statBookIconSprite != null)
        {
            statBookIconImage.sprite = statBookIconSprite;
            statBookIconImage.enabled = true;
            statBookIconImage.preserveAspect = true;
        }
        else
        {
            statBookIconImage.enabled = false;
        }
    }

    private void UpdateFromManager()
    {
        if (statBookEncounter == null)
        {
            SetVisible(!hideIfNoStatBookManager);

            if (!hideIfNoStatBookManager)
                SetCounter(0, 2);

            return;
        }

        SetVisible(true);
        SetCounter(statBookEncounter.CorrectAnswersThisLevel, statBookEncounter.RequiredCorrectStatBooks);
    }

    private void SetCounter(int current, int required)
    {
        required = Mathf.Max(1, required);
        current = Mathf.Clamp(current, 0, required);

        if (lastCurrent == current && lastRequired == required)
            return;

        lastCurrent = current;
        lastRequired = required;

        if (counterText != null)
            counterText.text = string.Format(counterFormat, current, required);

        if (debugLogs)
            Debug.Log($"StatBookCounterUI2D: contador actualizado a {current}/{required}.", this);
    }

    private void SetVisible(bool visible)
    {
        if (generatedRoot != null && generatedRoot.gameObject.activeSelf != visible)
            generatedRoot.gameObject.SetActive(visible);

        if (counterText != null && counterText.gameObject.activeSelf != visible)
            counterText.gameObject.SetActive(visible);

        if (statBookIconImage != null && statBookIconImage.gameObject.activeSelf != visible)
            statBookIconImage.gameObject.SetActive(visible);
    }
}
