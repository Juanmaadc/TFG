using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Convierte visualmente al profesor en un holograma azul/transparente.
/// No cambia la lógica de diálogo: solo modifica el color/alpha de los SpriteRenderer.
/// </summary>
[DisallowMultipleComponent]
public class BossProfessorHologramEffect2D : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Color hologramTint = new Color(0.25f, 0.85f, 1f, 0.55f);
    [SerializeField, Min(0f)] private float fadeInDuration = 0.75f;
    [SerializeField, Range(0f, 0.8f)] private float flickerAmount = 0.18f;
    [SerializeField, Min(0.1f)] private float flickerSpeed = 12f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Behaviour")]
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool restoreOriginalColorsOnDisable = true;

    private readonly List<SpriteRenderer> spriteRenderers = new List<SpriteRenderer>();
    private readonly List<Color> originalColors = new List<Color>();
    private float startTime;
    private bool effectActive;

    private void Awake()
    {
        CacheRenderers();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        if (restoreOriginalColorsOnDisable)
            RestoreOriginalColors();
    }

    private void Update()
    {
        if (!effectActive)
            return;

        ApplyHologramColor();
    }

    public void Configure(Color tint, float fadeDuration, float flicker, float speed, bool unscaledTime)
    {
        hologramTint = tint;
        fadeInDuration = Mathf.Max(0f, fadeDuration);
        flickerAmount = Mathf.Clamp(flicker, 0f, 0.8f);
        flickerSpeed = Mathf.Max(0.1f, speed);
        useUnscaledTime = unscaledTime;

        CacheRenderers();
        Play();
    }

    public void Play()
    {
        CacheRenderers();
        startTime = CurrentTime;
        effectActive = true;
        ApplyHologramColor();
    }

    public void StopAndRestore()
    {
        effectActive = false;
        RestoreOriginalColors();
    }

    private void CacheRenderers()
    {
        SpriteRenderer[] foundRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        bool sameCache = foundRenderers.Length == spriteRenderers.Count;
        if (sameCache)
        {
            for (int i = 0; i < foundRenderers.Length; i++)
            {
                if (foundRenderers[i] != spriteRenderers[i])
                {
                    sameCache = false;
                    break;
                }
            }
        }

        if (sameCache)
            return;

        spriteRenderers.Clear();
        originalColors.Clear();

        foreach (SpriteRenderer sr in foundRenderers)
        {
            if (sr == null)
                continue;

            spriteRenderers.Add(sr);
            originalColors.Add(sr.color);
        }
    }

    private void ApplyHologramColor()
    {
        if (spriteRenderers.Count == 0)
            return;

        float elapsed = Mathf.Max(0f, CurrentTime - startTime);
        float fade = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeInDuration);

        // Dos ondas combinadas para que el parpadeo parezca menos mecánico.
        float waveA = (Mathf.Sin(elapsed * flickerSpeed) + 1f) * 0.5f;
        float waveB = (Mathf.Sin(elapsed * flickerSpeed * 2.17f + 1.3f) + 1f) * 0.5f;
        float flicker = 1f - (waveA * 0.65f + waveB * 0.35f) * flickerAmount;

        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr == null)
                continue;

            Color original = i < originalColors.Count ? originalColors[i] : Color.white;
            Color tinted = Color.Lerp(original, hologramTint, 0.85f);
            tinted.a = original.a * hologramTint.a * fade * flicker;
            sr.color = tinted;
        }
    }

    private void RestoreOriginalColors()
    {
        for (int i = 0; i < spriteRenderers.Count; i++)
        {
            if (spriteRenderers[i] != null && i < originalColors.Count)
                spriteRenderers[i].color = originalColors[i];
        }
    }

    private float CurrentTime => useUnscaledTime ? Time.unscaledTime : Time.time;
}
