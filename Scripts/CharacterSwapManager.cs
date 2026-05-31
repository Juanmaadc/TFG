using UnityEngine;
using Unity.Cinemachine;

public class CharacterSwapManager : MonoBehaviour
{
    [System.Serializable]
    public class CharacterEntry
    {
        public string id;
        public GameObject prefab;
    }

    [Header("Initial player in scene")]
    [SerializeField] private GameObject initialPlayer;

    [Header("Characters")]
    [SerializeField] private CharacterEntry[] characters;

    [Header("Scene references")]
    [SerializeField] private DungeonMinimapUI minimap;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    private GameObject currentPlayer;

    public GameObject CurrentPlayer => currentPlayer;

    void Start()
    {
        ResolveSceneReferences();

        if (initialPlayer == null)
            initialPlayer = GameObject.FindGameObjectWithTag("Player");

        if (initialPlayer == null)
        {
            Debug.LogError("CharacterSwapManager: no se encontró el Player inicial en la escena.");
            return;
        }

        currentPlayer = BuildSelectedCharacterIfNeeded(initialPlayer);
        EnsurePlayerTag(currentPlayer);
        CharacterSelectionState.CaptureFromPlayer(currentPlayer);
        UpdateSceneReferences(currentPlayer);
        RetargetHeartUIs(currentPlayer);

        // Importante al cambiar de escena: otros scripts pueden haber cogido como objetivo
        // al Player colocado inicialmente en la escena antes de que este gestor lo reemplace.
        // Por eso se fuerza el objetivo del enemigo/cámara al Player definitivo.
        RetargetEnemies(GetTrackingTarget(currentPlayer));
    }

    public void SwapToRandomDifferent(GameObject oldPlayer)
    {
        if (!TryGetRandomDifferentCharacter(oldPlayer, out string targetCharacterId, out _))
            return;

        SwapToCharacter(oldPlayer, targetCharacterId);
    }

    public bool TryGetRandomDifferentCharacter(GameObject referencePlayer, out string characterId, out GameObject prefab)
    {
        characterId = string.Empty;
        prefab = null;

        if (characters == null || characters.Length == 0)
            return false;

        int currentIndex = GetCharacterIndexForPlayer(referencePlayer);
        int nextIndex = GetRandomDifferentIndex(currentIndex);
        if (!IsValidCharacterIndex(nextIndex))
            return false;

        characterId = characters[nextIndex].id;
        prefab = characters[nextIndex].prefab;
        return prefab != null;
    }

    public bool SwapToCharacter(GameObject oldPlayer, string targetCharacterId)
    {
        if (string.IsNullOrWhiteSpace(targetCharacterId))
            return false;

        int targetIndex = FindIndexById(targetCharacterId);
        if (!IsValidCharacterIndex(targetIndex))
        {
            Debug.LogWarning($"CharacterSwapManager: no se encontró el personaje '{targetCharacterId}'.");
            return false;
        }

        return SwapToCharacterByIndex(oldPlayer, targetIndex);
    }

    private bool SwapToCharacterByIndex(GameObject oldPlayer, int targetIndex)
    {
        if (oldPlayer == null || !IsValidCharacterIndex(targetIndex))
            return false;

        if (characters[targetIndex].prefab == null)
        {
            Debug.LogWarning("CharacterSwapManager: el prefab del personaje destino es nulo.");
            return false;
        }

        Vector3 spawnPos = oldPlayer.transform.position;
        Quaternion spawnRot = oldPlayer.transform.rotation;

        int facingSign = oldPlayer.transform.localScale.x >= 0f ? 1 : -1;

        PlayerMovement oldMovement = oldPlayer.GetComponent<PlayerMovement>();

        PrepareOldPlayerForRemoval(oldPlayer);

        GameObject newPlayer = Instantiate(characters[targetIndex].prefab, spawnPos, spawnRot);
        EnsurePlayerTag(newPlayer);

        Vector3 newScale = newPlayer.transform.localScale;
        newScale.x = Mathf.Abs(newScale.x) * facingSign;
        newPlayer.transform.localScale = newScale;

        PlayerMovement newMovement = newPlayer.GetComponent<PlayerMovement>();
        if (oldMovement != null && newMovement != null)
            newMovement.facingDirection = oldMovement.facingDirection;

        currentPlayer = newPlayer;
        CharacterSelectionState.CaptureFromPlayer(currentPlayer);

        UpdateSceneReferences(newPlayer);
        RetargetHeartUIs(newPlayer);
        RetargetEnemies(GetTrackingTarget(newPlayer));

        Destroy(oldPlayer);
        return true;
    }

    private GameObject BuildSelectedCharacterIfNeeded(GameObject scenePlayer)
    {
        if (scenePlayer == null)
            return null;

        if (!CharacterSelectionState.HasSelection)
            return scenePlayer;

        PlayerCharacterId sceneCharacterId = scenePlayer.GetComponent<PlayerCharacterId>();
        if (sceneCharacterId == null)
            sceneCharacterId = scenePlayer.GetComponentInChildren<PlayerCharacterId>();

        string sceneId = sceneCharacterId != null ? sceneCharacterId.characterId : "";
        string selectedId = CharacterSelectionState.SelectedCharacterId;

        if (sceneId == selectedId)
            return scenePlayer;

        if (characters == null || characters.Length == 0)
        {
            Debug.LogWarning($"CharacterSwapManager: no hay lista de personajes para restaurar '{selectedId}'. Se usará el Player colocado en la escena.");
            return scenePlayer;
        }

        int selectedIndex = FindIndexById(selectedId);
        if (selectedIndex < 0 || selectedIndex >= characters.Length || characters[selectedIndex].prefab == null)
        {
            Debug.LogWarning($"CharacterSwapManager: no hay prefab configurado para restaurar el personaje '{selectedId}'. Se usará el Player colocado en la escena.");
            return scenePlayer;
        }

        Vector3 spawnPos = scenePlayer.transform.position;
        Quaternion spawnRot = scenePlayer.transform.rotation;
        int facingSign = scenePlayer.transform.localScale.x >= 0f ? 1 : -1;

        PrepareOldPlayerForRemoval(scenePlayer);

        GameObject restoredPlayer = Instantiate(characters[selectedIndex].prefab, spawnPos, spawnRot);
        EnsurePlayerTag(restoredPlayer);

        Vector3 restoredScale = restoredPlayer.transform.localScale;
        restoredScale.x = Mathf.Abs(restoredScale.x) * facingSign;
        restoredPlayer.transform.localScale = restoredScale;

        Destroy(scenePlayer);
        return restoredPlayer;
    }

    private void ResolveSceneReferences()
    {
        if (minimap == null)
            minimap = FindObjectOfType<DungeonMinimapUI>();

        if (cinemachineCamera == null)
            cinemachineCamera = FindObjectOfType<CinemachineCamera>();
    }

    private void EnsurePlayerTag(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        if (!playerObject.CompareTag("Player"))
            playerObject.tag = "Player";
    }

    private void PrepareOldPlayerForRemoval(GameObject oldPlayer)
    {
        oldPlayer.tag = "Untagged";

        PlayerMovement movement = oldPlayer.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.enabled = false;

        Rigidbody2D rb = oldPlayer.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
        }

        Collider2D[] colliders = oldPlayer.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col in colliders)
            col.enabled = false;
    }

    private void UpdateSceneReferences(GameObject playerObject)
    {
        ResolveSceneReferences();

        Transform trackingTarget = GetTrackingTarget(playerObject);
        if (trackingTarget == null)
            return;

        if (minimap != null)
        {
            // minimap.SetPlayerTarget(trackingTarget);
        }

        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = trackingTarget;
            cinemachineCamera.LookAt = null;

            var target = cinemachineCamera.Target;
            target.TrackingTarget = trackingTarget;
            target.CustomLookAtTarget = false;
            target.LookAtTarget = null;
            cinemachineCamera.Target = target;

            cinemachineCamera.Prioritize();
        }
        else
        {
            Debug.LogWarning("CharacterSwapManager: no se encontró CinemachineCamera en la escena. Asigna la cámara o deja que se encuentre automáticamente.");
        }

        Debug.Log("Tracking target -> " + trackingTarget.name + " pos " + trackingTarget.position);
    }

    private Transform GetTrackingTarget(GameObject playerObject)
    {
        if (playerObject == null)
            return null;

        PlayerMovement movement = playerObject.GetComponent<PlayerMovement>();
        if (movement != null && movement.rb != null)
            return movement.rb.transform;

        PlayerMovement movementInChildren = playerObject.GetComponentInChildren<PlayerMovement>();
        if (movementInChildren != null && movementInChildren.rb != null)
            return movementInChildren.rb.transform;

        Rigidbody2D rb = playerObject.GetComponent<Rigidbody2D>();
        if (rb != null)
            return rb.transform;

        Rigidbody2D rbInChildren = playerObject.GetComponentInChildren<Rigidbody2D>();
        if (rbInChildren != null)
            return rbInChildren.transform;

        return playerObject.transform;
    }

    private void RetargetHeartUIs(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        PlayerHealth2D health = playerObject.GetComponent<PlayerHealth2D>();
        if (health == null)
            health = playerObject.GetComponentInChildren<PlayerHealth2D>(true);

        if (health == null)
            return;

        PlayerHeartsUI[] heartUis = FindObjectsOfType<PlayerHeartsUI>();
        foreach (PlayerHeartsUI heartUi in heartUis)
        {
            if (heartUi != null)
                heartUi.SetTarget(health);
        }
    }

    private void RetargetEnemies(Transform newTarget)
    {
        if (newTarget == null)
            return;

        EnemyChaser2D[] enemies = FindObjectsOfType<EnemyChaser2D>();

        foreach (EnemyChaser2D enemy in enemies)
        {
            if (enemy != null)
                enemy.Retarget(newTarget);
        }

        FireballEnemy2D[] fireballEnemies = FindObjectsOfType<FireballEnemy2D>();

        foreach (FireballEnemy2D enemy in fireballEnemies)
        {
            if (enemy != null)
                enemy.Retarget(newTarget);
        }
    }

    private int GetCharacterIndexForPlayer(GameObject playerObject)
    {
        if (playerObject == null)
            return -1;

        PlayerCharacterId oldId = playerObject.GetComponent<PlayerCharacterId>();
        if (oldId == null)
            oldId = playerObject.GetComponentInChildren<PlayerCharacterId>(true);

        string currentId = oldId != null ? oldId.characterId : string.Empty;
        return FindIndexById(currentId);
    }

    private bool IsValidCharacterIndex(int index)
    {
        return characters != null && index >= 0 && index < characters.Length;
    }

    private int FindIndexById(string id)
    {
        if (characters == null)
            return -1;

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i].id == id)
                return i;
        }

        return -1;
    }

    private int GetRandomDifferentIndex(int currentIndex)
    {
        if (characters == null || characters.Length == 0)
            return -1;

        if (characters.Length == 1)
            return 0;

        int newIndex = currentIndex;

        while (newIndex == currentIndex)
            newIndex = Random.Range(0, characters.Length);

        return newIndex;
    }
}
