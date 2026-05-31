using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ProfessorEncounterManager2D : MonoBehaviour
{
    [System.Serializable]
    public class ProfessorAnswerOption
    {
        public string text;
        public bool isCorrect;
    }

    [System.Serializable]
    public class ProfessorQuestionData
    {
        [TextArea(2, 5)] public string question;
        public ProfessorAnswerOption[] answers;
    }

    private enum RewardType
    {
        StatsBuff,
        CharacterChange
    }

    [Header("References")]
    [SerializeField] private DungeonRoomsCorridors2D dungeon;
    [SerializeField] private DungeonEnemySpawner2D enemySpawner;
    [SerializeField] private GameObject professorPrefab;
    [SerializeField] private ProfessorQuestionUI2D questionUI;
    [SerializeField] private GameObject characterChangeRewardPrefab;

    [Header("Professor spawn")]
    [SerializeField] private int spawnMarginInsideRoom = 1;
    [SerializeField] private bool skipOriginRoom = true;
    [SerializeField] private bool skipLastRoom = true;

    [Header("Wrong answer")]
    [SerializeField] private int extraEnemiesOnWrongAnswer = 2;

    [Header("Correct answer reward")]
    [SerializeField] private bool allowStatBuffReward = true;
    [SerializeField] private bool allowCharacterChangeReward = true;
    [SerializeField] private Vector3 characterRewardOffset = new Vector3(1f, 0f, 0f);
    [SerializeField] private UnityEvent onStatBuffReward;

    [Header("Questions")]
    [SerializeField] private ProfessorQuestionData[] questions;

    private Transform runtimeRoot;
    private bool encounterSpawnedThisLevel;
    private ProfessorNPC2D currentProfessor;
    private RectInt encounterRoom;
    private bool encounterResolved;
    private bool encounterOpen;
    private PlayerMovement lockedPlayerMovement;

    public bool IsEncounterResolved => !encounterSpawnedThisLevel || encounterResolved;

    void OnEnable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated += RebuildEncounter;
    }

    void OnDisable()
    {
        if (dungeon != null)
            dungeon.OnDungeonGenerated -= RebuildEncounter;

        if (encounterOpen)
            CloseQuestionUI();
    }

    void Start()
    {
        if (dungeon != null && dungeon.HasGeneratedMap)
            RebuildEncounter();
    }

    public void RebuildEncounter()
    {
        if (dungeon == null || professorPrefab == null)
        {
            Debug.LogWarning("ProfessorEncounterManager2D: faltan referencias.");
            return;
        }

        ClearPrevious();
        encounterResolved = false;
        encounterOpen = false;
        encounterSpawnedThisLevel = false;

        if (!TryChooseEncounterRoom(out encounterRoom))
        {
            Debug.LogWarning("ProfessorEncounterManager2D: no hay una sala válida para colocar al profesor.");
            return;
        }

        EnsureRuntimeRoot();

        Vector3 spawnPos = dungeon.GetRandomWorldPositionInRoom(encounterRoom, spawnMarginInsideRoom);
        GameObject professorGO = Instantiate(professorPrefab, spawnPos, Quaternion.identity, runtimeRoot);

        currentProfessor = professorGO.GetComponent<ProfessorNPC2D>();
        if (currentProfessor == null)
            currentProfessor = professorGO.AddComponent<ProfessorNPC2D>();

        currentProfessor.Configure(this);
        encounterSpawnedThisLevel = true;
    }

    public void TryStartEncounter(ProfessorNPC2D professor)
    {
        if (encounterResolved || encounterOpen || professor == null || professor != currentProfessor)
            return;

        ProfessorQuestionData question = GetRandomValidQuestion();
        if (question == null)
        {
            Debug.LogWarning("ProfessorEncounterManager2D: no hay preguntas configuradas correctamente.");
            return;
        }

        List<ProfessorAnswerOption> validAnswers = GetValidAnswers(question);
        if (validAnswers.Count == 0)
        {
            Debug.LogWarning("ProfessorEncounterManager2D: la pregunta elegida no tiene respuestas válidas.");
            return;
        }

        if (questionUI == null)
        {
            Debug.LogWarning("ProfessorEncounterManager2D: no hay UI de preguntas asignada.");
            return;
        }

        encounterOpen = true;
        LockPlayer();
        Time.timeScale = 0f;

        string[] answerTexts = new string[validAnswers.Count];
        for (int i = 0; i < validAnswers.Count; i++)
            answerTexts[i] = validAnswers[i].text;

        questionUI.ShowQuestion(
            question.question,
            answerTexts,
            selectedIndex => ResolveAnswer(validAnswers, selectedIndex)
        );
    }

    private void ResolveAnswer(List<ProfessorAnswerOption> validAnswers, int selectedIndex)
    {
        CloseQuestionUI();

        bool answeredCorrectly =
            selectedIndex >= 0 &&
            selectedIndex < validAnswers.Count &&
            validAnswers[selectedIndex].isCorrect;

        if (answeredCorrectly)
            GiveCorrectAnswerReward();
        else
            SpawnPenaltyEnemies();

        encounterResolved = true;

        if (currentProfessor != null)
        {
            currentProfessor.MarkResolved();
            Destroy(currentProfessor.gameObject);
            currentProfessor = null;
        }
    }

    private void GiveCorrectAnswerReward()
    {
        RewardType reward = PickRewardType();

        switch (reward)
        {
            case RewardType.StatsBuff:
                Debug.Log("Profesor: recompensa de aumento de estadísticas.");
                onStatBuffReward?.Invoke();
                break;

            case RewardType.CharacterChange:
                SpawnCharacterChangeReward();
                break;
        }
    }

    private void SpawnPenaltyEnemies()
    {
        if (enemySpawner == null)
        {
            Debug.LogWarning("ProfessorEncounterManager2D: no hay DungeonEnemySpawner2D asignado para el castigo.");
            return;
        }

        Transform playerTarget = GetCurrentPlayerTransform();
        enemySpawner.SpawnExtraEnemiesInRoom(encounterRoom, extraEnemiesOnWrongAnswer, playerTarget);
    }

    private void SpawnCharacterChangeReward()
    {
        if (characterChangeRewardPrefab == null)
        {
            Debug.LogWarning("ProfessorEncounterManager2D: falta el prefab de recompensa para cambiar de personaje.");
            return;
        }

        EnsureRuntimeRoot();

        Vector3 basePosition = currentProfessor != null
            ? currentProfessor.transform.position
            : Vector3.zero;

        GameObject reward = Instantiate(
            characterChangeRewardPrefab,
            basePosition + characterRewardOffset,
            Quaternion.identity,
            runtimeRoot
        );

        reward.name = characterChangeRewardPrefab.name;
    }

    private RewardType PickRewardType()
    {
        List<RewardType> availableRewards = new List<RewardType>();

        if (allowStatBuffReward)
            availableRewards.Add(RewardType.StatsBuff);

        if (allowCharacterChangeReward)
            availableRewards.Add(RewardType.CharacterChange);

        if (availableRewards.Count == 0)
        {
            Debug.LogWarning("ProfessorEncounterManager2D: no hay recompensas habilitadas. Se usará cambio de personaje por defecto.");
            return RewardType.CharacterChange;
        }

        return availableRewards[Random.Range(0, availableRewards.Count)];
    }

    private ProfessorQuestionData GetRandomValidQuestion()
    {
        if (questions == null || questions.Length == 0)
            return null;

        List<ProfessorQuestionData> validQuestions = new List<ProfessorQuestionData>();

        foreach (ProfessorQuestionData question in questions)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.question))
                continue;

            List<ProfessorAnswerOption> validAnswers = GetValidAnswers(question);
            if (validAnswers.Count > 0)
                validQuestions.Add(question);
        }

        if (validQuestions.Count == 0)
            return null;

        return validQuestions[Random.Range(0, validQuestions.Count)];
    }

    private List<ProfessorAnswerOption> GetValidAnswers(ProfessorQuestionData question)
    {
        List<ProfessorAnswerOption> validAnswers = new List<ProfessorAnswerOption>();

        if (question == null || question.answers == null)
            return validAnswers;

        foreach (ProfessorAnswerOption answer in question.answers)
        {
            if (answer == null || string.IsNullOrWhiteSpace(answer.text))
                continue;

            validAnswers.Add(answer);
        }

        return validAnswers;
    }

    private bool TryChooseEncounterRoom(out RectInt room)
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

            if (skipOriginRoom && isOriginRoom)
                continue;

            if (skipLastRoom && isLastRoom)
                continue;

            validIndexes.Add(i);
        }

        if (validIndexes.Count == 0)
            return false;

        int selectedIndex = validIndexes[Random.Range(0, validIndexes.Count)];
        room = dungeon.Rooms[selectedIndex];
        return true;
    }

    private Transform GetCurrentPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void LockPlayer()
    {
        lockedPlayerMovement = null;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
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

    private void CloseQuestionUI()
    {
        encounterOpen = false;
        Time.timeScale = 1f;
        UnlockPlayer();

        if (questionUI != null)
            questionUI.Hide();
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
            return;

        GameObject root = new GameObject("ProfessorEncounterRuntime");
        runtimeRoot = root.transform;
    }

    private void ClearPrevious()
    {
        if (runtimeRoot == null)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
            Destroy(runtimeRoot.GetChild(i).gameObject);

        currentProfessor = null;
    }
}

