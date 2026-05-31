using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a short red full-screen flash when the player receives damage.
/// Add it to a Canvas or to an empty GameObject in each playable scene.
/// It can create the UI overlay automatically if references are not assigned.
/// </summary>
public class PlayerDamageScreenFlash2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup flashCanvasGroup;
    [SerializeField] private Image flashImage;

    [Header("Flash")]
    [SerializeField] private bool createUIIfMissing = true;
    [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.35f);
    [SerializeField] private float flashFadeOutDuration = 0.35f;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool scaleAlphaByDamage = false;
    [SerializeField] private float alphaPerDamagePoint = 0.15f;
    [SerializeField] private float maxAlpha = 0.55f;

    [Header("Filtering")]
    [SerializeField] private bool onlyReactToPlayerTag = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Sorting")]
    [SerializeField] private int autoCanvasSortingOrder = 950;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private Coroutine flashRoutine;

    private void Awake()
    {
        if (createUIIfMissing)
            EnsureUIExists();

        HideInstantly();
    }

    private void OnEnable()
    {
        PlayerHealth2D.OnAnyDamaged += HandlePlayerDamaged;
    }

    private void OnDisable()
    {
        PlayerHealth2D.OnAnyDamaged -= HandlePlayerDamaged;
    }

    private void HandlePlayerDamaged(PlayerHealth2D damagedPlayer, int damage, int currentHealth, int maxHealthValue)
    {
        if (damagedPlayer == null)
            return;

        if (onlyReactToPlayerTag && !BelongsToTaggedPlayer(damagedPlayer.transform))
            return;

        TriggerFlash(damage);
    }

    [ContextMenu("Test Damage Flash")]
    public void TestFlash()
    {
        TriggerFlash(1);
    }

    public void TriggerFlash(int damage = 1)
    {
        if (createUIIfMissing)
            EnsureUIExists();

        if (flashImage == null || flashCanvasGroup == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine(damage));

        if (debugLogs)
            Debug.Log("PlayerDamageScreenFlash2D: halo rojo mostrado por daño.", this);
    }

    private IEnumerator FlashRoutine(int damage)
    {
        float targetAlpha = flashColor.a;

        if (scaleAlphaByDamage)
            targetAlpha = Mathf.Min(maxAlpha, Mathf.Max(flashColor.a, damage * alphaPerDamagePoint));

        Color color = flashColor;
        color.a = 1f;
        flashImage.color = color;

        flashImage.gameObject.SetActive(true);
        flashCanvasGroup.gameObject.SetActive(true);
        flashCanvasGroup.alpha = targetAlpha;
        flashCanvasGroup.interactable = false;
        flashCanvasGroup.blocksRaycasts = false;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, flashFadeOutDuration);

        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            flashCanvasGroup.alpha = Mathf.Lerp(targetAlpha, 0f, t);
            yield return null;
        }

        HideInstantly();
        flashRoutine = null;
    }

    private void HideInstantly()
    {
        if (flashCanvasGroup != null)
        {
            flashCanvasGroup.alpha = 0f;
            flashCanvasGroup.interactable = false;
            flashCanvasGroup.blocksRaycasts = false;
        }

        if (flashImage != null)
            flashImage.gameObject.SetActive(false);
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
            GameObject canvasObject = new GameObject("DamageFlashCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = autoCanvasSortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (flashCanvasGroup == null || flashImage == null)
        {
            GameObject overlayObject = new GameObject("DamageRedHaloOverlay");
            overlayObject.transform.SetParent(canvas.transform, false);

            RectTransform rect = overlayObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            flashImage = overlayObject.AddComponent<Image>();
            flashImage.color = flashColor;
            flashImage.raycastTarget = false;

            flashCanvasGroup = overlayObject.AddComponent<CanvasGroup>();
            flashCanvasGroup.interactable = false;
            flashCanvasGroup.blocksRaycasts = false;
        }
    }

    private bool BelongsToTaggedPlayer(Transform start)
    {
        Transform current = start;

        while (current != null)
        {
            if (current.CompareTag(playerTag))
                return true;

            current = current.parent;
        }

        return false;
    }
}
