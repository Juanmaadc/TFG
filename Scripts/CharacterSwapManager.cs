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

    void Start()
    {
        if (initialPlayer == null)
            initialPlayer = GameObject.FindGameObjectWithTag("Player");

        if (initialPlayer == null)
        {
            Debug.LogError("CharacterSwapManager: no se encontró el Player inicial en la escena.");
            return;
        }

        currentPlayer = initialPlayer;
        UpdateSceneReferences(currentPlayer);
    }

    public void SwapToRandomDifferent(GameObject oldPlayer)
    {
        if (oldPlayer == null || characters == null || characters.Length == 0)
            return;

        PlayerCharacterId oldId = oldPlayer.GetComponent<PlayerCharacterId>();
        string currentId = oldId != null ? oldId.characterId : "";

        int currentIndex = FindIndexById(currentId);
        int nextIndex = GetRandomDifferentIndex(currentIndex);

        if (nextIndex < 0 || characters[nextIndex].prefab == null)
        {
            Debug.LogWarning("CharacterSwapManager: no se encontró prefab válido para el cambio.");
            return;
        }

        Vector3 spawnPos = oldPlayer.transform.position;
        Quaternion spawnRot = oldPlayer.transform.rotation;

        int facingSign = oldPlayer.transform.localScale.x >= 0f ? 1 : -1;

        PlayerMovement oldMovement = oldPlayer.GetComponent<PlayerMovement>();

        PrepareOldPlayerForRemoval(oldPlayer);

        GameObject newPlayer = Instantiate(characters[nextIndex].prefab, spawnPos, spawnRot);

        Vector3 newScale = newPlayer.transform.localScale;
        newScale.x = Mathf.Abs(newScale.x) * facingSign;
        newPlayer.transform.localScale = newScale;

        PlayerMovement newMovement = newPlayer.GetComponent<PlayerMovement>();
        if (oldMovement != null && newMovement != null)
            newMovement.facingDirection = oldMovement.facingDirection;

        currentPlayer = newPlayer;

        UpdateSceneReferences(newPlayer);
        RetargetAwakeEnemies(GetTrackingTarget(newPlayer));

        Destroy(oldPlayer);
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
        Transform trackingTarget = GetTrackingTarget(playerObject);

        if (minimap != null)
            //minimap.SetPlayerTarget(trackingTarget);

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

    private void RetargetAwakeEnemies(Transform newTarget)
    {
        EnemyChaser2D[] enemies = FindObjectsOfType<EnemyChaser2D>();

        foreach (EnemyChaser2D enemy in enemies)
        {
            if (enemy != null && enemy.IsAwake)
                enemy.Retarget(newTarget);
        }
    }

    private int FindIndexById(string id)
    {
        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i].id == id)
                return i;
        }

        return -1;
    }

    private int GetRandomDifferentIndex(int currentIndex)
    {
        if (characters.Length == 0)
            return -1;

        if (characters.Length == 1)
            return 0;

        int newIndex = currentIndex;

        while (newIndex == currentIndex)
            newIndex = Random.Range(0, characters.Length);

        return newIndex;
    }
}