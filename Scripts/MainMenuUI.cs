using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;

    [Header("Config")]
    [SerializeField] private string gameTitle = "Save the teacher!";
    [SerializeField] private string firstLevelSceneName = "Level1";

    void Awake()
    {
        RefreshTitle();
    }

    void OnValidate()
    {
        RefreshTitle();
    }

    private void RefreshTitle()
    {
        if (titleText != null)
            titleText.text = gameTitle;
    }

    public void StartNewGame()
    {
        if (string.IsNullOrWhiteSpace(firstLevelSceneName))
        {
            Debug.LogError("MainMenuUI: firstLevelSceneName está vacío.");
            return;
        }

        SceneManager.LoadScene(firstLevelSceneName);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}