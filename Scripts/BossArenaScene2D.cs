using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public class BossArenaScene2D : MonoBehaviour
{
    private enum CaptiveProfessorCorner2D
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [Header("Arena tiles")]
    [SerializeField] private Tilemap floorTilemap;
    [SerializeField] private Tilemap wallTilemap;
    [SerializeField] private TileBase floorTile;
    [SerializeField] private TileBase wallTile;

    [Header("Arena shape")]
    [SerializeField, Min(5)] private int arenaSize = 24;
    [SerializeField, Min(1)] private int wallThickness = 1;
    [SerializeField] private Vector3Int arenaCenterCell = Vector3Int.zero;
    [SerializeField] private bool clearTilemapsBeforeBuild = true;
    [SerializeField] private bool buildOnAwake = true;

    [Header("Visual wall fill outside arena")]
    [Tooltip("Mantiene el mismo cuadrado jugable, pero rellena con wallTile una zona grande alrededor para que no se vea el fondo azul fuera de la arena.")]
    [SerializeField] private bool fillVisibleOutsideArenaWithWallTile = true;

    [Tooltip("Cantidad de celdas extra de pared que se pintan fuera de la franja normal de muros. No aumenta el tamaño jugable, solo cubre visualmente el exterior.")]
    [SerializeField, Min(0)] private int outsideWallFillPaddingCells = 12;

    [Tooltip("Si está activo, también pinta el borde normal de paredes aunque outsideWallFillPaddingCells sea 0.")]
    [SerializeField] private bool alwaysBuildArenaWallBand = true;

    [Header("Boss activation")]
    [SerializeField] private EnemyChaser2D bossEnemy;
    [SerializeField] private List<EnemyChaser2D> additionalEnemiesToWake = new List<EnemyChaser2D>();
    [SerializeField] private bool autoFindEnemiesIfListIsEmpty = false;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool wakeEnemiesOnStart = true;
    [SerializeField, Min(0f)] private float playerLookupRetryDuration = 2f;
    [SerializeField, Min(0.05f)] private float playerLookupRetryInterval = 0.2f;

    [Header("Scene flow after boss defeated")]
    [Tooltip("Si está activo, al derrotar a todos los enemigos de la escena de jefe se activará el flujo posterior al jefe.")]
    [SerializeField] private bool advanceWhenBossDefeated = true;

    [Tooltip("Actívalo solo en el jefe del tercer nivel.")]
    [SerializeField] private bool finalBoss = false;

    [Tooltip("Escena de nivel normal que se cargará después de hablar con el profesor. Ejemplo: Level2 o Level3. En el jefe final también puede usarse como escena de victoria si endGameSceneName está vacío.")]
    [SerializeField] private string nextLevelSceneName;

    [Tooltip("Escena de final/victoria que se cargará después de hablar con el profesor en el jefe final. Ejemplo: VictoryScene, MainMenu o Creditos.")]
    [SerializeField] private string endGameSceneName;

    [Tooltip("Si está activo, el jefe final cargará nextLevelSceneName cuando endGameSceneName esté vacío. Esto evita quedarse bloqueado en la sala final por olvidar configurar End Game Scene Name.")]
    [SerializeField] private bool useNextLevelSceneIfFinalEndSceneIsEmpty = true;

    [Tooltip("Solo se usa si finalBoss está activo y endGameSceneName y nextLevelSceneName están vacíos.")]
    [SerializeField] private bool quitGameIfFinalBossHasNoEndScene = false;

    [Header("Victory screen")]
    [Tooltip("Si está activo, al completar el jefe final se mostrará una pantalla de victoria con el mismo estilo que Game Over, en lugar de cargar directamente otra escena.")]
    [SerializeField] private bool showVictoryScreenOnFinalBoss = true;

    [SerializeField, TextArea(2, 5)] private string victoryMessage = "¡Has salvado al profesor! Muchas gracias por jugar.\nPulsa Space para ir al menú principal.";

    [SerializeField] private string victoryMainMenuSceneName = "MainMenu";
    [SerializeField] private KeyCode victoryReturnKey = KeyCode.Space;

    [SerializeField, Min(0.05f)] private float bossDefeatCheckInterval = 0.25f;
    [SerializeField, Min(0f)] private float sceneLoadDelay = 1f;

    [Tooltip("Evita que la escena avance por error si todavía no se ha creado o detectado ningún jefe/enemigo.")]
    [SerializeField] private bool requireAtLeastOneEnemyBeforeAdvancing = true;

    [SerializeField, Min(0f)] private float noEnemyWarningDelay = 3f;

    [Header("Professor story after boss")]
    [Tooltip("Si está activo, al derrotar al jefe aparecerá el profesor y el jugador deberá hablar con él antes de cambiar de escena.")]
    [SerializeField] private bool requireProfessorStoryBeforeAdvancing = true;

    [SerializeField] private GameObject professorPrefab;
    [SerializeField] private ProfessorQuestionUI2D storyUI;

    [Tooltip("Opcional. Si está asignado, el profesor aparecerá aquí. Si no, aparecerá cerca del jugador.")]
    [SerializeField] private Transform professorSpawnPoint;

    [SerializeField] private bool spawnProfessorNearPlayer = true;
    [SerializeField] private Vector3 professorSpawnOffsetFromPlayer = new Vector3(1.5f, 0f, 0f);
    [SerializeField] private bool professorTalksAutomaticallyOnTouch = false;
    [SerializeField] private bool destroyProfessorAfterStory = false;

    [Header("Final boss captive professor")]
    [Tooltip("Actívalo para que, en el jefe final, el profesor esté ya en la escena encerrado en una celda. Al derrotar al jefe se borran los muros y se puede hablar con él.")]
    [SerializeField] private bool useCaptiveProfessorForFinalBoss = true;

    [Tooltip("Si está activo, la celda del profesor solo se usará cuando Final Boss esté activado.")]
    [SerializeField] private bool captiveProfessorOnlyOnFinalBoss = true;

    [Tooltip("Profesor ya colocado manualmente en la escena. Si lo dejas vacío, se instanciará captiveProfessorPrefab o professorPrefab.")]
    [SerializeField] private Transform captiveProfessorInScene;

    [Tooltip("Prefab opcional del profesor para la escena final. Si está vacío se usará Professor Prefab.")]
    [SerializeField] private GameObject captiveProfessorPrefab;

    [Tooltip("Punto exacto donde se colocará el profesor encerrado. Si está vacío, se calcula automáticamente en la esquina elegida.")]
    [SerializeField] private Transform captiveProfessorSpawnPoint;

    [Tooltip("Si está activo, mueve el profesor al centro de la celda automáticamente al iniciar la escena.")]
    [SerializeField] private bool moveCaptiveProfessorToPrison = true;

    [Tooltip("Esquina de la arena donde estará encerrado el profesor si no hay captiveProfessorSpawnPoint.")]
    [SerializeField] private CaptiveProfessorCorner2D captiveProfessorCorner = CaptiveProfessorCorner2D.TopRight;

    [Tooltip("Tamaño de la celda del profesor en tiles. Recomendado: 5x5 o 6x6.")]
    [SerializeField] private Vector2Int captivePrisonSize = new Vector2Int(5, 5);

    [Tooltip("Separación de la celda respecto a las paredes interiores de la arena.")]
    [SerializeField] private Vector2Int captivePrisonOffsetFromArenaCorner = new Vector2Int(2, 2);

    [Tooltip("Tile especial para los muros que encierran al profesor. Si está vacío, se usará Wall Tile.")]
    [SerializeField] private TileBase captivePrisonWallTile;

    [SerializeField] private bool buildCaptiveProfessorPrison = true;
    [SerializeField] private bool clearCaptivePrisonWhenBossDies = true;
    [SerializeField] private GameObject captivePrisonReleaseEffectPrefab;


    [Header("Professor hologram effect")]
    [Tooltip("Si está activo, en los jefes que NO son finales el profesor aparecerá como holograma. En el jefe final aparecerá normal.")]
    [SerializeField] private bool useHologramForNonFinalBosses = true;

    [SerializeField] private Color hologramTint = new Color(0.25f, 0.85f, 1f, 0.55f);
    [SerializeField, Min(0f)] private float hologramFadeInDuration = 0.75f;
    [SerializeField, Range(0f, 0.8f)] private float hologramFlickerAmount = 0.18f;
    [SerializeField, Min(0.1f)] private float hologramFlickerSpeed = 12f;
    [SerializeField] private bool hologramUsesUnscaledTime = true;

    [SerializeField] private string continueButtonText = "Continuar";
    [SerializeField] private string finishButtonText = "Finalizar";

    [Tooltip("Historia que cuenta el profesor tras derrotar al jefe de los niveles 1 y 2. Cada elemento es una página de diálogo.")]
    [SerializeField, TextArea(3, 8)] private string[] storyPagesAfterBoss =
    {
        "No puedo mantener esta conexión durante mucho tiempo... Has debilitado la barrera del jefe, pero sigo atrapado.",
        "Sigue avanzando y encuentra más StatBooks. Cada pista nos acercará más a mi ubicación real."
    };

    [Tooltip("Historia que cuenta el profesor tras derrotar al jefe del nivel 3. Cada elemento es una página de diálogo.")]
    [SerializeField, TextArea(3, 8)] private string[] finalBossStoryPages =
    {
        "¡Lo has conseguido! Al derrotar al último jefe has salvado al profesor.",
        "Gracias a todos los StatBooks y a las pistas que reuniste, el profesor ha podido ser liberado."
    };

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = false;

    private Coroutine wakeRoutine;
    private Coroutine bossDefeatRoutine;
    private bool transitionStarted;
    private bool hasSeenEnemy;
    private bool warnedNoEnemyFound;

    private Transform runtimeRoot;
    private BossProfessorNPC2D currentStoryProfessor;
    private bool professorStoryOpen;
    private bool professorStoryResolved;
    private int currentStoryPageIndex;
    private PlayerMovement lockedPlayerMovement;

    void Awake()
    {
        if (buildOnAwake)
            BuildArena();
    }

    void Start()
    {
        Time.timeScale = 1f;
        ResolveStoryReferences();
        SetupCaptiveProfessorAtStart();

        if (wakeEnemiesOnStart)
            wakeRoutine = StartCoroutine(WakeEnemiesWhenPlayerIsAvailable());

        if (advanceWhenBossDefeated)
            bossDefeatRoutine = StartCoroutine(WatchBossDefeat());
    }

    void OnDisable()
    {
        if (wakeRoutine != null)
        {
            StopCoroutine(wakeRoutine);
            wakeRoutine = null;
        }

        if (bossDefeatRoutine != null)
        {
            StopCoroutine(bossDefeatRoutine);
            bossDefeatRoutine = null;
        }

        if (professorStoryOpen)
            CloseStoryUI(false);
    }

    [ContextMenu("Build boss arena")]
    public void BuildArena()
    {
        if (floorTilemap == null || wallTilemap == null)
        {
            Debug.LogWarning("BossArenaScene2D: faltan floorTilemap o wallTilemap.");
            return;
        }

        if (floorTile == null || wallTile == null)
        {
            Debug.LogWarning("BossArenaScene2D: faltan floorTile o wallTile.");
            return;
        }

        if (clearTilemapsBeforeBuild)
        {
            floorTilemap.ClearAllTiles();
            wallTilemap.ClearAllTiles();
        }

        int innerHalf = arenaSize / 2;
        int minX = arenaCenterCell.x - innerHalf;
        int minY = arenaCenterCell.y - innerHalf;
        int maxX = minX + arenaSize - 1;
        int maxY = minY + arenaSize - 1;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                floorTilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
            }
        }

        int baseOuterMinX = minX - wallThickness;
        int baseOuterMinY = minY - wallThickness;
        int baseOuterMaxX = maxX + wallThickness;
        int baseOuterMaxY = maxY + wallThickness;

        int visualPadding = fillVisibleOutsideArenaWithWallTile ? outsideWallFillPaddingCells : 0;

        int outerMinX = baseOuterMinX - visualPadding;
        int outerMinY = baseOuterMinY - visualPadding;
        int outerMaxX = baseOuterMaxX + visualPadding;
        int outerMaxY = baseOuterMaxY + visualPadding;

        for (int x = outerMinX; x <= outerMaxX; x++)
        {
            for (int y = outerMinY; y <= outerMaxY; y++)
            {
                bool insideFloor = x >= minX && x <= maxX && y >= minY && y <= maxY;
                if (insideFloor)
                    continue;

                bool withinNormalWallBand =
                    x >= baseOuterMinX && x <= baseOuterMaxX &&
                    y >= baseOuterMinY && y <= baseOuterMaxY;

                bool withinVisualFill = fillVisibleOutsideArenaWithWallTile;

                if ((alwaysBuildArenaWallBand && withinNormalWallBand) || withinVisualFill)
                    wallTilemap.SetTile(new Vector3Int(x, y, 0), wallTile);
            }
        }

        if (ShouldUseCaptiveProfessor() && buildCaptiveProfessorPrison)
            BuildCaptiveProfessorPrison();

        wallTilemap.CompressBounds();
        floorTilemap.CompressBounds();
    }

    private IEnumerator WakeEnemiesWhenPlayerIsAvailable()
    {
        float deadline = Time.unscaledTime + playerLookupRetryDuration;

        while (true)
        {
            Transform player = FindPlayerTransform();
            if (player != null)
            {
                WakeConfiguredEnemies(player);
                yield break;
            }

            if (Time.unscaledTime >= deadline)
            {
                Debug.LogWarning("BossArenaScene2D: no se encontró al jugador para activar al jefe.");
                yield break;
            }

            yield return new WaitForSecondsRealtime(playerLookupRetryInterval);
        }
    }

    private IEnumerator WatchBossDefeat()
    {
        yield return null;
        yield return new WaitForSeconds(0.25f);

        float noEnemyWarningTime = Time.time + noEnemyWarningDelay;

        while (!transitionStarted)
        {
            int aliveEnemies = CountLivingEnemies();

            if (aliveEnemies > 0)
            {
                hasSeenEnemy = true;
            }
            else if (hasSeenEnemy || !requireAtLeastOneEnemyBeforeAdvancing)
            {
                transitionStarted = true;
                yield return new WaitForSeconds(sceneLoadDelay);
                HandleBossDefeated();
                yield break;
            }
            else if (!warnedNoEnemyFound && Time.time >= noEnemyWarningTime)
            {
                warnedNoEnemyFound = true;
                Debug.LogWarning("BossArenaScene2D: no se ha detectado ningún EnemyHealth2D en la escena de jefe. No se avanzará de escena hasta detectar y derrotar al menos un enemigo.");
            }

            yield return new WaitForSeconds(bossDefeatCheckInterval);
        }
    }

    private int CountLivingEnemies()
    {
        EnemyHealth2D[] enemies = FindObjectsByType<EnemyHealth2D>(FindObjectsSortMode.None);
        return enemies != null ? enemies.Length : 0;
    }

    private void HandleBossDefeated()
    {
        SaveCurrentPlayerSelection();

        if (requireProfessorStoryBeforeAdvancing)
        {
            if (ShouldUseCaptiveProfessor())
            {
                ReleaseCaptiveProfessorAndAllowStory();
                return;
            }

            SpawnProfessorForStory();
            return;
        }

        AdvanceAfterProfessorStory();
    }

    private bool ShouldUseCaptiveProfessor()
    {
        return useCaptiveProfessorForFinalBoss && (!captiveProfessorOnlyOnFinalBoss || finalBoss);
    }

    private void SetupCaptiveProfessorAtStart()
    {
        if (!ShouldUseCaptiveProfessor())
            return;

        GameObject professorGO = null;

        if (captiveProfessorInScene != null)
        {
            professorGO = captiveProfessorInScene.gameObject;
        }
        else
        {
            GameObject prefabToUse = captiveProfessorPrefab != null ? captiveProfessorPrefab : professorPrefab;
            if (prefabToUse == null)
            {
                Debug.LogWarning("BossArenaScene2D: está activado el profesor encerrado, pero no hay captiveProfessorPrefab ni professorPrefab asignado.", this);
                return;
            }

            EnsureRuntimeRoot();
            professorGO = Instantiate(prefabToUse, GetCaptiveProfessorPosition(), Quaternion.identity, runtimeRoot);
            professorGO.name = prefabToUse.name;
            captiveProfessorInScene = professorGO.transform;
        }

        if (professorGO == null)
            return;

        if (moveCaptiveProfessorToPrison)
            professorGO.transform.position = GetCaptiveProfessorPosition();

        ProfessorNPC2D oldProfessorNpc = professorGO.GetComponent<ProfessorNPC2D>();
        if (oldProfessorNpc != null)
            oldProfessorNpc.enabled = false;

        currentStoryProfessor = professorGO.GetComponent<BossProfessorNPC2D>();
        if (currentStoryProfessor == null)
            currentStoryProfessor = professorGO.AddComponent<BossProfessorNPC2D>();

        currentStoryProfessor.Configure(this, professorTalksAutomaticallyOnTouch, playerTag);
        currentStoryProfessor.SetInteractionEnabled(false);

        if (logDebugMessages)
            Debug.Log("BossArenaScene2D: profesor final colocado encerrado y con interacción desactivada hasta derrotar al jefe.", professorGO);
    }

    private void ReleaseCaptiveProfessorAndAllowStory()
    {
        if (logDebugMessages)
            Debug.Log("BossArenaScene2D: jefe final derrotado. Liberando al profesor encerrado.", this);

        if (clearCaptivePrisonWhenBossDies)
            ClearCaptiveProfessorPrison();

        if (captivePrisonReleaseEffectPrefab != null)
            Instantiate(captivePrisonReleaseEffectPrefab, GetCaptiveProfessorPosition(), Quaternion.identity);

        if (currentStoryProfessor == null)
            SetupCaptiveProfessorAtStart();

        if (currentStoryProfessor == null)
        {
            ShowProfessorStoryDirectly();
            return;
        }

        currentStoryProfessor.Configure(this, professorTalksAutomaticallyOnTouch, playerTag);
        currentStoryProfessor.SetInteractionEnabled(true);
    }

    private void BuildCaptiveProfessorPrison()
    {
        if (wallTilemap == null)
            return;

        TileBase prisonTile = captivePrisonWallTile != null ? captivePrisonWallTile : wallTile;
        if (prisonTile == null)
            return;

        foreach (Vector3Int cell in GetCaptiveProfessorPrisonWallCells())
        {
            wallTilemap.SetTile(cell, prisonTile);
        }
    }

    private void ClearCaptiveProfessorPrison()
    {
        if (wallTilemap == null)
            return;

        foreach (Vector3Int cell in GetCaptiveProfessorPrisonWallCells())
        {
            wallTilemap.SetTile(cell, null);
        }

        wallTilemap.CompressBounds();
    }

    private List<Vector3Int> GetCaptiveProfessorPrisonWallCells()
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        GetCaptiveProfessorPrisonBounds(out int minX, out int minY, out int maxX, out int maxY);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                bool isBorder = x == minX || x == maxX || y == minY || y == maxY;
                if (isBorder)
                    cells.Add(new Vector3Int(x, y, 0));
            }
        }

        return cells;
    }

    private Vector3 GetCaptiveProfessorPosition()
    {
        if (captiveProfessorSpawnPoint != null)
            return captiveProfessorSpawnPoint.position;

        GetCaptiveProfessorPrisonBounds(out int minX, out int minY, out int maxX, out int maxY);
        int centerX = Mathf.RoundToInt((minX + maxX) * 0.5f);
        int centerY = Mathf.RoundToInt((minY + maxY) * 0.5f);
        Vector3Int centerCell = new Vector3Int(centerX, centerY, 0);

        if (floorTilemap != null)
            return floorTilemap.GetCellCenterWorld(centerCell);

        return transform.position;
    }

    private void GetCaptiveProfessorPrisonBounds(out int minX, out int minY, out int maxX, out int maxY)
    {
        GetArenaInnerBounds(out int arenaMinX, out int arenaMinY, out int arenaMaxX, out int arenaMaxY);

        int width = Mathf.Max(3, captivePrisonSize.x);
        int height = Mathf.Max(3, captivePrisonSize.y);

        Transform manualCenter = captiveProfessorSpawnPoint != null
            ? captiveProfessorSpawnPoint
            : (!moveCaptiveProfessorToPrison ? captiveProfessorInScene : null);

        if (manualCenter != null && floorTilemap != null)
        {
            Vector3Int centerCell = floorTilemap.WorldToCell(manualCenter.position);
            minX = centerCell.x - width / 2;
            maxX = minX + width - 1;
            minY = centerCell.y - height / 2;
            maxY = minY + height - 1;
            ClampCaptiveProfessorPrisonToArena(ref minX, ref minY, ref maxX, ref maxY, arenaMinX, arenaMinY, arenaMaxX, arenaMaxY);
            return;
        }

        int offsetX = Mathf.Max(1, captivePrisonOffsetFromArenaCorner.x);
        int offsetY = Mathf.Max(1, captivePrisonOffsetFromArenaCorner.y);

        bool right = captiveProfessorCorner == CaptiveProfessorCorner2D.TopRight || captiveProfessorCorner == CaptiveProfessorCorner2D.BottomRight;
        bool top = captiveProfessorCorner == CaptiveProfessorCorner2D.TopRight || captiveProfessorCorner == CaptiveProfessorCorner2D.TopLeft;

        if (right)
        {
            maxX = arenaMaxX - offsetX;
            minX = maxX - width + 1;
        }
        else
        {
            minX = arenaMinX + offsetX;
            maxX = minX + width - 1;
        }

        if (top)
        {
            maxY = arenaMaxY - offsetY;
            minY = maxY - height + 1;
        }
        else
        {
            minY = arenaMinY + offsetY;
            maxY = minY + height - 1;
        }

        ClampCaptiveProfessorPrisonToArena(ref minX, ref minY, ref maxX, ref maxY, arenaMinX, arenaMinY, arenaMaxX, arenaMaxY);
    }

    private void ClampCaptiveProfessorPrisonToArena(ref int minX, ref int minY, ref int maxX, ref int maxY, int arenaMinX, int arenaMinY, int arenaMaxX, int arenaMaxY)
    {
        int width = maxX - minX + 1;
        int height = maxY - minY + 1;

        minX = Mathf.Clamp(minX, arenaMinX + 1, arenaMaxX - width);
        maxX = minX + width - 1;

        minY = Mathf.Clamp(minY, arenaMinY + 1, arenaMaxY - height);
        maxY = minY + height - 1;
    }

    private void GetArenaInnerBounds(out int minX, out int minY, out int maxX, out int maxY)
    {
        int innerHalf = arenaSize / 2;
        minX = arenaCenterCell.x - innerHalf;
        minY = arenaCenterCell.y - innerHalf;
        maxX = minX + arenaSize - 1;
        maxY = minY + arenaSize - 1;
    }

    private void SpawnProfessorForStory()
    {
        ResolveStoryReferences();

        if (logDebugMessages)
            Debug.Log("BossArenaScene2D: jefe derrotado. Aparecerá el profesor antes de avanzar.", this);

        if (professorPrefab == null)
        {
            Debug.LogWarning("BossArenaScene2D: falta professorPrefab. Se mostrará la historia directamente si hay Story UI.", this);
            ShowProfessorStoryDirectly();
            return;
        }

        EnsureRuntimeRoot();

        Vector3 spawnPosition = GetProfessorSpawnPosition();
        GameObject professorGO = Instantiate(professorPrefab, spawnPosition, Quaternion.identity, runtimeRoot);
        professorGO.name = professorPrefab.name;

        ProfessorNPC2D oldProfessorNpc = professorGO.GetComponent<ProfessorNPC2D>();
        if (oldProfessorNpc != null)
            oldProfessorNpc.enabled = false;

        ApplyProfessorHologramIfNeeded(professorGO);

        currentStoryProfessor = professorGO.GetComponent<BossProfessorNPC2D>();
        if (currentStoryProfessor == null)
            currentStoryProfessor = professorGO.AddComponent<BossProfessorNPC2D>();

        currentStoryProfessor.Configure(this, professorTalksAutomaticallyOnTouch, playerTag);
    }


    private void ApplyProfessorHologramIfNeeded(GameObject professorGO)
    {
        if (professorGO == null || !useHologramForNonFinalBosses || finalBoss)
            return;

        BossProfessorHologramEffect2D hologram = professorGO.GetComponent<BossProfessorHologramEffect2D>();
        if (hologram == null)
            hologram = professorGO.AddComponent<BossProfessorHologramEffect2D>();

        hologram.Configure(
            hologramTint,
            hologramFadeInDuration,
            hologramFlickerAmount,
            hologramFlickerSpeed,
            hologramUsesUnscaledTime
        );

        if (logDebugMessages)
            Debug.Log("BossArenaScene2D: profesor creado como holograma temporal.", professorGO);
    }

    private Vector3 GetProfessorSpawnPosition()
    {
        if (professorSpawnPoint != null)
            return professorSpawnPoint.position;

        Transform player = FindPlayerTransform();
        if (spawnProfessorNearPlayer && player != null)
            return player.position + professorSpawnOffsetFromPlayer;

        if (floorTilemap != null)
            return floorTilemap.GetCellCenterWorld(arenaCenterCell);

        return transform.position;
    }

    private void ShowProfessorStoryDirectly()
    {
        if (storyUI == null)
        {
            Debug.LogWarning("BossArenaScene2D: falta Story UI. No se puede mostrar la historia del profesor, así que se avanzará de escena.", this);
            AdvanceAfterProfessorStory();
            return;
        }

        OpenProfessorStory(null);
    }

    public void OpenProfessorStory(BossProfessorNPC2D professor)
    {
        if (professorStoryOpen || professorStoryResolved)
            return;

        ResolveStoryReferences();

        if (currentStoryProfessor != null && professor != null && professor != currentStoryProfessor)
            return;

        if (storyUI == null)
        {
            Debug.LogWarning("BossArenaScene2D: no hay Story UI asignada para mostrar la historia del profesor.", this);
            AdvanceAfterProfessorStory();
            return;
        }

        professorStoryOpen = true;
        currentStoryPageIndex = 0;
        LockPlayer();
        Time.timeScale = 0f;
        ShowCurrentStoryPage();
    }

    private void ShowCurrentStoryPage()
    {
        if (!professorStoryOpen || storyUI == null)
            return;

        string[] pages = GetCurrentStoryPages();
        if (pages == null || pages.Length == 0)
        {
            FinishProfessorStory();
            return;
        }

        currentStoryPageIndex = Mathf.Clamp(currentStoryPageIndex, 0, pages.Length - 1);
        bool isLastPage = currentStoryPageIndex >= pages.Length - 1;
        string buttonText = isLastPage ? finishButtonText : continueButtonText;

        storyUI.ShowQuestion(
            pages[currentStoryPageIndex],
            new string[] { buttonText },
            _ =>
            {
                if (isLastPage)
                    FinishProfessorStory();
                else
                {
                    currentStoryPageIndex++;
                    ShowCurrentStoryPage();
                }
            }
        );
    }

    private string[] GetCurrentStoryPages()
    {
        if (finalBoss)
        {
            string[] finalPages = CleanPages(finalBossStoryPages);
            if (finalPages.Length > 0)
                return finalPages;

            return new string[] { "¡Lo has conseguido! Has salvado al profesor." };
        }

        string[] normalPages = CleanPages(storyPagesAfterBoss);
        if (normalPages.Length > 0)
            return normalPages;

        return new string[] { "Has derrotado al jefe. Habla con el profesor para descubrir la siguiente pista." };
    }

    private string[] CleanPages(string[] pages)
    {
        if (pages == null)
            return new string[0];

        List<string> cleanedPages = new List<string>();
        foreach (string page in pages)
        {
            if (!string.IsNullOrWhiteSpace(page))
                cleanedPages.Add(page);
        }

        return cleanedPages.ToArray();
    }

    private void FinishProfessorStory()
    {
        professorStoryResolved = true;
        CloseStoryUI(true);

        if (currentStoryProfessor != null)
        {
            currentStoryProfessor.MarkResolved();

            if (destroyProfessorAfterStory)
                Destroy(currentStoryProfessor.gameObject);
        }

        AdvanceAfterProfessorStory();
    }

    private void CloseStoryUI(bool keepResolvedState)
    {
        professorStoryOpen = false;
        Time.timeScale = 1f;
        UnlockPlayer();

        if (!keepResolvedState)
            professorStoryResolved = false;

        if (storyUI != null)
            storyUI.Hide();
    }

    private void AdvanceAfterProfessorStory()
    {
        SaveCurrentPlayerSelection();

        if (finalBoss)
        {
            FinishGame();
            return;
        }

        if (string.IsNullOrWhiteSpace(nextLevelSceneName))
        {
            Debug.LogWarning("BossArenaScene2D: el jefe fue derrotado y la historia terminó, pero nextLevelSceneName está vacío.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(nextLevelSceneName);
    }

    private void FinishGame()
    {
        Time.timeScale = 1f;

        if (showVictoryScreenOnFinalBoss)
        {
            VictoryScreen2D.ShowVictory(victoryMessage, victoryMainMenuSceneName, victoryReturnKey);

            if (logDebugMessages)
                Debug.Log("BossArenaScene2D: jefe final completado. Mostrando pantalla de victoria.", this);

            return;
        }

        if (!string.IsNullOrWhiteSpace(endGameSceneName))
        {
            if (logDebugMessages)
                Debug.Log("BossArenaScene2D: historia final completada. Cargando escena de victoria: " + endGameSceneName, this);

            SceneManager.LoadScene(endGameSceneName);
            return;
        }

        if (useNextLevelSceneIfFinalEndSceneIsEmpty && !string.IsNullOrWhiteSpace(nextLevelSceneName))
        {
            if (logDebugMessages)
                Debug.Log("BossArenaScene2D: End Game Scene Name está vacío. Usando Next Level Scene Name como escena final: " + nextLevelSceneName, this);

            SceneManager.LoadScene(nextLevelSceneName);
            return;
        }

        if (quitGameIfFinalBossHasNoEndScene)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
            return;
        }

        Debug.LogWarning("BossArenaScene2D: jefe final derrotado y profesor salvado, pero no hay escena final configurada. Asigna End Game Scene Name, usa Next Level Scene Name como escena de victoria o activa Show Victory Screen On Final Boss.", this);
    }

    private void SaveCurrentPlayerSelection()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        CharacterSelectionState.CaptureFromPlayer(player);
    }

    private Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        return player != null ? player.transform : null;
    }

    private void WakeConfiguredEnemies(Transform player)
    {
        HashSet<EnemyChaser2D> uniqueEnemies = new HashSet<EnemyChaser2D>();

        if (bossEnemy != null)
            uniqueEnemies.Add(bossEnemy);

        foreach (EnemyChaser2D enemy in additionalEnemiesToWake)
        {
            if (enemy != null)
                uniqueEnemies.Add(enemy);
        }

        if (uniqueEnemies.Count == 0 && autoFindEnemiesIfListIsEmpty)
        {
            EnemyChaser2D[] sceneEnemies = FindObjectsByType<EnemyChaser2D>(FindObjectsSortMode.None);
            foreach (EnemyChaser2D enemy in sceneEnemies)
            {
                if (enemy != null)
                    uniqueEnemies.Add(enemy);
            }
        }

        foreach (EnemyChaser2D enemy in uniqueEnemies)
        {
            enemy.WakeUp(player);
        }
    }

    private void ResolveStoryReferences()
    {
        if (storyUI == null)
            storyUI = FindFirstObjectByType<ProfessorQuestionUI2D>();
    }

    private void LockPlayer()
    {
        lockedPlayerMovement = null;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return;

        lockedPlayerMovement = player.GetComponent<PlayerMovement>();
        if (lockedPlayerMovement != null)
            lockedPlayerMovement.enabled = false;
    }

    private void UnlockPlayer()
    {
        if (lockedPlayerMovement != null)
            lockedPlayerMovement.enabled = true;

        lockedPlayerMovement = null;
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
            return;

        GameObject root = new GameObject("BossProfessorStoryRuntime");
        runtimeRoot = root.transform;
    }
}
