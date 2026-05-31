using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHeartsUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerHealth2D targetHealth;
    [SerializeField] private bool autoFindPlayerByTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float retargetCheckInterval = 0.25f;

    [Header("Heart UI")]
    [Tooltip("Padre donde se crearán los corazones. Si está vacío, se usará este mismo objeto.")]
    [SerializeField] private Transform heartsContainer;

    [Tooltip("Prefab UI de un corazón. Debe ser un GameObject con componente Image. Es opcional si asignas Full Heart Sprite.")]
    [SerializeField] private Image heartPrefab;

    [Tooltip("Sprite del corazón lleno. Úsalo si el prefab no trae ya el sprite asignado.")]
    [SerializeField] private Sprite fullHeartSprite;

    [Tooltip("Sprite opcional para vida perdida. Si no lo asignas y Hide Missing Hearts está activo, los corazones perdidos se ocultarán.")]
    [SerializeField] private Sprite emptyHeartSprite;

    [Tooltip("Si está activo, al perder vida desaparece el corazón. Si está desactivado, intentará mostrar Empty Heart Sprite.")]
    [SerializeField] private bool hideMissingHearts = true;

    [Tooltip("Tamaño usado cuando no se proporciona Heart Prefab y el script crea las imágenes automáticamente.")]
    [SerializeField] private Vector2 generatedHeartSize = new Vector2(48f, 48f);

    [Header("Behavior")]
    [Tooltip("Objeto visual que se oculta/muestra. Si es este mismo GameObject, se usará CanvasGroup para no desactivar el script.")]
    [SerializeField] private GameObject root;
    [SerializeField] private bool hideIfNoPlayer = false;
    [SerializeField] private bool rebuildWhenMaxHealthChanges = true;

    [Tooltip("Escucha cualquier cambio real de vida en PlayerHealth2D. Útil si el personaje inicial tiene la vida en un hijo distinto al que encuentra la UI.")]
    [SerializeField] private bool listenToAnyPlayerHealthChange = true;

    [Tooltip("Además del evento, comprueba el valor de vida cada frame. Evita que la UI se quede desactualizada si se perdió el evento.")]
    [SerializeField] private bool pollTargetHealthEveryFrame = true;

    [SerializeField] private bool debugLogs = false;

    private readonly List<Image> heartImages = new List<Image>();
    private float retargetTimer;
    private int lastBuiltMaxHealth = -1;
    private int lastShownCurrent = -999;
    private int lastShownMax = -999;
    private CanvasGroup rootCanvasGroup;
    private Coroutine delayedBindRoutine;

    void Awake()
    {
        if (heartsContainer == null)
            heartsContainer = transform;

        if (root == null)
            root = gameObject;

        PrepareRootVisibilityHandler();
    }

    void Start()
    {
        // En algunas escenas el CharacterSwapManager asigna/instancia el jugador en Start.
        // Por eso volvemos a buscar al final del primer frame.
        RequestDelayedBind();
    }

    void OnEnable()
    {
        if (listenToAnyPlayerHealthChange)
            PlayerHealth2D.OnAnyHealthChanged += HandleAnyHealthChanged;

        BindToAvailablePlayer(forceRefresh: true);
        RefreshImmediate();
        RequestDelayedBind();
    }

    void OnDisable()
    {
        if (listenToAnyPlayerHealthChange)
            PlayerHealth2D.OnAnyHealthChanged -= HandleAnyHealthChanged;

        UnbindCurrentTarget();

        if (delayedBindRoutine != null)
        {
            StopCoroutine(delayedBindRoutine);
            delayedBindRoutine = null;
        }
    }

    void Update()
    {
        if (pollTargetHealthEveryFrame && targetHealth != null && targetHealth.gameObject.activeInHierarchy)
        {
            if (targetHealth.CurrentHealth != lastShownCurrent || targetHealth.MaxHealth != lastShownMax)
                ApplyValues(targetHealth.CurrentHealth, targetHealth.MaxHealth);
        }

        if (!autoFindPlayerByTag)
            return;

        retargetTimer -= Time.unscaledDeltaTime;
        if (retargetTimer > 0f)
            return;

        retargetTimer = retargetCheckInterval;

        // Rebuscar también si no tenemos target. Esto arregla el caso del personaje inicial
        // cuando la UI se activa antes de que el CharacterSwapManager marque el Player.
        if (targetHealth == null || !targetHealth.gameObject.activeInHierarchy)
            BindToAvailablePlayer(forceRefresh: false);
    }

    public void SetTarget(PlayerHealth2D newTarget)
    {
        if (targetHealth == newTarget)
        {
            RefreshImmediate();
            return;
        }

        UnbindCurrentTarget();
        targetHealth = newTarget;

        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged += HandleHealthChanged;

            if (debugLogs)
                Debug.Log("PlayerHeartsUI: objetivo asignado -> " + targetHealth.name, targetHealth);
        }

        RefreshImmediate();
    }

    public void SetTargetFromPlayer(GameObject playerObject)
    {
        SetTarget(FindBestHealthOnObject(playerObject));
    }

    private void RequestDelayedBind()
    {
        if (!autoFindPlayerByTag || !isActiveAndEnabled)
            return;

        if (delayedBindRoutine != null)
            StopCoroutine(delayedBindRoutine);

        delayedBindRoutine = StartCoroutine(DelayedBindNextFrame());
    }

    private IEnumerator DelayedBindNextFrame()
    {
        yield return null;
        BindToAvailablePlayer(forceRefresh: true);
        delayedBindRoutine = null;
    }

    private void BindToAvailablePlayer(bool forceRefresh)
    {
        if (!forceRefresh && targetHealth != null && targetHealth.gameObject.activeInHierarchy)
        {
            RefreshImmediate();
            return;
        }

        PlayerHealth2D foundHealth = FindBestPlayerHealth();
        SetTarget(foundHealth);
    }

    private PlayerHealth2D FindBestPlayerHealth()
    {
        // 1) Si hubo un cambio real de vida recientemente, usamos ese componente.
        // Esto arregla el caso en el que el personaje principal tiene varios objetos/hijos
        // y la UI se enganchó a una vida distinta a la que recibe el daño.
        PlayerHealth2D lastChanged = PlayerHealth2D.LastChangedHealth;
        if (IsUsablePlayerHealth(lastChanged))
            return lastChanged;

        // 2) Primero intentamos usar el jugador que conoce el CharacterSwapManager.
        CharacterSwapManager swapManager = FindObjectOfType<CharacterSwapManager>();
        if (swapManager != null && swapManager.CurrentPlayer != null)
        {
            PlayerHealth2D fromSwap = FindBestHealthOnObject(swapManager.CurrentPlayer);
            if (IsUsablePlayerHealth(fromSwap))
                return fromSwap;
        }

        // 3) Luego buscamos por tag Player.
        if (autoFindPlayerByTag && !string.IsNullOrEmpty(playerTag))
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            PlayerHealth2D foundHealth = FindBestHealthOnObject(player);

            if (IsUsablePlayerHealth(foundHealth))
                return foundHealth;
        }

        // 4) Respaldo: si hay un PlayerHealth2D activo en escena, lo usamos.
        // Esto cubre el caso de que el personaje inicial todavía no tenga tag Player.
        PlayerHealth2D[] allHealths = FindObjectsOfType<PlayerHealth2D>();
        foreach (PlayerHealth2D health in allHealths)
        {
            if (IsUsablePlayerHealth(health))
                return health;
        }

        return null;
    }

    private PlayerHealth2D FindBestHealthOnObject(GameObject playerObject)
    {
        if (playerObject == null)
            return null;

        // Preferimos una vida que esté en el mismo objeto que el movimiento, porque suele ser
        // el objeto real del jugador. Si no existe, usamos la primera vida activa encontrada.
        PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
        if (movement == null)
            movement = playerObject.GetComponentInChildren<PlayerMovement>(true);

        if (movement != null)
        {
            PlayerHealth2D sameObjectHealth = movement.GetComponent<PlayerHealth2D>();
            if (IsUsablePlayerHealth(sameObjectHealth))
                return sameObjectHealth;

            PlayerHealth2D parentHealth = movement.GetComponentInParent<PlayerHealth2D>();
            if (IsUsablePlayerHealth(parentHealth))
                return parentHealth;
        }

        PlayerHealth2D directHealth = playerObject.GetComponent<PlayerHealth2D>();
        if (IsUsablePlayerHealth(directHealth))
            return directHealth;

        PlayerHealth2D[] childHealths = playerObject.GetComponentsInChildren<PlayerHealth2D>(true);
        foreach (PlayerHealth2D health in childHealths)
        {
            if (IsUsablePlayerHealth(health))
                return health;
        }

        return null;
    }

    private bool IsUsablePlayerHealth(PlayerHealth2D health)
    {
        return health != null && health.gameObject.activeInHierarchy;
    }

    private bool ShouldUseChangedHealth(PlayerHealth2D changedHealth)
    {
        if (!IsUsablePlayerHealth(changedHealth))
            return false;

        if (targetHealth == null || targetHealth == changedHealth)
            return true;

        CharacterSwapManager swapManager = FindObjectOfType<CharacterSwapManager>();
        if (swapManager != null && swapManager.CurrentPlayer != null)
        {
            Transform currentRoot = swapManager.CurrentPlayer.transform;
            Transform changedTransform = changedHealth.transform;

            if (changedTransform == currentRoot || changedTransform.IsChildOf(currentRoot))
                return true;
        }

        // Si ambos pertenecen al mismo personaje raíz, también lo aceptamos.
        if (targetHealth.transform.root == changedHealth.transform.root)
            return true;

        // En este proyecto PlayerHealth2D solo representa vida del jugador, así que si llega
        // un evento de daño/curación activo, es más fiable que mantener una referencia vieja.
        return true;
    }

    private void UnbindCurrentTarget()
    {
        if (targetHealth != null)
            targetHealth.OnHealthChanged -= HandleHealthChanged;
    }

    private void HandleHealthChanged(int current, int max)
    {
        ApplyValues(current, max);
    }

    private void HandleAnyHealthChanged(PlayerHealth2D changedHealth, int current, int max)
    {
        if (!ShouldUseChangedHealth(changedHealth))
            return;

        if (targetHealth != changedHealth)
            SetTarget(changedHealth);
        else
            ApplyValues(current, max);
    }

    private void RefreshImmediate()
    {
        if (targetHealth == null)
        {
            SetRootVisible(!hideIfNoPlayer);

            if (heartImages.Count > 0)
                ApplyValues(0, Mathf.Max(1, lastBuiltMaxHealth));

            return;
        }

        SetRootVisible(true);
        ApplyValues(targetHealth.CurrentHealth, targetHealth.MaxHealth);
    }

    private void ApplyValues(int current, int max)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);

        if (rebuildWhenMaxHealthChanges || heartImages.Count != max)
            EnsureHeartCount(max);

        for (int i = 0; i < heartImages.Count; i++)
        {
            Image heart = heartImages[i];
            if (heart == null)
                continue;

            bool isFull = i < current;

            if (isFull)
            {
                heart.gameObject.SetActive(true);

                if (fullHeartSprite != null)
                    heart.sprite = fullHeartSprite;

                heart.enabled = true;
            }
            else
            {
                if (hideMissingHearts || emptyHeartSprite == null)
                {
                    heart.gameObject.SetActive(false);
                }
                else
                {
                    heart.gameObject.SetActive(true);
                    heart.sprite = emptyHeartSprite;
                    heart.enabled = true;
                }
            }
        }

        lastShownCurrent = current;
        lastShownMax = max;

        if (debugLogs)
            Debug.Log($"PlayerHeartsUI: vida mostrada {current}/{max}" + (targetHealth != null ? " desde " + targetHealth.name : ""), this);
    }

    private void EnsureHeartCount(int desiredCount)
    {
        desiredCount = Mathf.Max(1, desiredCount);

        for (int i = heartImages.Count - 1; i >= 0; i--)
        {
            if (heartImages[i] == null)
                heartImages.RemoveAt(i);
        }

        while (heartImages.Count < desiredCount)
        {
            Image newHeart = CreateHeartImage();
            heartImages.Add(newHeart);
        }

        while (heartImages.Count > desiredCount)
        {
            int lastIndex = heartImages.Count - 1;
            Image extraHeart = heartImages[lastIndex];
            heartImages.RemoveAt(lastIndex);

            if (extraHeart != null)
                Destroy(extraHeart.gameObject);
        }

        lastBuiltMaxHealth = desiredCount;
    }

    private Image CreateHeartImage()
    {
        Image newHeart;

        if (heartPrefab != null)
        {
            newHeart = Instantiate(heartPrefab, heartsContainer);
        }
        else
        {
            GameObject heartObject = new GameObject("Heart", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            heartObject.transform.SetParent(heartsContainer, false);

            newHeart = heartObject.GetComponent<Image>();
            newHeart.raycastTarget = false;

            RectTransform rect = heartObject.GetComponent<RectTransform>();
            rect.sizeDelta = generatedHeartSize;
        }

        newHeart.name = "Heart";
        newHeart.raycastTarget = false;

        if (fullHeartSprite != null)
            newHeart.sprite = fullHeartSprite;

        newHeart.preserveAspect = true;
        newHeart.gameObject.SetActive(true);

        return newHeart;
    }

    private void PrepareRootVisibilityHandler()
    {
        if (root == null)
            return;

        // Si root es este mismo objeto, no lo desactivamos con SetActive(false), porque eso
        // pararía este script y no podría volver a encontrar al personaje inicial.
        if (root == gameObject)
        {
            rootCanvasGroup = root.GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null)
                rootCanvasGroup = root.AddComponent<CanvasGroup>();
        }
    }

    private void SetRootVisible(bool visible)
    {
        if (root == null)
            return;

        if (root == gameObject)
        {
            if (rootCanvasGroup == null)
                PrepareRootVisibilityHandler();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = visible ? 1f : 0f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }
        }
        else if (root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }
}
