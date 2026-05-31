using System.Collections;
using System.Reflection;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps CinemachineCamera working correctly when a procedural CameraBounds object is used.
/// It retargets the camera to the active Player and refreshes the Confiner 2D after the dungeon bounds are rebuilt.
/// Add this component to the CinemachineCamera in every playable scene.
/// </summary>
[DefaultExecutionOrder(1000)]
public class CinemachineDungeonCameraBootstrap2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private DungeonCameraBounds2D cameraBounds;
    [SerializeField] private string playerTag = "Player";

    [Header("Behaviour")]
    [SerializeField] private bool retargetCameraToPlayer = true;
    [SerializeField] private bool rebuildCameraBounds = true;
    [SerializeField] private bool assignBoundsToConfiner2D = true;
    [SerializeField] private bool invalidateConfinerCache = true;
    [SerializeField] private bool prioritizeCamera = true;

    [Header("Retry")]
    [SerializeField] private bool runOnEnable = true;
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private bool runOnSceneLoaded = true;
    [SerializeField] private int retryFrames = 30;

    [Header("Debug")]
    [SerializeField] private bool debugLogs;

    private Coroutine setupRoutine;

    private void Reset()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
#if UNITY_2023_1_OR_NEWER
        cameraBounds = FindFirstObjectByType<DungeonCameraBounds2D>();
#else
        cameraBounds = FindObjectOfType<DungeonCameraBounds2D>();
#endif
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (runOnSceneLoaded)
            SceneManager.sceneLoaded += OnSceneLoaded;

        if (runOnEnable)
            RestartSetupRoutine();
    }

    private void OnDisable()
    {
        if (runOnSceneLoaded)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (runOnStart)
            RestartSetupRoutine();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (runOnSceneLoaded)
            RestartSetupRoutine();
    }

    [ContextMenu("Setup Camera Now")]
    public void SetupCameraNow()
    {
        ResolveReferences();
        ApplySetup();
    }

    private void RestartSetupRoutine()
    {
        if (!isActiveAndEnabled)
            return;

        if (setupRoutine != null)
            StopCoroutine(setupRoutine);

        setupRoutine = StartCoroutine(SetupForFirstFrames());
    }

    private IEnumerator SetupForFirstFrames()
    {
        int frames = Mathf.Max(1, retryFrames);

        for (int i = 0; i < frames; i++)
        {
            ApplySetup();
            yield return null;
        }

        ApplySetup();
        setupRoutine = null;
    }

    private void ResolveReferences()
    {
        if (cinemachineCamera == null)
            cinemachineCamera = GetComponent<CinemachineCamera>();

        if (cinemachineCamera == null)
        {
#if UNITY_2023_1_OR_NEWER
            cinemachineCamera = FindFirstObjectByType<CinemachineCamera>();
#else
            cinemachineCamera = FindObjectOfType<CinemachineCamera>();
#endif
        }

        if (cameraBounds == null)
        {
#if UNITY_2023_1_OR_NEWER
            cameraBounds = FindFirstObjectByType<DungeonCameraBounds2D>();
#else
            cameraBounds = FindObjectOfType<DungeonCameraBounds2D>();
#endif
        }
    }

    private void ApplySetup()
    {
        ResolveReferences();

        if (rebuildCameraBounds && cameraBounds != null)
            cameraBounds.RebuildBounds();

        if (retargetCameraToPlayer)
            RetargetCamera();

        if (assignBoundsToConfiner2D)
            ConfigureConfiner();
    }

    private void RetargetCamera()
    {
        if (cinemachineCamera == null)
            return;

        Transform target = FindPlayerTrackingTarget();
        if (target == null)
            return;

        cinemachineCamera.Follow = target;
        cinemachineCamera.LookAt = null;

        var cameraTarget = cinemachineCamera.Target;
        cameraTarget.TrackingTarget = target;
        cameraTarget.CustomLookAtTarget = false;
        cameraTarget.LookAtTarget = null;
        cinemachineCamera.Target = cameraTarget;

        if (prioritizeCamera)
            cinemachineCamera.Prioritize();

        if (debugLogs)
            Debug.Log($"CinemachineDungeonCameraBootstrap2D: cámara '{cinemachineCamera.name}' sigue a '{target.name}'.", this);
    }

    private Transform FindPlayerTrackingTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return null;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null && movement.rb != null)
            return movement.rb.transform;

        PlayerMovement movementInChildren = player.GetComponentInChildren<PlayerMovement>(true);
        if (movementInChildren != null && movementInChildren.rb != null)
            return movementInChildren.rb.transform;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb != null)
            return rb.transform;

        Rigidbody2D rbInChildren = player.GetComponentInChildren<Rigidbody2D>(true);
        if (rbInChildren != null)
            return rbInChildren.transform;

        return player.transform;
    }

    private void ConfigureConfiner()
    {
        if (cinemachineCamera == null || cameraBounds == null || cameraBounds.BoundsCollider == null)
            return;

        MonoBehaviour[] behaviours = cinemachineCamera.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            if (!behaviour.GetType().Name.Contains("Confiner2D"))
                continue;

            AssignBoundingShapeIfPossible(behaviour, cameraBounds.BoundsCollider);

            if (invalidateConfinerCache)
            {
                InvokeIfExists(behaviour, "InvalidateBoundingShapeCache");
                InvokeIfExists(behaviour, "InvalidateLensCache");
            }

            if (debugLogs)
                Debug.Log($"CinemachineDungeonCameraBootstrap2D: confiner '{behaviour.GetType().Name}' actualizado con '{cameraBounds.BoundsCollider.name}'.", this);
        }
    }

    private void AssignBoundingShapeIfPossible(MonoBehaviour confiner, Collider2D collider)
    {
        if (confiner == null || collider == null)
            return;

        System.Type type = confiner.GetType();

        PropertyInfo property = type.GetProperty(
            "BoundingShape2D",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (property != null && property.CanWrite && property.PropertyType.IsAssignableFrom(typeof(Collider2D)))
        {
            property.SetValue(confiner, collider);
            return;
        }

        FieldInfo field = type.GetField(
            "m_BoundingShape2D",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field != null && field.FieldType.IsAssignableFrom(typeof(Collider2D)))
        {
            field.SetValue(confiner, collider);
            return;
        }

        field = type.GetField(
            "BoundingShape2D",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (field != null && field.FieldType.IsAssignableFrom(typeof(Collider2D)))
            field.SetValue(confiner, collider);
    }

    private void InvokeIfExists(MonoBehaviour target, string methodName)
    {
        if (target == null)
            return;

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        if (method != null)
            method.Invoke(target, null);
    }
}
