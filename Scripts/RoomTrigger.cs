using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RoomTrigger2D : MonoBehaviour
{
    [SerializeField] private bool activateOnlyOnce = false;

    [Tooltip("Si está desactivado, los enemigos que se despiertan en una sala seguirán persiguiendo al jugador aunque salga de ella.")]
    [SerializeField] private bool sleepEnemiesOnExit = false;

    [SerializeField] private bool returnEnemiesToSpawnOnExit = false;

    private readonly List<EnemyChaser2D> enemies = new();
    private bool alreadyActivated;

    public void Configure(List<EnemyChaser2D> roomEnemies)
    {
        enemies.Clear();

        if (roomEnemies != null)
            enemies.AddRange(roomEnemies);

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
    }

    public void AddEnemy(EnemyChaser2D enemy)
    {
        if (enemy == null || enemies.Contains(enemy))
            return;

        enemies.Add(enemy);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (activateOnlyOnce && alreadyActivated)
            return;

        alreadyActivated = true;

        Transform player = other.transform;

        foreach (EnemyChaser2D enemy in enemies)
        {
            if (enemy != null)
                enemy.WakeUp(player);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (!sleepEnemiesOnExit)
            return;

        foreach (EnemyChaser2D enemy in enemies)
        {
            if (enemy == null)
                continue;

            enemy.Sleep();

            if (returnEnemiesToSpawnOnExit)
                enemy.ReturnToSpawn();
        }
    }
}
