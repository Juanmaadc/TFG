using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerHealth2D targetHealth;
    [SerializeField] private bool autoFindPlayerByTag = true;
    [SerializeField] private float retargetCheckInterval = 0.25f;

    [Header("UI")]
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject root;

    [Header("Behavior")]
    [SerializeField] private bool hideIfNoPlayer = false;

    private float retargetTimer;

    void Awake()
    {
        if (slider == null)
            slider = GetComponent<Slider>();

        if (root == null)
            root = gameObject;
    }

    void OnEnable()
    {
        BindToAvailablePlayer();
        RefreshImmediate();
    }

    void OnDisable()
    {
        UnbindCurrentTarget();
    }

    void Update()
    {
        if (!autoFindPlayerByTag)
            return;

        retargetTimer -= Time.unscaledDeltaTime;
        if (retargetTimer > 0f)
            return;

        retargetTimer = retargetCheckInterval;

        if (targetHealth == null || !targetHealth.gameObject.activeInHierarchy)
            BindToAvailablePlayer();
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
            targetHealth.OnHealthChanged += HandleHealthChanged;

        RefreshImmediate();
    }

    private void BindToAvailablePlayer()
    {
        if (targetHealth != null && targetHealth.gameObject.activeInHierarchy)
        {
            RefreshImmediate();
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerHealth2D foundHealth = null;

        if (player != null)
        {
            foundHealth = player.GetComponent<PlayerHealth2D>();
            if (foundHealth == null)
                foundHealth = player.GetComponentInChildren<PlayerHealth2D>();
        }

        SetTarget(foundHealth);
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

    private void RefreshImmediate()
    {
        if (targetHealth == null)
        {
            SetRootVisible(!hideIfNoPlayer);
            ApplyValues(0, 1);
            return;
        }

        SetRootVisible(true);
        ApplyValues(targetHealth.CurrentHealth, targetHealth.MaxHealth);
    }

    private void ApplyValues(int current, int max)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = max;
            slider.value = current;
        }

        if (fillImage != null)
            fillImage.fillAmount = (float)current / max;

        if (healthText != null)
            healthText.text = current + " / " + max;
    }

    private void SetRootVisible(bool visible)
    {
        if (root != null && root.activeSelf != visible)
            root.SetActive(visible);
    }
}
