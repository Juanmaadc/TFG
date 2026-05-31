using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDamageFlash2D : MonoBehaviour
{
    [Header("Flash")]
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Renderers")]
    [Tooltip("Si está activo, busca automáticamente todos los SpriteRenderer del enemigo y sus hijos.")]
    [SerializeField] private bool autoFindSpriteRenderers = true;

    [Tooltip("Actívalo si tus sprites del enemigo están en objetos desactivados al iniciar.")]
    [SerializeField] private bool includeInactiveRenderers = true;

    [Tooltip("Raíz visual donde se buscarán los SpriteRenderer. En prefabs como enemy2, esto debe ser la raíz del enemigo, no solo el hijo con EnemyChaser2D.")]
    [SerializeField] private Transform renderersSearchRoot;

    [SerializeField] private SpriteRenderer[] spriteRenderers;

    [Header("Overlay flash fallback")]
    [Tooltip("Usa una copia roja temporal encima del sprite. Es más robusto para enemigos con Animator, materiales especiales o prefabs divididos en varios hijos, como enemy2.")]
    [SerializeField] private bool useOverlayFlash = true;

    [Tooltip("Si el overlay queda detrás del sprite original, sube este valor.")]
    [SerializeField] private int overlaySortingOrderOffset = 20;

    [Tooltip("Nombre que tendrán los objetos visuales temporales del flash rojo.")]
    [SerializeField] private string overlayName = "DamageRedFlashOverlay";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    private readonly Dictionary<SpriteRenderer, SpriteRenderer> overlayRenderers = new Dictionary<SpriteRenderer, SpriteRenderer>();
    private Coroutine flashRoutine;
    private bool overlayActive;

    private void Awake()
    {
        CacheRenderersIfNeeded(true);
        CaptureOriginalColors();
        EnsureOverlayRenderers();
        SetOverlayActive(false);
    }

    private void OnEnable()
    {
        CacheRenderersIfNeeded(true);
        CaptureOriginalColors();
        EnsureOverlayRenderers();
        SetOverlayActive(false);
    }

    private void LateUpdate()
    {
        if (overlayActive)
            SyncOverlayRenderers();
    }

    private void OnDisable()
    {
        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        SetOverlayActive(false);
        RestoreOriginalColors();
    }

    public void SetRenderersSearchRoot(Transform newRoot)
    {
        if (newRoot == null)
            return;

        if (renderersSearchRoot == newRoot && spriteRenderers != null && spriteRenderers.Length > 0)
            return;

        renderersSearchRoot = newRoot;
        spriteRenderers = null;
        originalColors.Clear();
        overlayRenderers.Clear();
        CacheRenderersIfNeeded(true);
        CaptureOriginalColors();
        EnsureOverlayRenderers();
        SetOverlayActive(false);
    }

    public void Flash()
    {
        Flash(flashColor, flashDuration);
    }

    public void Flash(Color color, float duration)
    {
        if (!isActiveAndEnabled)
            return;

        CacheRenderersIfNeeded(false);

        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            if (debugLogs)
                Debug.LogWarning($"EnemyDamageFlash2D: {name} no encontró ningún SpriteRenderer para aplicar flash rojo.", this);
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
            SetOverlayActive(false);
            RestoreOriginalColors();
        }

        CaptureOriginalColors();
        flashRoutine = StartCoroutine(FlashRoutine(color, Mathf.Max(0.01f, duration)));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        float timer = 0f;

        if (useOverlayFlash)
        {
            EnsureOverlayRenderers();
            ApplyOverlayColor(color);
            SyncOverlayRenderers();
            SetOverlayActive(true);
        }
        else
        {
            SetAllRendererColors(color);
        }

        while (timer < duration)
        {
            if (useOverlayFlash)
            {
                ApplyOverlayColor(color);
                SyncOverlayRenderers();
                SetOverlayActive(true);
            }
            else
            {
                // Se reaplica cada frame para evitar que un Animator sobrescriba el color.
                SetAllRendererColors(color);
            }

            timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        SetOverlayActive(false);
        RestoreOriginalColors();
        flashRoutine = null;
    }

    private void CacheRenderersIfNeeded(bool force)
    {
        if (!force && !autoFindSpriteRenderers && spriteRenderers != null && spriteRenderers.Length > 0)
            return;

        if (!force && spriteRenderers != null && spriteRenderers.Length > 0)
            return;

        Transform root = renderersSearchRoot != null ? renderersSearchRoot : transform;
        SpriteRenderer[] foundRenderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactiveRenderers);

        // Evita que se cacheen overlays creados por este mismo componente.
        List<SpriteRenderer> validRenderers = new List<SpriteRenderer>();
        foreach (SpriteRenderer renderer in foundRenderers)
        {
            if (renderer == null)
                continue;

            if (renderer.gameObject.name.Contains(overlayName))
                continue;

            validRenderers.Add(renderer);
        }

        spriteRenderers = validRenderers.ToArray();
    }

    private void CaptureOriginalColors()
    {
        if (spriteRenderers == null)
            return;

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
                continue;

            if (!originalColors.ContainsKey(spriteRenderer))
                originalColors.Add(spriteRenderer, spriteRenderer.color);
        }
    }

    private void SetAllRendererColors(Color color)
    {
        if (spriteRenderers == null)
            return;

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
                continue;

            spriteRenderer.color = color;
        }
    }

    private void RestoreOriginalColors()
    {
        if (originalColors.Count == 0)
            return;

        foreach (KeyValuePair<SpriteRenderer, Color> entry in originalColors)
        {
            if (entry.Key == null)
                continue;

            entry.Key.color = entry.Value;
        }
    }

    private void EnsureOverlayRenderers()
    {
        if (!useOverlayFlash)
            return;

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        foreach (SpriteRenderer source in spriteRenderers)
        {
            if (source == null)
                continue;

            if (overlayRenderers.ContainsKey(source) && overlayRenderers[source] != null)
                continue;

            GameObject overlayObject = new GameObject(overlayName);
            overlayObject.transform.SetParent(source.transform, false);
            overlayObject.transform.localPosition = Vector3.zero;
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;

            SpriteRenderer overlay = overlayObject.AddComponent<SpriteRenderer>();
            overlayRenderers[source] = overlay;
            CopyRendererSettings(source, overlay);
            overlay.color = flashColor;
            overlay.enabled = false;
            overlayObject.SetActive(false);
        }
    }

    private void SyncOverlayRenderers()
    {
        if (!useOverlayFlash)
            return;

        foreach (KeyValuePair<SpriteRenderer, SpriteRenderer> pair in overlayRenderers)
        {
            SpriteRenderer source = pair.Key;
            SpriteRenderer overlay = pair.Value;

            if (source == null || overlay == null)
                continue;

            CopyRendererSettings(source, overlay);
        }
    }

    private void CopyRendererSettings(SpriteRenderer source, SpriteRenderer overlay)
    {
        if (source == null || overlay == null)
            return;

        overlay.sprite = source.sprite;
        overlay.flipX = source.flipX;
        overlay.flipY = source.flipY;
        overlay.drawMode = source.drawMode;
        overlay.size = source.size;
        overlay.tileMode = source.tileMode;
        overlay.maskInteraction = source.maskInteraction;
        overlay.sortingLayerID = source.sortingLayerID;
        overlay.sortingOrder = source.sortingOrder + overlaySortingOrderOffset;
        overlay.enabled = source.enabled;
    }

    private void ApplyOverlayColor(Color color)
    {
        foreach (SpriteRenderer overlay in overlayRenderers.Values)
        {
            if (overlay == null)
                continue;

            overlay.color = color;
        }
    }

    private void SetOverlayActive(bool active)
    {
        overlayActive = active;

        foreach (SpriteRenderer overlay in overlayRenderers.Values)
        {
            if (overlay == null)
                continue;

            overlay.enabled = active;
            overlay.gameObject.SetActive(active);
        }
    }
}
