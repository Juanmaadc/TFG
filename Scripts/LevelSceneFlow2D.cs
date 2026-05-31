using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSceneFlow2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonRoomsCorridors2D dungeon;
    [SerializeField] private StatBookEncounterManager2D statBookEncounter;

    [Header("Scene flow")]
    [SerializeField] private string bossSceneName;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private float sceneLoadDelay = 1f;
    [SerializeField] private bool requireStatBooksResolved = true;
    [SerializeField] private bool requireAllEnemiesDefeated = true;

    [Header("Debug")]
    [Tooltip("Actívalo para ver en consola qué está bloqueando el paso de nivel: StatBooks o enemigos vivos.")]
    [SerializeField] private bool debugProgressLogs = false;

    [SerializeField] private float debugLogInterval = 2f;

    private Coroutine watchCoroutine;
    private bool transitionStarted;
    private bool warnedMissingStatBookManager;
    private float nextDebugLogTime;

    void OnEnable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated += HandleDungeonGenerated;

        EnemyHealth2D.OnAnyEnemyDied += HandleAnyEnemyDied;
    }

    void OnDisable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated -= HandleDungeonGenerated;

        EnemyHealth2D.OnAnyEnemyDied -= HandleAnyEnemyDied;

        if (watchCoroutine != null)
        {
            StopCoroutine(watchCoroutine);
            watchCoroutine = null;
        }
    }

    void Start()
    {
        ResolveReferences();

        if (dungeon == null || dungeon.HasGeneratedMap)
            StartWatching();
    }

    private void HandleDungeonGenerated()
    {
        StartWatching();
    }

    private void HandleAnyEnemyDied(EnemyHealth2D enemy)
    {
        // Forzamos una comprobación rápida justo después de matar un enemigo.
        if (!transitionStarted && watchCoroutine == null)
            StartWatching();
    }

    private void ResolveReferences()
    {
        if (dungeon == null)
            dungeon = FindFirstObjectByType<DungeonRoomsCorridors2D>();

        if (statBookEncounter == null)
            statBookEncounter = FindFirstObjectByType<StatBookEncounterManager2D>();
    }

    private void StartWatching()
    {
        ResolveReferences();
        warnedMissingStatBookManager = false;
        transitionStarted = false;
        nextDebugLogTime = 0f;

        if (watchCoroutine != null)
            StopCoroutine(watchCoroutine);

        watchCoroutine = StartCoroutine(WatchLevelState());
    }

    private IEnumerator WatchLevelState()
    {
        yield return null;
        yield return new WaitForSeconds(0.25f);

        while (!transitionStarted)
        {
            if (CanAdvanceToBossScene())
            {
                transitionStarted = true;
                yield return new WaitForSeconds(sceneLoadDelay);

                if (string.IsNullOrWhiteSpace(bossSceneName))
                {
                    Debug.LogWarning("LevelSceneFlow2D: no hay nombre de escena de jefe configurado.");
                }
                else
                {
                    SaveCurrentPlayerSelection();
                    SceneManager.LoadScene(bossSceneName);
                }

                yield break;
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    private void SaveCurrentPlayerSelection()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        CharacterSelectionState.CaptureFromPlayer(player);
    }

    private bool CanAdvanceToBossScene()
    {
        ResolveReferences();

        bool statBooksOk = AreStatBooksResolved();
        int blockingEnemyCount = CountBlockingEnemies();
        bool enemiesOk = !requireAllEnemiesDefeated || blockingEnemyCount == 0;

        if (debugProgressLogs && Time.time >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.time + Mathf.Max(0.25f, debugLogInterval);
            Debug.Log($"LevelSceneFlow2D: progreso -> StatBooks OK: {statBooksOk}, enemigos bloqueando: {blockingEnemyCount}.", this);

            if (blockingEnemyCount > 0)
                LogBlockingEnemies();
        }

        return statBooksOk && enemiesOk;
    }

    private bool AreStatBooksResolved()
    {
        if (!requireStatBooksResolved)
            return true;

        if (statBookEncounter == null)
        {
            if (!warnedMissingStatBookManager)
            {
                warnedMissingStatBookManager = true;
                Debug.LogWarning("LevelSceneFlow2D: requireStatBooksResolved está activo, pero no hay StatBookEncounterManager2D en la escena.");
            }

            return false;
        }

        return statBookEncounter.IsLevelRequirementMet;
    }

    private int CountBlockingEnemies()
    {
        if (!requireAllEnemiesDefeated)
            return 0;

        EnemyHealth2D[] enemies = FindObjectsByType<EnemyHealth2D>(FindObjectsSortMode.None);
        int count = 0;

        foreach (EnemyHealth2D enemy in enemies)
        {
            if (IsBlockingEnemy(enemy))
                count++;
        }

        return count;
    }

    private bool IsBlockingEnemy(EnemyHealth2D enemy)
    {
        if (enemy == null)
            return false;

        if (enemy.IsDead)
            return false;

        if (!enemy.CountsForLevelClear)
            return false;

        if (!enemy.gameObject.activeInHierarchy)
            return false;

        return true;
    }

    private void LogBlockingEnemies()
    {
        EnemyHealth2D[] enemies = FindObjectsByType<EnemyHealth2D>(FindObjectsSortMode.None);

        foreach (EnemyHealth2D enemy in enemies)
        {
            if (!IsBlockingEnemy(enemy))
                continue;

            Debug.Log($"LevelSceneFlow2D: enemigo vivo que bloquea el avance -> {enemy.name} en {enemy.transform.position}. Vida {enemy.CurrentHealth}/{enemy.MaxHealth}.", enemy);
        }
    }
}
