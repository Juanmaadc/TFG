using UnityEngine;

public class EnemyDeathSpawner2D : MonoBehaviour
{
    [Header("Spawn on death")]
    [Tooltip("Prefab del enemigo que aparecerá cuando este enemigo muera.")]
    [SerializeField] private GameObject enemyPrefabToSpawn;

    [Tooltip("Si rellenas esta lista, se elegirá aleatoriamente uno de estos prefabs en vez de usar solo Enemy Prefab To Spawn.")]
    [SerializeField] private GameObject[] possibleEnemyPrefabs;

    [Tooltip("Cantidad de enemigos que aparecerán al morir este enemigo.")]
    [SerializeField] private int enemiesToSpawn = 2;

    [Tooltip("Distancia alrededor del enemigo muerto donde aparecerán los enemigos nuevos.")]
    [SerializeField] private float spawnRadius = 0.75f;

    [Tooltip("Pequeño ángulo aleatorio para que no aparezcan siempre exactamente en la misma orientación.")]
    [SerializeField] private bool randomizeSpawnAngle = true;

    [Header("Behaviour of spawned enemies")]
    [Tooltip("Si está activo, los enemigos nuevos se despiertan automáticamente y atacan al Player actual.")]
    [SerializeField] private bool wakeSpawnedEnemies = true;

    [Tooltip("Si está activo, los enemigos nuevos se añaden al RoomTrigger de la habitación actual cuando existe.")]
    [SerializeField] private bool addSpawnedEnemiesToCurrentRoomTrigger = true;

    [Tooltip("Si está activo, los enemigos nuevos se colocan bajo el mismo padre que el enemigo muerto.")]
    [SerializeField] private bool parentSpawnedEnemiesToSameParent = true;

    [Tooltip("Si está desactivado, se elimina EnemyDeathSpawner2D de los enemigos generados para evitar divisiones infinitas accidentales.")]
    [SerializeField] private bool spawnedEnemiesCanSplitAgain = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool hasSpawned;

    private void OnEnable()
    {
        EnemyHealth2D.OnAnyEnemyDied += HandleAnyEnemyDied;
    }

    private void OnDisable()
    {
        EnemyHealth2D.OnAnyEnemyDied -= HandleAnyEnemyDied;
    }

    private void HandleAnyEnemyDied(EnemyHealth2D deadEnemy)
    {
        if (hasSpawned)
            return;

        if (!BelongsToThisEnemy(deadEnemy))
            return;

        SpawnEnemies(deadEnemy);
    }

    private bool BelongsToThisEnemy(EnemyHealth2D deadEnemy)
    {
        if (deadEnemy == null)
            return false;

        Transform deadTransform = deadEnemy.transform;

        if (deadTransform == transform)
            return true;

        if (deadTransform.IsChildOf(transform))
            return true;

        if (transform.IsChildOf(deadTransform))
            return true;

        return false;
    }

    private void SpawnEnemies(EnemyHealth2D deadEnemy)
    {
        GameObject prefab = GetPrefabToSpawn();

        if (prefab == null)
        {
            Debug.LogWarning($"EnemyDeathSpawner2D: {name} no tiene ningún prefab asignado para generar enemigos al morir.", this);
            return;
        }

        hasSpawned = true;

        int amount = Mathf.Max(0, enemiesToSpawn);
        if (amount == 0)
            return;

        Vector3 origin = deadEnemy != null ? deadEnemy.transform.position : transform.position;
        Transform parent = parentSpawnedEnemiesToSameParent ? transform.parent : null;
        Transform player = FindPlayerTransform();
        RoomTrigger2D currentRoomTrigger = addSpawnedEnemiesToCurrentRoomTrigger ? GetComponentInParent<RoomTrigger2D>() : null;

        float initialAngle = randomizeSpawnAngle ? Random.Range(0f, 360f) : 0f;

        for (int i = 0; i < amount; i++)
        {
            Vector3 spawnPosition = GetSpawnPosition(origin, i, amount, initialAngle);
            GameObject spawnedEnemy = Instantiate(prefab, spawnPosition, Quaternion.identity, parent);

            if (!spawnedEnemiesCanSplitAgain)
                RemoveDeathSpawnerFromSpawnedEnemy(spawnedEnemy);

            ConfigureSpawnedEnemy(spawnedEnemy, player, currentRoomTrigger);
        }

        if (debugLogs)
            Debug.Log($"EnemyDeathSpawner2D: {name} ha generado {amount} enemigo(s) al morir.", this);
    }

    private GameObject GetPrefabToSpawn()
    {
        if (possibleEnemyPrefabs != null && possibleEnemyPrefabs.Length > 0)
        {
            int attempts = 0;

            while (attempts < possibleEnemyPrefabs.Length)
            {
                GameObject candidate = possibleEnemyPrefabs[Random.Range(0, possibleEnemyPrefabs.Length)];
                if (candidate != null)
                    return candidate;

                attempts++;
            }
        }

        return enemyPrefabToSpawn;
    }

    private Vector3 GetSpawnPosition(Vector3 origin, int index, int total, float initialAngle)
    {
        if (total <= 1 || spawnRadius <= 0f)
            return origin;

        float angle = initialAngle + (360f / total) * index;
        Vector2 offset = new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
        ) * spawnRadius;

        return origin + (Vector3)offset;
    }

    private void ConfigureSpawnedEnemy(GameObject spawnedEnemy, Transform player, RoomTrigger2D roomTrigger)
    {
        if (spawnedEnemy == null)
            return;

        EnemyChaser2D[] chasers = spawnedEnemy.GetComponentsInChildren<EnemyChaser2D>(true);

        foreach (EnemyChaser2D chaser in chasers)
        {
            if (chaser == null)
                continue;

            if (roomTrigger != null)
                roomTrigger.AddEnemy(chaser);

            if (player != null)
            {
                if (wakeSpawnedEnemies)
                    chaser.WakeUp(player);
                else
                    chaser.Retarget(player);
            }
        }

        FireballEnemy2D[] fireballEnemies = spawnedEnemy.GetComponentsInChildren<FireballEnemy2D>(true);

        foreach (FireballEnemy2D fireballEnemy in fireballEnemies)
        {
            if (fireballEnemy != null && player != null)
                fireballEnemy.Retarget(player);
        }
    }

    private void RemoveDeathSpawnerFromSpawnedEnemy(GameObject spawnedEnemy)
    {
        if (spawnedEnemy == null)
            return;

        EnemyDeathSpawner2D[] deathSpawners = spawnedEnemy.GetComponentsInChildren<EnemyDeathSpawner2D>(true);

        foreach (EnemyDeathSpawner2D deathSpawner in deathSpawners)
        {
            if (deathSpawner != null)
                Destroy(deathSpawner);
        }
    }

    private Transform FindPlayerTransform()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        return playerObject != null ? playerObject.transform : null;
    }
}
