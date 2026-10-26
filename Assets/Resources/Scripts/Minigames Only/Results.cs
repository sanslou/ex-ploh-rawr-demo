
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Results : MonoBehaviour
{
    [Header("Results")]
    private int score;
    private int maxScore;

    [Tooltip("Grades")]
    private string[] grade = { "S+", "A", "B", "C", "D", "F" };

    [Header("References")]
    [SerializeField] private TMP_InputField scoreText;
    [SerializeField] private TMP_InputField gradeText;
    [SerializeField] private Button btnDone;
    [SerializeField] private Canvas resultsScreen;
    [SerializeField] private Button btnRetry;
    [SerializeField] private bool isMinigame = false;

    private Image bg;
    private Color originalBgColor;

    private void Awake()
    {
        scoreText = transform.Find("Result Score").GetComponent<TMP_InputField>();
        gradeText = transform.Find("Result Grade").GetComponent<TMP_InputField>();

        resultsScreen = GetComponent<Canvas>();

        btnDone = transform.Find("Button Done").GetComponent<Button>();

        bg = transform.Find("BG").GetComponent<Image>();
        originalBgColor = bg.color;

        if (btnDone != null)
        {
            btnDone.onClick.AddListener(closeResults);
        }

        if (isMinigame)
        {
            Transform retryTransform = transform.Find("Button Retry");

            if (retryTransform != null)
            {
                btnRetry = retryTransform.GetComponent<Button>();

                if (btnRetry != null)
                {
                    btnRetry.onClick.AddListener(RetryMinigame);
                    btnRetry.gameObject.SetActive(false);
                }
            }
            else
            {
                Debug.LogWarning(
                    "Results: Button Retry was not found!"
                );
            }
        }
        else
        {
            if (btnRetry != null)
            {
                btnRetry.gameObject.SetActive(false);
            }
        }
    }

    public void displayResults(int sc, int max, bool failed = false)
    {
        score = sc;
        maxScore = max;

        // Reset background color.
        bg.color = originalBgColor;

        scoreText.text = score + "/" + maxScore;
        gradeText.text = determineGrade(score, maxScore);

        // Special death screen.
        if (score == 0 && maxScore == 0)
        {
            scoreText.text = "slain!";
            gradeText.text = "F";
            bg.color = Color.red;
        }

        // Minigame failure overrides the calculated grade.
        if (failed)
        {
            gradeText.text = "F";
            bg.color = Color.red;
        }

        // Hide Retry by default.
        if (btnRetry != null)
        {
            btnRetry.gameObject.SetActive(false);
        }

        resultsScreen.enabled = true;
    }

    public void ShowRetry()
    {
        if (isMinigame && btnRetry != null)
        {
            btnRetry.gameObject.SetActive(true);
            btnRetry.interactable = true;
        }
    }

    public void closeResults()
    {
        resultsScreen.enabled = false;

        Time.timeScale = 1f;


        if (SceneManager.GetActiveScene().name == "TowerDefense")
        {
        SceneManager.LoadScene("InGame");
        }
        if (SceneManager.GetActiveScene().name == "WallPushMinigame")
        {
            SceneManager.LoadScene("RepairSyntaxMinigame");
        }
        if (SceneManager.GetActiveScene().name == "RepairSyntaxMinigame")
        {
            SceneManager.LoadScene("LTV2");
        }
    }

    public void RetryMinigame()
    {
        Debug.Log("Retrying minigame...");

        // Prevent an old victory from advancing Chapter 1.
        if (SceneManager.GetActiveScene().name == "TowerDefense")
        {
            PlayerPrefs.SetInt("Chapter1_TowerDefenseWon", 0);
            PlayerPrefs.Save();
        }

        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().name
        );
    }

    public void TimeRanOut()
    {
        displayResults(0, 0, true);

        scoreText.text = "expired";
        gradeText.text = "F";

        ShowRetry();
    }

    public void TowerZeroHP()
    {
        displayResults(0, 0, true);

        scoreText.text = "towered";
        gradeText.text = "F";

        ShowRetry();
    }

    public string determineGrade(int sc, int max)
    {
        if (max <= 0)
        {
            bg.color = Color.red;
            return grade[5];
        }

        float weight = ((float)sc / max) * 100f;

        if (weight >= 100f)
        {
            bg.color = Color.yellow;
            return grade[0]; // S+
        }
        else if (weight >= 90f)
        {
            bg.color = Color.green;
            return grade[1]; // A
        }
        else if (weight >= 80f)
        {
            return grade[2]; // B
        }
        else if (weight >= 70f)
        {
            return grade[3]; // C
        }
        else if (weight >= 60f)
        {
            return grade[4]; // D
        }
        else
        {
            bg.color = Color.red;
            return grade[5]; // F
        }
    }
}
