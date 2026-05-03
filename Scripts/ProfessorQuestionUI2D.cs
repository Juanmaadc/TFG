using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfessorQuestionUI2D : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TMP_Text[] answerLabels;

    private Action<int> onAnswerSelected;

    void Reset()
    {
        root = gameObject;
    }

    void Awake()
    {
        if (root == null)
            root = gameObject;

        Hide();
    }

    public void ShowQuestion(string question, string[] answers, Action<int> callback)
    {
        if (root == null)
            root = gameObject;

        onAnswerSelected = callback;
        root.SetActive(true);

        if (questionText != null)
            questionText.text = question;

        ClearButtons();

        int count = Mathf.Min(answers != null ? answers.Length : 0, answerButtons != null ? answerButtons.Length : 0);

        for (int i = 0; i < count; i++)
        {
            int answerIndex = i;
            Button button = answerButtons[i];
            if (button == null)
                continue;

            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleAnswer(answerIndex));

            TMP_Text label = GetLabelForIndex(i, button);
            if (label != null)
                label.text = answers[i];
        }
    }

    public void Hide()
    {
        onAnswerSelected = null;
        ClearButtons();

        if (root != null)
            root.SetActive(false);
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

        return button != null ? button.GetComponentInChildren<TMP_Text>() : null;
    }
}
