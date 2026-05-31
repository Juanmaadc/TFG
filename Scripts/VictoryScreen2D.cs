using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows a victory/end screen with the same simple full-screen style as GameOverScreen2D.
/// It can be placed manually in a Canvas or created automatically from BossArenaScene2D.
/// </summary>
public class VictoryScreen2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup victoryPanel;
    [SerializeField] private TMP_Text victoryText;

    [Header("Text")]
    [SerializeField, TextArea(2, 5)] private string victoryMessage = "¡Has salvado al profesor! Muchas gracias por jugar.\nPulsa Space para ir al menú principal.";
    [SerializeField] private float fontSize = 58f;

    [Header("Behaviour")]
    [SerializeField] private bool createUIIfMissing = true;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool pauseGameOnVictory = true;

    [Header("Return to main menu")]
    [SerializeField] private bool allowReturnToMainMenu = true;
    [SerializeField] private KeyCode returnToMainMenuKey = KeyCode.Space;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool victoryShown;

    private void Awake()
    {
        if (createUIIfMissing)
            EnsureUIExists();

        if (hideOnStart)
            Hide();
    }

    private void Update()
    {
        if (!victoryShown || !allowReturnToMainMenu)
            return;

        if (Input.GetKeyDown(returnToMainMenuKey) && !string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public static VictoryScreen2D ShowVictory(string message, string mainMenuSceneName, KeyCode returnKey)
    {
#if UNITY_2023_1_OR_NEWER
        VictoryScreen2D screen = FindFirstObjectByType<VictoryScreen2D>(FindObjectsInactive.Include);
#else
        VictoryScreen2D screen = FindObjectOfType<VictoryScreen2D>(true);
#endif
        if (screen == null)
        {
            GameObject screenObject = new GameObject("VictoryScreen2D");
            screen = screenObject.AddComponent<VictoryScreen2D>();
        }

        screen.Configure(message, mainMenuSceneName, returnKey);
        screen.Show();
        return screen;
    }

    public void Configure(string message, string menuSceneName, KeyCode returnKey)
    {
        if (!string.IsNullOrWhiteSpace(message))
            victoryMessage = message;

        if (!string.IsNullOrWhiteSpace(menuSceneName))
            mainMenuSceneName = menuSceneName;

        returnToMainMenuKey = returnKey;
    }

    [ContextMenu("Show Victory")]
    public void Show()
    {
        if (createUIIfMissing)
            EnsureUIExists();

        victoryShown = true;

        if (victoryText != null)
            victoryText.text = victoryMessage;

        if (victoryPanel != null)
        {
            victoryPanel.gameObject.SetActive(true);
            victoryPanel.alpha = 1f;
            victoryPanel.interactable = true;
            victoryPanel.blocksRaycasts = true;
        }

        if (pauseGameOnVictory)
            Time.timeScale = 0f;

        if (debugLogs)
            Debug.Log("VictoryScreen2D: pantalla de victoria mostrada.", this);
    }

    [ContextMenu("Hide Victory")]
    public void Hide()
    {
        victoryShown = false;

        if (victoryPanel != null)
        {
            victoryPanel.alpha = 0f;
            victoryPanel.interactable = false;
            victoryPanel.blocksRaycasts = false;
            victoryPanel.gameObject.SetActive(false);
        }
    }

    private void EnsureUIExists()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
            {
#if UNITY_2023_1_OR_NEWER
                canvas = FindFirstObjectByType<Canvas>();
#else
                canvas = FindObjectOfType<Canvas>();
#endif
            }
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("VictoryCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1001;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (victoryPanel == null)
        {
            GameObject panelObject = new GameObject("VictoryPanel");
            panelObject.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.75f);

            victoryPanel = panelObject.AddComponent<CanvasGroup>();
        }

        if (victoryText == null)
        {
            GameObject textObject = new GameObject("VictoryText");
            textObject.transform.SetParent(victoryPanel.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(120f, 0f);
            textRect.offsetMax = new Vector2(-120f, 0f);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = victoryMessage;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = true;

            victoryText = text;
        }
    }
}
