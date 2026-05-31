using UnityEngine;

/// <summary>
/// Shakes the visible camera whenever the active player receives damage.
/// Recommended setup: add this component to the Main Camera, the one that has Cinemachine Brain.
/// It works with Cinemachine because it applies a small visual offset after the camera has been positioned.
/// </summary>
[DefaultExecutionOrder(10000)]
public class PlayerDamageCameraShake2D : MonoBehaviour
{
    [Header("Target Camera")]
    [SerializeField] private Transform cameraToShake;
    [SerializeField] private bool useMainCameraIfMissing = true;

    [Header("Player Filter")]
    [SerializeField] private bool onlyShakeForPlayerTag = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Shake Settings")]
    [SerializeField] private float shakeDuration = 0.18f;
    [SerializeField] private float shakeStrength = 0.18f;
    [SerializeField] private float shakeFrequency = 45f;
    [SerializeField] private bool scaleStrengthByDamage = true;
    [SerializeField] private float maxStrengthMultiplier = 2f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private float remainingTime;
    private float totalTime;
    private float currentStrength;
    private float randomSeedX;
    private float randomSeedY;

    private void Awake()
    {
        ResolveCamera();
    }

    private void OnEnable()
    {
        PlayerHealth2D.OnAnyDamaged += HandlePlayerDamaged;
    }

    private void OnDisable()
    {
        PlayerHealth2D.OnAnyDamaged -= HandlePlayerDamaged;
    }

    private void HandlePlayerDamaged(PlayerHealth2D damagedPlayer, int damage, int currentHealth, int maxHealth)
    {
        if (damagedPlayer == null)
            return;

        if (onlyShakeForPlayerTag && !BelongsToTaggedPlayer(damagedPlayer.transform))
            return;

        StartShake(damage);

        if (debugLogs)
            Debug.Log($"PlayerDamageCameraShake2D: shake por daño al jugador. Daño={damage}, vida={currentHealth}/{maxHealth}.", this);
    }

    public void StartShake(int damage = 1)
    {
        ResolveCamera();

        totalTime = Mathf.Max(0.01f, shakeDuration);
        remainingTime = totalTime;

        float damageMultiplier = scaleStrengthByDamage ? Mathf.Clamp(Mathf.Max(1, damage), 1f, maxStrengthMultiplier) : 1f;
        currentStrength = Mathf.Max(0f, shakeStrength) * damageMultiplier;

        randomSeedX = Random.Range(0f, 1000f);
        randomSeedY = Random.Range(0f, 1000f);
    }

    private void LateUpdate()
    {
        if (remainingTime <= 0f)
            return;

        Transform cam = ResolveCamera();
        if (cam == null)
            return;

        float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        remainingTime -= delta;

        float normalized = Mathf.Clamp01(remainingTime / totalTime);
        float strength = currentStrength * normalized;
        float time = (useUnscaledTime ? Time.unscaledTime : Time.time) * shakeFrequency;

        float offsetX = (Mathf.PerlinNoise(randomSeedX, time) - 0.5f) * 2f * strength;
        float offsetY = (Mathf.PerlinNoise(randomSeedY, time) - 0.5f) * 2f * strength;

        cam.position += new Vector3(offsetX, offsetY, 0f);
    }

    private Transform ResolveCamera()
    {
        if (cameraToShake != null)
            return cameraToShake;

        Camera ownCamera = GetComponent<Camera>();
        if (ownCamera != null)
        {
            cameraToShake = ownCamera.transform;
            return cameraToShake;
        }

        if (useMainCameraIfMissing && Camera.main != null)
        {
            cameraToShake = Camera.main.transform;
            return cameraToShake;
        }

        return null;
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
