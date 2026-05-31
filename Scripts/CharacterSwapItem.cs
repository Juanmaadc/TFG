using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CharacterChangeItem : MonoBehaviour
{
    [SerializeField] private CharacterSwapManager swapManager;
    [SerializeField] private bool destroyOnUse = true;

    [Header("Pickup delay")]
    [Tooltip("Tiempo inicial durante el cual el libro no puede recogerse. Útil para objetos que aparecen al morir un enemigo.")]
    [SerializeField, Min(0f)] private float initialPickupDelaySeconds = 0f;

    [Tooltip("Si el jugador toca el libro durante el tiempo de bloqueo, deberá salir y volver a entrar para recogerlo. Evita cogerlo sin querer si aparece encima del jugador.")]
    [SerializeField] private bool requirePlayerExitIfTouchedDuringDelay = true;

    [Header("Character preview / target")]
    [Tooltip("Si está activo, el libro elige al aparecer el personaje al que transformará al jugador y lo mantiene fijo.")]
    [SerializeField] private bool assignTargetCharacterOnSpawn = true;

    [Tooltip("ID opcional del personaje objetivo. Si se rellena, el libro siempre transformará al jugador en este personaje y lo mostrará flotando encima.")]
    [SerializeField] private string forcedTargetCharacterId = string.Empty;

    [Header("Floating preview")]
    [SerializeField] private bool showFloatingCharacterPreview = true;
    [SerializeField] private Vector3 previewLocalOffset = new Vector3(0f, 1.1f, 0f);
    [SerializeField, Min(0.1f)] private float previewScaleMultiplier = 1f;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float bobSpeed = 2f;
    [SerializeField] private bool copyTargetSortingLayer = true;
    [SerializeField] private string fallbackSortingLayerName = "Default";
    [SerializeField] private int sortingOrderOffset = 10;

    [Header("Debug")]
    [SerializeField] private bool debugPickupDelayLogs = false;
    [SerializeField] private bool debugPreviewLogs = false;

    private bool used;
    private float pickupAllowedAt;
    private bool playerMustExitBeforePickup;

    private string targetCharacterId = string.Empty;
    private GameObject targetCharacterPrefab;
    private bool targetCharacterResolved;

    private Transform previewRoot;
    private SpriteRenderer previewRenderer;
    private Vector3 previewBaseLocalPosition;
    private float previewTimeOffset;

    private void Awake()
    {
        ApplyInitialDelay();
        ResolveTargetCharacterAndBuildPreview();
    }

    private void OnEnable()
    {
        ApplyInitialDelay();
        ResolveTargetCharacterAndBuildPreview();
    }

    private void Update()
    {
        UpdateFloatingPreview();
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    public void SetPickupDelay(float seconds, bool requireExitIfTouchedDuringDelay = true)
    {
        float delay = Mathf.Max(0f, seconds);
        pickupAllowedAt = Time.time + delay;
        playerMustExitBeforePickup = false;
        requirePlayerExitIfTouchedDuringDelay = requireExitIfTouchedDuringDelay;

        if (debugPickupDelayLogs && delay > 0f)
            Debug.Log($"CharacterChangeItem: recogida bloqueada durante {delay:0.00} segundos.", this);
    }

    private void ApplyInitialDelay()
    {
        if (initialPickupDelaySeconds > 0f)
            SetPickupDelay(initialPickupDelaySeconds, requirePlayerExitIfTouchedDuringDelay);
        else if (pickupAllowedAt <= 0f)
            pickupAllowedAt = Time.time;
    }

    private void ResolveTargetCharacterAndBuildPreview()
    {
        EnsureSwapManager();

        bool needsResolution = !targetCharacterResolved || string.IsNullOrWhiteSpace(targetCharacterId) || targetCharacterPrefab == null;
        if (needsResolution)
            ResolveTargetCharacter();

        if (showFloatingCharacterPreview)
            BuildOrRefreshPreviewVisual();
        else
            RemovePreviewVisual();
    }

    private void ResolveTargetCharacter()
    {
        targetCharacterResolved = false;
        targetCharacterId = string.Empty;
        targetCharacterPrefab = null;

        if (swapManager == null)
            return;

        if (!string.IsNullOrWhiteSpace(forcedTargetCharacterId))
        {
            GameObject currentPlayer = GetCurrentPlayerObject();
            targetCharacterId = forcedTargetCharacterId;
            targetCharacterResolved = true;

            // Recuperamos el prefab del personaje forzado usando la propia API del manager cuando sea posible.
            if (!swapManager.TryGetRandomDifferentCharacter(currentPlayer, out _, out _))
            {
                // no-op, solo sirve para asegurar que el manager ya está listo.
            }

            // Intentar resolver el prefab a partir del propio cambio.
            // Si no se puede, el intercambio por ID seguirá funcionando, solo faltaría la preview.
        }

        if (!targetCharacterResolved && assignTargetCharacterOnSpawn)
        {
            GameObject currentPlayer = GetCurrentPlayerObject();
            if (swapManager.TryGetRandomDifferentCharacter(currentPlayer, out string randomId, out GameObject randomPrefab))
            {
                targetCharacterId = randomId;
                targetCharacterPrefab = randomPrefab;
                targetCharacterResolved = true;
            }
        }

        // Si hay ID forzado pero no prefab forzado, intentamos obtener el prefab buscando entre las opciones del manager
        // mediante diferentes jugadores de referencia hasta encontrar el personaje.
        if (targetCharacterResolved && targetCharacterPrefab == null && !string.IsNullOrWhiteSpace(targetCharacterId))
        {
            targetCharacterPrefab = FindCharacterPrefabByTryingRandomSelections(targetCharacterId);
        }

        if (debugPreviewLogs)
            Debug.Log($"CharacterChangeItem: targetCharacterResolved={targetCharacterResolved}, id='{targetCharacterId}', prefab='{(targetCharacterPrefab != null ? targetCharacterPrefab.name : "null")}'.", this);
    }

    private GameObject FindCharacterPrefabByTryingRandomSelections(string desiredId)
    {
        if (swapManager == null || string.IsNullOrWhiteSpace(desiredId))
            return null;

        GameObject currentPlayer = GetCurrentPlayerObject();
        if (currentPlayer == null)
            return null;

        // Primer intento: si el random ya coincide alguna vez con el deseado.
        for (int i = 0; i < 20; i++)
        {
            if (swapManager.TryGetRandomDifferentCharacter(currentPlayer, out string randomId, out GameObject randomPrefab) && randomId == desiredId)
                return randomPrefab;
        }

        return null;
    }

    private void BuildOrRefreshPreviewVisual()
    {
        if (targetCharacterPrefab == null)
            return;

        SpriteRenderer sourceRenderer = FindFirstVisibleSpriteRenderer(targetCharacterPrefab);
        if (sourceRenderer == null || sourceRenderer.sprite == null)
            return;

        if (previewRoot == null)
        {
            GameObject previewObject = new GameObject("FloatingCharacterPreview");
            previewObject.transform.SetParent(transform, false);
            previewRoot = previewObject.transform;
            previewRenderer = previewObject.AddComponent<SpriteRenderer>();
            previewTimeOffset = Random.Range(0f, 100f);
        }
        else if (previewRenderer == null)
        {
            previewRenderer = previewRoot.GetComponent<SpriteRenderer>();
            if (previewRenderer == null)
                previewRenderer = previewRoot.gameObject.AddComponent<SpriteRenderer>();
        }

        previewBaseLocalPosition = previewLocalOffset;
        previewRoot.localPosition = previewBaseLocalPosition;
        previewRoot.localRotation = Quaternion.identity;
        previewRoot.localScale = sourceRenderer.transform.localScale * previewScaleMultiplier;

        previewRenderer.sprite = sourceRenderer.sprite;
        previewRenderer.color = sourceRenderer.color;
        previewRenderer.flipX = sourceRenderer.flipX;
        previewRenderer.flipY = sourceRenderer.flipY;
        previewRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        previewRenderer.maskInteraction = SpriteMaskInteraction.None;
        previewRenderer.drawMode = SpriteDrawMode.Simple;

        if (copyTargetSortingLayer)
            previewRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        else
            previewRenderer.sortingLayerName = fallbackSortingLayerName;

        previewRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        previewRenderer.enabled = true;
    }

    private void UpdateFloatingPreview()
    {
        if (!showFloatingCharacterPreview || previewRoot == null)
            return;

        float yOffset = bobAmplitude > 0f && bobSpeed > 0f
            ? Mathf.Sin((Time.time + previewTimeOffset) * bobSpeed) * bobAmplitude
            : 0f;

        previewRoot.localPosition = previewBaseLocalPosition + new Vector3(0f, yOffset, 0f);
    }

    private void RemovePreviewVisual()
    {
        if (previewRoot != null)
        {
            Destroy(previewRoot.gameObject);
            previewRoot = null;
            previewRenderer = null;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryPickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        if (IsPickupDelayed())
            MarkPlayerTouchedDuringDelay();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayer(other))
            return;

        if (playerMustExitBeforePickup)
        {
            playerMustExitBeforePickup = false;

            if (debugPickupDelayLogs)
                Debug.Log("CharacterChangeItem: el jugador salió del libro. Ya puede volver a entrar para recogerlo.", this);
        }
    }

    private void TryPickup(Collider2D other)
    {
        if (used)
            return;

        if (!IsPlayer(other))
            return;

        if (IsPickupDelayed())
        {
            MarkPlayerTouchedDuringDelay();
            return;
        }

        if (playerMustExitBeforePickup)
        {
            if (debugPickupDelayLogs)
                Debug.Log("CharacterChangeItem: el jugador tocó el libro durante el bloqueo; debe salir y volver a entrar para recogerlo.", this);

            return;
        }

        EnsureSwapManager();
        if (swapManager == null)
        {
            Debug.LogWarning("CharacterChangeItem: no se encontró CharacterSwapManager.");
            return;
        }

        used = true;

        if (AudioManager2D.Instance != null)
            AudioManager2D.Instance.PlayCharacterChangeBookPickupSound();

        GameObject playerObject = other.gameObject;
        PlayerMovement movement = other.GetComponent<PlayerMovement>();
        if (movement == null)
            movement = other.GetComponentInParent<PlayerMovement>();

        if (movement != null)
            playerObject = movement.gameObject;

        bool swapped = false;
        if (!string.IsNullOrWhiteSpace(targetCharacterId))
            swapped = swapManager.SwapToCharacter(playerObject, targetCharacterId);

        if (!swapped)
            swapManager.SwapToRandomDifferent(playerObject);

        if (destroyOnUse)
            Destroy(gameObject);
    }

    private void EnsureSwapManager()
    {
        if (swapManager == null)
            swapManager = FindObjectOfType<CharacterSwapManager>();
    }

    private GameObject GetCurrentPlayerObject()
    {
        if (swapManager != null && swapManager.CurrentPlayer != null)
            return swapManager.CurrentPlayer;

        return GameObject.FindGameObjectWithTag("Player");
    }

    private bool IsPickupDelayed()
    {
        return Time.time < pickupAllowedAt;
    }

    private void MarkPlayerTouchedDuringDelay()
    {
        if (requirePlayerExitIfTouchedDuringDelay)
            playerMustExitBeforePickup = true;

        if (debugPickupDelayLogs)
        {
            float remaining = Mathf.Max(0f, pickupAllowedAt - Time.time);
            Debug.Log($"CharacterChangeItem: aún no se puede recoger. Quedan {remaining:0.00} segundos.", this);
        }
    }

    private bool IsPlayer(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        Transform root = other.transform.root;
        return root != null && root.CompareTag("Player");
    }

    private SpriteRenderer FindFirstVisibleSpriteRenderer(GameObject rootObject)
    {
        if (rootObject == null)
            return null;

        SpriteRenderer[] renderers = rootObject.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr != null && sr.sprite != null)
                return sr;
        }

        return null;
    }
}
