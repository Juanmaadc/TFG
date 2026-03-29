using UnityEngine;

public class Spawner : MonoBehaviour
{
    public DungeonRoomsCorridors2D generator;
    public GameObject playerPrefab;
    public GameObject exitPrefab;

    void Start()
    {
        if (generator == null)
        {
            Debug.LogError("Spawner: Generator no asignado.");
            return;
        }

        if (!generator.HasGeneratedMap)
        {
            generator.Generate();
        }

        if (!generator.HasGeneratedMap)
        {
            Debug.LogError("Spawner: no se pudo generar un mapa válido.");
            return;
        }

        if (playerPrefab != null)
        {
            if (generator.TryGetSpawnWorldPositionInFirstRoom(out Vector3 playerSpawn))
            {
                Instantiate(playerPrefab, playerSpawn, Quaternion.identity);
            }
            else
            {
                Debug.LogError("Spawner: no se encontró una posición válida para el jugador.");
            }
        }

        if (exitPrefab != null)
        {
            if (generator.TryGetSpawnWorldPositionInLastRoom(out Vector3 exitSpawn))
            {
                Instantiate(exitPrefab, exitSpawn, Quaternion.identity);
            }
            else
            {
                Debug.LogError("Spawner: no se encontró una posición válida para la salida.");
            }
        }
    }
}