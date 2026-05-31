using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shows a Game Over screen when the player dies.
/// You can either assign your own panel/text from the Inspector or let the script create a simple UI automatically.
/// Recommended setup: add this component to a Canvas in every playable scene, or to an empty GameObject.
/// </summary>
public class GameOverScreen2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup gameOverPanel;
    [SerializeField] private TMP_Text gameOverText;

    [Header("Text")]
    [SerializeField] private string gameOverMessage = "Game Over";
    [SerializeField] private float fontSize = 76f;

    [Header("Behaviour")]
    [SerializeField] private bool createUIIfMissing = true;
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool pauseGameOnGameOver = true;
    [SerializeField] private float showDelay = 0f;
    [SerializeField] private bool onlyReactToPlayerTag = true;
    [SerializeField] private string playerTag = "Player";

    [Header("Optional Restart")]
    [SerializeField] private bool allowRestartCurrentScene = false;
    [SerializeField] private KeyCode restartKey = KeyCode.R;
    [SerializeField] private bool allowReturnToMainMenu = false;
    [SerializeField] private KeyCode mainMenuKey = KeyCode.Escape;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private bool gameOverShown;

    private void Awake()
    {
        if (createUIIfMissing)
            EnsureUIExists();

        if (hideOnStart)
            Hide();
    }

    private void OnEnable()
    {
        PlayerHealth2D.OnAnyDied += HandlePlayerDied;
    }

    private void OnDisable()
    {
        PlayerHealth2D.OnAnyDied -= HandlePlayerDied;
    }

    private void Update()
    {
        if (!gameOverShown)
            return;

        if (allowRestartCurrentScene && Input.GetKeyDown(restartKey))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            return;
        }

        if (allowReturnToMainMenu && Input.GetKeyDown(mainMenuKey) && !string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    private void HandlePlayerDied(PlayerHealth2D deadPlayer)
    {
        if (gameOverShown || deadPlayer == null)
            return;

        if (onlyReactToPlayerTag && !BelongsToTaggedPlayer(deadPlayer.transform))
            return;

        StartCoroutine(ShowGameOverRoutine());
    }

    private IEnumerator ShowGameOverRoutine()
    {
        if (showDelay > 0f)
            yield return new WaitForSecondsRealtime(showDelay);

        Show();
    }

    [ContextMenu("Show Game Over")]
    public void Show()
    {
        if (createUIIfMissing)
            EnsureUIExists();

        gameOverShown = true;

        if (gameOverText != null)
            gameOverText.text = gameOverMessage;

        if (gameOverPanel != null)
        {
            gameOverPanel.gameObject.SetActive(true);
            gameOverPanel.alpha = 1f;
            gameOverPanel.interactable = true;
            gameOverPanel.blocksRaycasts = true;
        }

        if (pauseGameOnGameOver)
            Time.timeScale = 0f;

        if (debugLogs)
            Debug.Log("GameOverScreen2D: Game Over mostrado.", this);
    }

    [ContextMenu("Hide Game Over")]
    public void Hide()
    {
        gameOverShown = false;

        if (gameOverPanel != null)
        {
            gameOverPanel.alpha = 0f;
            gameOverPanel.interactable = false;
            gameOverPanel.blocksRaycasts = false;
            gameOverPanel.gameObject.SetActive(false);
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
            GameObject canvasObject = new GameObject("GameOverCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (gameOverPanel == null)
        {
            GameObject panelObject = new GameObject("GameOverPanel");
            panelObject.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.75f);

            gameOverPanel = panelObject.AddComponent<CanvasGroup>();
        }

        if (gameOverText == null)
        {
            GameObject textObject = new GameObject("GameOverText");
            textObject.transform.SetParent(gameOverPanel.transform, false);

            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = gameOverMessage;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;

            gameOverText = text;
        }
    }

    private bool BelongsToTaggedPlayer(Transform start)
    {
        Transform current = start;

        while (current != null)
        {
            if (current.CompareTag(playerTag))
                return true;

            current = current.parent;
        }

        return false;
    }
}
