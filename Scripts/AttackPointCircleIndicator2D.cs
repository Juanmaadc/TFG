using UnityEngine;

/// <summary>
/// Dibuja un círculo rojo semitransparente en el AttackPoint del jugador.
/// Es una alternativa sencilla al indicador de hitbox: marca visualmente dónde está el punto de ataque.
/// Añádelo al prefab del personaje.
/// </summary>
public class AttackPointCircleIndicator2D : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Se detecta automáticamente si está en el mismo objeto que PlayerMovement.")]
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("Si se deja vacío, se usa el attackPoint de PlayerMovement. Si tampoco existe, se usa la posición del jugador.")]
    [SerializeField] private Transform attackPoint;

    [Header("Circle")]
    [SerializeField] private bool showIndicator = true;

    [Tooltip("Si está activo, usa PlayerMovement.attackRadius como radio del círculo.")]
    [SerializeField] private bool usePlayerAttackRadius = true;

    [Tooltip("Radio manual si no quieres usar PlayerMovement.attackRadius.")]
    [SerializeField] private float manualRadius = 0.7f;

    [Tooltip("Color del círculo. Recomendado: rojo con alpha 0.25 - 0.45.")]
    [SerializeField] private Color indicatorColor = new Color(1f, 0f, 0f, 0.35f);

    [Tooltip("Dibuja solo el borde y una sombra interior suave.")]
    [SerializeField] private bool softFilledCircle = true;

    [Header("Rendering")]
    [Tooltip("Orden de renderizado del círculo. Súbelo si queda detrás del suelo.")]
    [SerializeField] private int sortingOrder = 50;

    [Tooltip("Opcional. Si tu proyecto usa Sorting Layers, escribe aquí el nombre exacto, por ejemplo Player o UI. Vacío = Default.")]
    [SerializeField] private string sortingLayerName = "";

    [Tooltip("Pequeño desplazamiento en Z. En la mayoría de proyectos 2D no hace falta tocarlo.")]
    [SerializeField] private float zOffset = -0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private GameObject indicatorObject;
    private SpriteRenderer indicatorRenderer;
    private static Sprite cachedCircleSprite;

    private void Awake()
    {
        ResolveReferences();
        CreateIndicatorIfNeeded();
        RefreshVisual();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CreateIndicatorIfNeeded();
        RefreshVisual();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        CreateIndicatorIfNeeded();
        RefreshVisual();
        FollowAttackPoint();
    }

    private void ResolveReferences()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (attackPoint == null && playerMovement != null)
            attackPoint = playerMovement.attackPoint;

        if (attackPoint == null)
        {
            Transform found = transform.Find("AttackPoint");
            if (found != null)
                attackPoint = found;
        }
    }

    private void CreateIndicatorIfNeeded()
    {
        if (indicatorObject != null && indicatorRenderer != null)
            return;

        indicatorObject = new GameObject("AttackPoint_Red_Circle_Indicator");
        indicatorObject.transform.SetParent(transform, false);

        indicatorRenderer = indicatorObject.AddComponent<SpriteRenderer>();
        indicatorRenderer.sprite = GetCircleSprite();
        indicatorRenderer.color = indicatorColor;
        indicatorRenderer.sortingOrder = sortingOrder;

        if (!string.IsNullOrWhiteSpace(sortingLayerName))
            indicatorRenderer.sortingLayerName = sortingLayerName;

        if (debugLogs)
            Debug.Log($"AttackPointCircleIndicator2D: creado indicador para {name}.", this);
    }

    private void RefreshVisual()
    {
        if (indicatorRenderer == null)
            return;

        indicatorRenderer.enabled = showIndicator;
        indicatorRenderer.color = indicatorColor;
        indicatorRenderer.sortingOrder = sortingOrder;

        if (!string.IsNullOrWhiteSpace(sortingLayerName))
            indicatorRenderer.sortingLayerName = sortingLayerName;

        float radius = GetCurrentRadius();
        float diameter = radius * 2f;
        indicatorObject.transform.localScale = new Vector3(diameter, diameter, 1f);
    }

    private void FollowAttackPoint()
    {
        if (indicatorObject == null)
            return;

        Vector3 position = attackPoint != null ? attackPoint.position : transform.position;
        position.z += zOffset;
        indicatorObject.transform.position = position;
    }

    private float GetCurrentRadius()
    {
        if (usePlayerAttackRadius && playerMovement != null)
            return Mathf.Max(0.05f, playerMovement.attackRadius);

        return Mathf.Max(0.05f, manualRadius);
    }

    private Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null)
            return cachedCircleSprite;

        const int size = 128;
        const float center = (size - 1) * 0.5f;
        const float outerRadius = size * 0.48f;
        const float borderWidth = 5f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Generated_AttackPoint_Red_Circle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                byte alpha = 0;

                if (distance <= outerRadius)
                {
                    if (distance >= outerRadius - borderWidth)
                    {
                        alpha = 255;
                    }
                    else if (softFilledCircle)
                    {
                        float normalized = distance / outerRadius;
                        alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(80f, 35f, normalized));
                    }
                    else
                    {
                        alpha = 75;
                    }
                }

                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        cachedCircleSprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );

        cachedCircleSprite.name = "Generated_AttackPoint_Circle_Sprite";
        return cachedCircleSprite;
    }

    private void OnDrawGizmosSelected()
    {
        ResolveReferences();

        Gizmos.color = indicatorColor;
        Vector3 center = attackPoint != null ? attackPoint.position : transform.position;
        Gizmos.DrawWireSphere(center, GetCurrentRadius());
    }
}
