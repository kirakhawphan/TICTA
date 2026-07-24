using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class RhythmScoreManager : MonoBehaviour
{
    private static RhythmScoreManager instance;

    [Header("Scoring")]
    [SerializeField] private int tapScore = 100;
    [SerializeField] private int catchScore = 100;
    [SerializeField] private int holdScore = 200;
    [SerializeField] private int comboBonusEvery = 10;
    [SerializeField] private int comboBonusScore = 50;
    [SerializeField] private bool resetScoreOnStart = true;

    [Header("UI")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private bool createUiIfMissing = true;
    [SerializeField] private Vector2 uiOffset = new Vector2(32f, -32f);

    [Header("Events")]
    [SerializeField] private UnityEvent<int> onScoreChanged = new UnityEvent<int>();
    [SerializeField] private UnityEvent<int> onComboChanged = new UnityEvent<int>();

    private int score;
    private int combo;
    private int maxCombo;

    public static RhythmScoreManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RhythmScoreManager>();
            }

            if (instance == null)
            {
                GameObject scoreManagerObject = new GameObject("Rhythm Score Manager");
                instance = scoreManagerObject.AddComponent<RhythmScoreManager>();
            }

            return instance;
        }
    }

    public int Score => score;
    public int Combo => combo;
    public int MaxCombo => maxCombo;
    public UnityEvent<int> OnScoreChanged => onScoreChanged;
    public UnityEvent<int> OnComboChanged => onComboChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureUi();

        if (resetScoreOnStart)
        {
            ResetScore();
        }
        else
        {
            RefreshUi();
        }
    }

    public void ResetScore()
    {
        score = 0;
        combo = 0;
        maxCombo = 0;
        RefreshUi();
        onScoreChanged.Invoke(score);
        onComboChanged.Invoke(combo);
    }

    public void RegisterHit(RhythmNoteType noteType)
    {
        combo++;
        maxCombo = Mathf.Max(maxCombo, combo);
        score += GetBaseScore(noteType) + GetComboBonus();
        RefreshUi();
        onScoreChanged.Invoke(score);
        onComboChanged.Invoke(combo);
    }

    public void RegisterMiss()
    {
        if (combo == 0)
        {
            return;
        }

        combo = 0;
        RefreshUi();
        onComboChanged.Invoke(combo);
    }

    private int GetBaseScore(RhythmNoteType noteType)
    {
        switch (noteType)
        {
            case RhythmNoteType.Hold:
                return holdScore;
            case RhythmNoteType.Catch:
                return catchScore;
            default:
                return tapScore;
        }
    }

    private int GetComboBonus()
    {
        if (comboBonusEvery <= 0 || comboBonusScore <= 0)
        {
            return 0;
        }

        return combo / comboBonusEvery * comboBonusScore;
    }

    private void EnsureUi()
    {
        if (!createUiIfMissing || (scoreText != null && comboText != null))
        {
            return;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Rhythm Score Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        Transform panel = CreateUiPanel(canvas.transform);
        if (scoreText == null)
        {
            scoreText = CreateUiText(panel, "Score Text", 0f, 32);
        }

        if (comboText == null)
        {
            comboText = CreateUiText(panel, "Combo Text", -42f, 26);
        }
    }

    private Transform CreateUiPanel(Transform parent)
    {
        GameObject panelObject = new GameObject("Score Combo UI", typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = uiOffset;
        rectTransform.sizeDelta = new Vector2(340f, 96f);

        return panelObject.transform;
    }

    private TMP_Text CreateUiText(Transform parent, string objectName, float y, int fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(0f, y);
        rectTransform.sizeDelta = new Vector2(340f, 38f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }

    private void RefreshUi()
    {
        if (scoreText != null)
        {
            scoreText.text = $"SCORE {score}";
        }

        if (comboText != null)
        {
            comboText.text = combo > 0 ? $"COMBO {combo}" : "COMBO 0";
        }
    }

    private void OnValidate()
    {
        tapScore = Mathf.Max(0, tapScore);
        catchScore = Mathf.Max(0, catchScore);
        holdScore = Mathf.Max(0, holdScore);
        comboBonusEvery = Mathf.Max(0, comboBonusEvery);
        comboBonusScore = Mathf.Max(0, comboBonusScore);
    }
}
