using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class StatBookEncounterManager2D : MonoBehaviour
{
    [System.Serializable]
    public class StatBookAnswerOption
    {
        public string text;
        public bool isCorrect;
    }

    [System.Serializable]
    public class StatBookQuestionData
    {
        [TextArea(2, 5)] public string question;
        public StatBookAnswerOption[] answers;
    }

    [Header("References")]
    [SerializeField] private DungeonRoomsCorridors2D dungeon;
    [SerializeField] private GameObject statBookPrefab;
    [SerializeField] private ProfessorQuestionUI2D questionUI;

    [Header("Audio feedback")]
    [Tooltip("Reproduce un sonido desde AudioManager2D cuando el jugador acierta o falla una pregunta.")]
    [SerializeField] private bool playAnswerResultSounds = true;

    [Header("Level requirement")]
    [SerializeField, Min(1)] private int requiredCorrectStatBooks = 2;
    [Tooltip("Cantidad de StatBooks simultáneos en el mapa. Déjalo en 1 para que aparezcan de uno en uno.")]
    [SerializeField, Min(1)] private int activeStatBooksAtOnce = 1;

    [Header("Lore")]
    [SerializeField] private bool showLoreOnFirstStatBook = true;
    [SerializeField] private string continueButtonText = "Continuar";
    [SerializeField, TextArea(3, 8)] private string firstStatBookLore =
        "El profesor ha sido capturado, encuentra todos los statbooks del nivel para recoger más pistas y así lograr encontrarlo. Cuando encuentres un statbook, deberás responder correctamente a la pregunta.";

    [Header("Spawn")]
    [SerializeField] private int spawnMarginInsideRoom = 1;
    [SerializeField] private bool skipOriginRoom = true;
    [SerializeField] private bool skipLastRoom = true;

    [Header("Optional progress UI")]
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private string progressFormat = "StatBooks: {0}/{1}";

    [Header("Questions")]
    [SerializeField] private bool useFallbackQuestionIfEmpty = true;
    [Tooltip("Preguntas extra que puedes añadir desde el inspector. Se mezclarán con el banco educativo interno si está activo.")]
    [SerializeField] private StatBookQuestionData[] questions;

    [Header("Built-in educational question bank")]
    [Tooltip("Activa un banco interno con 100 preguntas de historia, matemáticas, geografía y literatura.")]
    [SerializeField] private bool useBuiltInEducationalQuestionBank = true;
    [Tooltip("Si está activo, las preguntas del inspector se mezclan con el banco interno. Si está desactivado, se usará solo el banco interno cuando esté activo.")]
    [SerializeField] private bool mixInspectorQuestionsWithBuiltInBank = true;
    [Tooltip("Evita repetir preguntas hasta que se hayan usado todas las disponibles en el nivel.")]
    [SerializeField] private bool avoidRepeatingQuestionsUntilPoolExhausted = true;
    [Tooltip("Mezcla el orden de las respuestas cada vez que se abre una pregunta.")]
    [SerializeField] private bool shuffleAnswerOrder = true;

    [Header("Events")]
    [SerializeField] private UnityEvent<int, int> onProgressChanged;
    [SerializeField] private UnityEvent onLevelRequirementMet;

    [Header("Debug")]
    [SerializeField] private bool logDebugMessages = false;

    private Transform runtimeRoot;
    private readonly List<StatBook2D> activeStatBooks = new List<StatBook2D>();
    private readonly Dictionary<StatBook2D, RectInt> activeStatBookRooms = new Dictionary<StatBook2D, RectInt>();

    private int correctAnswersThisLevel;
    private bool loreShownThisLevel;
    private bool questionOpen;
    private bool requirementEventInvoked;
    private PlayerMovement lockedPlayerMovement;
    private StatBook2D currentStatBook;

    private StatBookQuestionData fallbackQuestion;
    private StatBookQuestionData[] builtInEducationalQuestions;
    private readonly HashSet<string> usedQuestionTextsThisLevel = new HashSet<string>();

    public int CorrectAnswersThisLevel => correctAnswersThisLevel;
    public int RequiredCorrectStatBooks => requiredCorrectStatBooks;
    public bool IsLevelRequirementMet => correctAnswersThisLevel >= requiredCorrectStatBooks;
    public GameObject StatBookPrefab => statBookPrefab;

    public event System.Action<int, int> ProgressChanged;
    public static event System.Action<StatBookEncounterManager2D, int, int> AnyProgressChanged;

    public event System.Action<bool> AnswerResolved;
    public static event System.Action<StatBookEncounterManager2D, bool> AnyAnswerResolved;

    void Reset()
    {
        CreateDefaultFallbackQuestion();
        CreateBuiltInEducationalQuestionBank();
    }

    void Awake()
    {
        CreateDefaultFallbackQuestion();
        CreateBuiltInEducationalQuestionBank();
    }

    void OnEnable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated += RebuildStatBooks;
    }

    void OnDisable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated -= RebuildStatBooks;

        if (questionOpen)
            CloseQuestionUI();
    }

    void Start()
    {
        ResolveReferences();

        if (dungeon != null && dungeon.HasGeneratedMap)
            RebuildStatBooks();
        else if (statBookPrefab == null && logDebugMessages)
            Debug.LogWarning("StatBookEncounterManager2D: no hay StatBook Prefab asignado. Los StatBooks colocados manualmente podrán funcionar, pero no se generarán nuevos al fallar.", this);
    }

    private void ResolveReferences()
    {
        if (dungeon == null)
            dungeon = FindObjectOfType<DungeonRoomsCorridors2D>();

        if (questionUI == null)
            questionUI = FindObjectOfType<ProfessorQuestionUI2D>();
    }

    public void RebuildStatBooks()
    {
        ResolveReferences();
        ClearPrevious();

        correctAnswersThisLevel = 0;
        loreShownThisLevel = false;
        questionOpen = false;
        requirementEventInvoked = false;
        currentStatBook = null;
        usedQuestionTextsThisLevel.Clear();

        NotifyProgressChanged();

        if (dungeon == null || statBookPrefab == null)
        {
            if (logDebugMessages)
                Debug.LogWarning("StatBookEncounterManager2D: faltan referencias de Dungeon o StatBook Prefab. Si has colocado libros manualmente, esos libros seguirán intentando abrir pregunta.", this);
            return;
        }

        RefillActiveStatBooks();
    }

    public void TryStartStatBook(StatBook2D statBook)
    {
        ResolveReferences();

        if (questionOpen || statBook == null || IsLevelRequirementMet)
            return;

        // Permite que funcionen StatBooks colocados manualmente en la escena, no solo los que spawnea este manager.
        if (!activeStatBooks.Contains(statBook))
        {
            activeStatBooks.Add(statBook);
            statBook.Configure(this);
        }

        StatBookQuestionData question = GetRandomValidQuestion();
        if (question == null)
        {
            Debug.LogWarning("StatBookEncounterManager2D: no hay preguntas configuradas con al menos una respuesta correcta.", this);
            return;
        }

        List<StatBookAnswerOption> validAnswers = GetValidAnswers(question);
        if (validAnswers.Count == 0)
        {
            Debug.LogWarning("StatBookEncounterManager2D: la pregunta elegida no tiene respuestas válidas.", this);
            return;
        }

        if (questionUI == null)
        {
            Debug.LogWarning("StatBookEncounterManager2D: no hay UI de preguntas asignada. Asigna tu ProfessorQuestionUI2D en el inspector.", this);
            return;
        }

        if (logDebugMessages)
            Debug.Log("StatBookEncounterManager2D: abriendo StatBook.", statBook);

        currentStatBook = statBook;
        questionOpen = true;
        LockPlayer();
        Time.timeScale = 0f;

        if (showLoreOnFirstStatBook && !loreShownThisLevel)
        {
            loreShownThisLevel = true;
            questionUI.ShowQuestion(
                firstStatBookLore,
                new string[] { continueButtonText },
                _ => ShowQuestion(question, validAnswers)
            );
        }
        else
        {
            ShowQuestion(question, validAnswers);
        }
    }

    private void ShowQuestion(StatBookQuestionData question, List<StatBookAnswerOption> validAnswers)
    {
        if (!questionOpen || questionUI == null)
            return;

        if (shuffleAnswerOrder)
            ShuffleAnswers(validAnswers);

        string[] answerTexts = new string[validAnswers.Count];
        for (int i = 0; i < validAnswers.Count; i++)
            answerTexts[i] = validAnswers[i].text;

        questionUI.ShowQuestion(
            question.question,
            answerTexts,
            selectedIndex => ResolveAnswer(validAnswers, selectedIndex)
        );
    }

    private void ResolveAnswer(List<StatBookAnswerOption> validAnswers, int selectedIndex)
    {
        CloseQuestionUI();

        bool answeredCorrectly =
            selectedIndex >= 0 &&
            selectedIndex < validAnswers.Count &&
            validAnswers[selectedIndex].isCorrect;

        NotifyAnswerResolved(answeredCorrectly);

        StatBook2D resolvedStatBook = currentStatBook;
        currentStatBook = null;

        RemoveStatBook(resolvedStatBook);

        if (answeredCorrectly)
        {
            correctAnswersThisLevel++;
            Debug.Log($"StatBook correcto: {correctAnswersThisLevel}/{requiredCorrectStatBooks}");

            NotifyProgressChanged();

            if (IsLevelRequirementMet)
            {
                ClearPrevious();

                if (!requirementEventInvoked)
                {
                    requirementEventInvoked = true;
                    onLevelRequirementMet?.Invoke();
                }

                return;
            }
        }
        else
        {
            Debug.Log("StatBook fallado: aparecerá otro StatBook.");
        }

        RefillActiveStatBooks();
    }

    private void RefillActiveStatBooks()
    {
        if (IsLevelRequirementMet || dungeon == null || statBookPrefab == null)
            return;

        int targetCount = Mathf.Max(1, activeStatBooksAtOnce);
        while (activeStatBooks.Count < targetCount)
        {
            if (!SpawnStatBook())
                break;
        }
    }

    private bool SpawnStatBook()
    {
        if (dungeon == null || statBookPrefab == null)
            return false;

        if (!TryChooseStatBookRoom(out RectInt selectedRoom))
        {
            Debug.LogWarning("StatBookEncounterManager2D: no hay una sala válida para colocar un StatBook.", this);
            return false;
        }

        EnsureRuntimeRoot();

        Vector3 spawnPos = dungeon.GetRandomWorldPositionInRoom(selectedRoom, spawnMarginInsideRoom);
        GameObject statBookGO = Instantiate(statBookPrefab, spawnPos, Quaternion.identity, runtimeRoot);
        statBookGO.name = statBookPrefab.name;

        EnsureTriggerCollider(statBookGO);

        StatBook2D statBook = statBookGO.GetComponent<StatBook2D>();
        if (statBook == null)
            statBook = statBookGO.AddComponent<StatBook2D>();

        statBook.Configure(this);
        activeStatBooks.Add(statBook);
        activeStatBookRooms[statBook] = selectedRoom;

        if (logDebugMessages)
            Debug.Log($"StatBookEncounterManager2D: StatBook spawneado en {spawnPos}.", statBookGO);

        return true;
    }

    private void EnsureTriggerCollider(GameObject statBookGO)
    {
        Collider2D col = statBookGO.GetComponent<Collider2D>();
        if (col == null)
            col = statBookGO.AddComponent<CircleCollider2D>();

        col.isTrigger = true;
    }

    private void RemoveStatBook(StatBook2D statBook)
    {
        if (statBook == null)
            return;

        activeStatBooks.Remove(statBook);
        activeStatBookRooms.Remove(statBook);
        statBook.MarkResolved();
        Destroy(statBook.gameObject);
    }

    private bool TryChooseStatBookRoom(out RectInt room)
    {
        room = default;

        if (dungeon.Rooms == null || dungeon.Rooms.Count == 0)
            return false;

        List<int> validIndexes = new List<int>();

        for (int i = 0; i < dungeon.Rooms.Count; i++)
        {
            RectInt candidate = dungeon.Rooms[i];
            bool isOriginRoom = dungeon.HasOriginRoom && candidate.Equals(dungeon.OriginRoom);
            bool isLastRoom = i == dungeon.Rooms.Count - 1;
            bool alreadyHasActiveStatBook = RoomAlreadyHasActiveStatBook(candidate);

            if (skipOriginRoom && isOriginRoom)
                continue;

            if (skipLastRoom && isLastRoom)
                continue;

            if (alreadyHasActiveStatBook)
                continue;

            validIndexes.Add(i);
        }

        if (validIndexes.Count == 0)
        {
            for (int i = 0; i < dungeon.Rooms.Count; i++)
                validIndexes.Add(i);
        }

        int selectedIndex = validIndexes[Random.Range(0, validIndexes.Count)];
        room = dungeon.Rooms[selectedIndex];
        return true;
    }

    private bool RoomAlreadyHasActiveStatBook(RectInt room)
    {
        foreach (RectInt activeRoom in activeStatBookRooms.Values)
        {
            if (activeRoom.Equals(room))
                return true;
        }

        return false;
    }

    private StatBookQuestionData GetRandomValidQuestion()
    {
        CreateBuiltInEducationalQuestionBank();

        List<StatBookQuestionData> validQuestions = new List<StatBookQuestionData>();

        bool shouldUseInspectorQuestions =
            questions != null &&
            questions.Length > 0 &&
            (!useBuiltInEducationalQuestionBank || mixInspectorQuestionsWithBuiltInBank);

        if (shouldUseInspectorQuestions)
            AddValidQuestionsFromArray(validQuestions, questions);

        if (useBuiltInEducationalQuestionBank)
            AddValidQuestionsFromArray(validQuestions, builtInEducationalQuestions);

        if (validQuestions.Count == 0 && useFallbackQuestionIfEmpty && IsValidQuestion(fallbackQuestion))
            validQuestions.Add(fallbackQuestion);

        if (validQuestions.Count == 0)
            return null;

        List<StatBookQuestionData> availableQuestions = validQuestions;

        if (avoidRepeatingQuestionsUntilPoolExhausted && validQuestions.Count > 1)
        {
            availableQuestions = new List<StatBookQuestionData>();

            foreach (StatBookQuestionData question in validQuestions)
            {
                string key = GetQuestionKey(question);
                if (!usedQuestionTextsThisLevel.Contains(key))
                    availableQuestions.Add(question);
            }

            if (availableQuestions.Count == 0)
            {
                usedQuestionTextsThisLevel.Clear();
                availableQuestions = validQuestions;
            }
        }

        StatBookQuestionData chosenQuestion = availableQuestions[Random.Range(0, availableQuestions.Count)];

        if (avoidRepeatingQuestionsUntilPoolExhausted)
            usedQuestionTextsThisLevel.Add(GetQuestionKey(chosenQuestion));

        return chosenQuestion;
    }

    private void AddValidQuestionsFromArray(List<StatBookQuestionData> destination, StatBookQuestionData[] source)
    {
        if (destination == null || source == null)
            return;

        foreach (StatBookQuestionData question in source)
        {
            if (IsValidQuestion(question))
                destination.Add(question);
        }
    }

    private string GetQuestionKey(StatBookQuestionData question)
    {
        return question != null && !string.IsNullOrWhiteSpace(question.question)
            ? question.question.Trim()
            : string.Empty;
    }

    private void ShuffleAnswers(List<StatBookAnswerOption> answers)
    {
        if (answers == null || answers.Count <= 1)
            return;

        for (int i = answers.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            StatBookAnswerOption temp = answers[i];
            answers[i] = answers[randomIndex];
            answers[randomIndex] = temp;
        }
    }

    private bool IsValidQuestion(StatBookQuestionData question)
    {
        if (question == null || string.IsNullOrWhiteSpace(question.question))
            return false;

        List<StatBookAnswerOption> validAnswers = GetValidAnswers(question);
        if (validAnswers.Count == 0)
            return false;

        for (int i = 0; i < validAnswers.Count; i++)
        {
            if (validAnswers[i].isCorrect)
                return true;
        }

        return false;
    }

    private List<StatBookAnswerOption> GetValidAnswers(StatBookQuestionData question)
    {
        List<StatBookAnswerOption> validAnswers = new List<StatBookAnswerOption>();

        if (question == null || question.answers == null)
            return validAnswers;

        foreach (StatBookAnswerOption answer in question.answers)
        {
            if (answer == null || string.IsNullOrWhiteSpace(answer.text))
                continue;

            validAnswers.Add(answer);
        }

        return validAnswers;
    }

    private void CreateDefaultFallbackQuestion()
    {
        if (fallbackQuestion != null)
            return;

        fallbackQuestion = CreateQuestion(
            "¿Qué debe hacer el jugador para poder avanzar de nivel?",
            "Encontrar y responder correctamente los StatBooks necesarios.",
            "Ignorar los StatBooks.",
            "Encontrarse obligatoriamente con el profesor en cada nivel.",
            "Dejarse golpear por todos los enemigos."
        );
    }

    private void CreateBuiltInEducationalQuestionBank()
    {
        if (builtInEducationalQuestions != null && builtInEducationalQuestions.Length >= 100)
            return;

        builtInEducationalQuestions = new StatBookQuestionData[]
        {
            // Historia
            CreateQuestion("[Historia] ¿En qué año llegó Cristóbal Colón a América?", "1492", "1453", "1512", "1605"),
            CreateQuestion("[Historia] ¿En qué año comenzó la Revolución Francesa?", "1789", "1492", "1812", "1914"),
            CreateQuestion("[Historia] ¿Qué civilización construyó las pirámides de Giza?", "La civilización egipcia", "La civilización romana", "La civilización vikinga", "La civilización china"),
            CreateQuestion("[Historia] ¿Cuál fue la capital principal del Imperio romano?", "Roma", "Atenas", "París", "Londres"),
            CreateQuestion("[Historia] ¿En qué año comenzó la Primera Guerra Mundial?", "1914", "1939", "1815", "1492"),
            CreateQuestion("[Historia] ¿A quién se asocia la invención de la imprenta de tipos móviles en Europa?", "Johannes Gutenberg", "Leonardo da Vinci", "Isaac Newton", "Miguel de Cervantes"),
            CreateQuestion("[Historia] ¿En qué año cayó el Imperio romano de Occidente?", "476", "711", "1492", "1789"),
            CreateQuestion("[Historia] ¿Quién fue el primer emperador romano?", "Augusto", "Julio César", "Nerón", "Trajano"),
            CreateQuestion("[Historia] ¿Qué ruta comercial conectaba Europa y Asia durante siglos?", "La Ruta de la Seda", "El Camino de Santiago", "La Ruta del Ámbar", "La Vía Apia"),
            CreateQuestion("[Historia] ¿En qué año declararon su independencia los Estados Unidos?", "1776", "1789", "1812", "1918"),
            CreateQuestion("[Historia] ¿Qué acontecimiento histórico comenzó en 1939?", "La Segunda Guerra Mundial", "La Revolución Industrial", "La Guerra Fría", "La Revolución Francesa"),
            CreateQuestion("[Historia] ¿Qué pueblo fundó la ciudad de Roma según la tradición?", "Los latinos", "Los vikingos", "Los mayas", "Los fenicios"),
            CreateQuestion("[Historia] ¿Qué cultura precolombina construyó Machu Picchu?", "Los incas", "Los aztecas", "Los olmecas", "Los fenicios"),
            CreateQuestion("[Historia] ¿Quién fue Napoleón Bonaparte?", "Un emperador francés", "Un faraón egipcio", "Un rey vikingo", "Un explorador portugués"),
            CreateQuestion("[Historia] ¿En qué año terminó la Segunda Guerra Mundial?", "1945", "1939", "1918", "1969"),
            CreateQuestion("[Historia] ¿Qué edad histórica comienza tradicionalmente con la caída de Roma en 476?", "La Edad Media", "La Edad Moderna", "La Prehistoria", "La Edad Contemporánea"),
            CreateQuestion("[Historia] ¿Qué revolución transformó la producción mediante máquinas y fábricas?", "La Revolución Industrial", "La Revolución Neolítica", "La Revolución Francesa", "La Revolución Rusa"),
            CreateQuestion("[Historia] ¿Qué civilización usaba jeroglíficos como sistema de escritura?", "La egipcia", "La romana", "La griega", "La vikinga"),
            CreateQuestion("[Historia] ¿Quién fue Cleopatra?", "Una reina de Egipto", "Una emperatriz romana", "Una escritora medieval", "Una exploradora española"),
            CreateQuestion("[Historia] ¿Qué ciudad fue destruida por la erupción del Vesubio en el año 79?", "Pompeya", "Atenas", "Cartago", "Toledo"),
            CreateQuestion("[Historia] ¿Qué periodo histórico aparece antes de la Edad Antigua?", "La Prehistoria", "La Edad Media", "La Edad Moderna", "La Edad Contemporánea"),
            CreateQuestion("[Historia] ¿Qué guerra enfrentó principalmente al norte y al sur de Estados Unidos?", "La Guerra de Secesión", "La Guerra de los Cien Años", "La Guerra Fría", "La Guerra de Troya"),
            CreateQuestion("[Historia] ¿Qué explorador portugués llegó a la India bordeando África?", "Vasco da Gama", "Marco Polo", "Hernán Cortés", "James Cook"),
            CreateQuestion("[Historia] ¿Qué pueblo navegante procedía de Escandinavia en la Edad Media?", "Los vikingos", "Los egipcios", "Los persas", "Los incas"),
            CreateQuestion("[Historia] ¿Qué hecho se suele asociar con el inicio de la Edad Moderna en España?", "1492", "476", "1789", "1914"),

            // Matemáticas
            CreateQuestion("[Matemáticas] ¿Cuánto es 2 + 2?", "4", "3", "5", "6"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 7 × 8?", "56", "48", "54", "64"),
            CreateQuestion("[Matemáticas] ¿Cuál es la raíz cuadrada de 144?", "12", "10", "14", "16"),
            CreateQuestion("[Matemáticas] ¿Cuál es el área de un rectángulo de 5 por 3?", "15", "8", "10", "30"),
            CreateQuestion("[Matemáticas] ¿Cuántos grados tiene un ángulo recto?", "90", "45", "180", "360"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 15 ÷ 3?", "5", "3", "6", "12"),
            CreateQuestion("[Matemáticas] ¿Cuál es el perímetro de un cuadrado de lado 4?", "16", "8", "12", "20"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 3²?", "9", "6", "12", "27"),
            CreateQuestion("[Matemáticas] ¿Cuál de estos números es primo?", "13", "12", "15", "21"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 1/2 + 1/4?", "3/4", "1/4", "2/6", "1"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 9 × 6?", "54", "45", "63", "56"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 100 - 37?", "63", "73", "67", "53"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 25% de 80?", "20", "25", "40", "10"),
            CreateQuestion("[Matemáticas] ¿Cuántos lados tiene un hexágono?", "6", "5", "7", "8"),
            CreateQuestion("[Matemáticas] ¿Cuál es el resultado de 12 + 8 × 2?", "28", "40", "32", "24"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 5³?", "125", "25", "15", "100"),
            CreateQuestion("[Matemáticas] ¿Cuál es el valor de π aproximado a dos decimales?", "3,14", "2,71", "1,41", "4,13"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 3/5 de 100?", "60", "30", "50", "80"),
            CreateQuestion("[Matemáticas] ¿Qué figura tiene tres lados?", "Triángulo", "Cuadrado", "Pentágono", "Círculo"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 81 ÷ 9?", "9", "8", "7", "6"),
            CreateQuestion("[Matemáticas] ¿Cuál es el doble de 36?", "72", "62", "68", "74"),
            CreateQuestion("[Matemáticas] ¿Cuál es la mitad de 98?", "49", "48", "50", "39"),
            CreateQuestion("[Matemáticas] ¿Cuánto es 11 × 11?", "121", "111", "101", "131"),
            CreateQuestion("[Matemáticas] ¿Qué número falta en la secuencia 2, 4, 6, 8, ...?", "10", "9", "12", "16"),
            CreateQuestion("[Matemáticas] ¿Cuál es el área de un cuadrado de lado 6?", "36", "24", "12", "18"),

            // Geografía
            CreateQuestion("[Geografía] ¿Cuál es la capital de España?", "Madrid", "Barcelona", "Sevilla", "Valencia"),
            CreateQuestion("[Geografía] ¿Cuál es la capital de Francia?", "París", "Lyon", "Marsella", "Toulouse"),
            CreateQuestion("[Geografía] ¿Cuál es el océano más grande del planeta?", "El océano Pacífico", "El océano Atlántico", "El océano Índico", "El océano Ártico"),
            CreateQuestion("[Geografía] ¿Cuál es la montaña más alta del mundo?", "El Everest", "El Kilimanjaro", "El Teide", "El Mont Blanc"),
            CreateQuestion("[Geografía] ¿En qué continente está el desierto del Sahara?", "África", "Asia", "Europa", "Oceanía"),
            CreateQuestion("[Geografía] ¿Qué país tiene forma de bota en el mapa?", "Italia", "Portugal", "Grecia", "Noruega"),
            CreateQuestion("[Geografía] ¿Cuál es la capital de Japón?", "Tokio", "Kioto", "Osaka", "Hiroshima"),
            CreateQuestion("[Geografía] ¿En qué continente se encuentra la cordillera de los Andes?", "América del Sur", "Europa", "África", "Oceanía"),
            CreateQuestion("[Geografía] ¿Dónde se encuentra principalmente la selva amazónica?", "América del Sur", "Europa", "Asia Central", "Antártida"),
            CreateQuestion("[Geografía] ¿Cómo se llama la línea imaginaria que divide la Tierra en hemisferio norte y sur?", "El ecuador", "El meridiano de Greenwich", "El trópico de Cáncer", "El círculo polar ártico"),
            CreateQuestion("[Geografía] ¿Cuál es la capital de Italia?", "Roma", "Milán", "Venecia", "Nápoles"),
            CreateQuestion("[Geografía] ¿Cuál es el río más largo de la península ibérica?", "El Tajo", "El Ebro", "El Duero", "El Guadalquivir"),
            CreateQuestion("[Geografía] ¿En qué continente está Australia?", "Oceanía", "Asia", "África", "Europa"),
            CreateQuestion("[Geografía] ¿Cuál es la capital de Portugal?", "Lisboa", "Oporto", "Coímbra", "Faro"),
            CreateQuestion("[Geografía] ¿Qué océano baña la costa este de Estados Unidos?", "El océano Atlántico", "El océano Pacífico", "El océano Índico", "El océano Ártico"),
            CreateQuestion("[Geografía] ¿Cuál es el país más grande del mundo por superficie?", "Rusia", "Canadá", "China", "Brasil"),
            CreateQuestion("[Geografía] ¿Qué cordillera separa España y Francia?", "Los Pirineos", "Los Alpes", "Los Andes", "El Atlas"),
            CreateQuestion("[Geografía] ¿Cuál es la capital de Alemania?", "Berlín", "Múnich", "Hamburgo", "Frankfurt"),
            CreateQuestion("[Geografía] ¿En qué país se encuentra la Torre Eiffel?", "Francia", "Italia", "Alemania", "Bélgica"),
            CreateQuestion("[Geografía] ¿Cuál es el mar situado entre Europa, África y Asia?", "El mar Mediterráneo", "El mar del Norte", "El mar Rojo", "El mar Caribe"),
            CreateQuestion("[Geografía] ¿Cuál es la capital de Reino Unido?", "Londres", "Dublín", "Edimburgo", "Manchester"),
            CreateQuestion("[Geografía] ¿Qué continente está cubierto casi por completo de hielo?", "Antártida", "Europa", "América del Norte", "Asia"),
            CreateQuestion("[Geografía] ¿Cuál es la capital de Argentina?", "Buenos Aires", "Córdoba", "Rosario", "Mendoza"),
            CreateQuestion("[Geografía] ¿Qué río atraviesa Egipto?", "El Nilo", "El Amazonas", "El Danubio", "El Támesis"),
            CreateQuestion("[Geografía] ¿Cuál es la capital de Marruecos?", "Rabat", "Casablanca", "Marrakech", "Fez"),

            // Literatura
            CreateQuestion("[Literatura] ¿Quién escribió Don Quijote de la Mancha?", "Miguel de Cervantes", "Federico García Lorca", "William Shakespeare", "Benito Pérez Galdós"),
            CreateQuestion("[Literatura] ¿Qué autor escribió Romeo y Julieta?", "William Shakespeare", "Miguel de Cervantes", "Homero", "Gabriel García Márquez"),
            CreateQuestion("[Literatura] ¿A qué autor se atribuye La Odisea?", "Homero", "Virgilio", "Sófocles", "Dante Alighieri"),
            CreateQuestion("[Literatura] ¿Cuál es el nombre real del protagonista de Don Quijote?", "Alonso Quijano", "Sancho Panza", "Lázaro de Tormes", "Rodrigo Díaz"),
            CreateQuestion("[Literatura] ¿Cómo se llama un texto literario escrito en versos?", "Poema", "Novela", "Ensayo", "Noticia"),
            CreateQuestion("[Literatura] ¿Qué recurso compara dos elementos usando palabras como como o parece?", "Símil", "Hipérbole", "Metáfora pura", "Ironía"),
            CreateQuestion("[Literatura] ¿Qué tipo de narrador cuenta la historia usando yo?", "Narrador en primera persona", "Narrador omnisciente", "Narrador testigo externo", "Narrador en segunda persona"),
            CreateQuestion("[Literatura] ¿Quién escribió Cien años de soledad?", "Gabriel García Márquez", "Mario Vargas Llosa", "Pablo Neruda", "Jorge Luis Borges"),
            CreateQuestion("[Literatura] ¿Quién escribió Platero y yo?", "Juan Ramón Jiménez", "Antonio Machado", "Rubén Darío", "Lope de Vega"),
            CreateQuestion("[Literatura] ¿Quién es el autor de La Celestina?", "Fernando de Rojas", "Miguel de Cervantes", "Garcilaso de la Vega", "Calderón de la Barca"),
            CreateQuestion("[Literatura] ¿Qué género literario suele contar una historia extensa en prosa?", "Novela", "Poema", "Obra teatral breve", "Fábula musical"),
            CreateQuestion("[Literatura] ¿Qué personaje acompaña a Don Quijote en sus aventuras?", "Sancho Panza", "Romeo Montesco", "Ulises", "Hamlet"),
            CreateQuestion("[Literatura] ¿Quién escribió La casa de Bernarda Alba?", "Federico García Lorca", "Miguel de Unamuno", "Rosalía de Castro", "Ana María Matute"),
            CreateQuestion("[Literatura] ¿Qué obra empieza con la historia de un hidalgo manchego?", "Don Quijote de la Mancha", "La Regenta", "El Lazarillo de Tormes", "Fuenteovejuna"),
            CreateQuestion("[Literatura] ¿Quién escribió Veinte poemas de amor y una canción desesperada?", "Pablo Neruda", "Gabriel García Márquez", "Jorge Luis Borges", "Rubén Darío"),
            CreateQuestion("[Literatura] ¿Qué es una metáfora?", "Identificar una realidad con otra por semejanza", "Exagerar mucho una idea", "Repetir sonidos al inicio", "Contar una historia con animales"),
            CreateQuestion("[Literatura] ¿Qué es una fábula?", "Un relato breve con enseñanza o moraleja", "Una novela histórica muy larga", "Un poema sin rima", "Una noticia periodística"),
            CreateQuestion("[Literatura] ¿Qué autor escribió Hamlet?", "William Shakespeare", "Homero", "Dante Alighieri", "Lope de Vega"),
            CreateQuestion("[Literatura] ¿Quién escribió La divina comedia?", "Dante Alighieri", "Virgilio", "Homero", "Franz Kafka"),
            CreateQuestion("[Literatura] ¿Qué obra española tiene como protagonista a Lázaro?", "El Lazarillo de Tormes", "Don Quijote de la Mancha", "La Celestina", "Campos de Castilla"),
            CreateQuestion("[Literatura] ¿Quién escribió Campos de Castilla?", "Antonio Machado", "Juan Ramón Jiménez", "Federico García Lorca", "Miguel Hernández"),
            CreateQuestion("[Literatura] ¿Qué nombre recibe cada línea de un poema?", "Verso", "Párrafo", "Capítulo", "Escena"),
            CreateQuestion("[Literatura] ¿Qué conjunto de versos forma una unidad dentro de un poema?", "Estrofa", "Acto", "Prólogo", "Narrador"),
            CreateQuestion("[Literatura] ¿Quién escribió El principito?", "Antoine de Saint-Exupéry", "Julio Verne", "Charles Dickens", "Mark Twain"),
            CreateQuestion("[Literatura] ¿Qué escritor es conocido por obras como Viaje al centro de la Tierra?", "Julio Verne", "Miguel de Cervantes", "Edgar Allan Poe", "Lope de Vega")
        };
    }

    private StatBookQuestionData CreateQuestion(string question, string correctAnswer, string wrongAnswer1, string wrongAnswer2, string wrongAnswer3)
    {
        return new StatBookQuestionData
        {
            question = question,
            answers = new StatBookAnswerOption[]
            {
                new StatBookAnswerOption { text = correctAnswer, isCorrect = true },
                new StatBookAnswerOption { text = wrongAnswer1, isCorrect = false },
                new StatBookAnswerOption { text = wrongAnswer2, isCorrect = false },
                new StatBookAnswerOption { text = wrongAnswer3, isCorrect = false }
            }
        };
    }

    private void LockPlayer()
    {
        lockedPlayerMovement = null;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        lockedPlayerMovement = player.GetComponent<PlayerMovement>();
        if (lockedPlayerMovement == null)
            lockedPlayerMovement = player.GetComponentInChildren<PlayerMovement>();

        if (lockedPlayerMovement != null)
            lockedPlayerMovement.enabled = false;

        Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = player.GetComponentInChildren<Rigidbody2D>();

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void UnlockPlayer()
    {
        if (lockedPlayerMovement != null)
            lockedPlayerMovement.enabled = true;

        lockedPlayerMovement = null;
    }

    private void CloseQuestionUI()
    {
        questionOpen = false;
        Time.timeScale = 1f;
        UnlockPlayer();

        if (questionUI != null)
            questionUI.Hide();
    }

    private void NotifyAnswerResolved(bool answeredCorrectly)
    {
        PlayAnswerAudioFeedback(answeredCorrectly);
        AnswerResolved?.Invoke(answeredCorrectly);
        AnyAnswerResolved?.Invoke(this, answeredCorrectly);
    }

    private void PlayAnswerAudioFeedback(bool answeredCorrectly)
    {
        if (!playAnswerResultSounds || AudioManager2D.Instance == null)
            return;

        if (answeredCorrectly)
            AudioManager2D.Instance.PlayCorrectAnswerSound();
        else
            AudioManager2D.Instance.PlayWrongAnswerSound();
    }

    private void NotifyProgressChanged()
    {
        UpdateProgressUI();
        onProgressChanged?.Invoke(correctAnswersThisLevel, requiredCorrectStatBooks);
        ProgressChanged?.Invoke(correctAnswersThisLevel, requiredCorrectStatBooks);
        AnyProgressChanged?.Invoke(this, correctAnswersThisLevel, requiredCorrectStatBooks);
    }

    private void UpdateProgressUI()
    {
        if (progressText != null)
            progressText.text = string.Format(progressFormat, correctAnswersThisLevel, requiredCorrectStatBooks);
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
            return;

        GameObject root = new GameObject("StatBookEncounterRuntime");
        runtimeRoot = root.transform;
    }

    private void ClearPrevious()
    {
        activeStatBooks.Clear();
        activeStatBookRooms.Clear();

        if (runtimeRoot == null)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
            Destroy(runtimeRoot.GetChild(i).gameObject);
    }
}
