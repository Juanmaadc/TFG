using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSceneFlow2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private DungeonRoomsCorridors2D dungeon;
    [SerializeField] private ProfessorEncounterManager2D professorEncounter;

    [Header("Scene flow")]
    [SerializeField] private string bossSceneName;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private float sceneLoadDelay = 1f;
    [SerializeField] private bool requireProfessorEncounterResolved = true;

    private Coroutine watchCoroutine;
    private bool transitionStarted;

    void OnEnable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated += HandleDungeonGenerated;
    }

    void OnDisable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated -= HandleDungeonGenerated;

        if (watchCoroutine != null)
        {
            StopCoroutine(watchCoroutine);
            watchCoroutine = null;
        }
    }

    void Start()
    {
        if (dungeon == null || dungeon.HasGeneratedMap)
            StartWatching();
    }

    private void HandleDungeonGenerated()
    {
        StartWatching();
    }

    private void StartWatching()
    {
        transitionStarted = false;

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
                    SceneManager.LoadScene(bossSceneName);
                }

                yield break;
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    private bool CanAdvanceToBossScene()
    {
        if (requireProfessorEncounterResolved && professorEncounter != null && !professorEncounter.IsEncounterResolved)
            return false;

        EnemyHealth2D[] enemies = FindObjectsByType<EnemyHealth2D>(FindObjectsSortMode.None);
        return enemies == null || enemies.Length == 0;
    }
}
