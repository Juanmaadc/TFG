using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ProfessorQuestionUI2D : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TMP_Text[] answerLabels;

    [Header("Auto fix UI")]
    [Tooltip("Si está activo, el script buscará automáticamente botones/textos, creará EventSystem si falta y reparará raycasts básicos del Canvas.")]
    [SerializeField] private bool autoRepairUIReferences = true;

    [Tooltip("Permite avanzar diálogos de una sola opción con clic, Enter, Espacio o E aunque el botón no reciba el click de Unity UI.")]
    [SerializeField] private bool fallbackContinueInputForSingleButton = true;

    [SerializeField] private KeyCode fallbackContinueKey1 = KeyCode.Return;
    [SerializeField] private KeyCode fallbackContinueKey2 = KeyCode.Space;
    [SerializeField] private KeyCode fallbackContinueKey3 = KeyCode.E;

    private Action<int> onAnswerSelected;
    private bool isShowing;
    private int activeAnswerCount;
    private int shownFrame;

    void Reset()
    {
        root = gameObject;
        AutoWireMissingReferences();
    }

    void Awake()
    {
        if (root == null)
            root = gameObject;

        if (autoRepairUIReferences)
            RepairUISetup();

        Hide();
    }

    void Update()
    {
        if (!isShowing || !fallbackContinueInputForSingleButton || activeAnswerCount != 1 || onAnswerSelected == null)
            return;

        // Evita que la misma tecla E usada para hablar con el profesor pulse también Continuar en el mismo frame.
        if (Time.frameCount <= shownFrame)
            return;

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(fallbackContinueKey1) || Input.GetKeyDown(fallbackContinueKey2) || Input.GetKeyDown(fallbackContinueKey3))
        {
            HandleAnswer(0);
        }
    }

    public void ShowQuestion(string question, string[] answers, Action<int> callback)
    {
        if (root == null)
            root = gameObject;

        if (autoRepairUIReferences)
            RepairUISetup();

        onAnswerSelected = callback;
        isShowing = true;
        shownFrame = Time.frameCount;
        root.SetActive(true);

        EnableParentCanvasGroups();

        if (questionText != null)
            questionText.text = question;

        ClearButtons();

        int buttonLength = answerButtons != null ? answerButtons.Length : 0;
        activeAnswerCount = Mathf.Min(answers != null ? answers.Length : 0, buttonLength);

        for (int i = 0; i < activeAnswerCount; i++)
        {
            int answerIndex = i;
            Button button = answerButtons[i];
            if (button == null)
                continue;

            button.gameObject.SetActive(true);
            button.interactable = true;
            button.enabled = true;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleAnswer(answerIndex));

            Graphic buttonGraphic = button.targetGraphic;
            if (buttonGraphic != null)
                buttonGraphic.raycastTarget = true;

            TMP_Text label = GetLabelForIndex(i, button);
            if (label != null)
            {
                label.text = answers[i];
                // El texto dentro del botón no debe bloquear el raycast del propio botón.
                label.raycastTarget = false;
            }
        }

        if (activeAnswerCount == 0)
        {
            Debug.LogWarning("ProfessorQuestionUI2D: no hay botones asignados en answerButtons. El diálogo se ha abierto, pero no habrá botón de respuesta. Asigna los Button en el inspector o activa Auto Repair UI References.", this);
        }
    }

    public void Hide()
    {
        onAnswerSelected = null;
        isShowing = false;
        activeAnswerCount = 0;
        ClearButtons();

        if (root != null)
            root.SetActive(false);
    }

    // Puedes conectar este método manualmente al OnClick de un botón Continuar si alguna escena tiene un botón suelto.
    public void ContinueFirstOption()
    {
        if (onAnswerSelected != null)
            HandleAnswer(0);
    }

    private void HandleAnswer(int answerIndex)
    {
        Action<int> callback = onAnswerSelected;
        Hide();
        callback?.Invoke(answerIndex);
    }

    private void ClearButtons()
    {
        if (answerButtons == null)
            return;

        foreach (Button button in answerButtons)
        {
            if (button == null)
                continue;

            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);
        }
    }

    private TMP_Text GetLabelForIndex(int index, Button button)
    {
        if (answerLabels != null && index >= 0 && index < answerLabels.Length && answerLabels[index] != null)
            return answerLabels[index];

        return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private void RepairUISetup()
    {
        AutoWireMissingReferences();
        EnsureEventSystemExists();
        EnsureCanvasCanReceiveClicks();
        EnableParentCanvasGroups();
    }

    private void AutoWireMissingReferences()
    {
        if (root == null)
            root = gameObject;

        Transform searchRoot = root != null ? root.transform : transform;

        if ((answerButtons == null || answerButtons.Length == 0) && searchRoot != null)
        {
            answerButtons = searchRoot.GetComponentsInChildren<Button>(true);
        }

        if ((answerLabels == null || answerLabels.Length == 0) && answerButtons != null && answerButtons.Length > 0)
        {
            answerLabels = new TMP_Text[answerButtons.Length];
            for (int i = 0; i < answerButtons.Length; i++)
            {
                answerLabels[i] = answerButtons[i] != null ? answerButtons[i].GetComponentInChildren<TMP_Text>(true) : null;
            }
        }

        if (questionText == null && searchRoot != null)
        {
            TMP_Text[] texts = searchRoot.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text == null)
                    continue;

                bool isButtonLabel = false;
                if (answerLabels != null)
                {
                    foreach (TMP_Text label in answerLabels)
                    {
                        if (label == text)
                        {
                            isButtonLabel = true;
                            break;
                        }
                    }
                }

                if (!isButtonLabel)
                {
                    questionText = text;
                    break;
                }
            }
        }
    }

    private void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
            return;

        GameObject eventSystemGO = new GameObject("EventSystem");
        eventSystemGO.AddComponent<EventSystem>();
        eventSystemGO.AddComponent<StandaloneInputModule>();
    }

    private void EnsureCanvasCanReceiveClicks()
    {
        Canvas canvas = GetComponentInParent<Canvas>(true);
        if (canvas == null && root != null)
            canvas = root.GetComponentInParent<Canvas>(true);

        if (canvas == null)
            return;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private void EnableParentCanvasGroups()
    {
        if (root == null)
            return;

        CanvasGroup[] groups = root.GetComponentsInParent<CanvasGroup>(true);
        foreach (CanvasGroup group in groups)
        {
            if (group == null)
                continue;

            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }
}
