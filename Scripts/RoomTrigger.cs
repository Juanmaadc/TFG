using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class RoomTrigger2D : MonoBehaviour
{
    [SerializeField] private bool activateOnlyOnce = false;
    [SerializeField] private bool sleepEnemiesOnExit = true;
    [SerializeField] private bool returnEnemiesToSpawnOnExit = false;

    private readonly List<EnemyChaser2D> enemies = new();
    private bool alreadyActivated;

    public void Configure(List<EnemyChaser2D> roomEnemies)
    {
        enemies.Clear();
        enemies.AddRange(roomEnemies);

        BoxCollider2D col = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
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
            if (enemy == null) continue;

            enemy.Sleep();

            if (returnEnemiesToSpawnOnExit)
                enemy.ReturnToSpawn();
        }
    }
}