using System.Collections.Generic;
using UnityEngine;

public class LevelEnemyRewardDropManager2D : MonoBehaviour
{
    private enum DropType
    {
        Heart,
        CharacterChangeBook
    }

    [Header("Drop prefabs")]
    [Tooltip("Prefab del corazón que cura al jugador. Debe tener HeartHealItem o un Collider2D trigger.")]
    [SerializeField] private GameObject heartPrefab;

    [Tooltip("Prefab del libro que cambia de personaje. Debe tener CharacterChangeItem o un Collider2D trigger.")]
    [SerializeField] private GameObject characterChangeItemPrefab;

    [Header("Drops per level")]
    [SerializeField] private bool dropOneHeartPerLevel = true;
    [SerializeField] private bool dropOneCharacterChangeBookPerLevel = true;

    [Tooltip("Probabilidad base de soltar cada recompensa cuando muere un enemigo. Si no sale por probabilidad, se fuerza antes de que no queden enemigos.")]
    [SerializeField, Range(0f, 1f)] private float baseDropChancePerEnemyDeath = 0.25f;

    [Tooltip("Número mínimo de enemigos muertos antes de permitir drops aleatorios. La garantía final puede ignorar este valor para que nunca falten recompensas.")]
    [SerializeField, Min(0)] private int minimumEnemyDeathsBeforeRandomDrops = 1;

    [Tooltip("Intenta evitar que el corazón y el libro caigan del mismo enemigo. Si es el último enemigo y faltan ambos, caerán ambos igualmente.")]
    [SerializeField] private bool avoidBothDropsOnSameEnemy = true;

    [Tooltip("Garantiza que las recompensas pendientes aparezcan antes de que no queden enemigos vivos en el nivel.")]
    [SerializeField] private bool guaranteeMissingDropsBeforeAllEnemiesDie = true;

    [Tooltip("Si está activo, solo cuentan los enemigos cuyo EnemyHealth2D tenga Counts For Level Clear activado.")]
    [SerializeField] private bool onlyDropFromEnemiesThatCountForLevelClear = true;

    [Header("Spawn position")]
    [SerializeField] private Vector2 baseDropOffset = Vector2.zero;
    [SerializeField, Min(0f)] private float separationWhenBothDrop = 0.7f;
    [SerializeField] private bool parentDropsUnderRuntimeRoot = true;

    [Header("Character change book pickup delay")]
    [Tooltip("Tiempo que debe pasar desde que aparece el libro de cambio de personaje hasta que se puede recoger.")]
    [SerializeField, Min(0f)] private float characterChangeBookPickupDelayAfterDrop = 1f;

    [Tooltip("Si el jugador toca el libro durante el bloqueo, tendrá que salir y volver a entrar para recogerlo.")]
    [SerializeField] private bool requirePlayerExitAfterBookDelay = true;

    [Header("Compatibility with old random item spawners")]
    [Tooltip("Desactiva el spawner antiguo de libro de cambio de personaje para que ya no aparezca aleatoriamente por la dungeon.")]
    [SerializeField] private bool disableRandomCharacterChangeItemSpawners = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool heartDroppedThisLevel;
    private bool characterChangeBookDroppedThisLevel;
    private int enemyDeathsThisLevel;
    private Transform runtimeRoot;

    void Awake()
    {
        DisableOldRandomCharacterChangeSpawnersIfNeeded();
    }

    void OnEnable()
    {
        EnemyHealth2D.OnAnyEnemyDied += HandleEnemyDied;
    }

    void OnDisable()
    {
        EnemyHealth2D.OnAnyEnemyDied -= HandleEnemyDied;
    }

    void Start()
    {
        DisableOldRandomCharacterChangeSpawnersIfNeeded();
    }

    private void HandleEnemyDied(EnemyHealth2D deadEnemy)
    {
        if (deadEnemy == null)
            return;

        if (onlyDropFromEnemiesThatCountForLevelClear && !deadEnemy.CountsForLevelClear)
            return;

        enemyDeathsThisLevel++;

        List<DropType> missingDrops = GetMissingDrops();
        if (missingDrops.Count == 0)
            return;

        Shuffle(missingDrops);

        int remainingEnemiesAfterThisDeath = CountRemainingEligibleEnemies(deadEnemy);
        int dropsThatMustHappenNow = 0;

        if (guaranteeMissingDropsBeforeAllEnemiesDie)
            dropsThatMustHappenNow = Mathf.Max(0, missingDrops.Count - remainingEnemiesAfterThisDeath);

        List<DropType> dropsToSpawn = new List<DropType>();

        for (int i = 0; i < dropsThatMustHappenNow && i < missingDrops.Count; i++)
            dropsToSpawn.Add(missingDrops[i]);

        bool canRollRandomDrop = enemyDeathsThisLevel >= minimumEnemyDeathsBeforeRandomDrops;
        if (canRollRandomDrop)
        {
            foreach (DropType drop in missingDrops)
            {
                if (dropsToSpawn.Contains(drop))
                    continue;

                if (Random.value <= baseDropChancePerEnemyDeath)
                {
                    dropsToSpawn.Add(drop);

                    if (avoidBothDropsOnSameEnemy)
                        break;
                }
            }
        }

        if (dropsToSpawn.Count == 0)
        {
            if (debugLogs)
            {
                Debug.Log($"LevelEnemyRewardDropManager2D: enemigo muerto sin drop. Muertes: {enemyDeathsThisLevel}, enemigos restantes: {remainingEnemiesAfterThisDeath}.", deadEnemy);
            }

            return;
        }

        SpawnDrops(deadEnemy.transform.position, dropsToSpawn);
    }

    private List<DropType> GetMissingDrops()
    {
        List<DropType> missingDrops = new List<DropType>();

        if (dropOneHeartPerLevel && !heartDroppedThisLevel && heartPrefab != null)
            missingDrops.Add(DropType.Heart);

        if (dropOneCharacterChangeBookPerLevel && !characterChangeBookDroppedThisLevel && characterChangeItemPrefab != null)
            missingDrops.Add(DropType.CharacterChangeBook);

        return missingDrops;
    }

    private int CountRemainingEligibleEnemies(EnemyHealth2D deadEnemy)
    {
        EnemyHealth2D[] enemies = FindObjectsOfType<EnemyHealth2D>();
        int count = 0;

        foreach (EnemyHealth2D enemy in enemies)
        {
            if (enemy == null || enemy == deadEnemy || enemy.IsDead)
                continue;

            if (onlyDropFromEnemiesThatCountForLevelClear && !enemy.CountsForLevelClear)
                continue;

            count++;
        }

        return count;
    }

    private void SpawnDrops(Vector3 deathPosition, List<DropType> dropsToSpawn)
    {
        EnsureRuntimeRoot();

        for (int i = 0; i < dropsToSpawn.Count; i++)
        {
            DropType dropType = dropsToSpawn[i];
            GameObject prefab = GetPrefabForDrop(dropType);

            if (prefab == null)
                continue;

            Vector3 spawnPosition = deathPosition + (Vector3)baseDropOffset;

            if (dropsToSpawn.Count > 1)
            {
                float direction = i == 0 ? -1f : 1f;
                spawnPosition += Vector3.right * direction * separationWhenBothDrop * 0.5f;
            }

            Transform parent = parentDropsUnderRuntimeRoot ? runtimeRoot : null;
            GameObject spawnedDrop = Instantiate(prefab, spawnPosition, Quaternion.identity, parent);
            spawnedDrop.name = prefab.name;
            EnsureDropIsUsable(spawnedDrop, dropType);
            MarkDropAsSpawned(dropType);

            if (debugLogs)
                Debug.Log($"LevelEnemyRewardDropManager2D: drop generado -> {dropType} en {spawnPosition}.", spawnedDrop);
        }
    }

    private GameObject GetPrefabForDrop(DropType dropType)
    {
        switch (dropType)
        {
            case DropType.Heart:
                return heartPrefab;
            case DropType.CharacterChangeBook:
                return characterChangeItemPrefab;
            default:
                return null;
        }
    }

    private void MarkDropAsSpawned(DropType dropType)
    {
        switch (dropType)
        {
            case DropType.Heart:
                heartDroppedThisLevel = true;
                break;
            case DropType.CharacterChangeBook:
                characterChangeBookDroppedThisLevel = true;
                break;
        }
    }

    private void EnsureDropIsUsable(GameObject spawnedDrop, DropType dropType)
    {
        if (spawnedDrop == null)
            return;

        Collider2D collider = spawnedDrop.GetComponent<Collider2D>();
        if (collider == null)
            collider = spawnedDrop.AddComponent<CircleCollider2D>();

        collider.isTrigger = true;

        switch (dropType)
        {
            case DropType.Heart:
                if (spawnedDrop.GetComponent<HeartHealItem>() == null && spawnedDrop.GetComponentInChildren<HeartHealItem>() == null)
                    spawnedDrop.AddComponent<HeartHealItem>();
                break;

            case DropType.CharacterChangeBook:
                CharacterChangeItem item = spawnedDrop.GetComponent<CharacterChangeItem>();
                if (item == null)
                    item = spawnedDrop.GetComponentInChildren<CharacterChangeItem>(true);

                if (item == null)
                    item = spawnedDrop.AddComponent<CharacterChangeItem>();

                item.SetPickupDelay(characterChangeBookPickupDelayAfterDrop, requirePlayerExitAfterBookDelay);
                break;
        }
    }

    private void EnsureRuntimeRoot()
    {
        if (!parentDropsUnderRuntimeRoot || runtimeRoot != null)
            return;

        GameObject root = new GameObject("GeneratedEnemyRewardDrops");
        runtimeRoot = root.transform;
    }

    private void DisableOldRandomCharacterChangeSpawnersIfNeeded()
    {
        if (!disableRandomCharacterChangeItemSpawners)
            return;

        CharacterChangeItemSpawner2D[] spawners = FindObjectsOfType<CharacterChangeItemSpawner2D>();
        foreach (CharacterChangeItemSpawner2D spawner in spawners)
        {
            if (spawner == null)
                continue;

            spawner.SetRandomSpawnEnabled(false);
            spawner.ClearCurrentItem();
            spawner.enabled = false;
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        if (list == null || list.Count <= 1)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
