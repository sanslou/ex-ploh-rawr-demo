using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
public class RSManager : MonoBehaviour
{
    // Note: This class is responsible for the Repair Syntax minigame's flow: the timer, the time penalty, loading each challenge one after another, and when the results show up.

    [System.Serializable]
    private class DatabaseChallenge
    {
        public int id;
        public string Name;
        public List<RepairSyntaxWave> waves;
    }

    [System.Serializable]
    private class RepairContentResponse
    {
        public bool success;
        public string message;
        public DatabaseChallenge[] challenges;
    }

    private readonly List<RepairSyntaxData> runtimeChallenges =
        new List<RepairSyntaxData>();

    private IEnumerator Start()
    {
        timerRunning = false;

        string host = "localhost";

        if (!string.IsNullOrWhiteSpace(DBC.IP_ADDRESS))
        {
            host = DBC.IP_ADDRESS.Trim();
        }

        string url =
            "http://" + host +
            "/Explorer/php%20stuffs/get_repair_syntax_content.php" +
            "?chapter=" + assessmentChapter;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 20;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "Repair Syntax download failed: " +
                    request.error + "\n" +
                    request.downloadHandler.text,
                    this
                );
                yield break;
            }

            RepairContentResponse response = null;

            try
            {
                response = JsonUtility.FromJson<RepairContentResponse>(
                    request.downloadHandler.text
                );
            }
            catch (System.Exception error)
            {
                Debug.LogError(
                    "Repair Syntax: Invalid JSON: " + error.Message,
                    this
                );
            }

            if (response == null ||
                !response.success ||
                response.challenges == null ||
                response.challenges.Length == 0)
            {
                Debug.LogError(
                    "Repair Syntax: No usable database challenges received.",
                    this
                );
                yield break;
            }

            foreach (DatabaseChallenge row in response.challenges)
            {
                if (!ValidateDatabaseChallenge(row))
                {
                    Debug.LogError(
                        "Repair Syntax: Invalid challenge content. Game blocked.",
                        this
                    );
                    yield break;
                }

                RepairSyntaxData data =
                    ScriptableObject.CreateInstance<RepairSyntaxData>();

                data.name = "Database_RS_" + row.id;
                data.Name = row.Name;
                data.waves = row.waves;

                runtimeChallenges.Add(data);
            }
        }

        challengeData = runtimeChallenges.ToArray();

        buildChallengeOrder();
        InitializeLoadedGame();

        Debug.Log(
            "Repair Syntax: Loaded " + challengeData.Length +
            " database challenges.",
            this
        );
    }

    private bool ValidateDatabaseChallenge(DatabaseChallenge row)
    {
        if (row == null ||
            string.IsNullOrWhiteSpace(row.Name) ||
            row.waves == null ||
            row.waves.Count != 1)
        {
            return false;
        }

        RepairSyntaxWave wave = row.waves[0];

        if (wave == null ||
            wave.codeLines == null ||
            wave.codeLines.Count == 0)
        {
            return false;
        }

        int errorCount = 0;

        foreach (RepairSyntaxLineData line in wave.codeLines)
        {
            if (line == null ||
                string.IsNullOrWhiteSpace(line.originalCode))
            {
                return false;
            }

            if (!line.hasSyntaxError)
            {
                continue;
            }

            errorCount++;

            if (string.IsNullOrWhiteSpace(line.brokenCode) ||
                string.IsNullOrWhiteSpace(line.incorrectToken) ||
                string.IsNullOrWhiteSpace(line.correctAnswer) ||
                line.tokenChoices == null ||
                line.tokenChoices.Count == 0 ||
                line.tokenChoices.Count > 4 ||
                !line.tokenChoices.Contains(line.incorrectToken) ||
                !line.brokenCode.Contains(line.incorrectToken) ||
                line.incorrectToken == line.correctAnswer)
            {
                return false;
            }

            foreach (string token in line.tokenChoices)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return false;
                }
            }
        }

        return errorCount > 0;
    }

    private void OnDestroy()
    {
        foreach (RepairSyntaxData data in runtimeChallenges)
        {
            if (data != null)
            {
                Destroy(data);
            }
        }
    }

    [Header("Result Recording")]
    [SerializeField] private MinigameResultRecorder resultRecorder;

    [Header("Challenge Data")]
    [SerializeField] private RepairSyntaxData[] challengeData;

    [Header("Adaptive Difficulty")]
    [SerializeField] private bool useAdaptiveDifficulty = true;
    [SerializeField, Min(1)] private int assessmentChapter = 2; // prob 3

    [Header("Challenge Selection")]
    [Tooltip("Shuffles the order the challenges are played in. If off, they are played in the same order as the array above.")]
    [SerializeField] private bool randomizeOrder;
    [Tooltip("Seconds to wait after the last fix before the next challenge loads (so the green feedback can be seen).")]
    [SerializeField] private float nextChallengeDelay = 1.5f;

    [Header("Timer")]
    [SerializeField] private float startingTimeSeconds = 120f;
    [SerializeField] private float penaltyFraction = 0.25f; // drains this amount of percentage instead of fixed whole number times
    [SerializeField] private float wrongTokenPenaltyFraction = 0.1f; // When pressed wrong button  inside the Repair UI. 0.1 = 10%
    [SerializeField] private GameObject timerUI;

    [Header("Timer Colors")]
    [SerializeField] private Color greenColor = Color.green;
    [SerializeField] private Color yellowColor = Color.yellow;
    [SerializeField] private Color redColor = Color.red;

    [Header("References")]
    [SerializeField] private Results resultsUI;
    [SerializeField] private RSLineSpawner lineSpawner;

    

    // Fetch the references
    private TMP_Text timerText;
    private Image clockIcon;

    private float remainingTime;
    private int currentWaveIndex;
    private bool timerRunning;

    private List<int> challengeOrder = new List<int>(); // Indexes of challengeData in the order they will be played
    private int orderPosition;                           // Where we are inside challengeOrder

    private int repairedSyntaxCount;  // This challenge only
    private int totalSyntaxErrors;    // This challenge only
    private int allRepairedCount;     // Every challenge so far, used for the results
    private int allSyntaxErrors;      // Every challenge so far, used for the results

    private bool resultsShown;
    private bool isLoadingNext;       // True while waiting to load the next challenge
    private bool isRepairing;         // True while the Repair UI is open for a line
    public bool IsRepairing => isRepairing;

    public RepairSyntaxData ChallengeData // The challenge being played right now. Null if there is none
    {
        get
        {
            if (challengeData == null || orderPosition < 0 || orderPosition >= challengeOrder.Count)
                return null;

            return challengeData[challengeOrder[orderPosition]];
        }
    }

    public int CurrentWaveIndex => currentWaveIndex;

    public RepairSyntaxWave CurrentWave // The wave being played right now. Null if there is no valid wave
    {
        get
        {
            if (ChallengeData == null || ChallengeData.waves == null || currentWaveIndex < 0 || currentWaveIndex >= ChallengeData.waves.Count)
                return null;

            return ChallengeData.waves[currentWaveIndex];
        }
    }

    void Awake()
    {
        ApplyAdaptiveDifficulty();
        // 1. Build the play order here, because RSLineSpawner reads CurrentWave in its own Start

        if (timerUI == null)
        {
            Debug.LogError("RSManager: TimerUI is not assigned! (Did you assign it in the Inspector?)");
            return;
        }

        // 2. Find the timer's text and clock icon
        Transform textTransform = timerUI.transform.Find("Text");
        Transform clockTransform = timerUI.transform.Find("ClockIcon");

        if (textTransform != null) timerText = textTransform.GetComponent<TMP_Text>();
        if (clockTransform != null) clockIcon = clockTransform.GetComponent<Image>();

        if (timerText == null || clockIcon == null)
        {
            Debug.LogError("RSManager: TimerUI is missing a child! (Are \"Text\" and \"ClockIcon\" named exactly?)");
        }
    }

    private void InitializeLoadedGame()
    {
        // 1. Make sure there is a valid challenge to play
        if (ChallengeData == null)
        {
            Debug.LogError("RSManager: No valid challenge found! (Is challengeData filled in the Inspector?)");
            return;
        }

        if (ChallengeData.waves == null || ChallengeData.waves.Count == 0)
        {
            Debug.LogError("RSManager: The selected challenge has no waves!");
            return;
        }

        // 2. Find the results UI and the line spawner if they weren't assigned
        if (resultsUI == null)
        {
            resultsUI = FindFirstObjectByType<Results>();
        }

        if (resultsUI == null)
        {
            Debug.LogError("RSManager: Results component was not found! (Is there a Results script in the scene?)");
        }

        if (lineSpawner == null)
        {
            lineSpawner = FindFirstObjectByType<RSLineSpawner>();
        }

        if (lineSpawner == null)
        {
            Debug.LogError("RSManager: RSLineSpawner was not found! (Is there an RSLineSpawner in the scene?)");
        }

        // 3. Reset the progress
        currentWaveIndex = 0;
        allRepairedCount = 0;
        allSyntaxErrors = 0;
        resultsShown = false;
        isLoadingNext = false;
        isRepairing = false;

        // 4. Count the syntax errors in the first challenge
        if (!countSyntaxErrors())
            return;

        if (lineSpawner == null || resultsUI == null)
        {
            Debug.LogError(
                "Repair Syntax cannot start: spawner or results UI is missing.",
                this
            );
            return;
        }

        lineSpawner.SpawnCodeLines();

        // 5. Start the timer
        remainingTime = Mathf.Max(0f, startingTimeSeconds);
        timerRunning = remainingTime > 0f;

        updateTimerUI();
    }

    void Update()
    {
        if (!timerRunning)
            return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            timerRunning = false;

            if (resultsUI != null)
            {
                if (resultRecorder != null)
                {
                    resultRecorder.Record(
                        allRepairedCount,
                        allSyntaxErrors,
                        "Fail"
                    );
                }
                resultsUI.TimeRanOut();
                resultsShown = true;
            }
            else
            {
                Debug.LogError("RSManager: Results UI is missing!");
            }
        }

        updateTimerUI();
    }

    void buildChallengeOrder() // Fills challengeOrder with 0, 1, 2... and shuffles it if randomizeOrder is on
    {
        challengeOrder.Clear();
        orderPosition = 0;

        if (challengeData == null)
            return;

        for (int i = 0; i < challengeData.Length; i++)
        {
            if (challengeData[i] != null)
            {
                challengeOrder.Add(i);
            }
        }

        if (!randomizeOrder)
            return;

        // Fisher-Yates shuffle
        for (int i = challengeOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            int temp = challengeOrder[i];
            challengeOrder[i] = challengeOrder[j];
            challengeOrder[j] = temp;
        }
    }

    bool countSyntaxErrors() // Counts the syntax errors in the CURRENT wave only. Returns false if it can't
    {
        repairedSyntaxCount = 0;
        totalSyntaxErrors = 0;

        RepairSyntaxWave wave = CurrentWave;

        if (wave == null || wave.codeLines == null)
        {
            Debug.LogError("RSManager: Current wave or code lines are missing!");
            return false;
        }

        for (int i = 0; i < wave.codeLines.Count; i++)
        {
            RepairSyntaxLineData line = wave.codeLines[i];

            if (line != null && line.hasSyntaxError)
            {
                totalSyntaxErrors++;
            }
        }

        if (totalSyntaxErrors == 0)
        {
            Debug.LogError("RSManager: This challenge has no syntax errors, so it can never be finished! (Does at least one line have hasSyntaxError ticked?)");
            return false;
        }

        allSyntaxErrors += totalSyntaxErrors;
        return true;
    }

    void updateTimerUI() // Updates the MM:SS text, then colors the text and clock based on how much time is left
    {
        if (timerText != null)
        {
            int totalSeconds = Mathf.CeilToInt(remainingTime);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        Color currentColor = greenColor;

        if (startingTimeSeconds > 0f)
        {
            float timePercentage = remainingTime / startingTimeSeconds * 100f;

            if (timePercentage <= 10f)
            {
                currentColor = redColor;
            }
            else if (timePercentage <= 50f)
            {
                currentColor = yellowColor;
            }
        }
        else
        {
            currentColor = redColor;
        }

        if (timerText != null) timerText.color = currentColor;
        if (clockIcon != null) clockIcon.color = currentColor;
    }

    public void ApplyTimePenalty() // Drains a fraction of the STARTING time when the player hits a line with no error
    {
        drainTime(penaltyFraction);
    }

    public void ApplyWrongTokenPenalty() // Called by RSRepairUI when the player picks the wrong token button
    {
        drainTime(wrongTokenPenaltyFraction);
    }

    void drainTime(float fraction) // Takes a fraction of the STARTING time away, and ends the game if it hits zero
    {
        if (!timerRunning)
            return;

        remainingTime -= startingTimeSeconds * fraction;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            timerRunning = false;

            if (resultsUI != null)
            {
                if (resultRecorder != null)
                {
                    resultRecorder.Record(
                        allRepairedCount,
                        allSyntaxErrors,
                        "Fail"
                    );
                }
                resultsUI.TimeRanOut();
                resultsShown = true;
            }
        }

        updateTimerUI();
    }

    public void RegisterSyntaxRepair() // Called by RSRepairUI every time the player fixes a line
    {
        if (resultsShown)
            return;

        isRepairing = false;
        repairedSyntaxCount++;
        allRepairedCount++;

        // Not every broken line in this challenge is fixed yet
        if (repairedSyntaxCount < totalSyntaxErrors)
            return;

        // 1. There is another challenge left, so load it
        if (orderPosition + 1 < challengeOrder.Count)
        {
            StartCoroutine(LoadNextChallenge());
            return;
        }

        // 2. That was the last challenge, so stop the timer and show the results
        resultsShown = true;
        timerRunning = false;

        if (resultRecorder != null)
        {
            resultRecorder.Record(
                allRepairedCount,
                allSyntaxErrors,
                "Win"
            );
        }

        // WINNING CONDITIONS
        if (resultsUI != null)
        {
            resultsUI.displayResults(allRepairedCount, allSyntaxErrors);
        }
        else
        {
            Debug.LogError("RSManager: Results UI is missing!");
        }
    }

    IEnumerator LoadNextChallenge() // Despawns the current code lines, then spawns the next challenge's lines
    {
        isLoadingNext = true;

        yield return new WaitForSeconds(nextChallengeDelay); // Lets the player see the green feedback on the last fixed line

        if (resultsShown) // Time ran out while we were waiting
            yield break;

        // 1. Despawn the current code lines
        if (lineSpawner != null) lineSpawner.DespawnCodeLines();

        // 2. Move on to the next challenge
        orderPosition++;

        // 3. Count its syntax errors
        if (!countSyntaxErrors())
        {
            isLoadingNext = false;
            yield break;
        }

        // 4. Spawn its code lines
        if (lineSpawner != null) lineSpawner.SpawnCodeLines();

        isLoadingNext = false;
    }

    public void HandleCodeLineHit(RepairSyntaxLineData lineData) // What happens when the player hits a code line
    {
        if (lineData == null)
        {
            Debug.LogError("RSManager: Code line data is missing!");
            return;
        }

        if (resultsShown || isLoadingNext)
            return;

        // 1. If player hits a correct syntax. Apply punishment.
        if (!lineData.hasSyntaxError)
        {
            ApplyTimePenalty();
            return;
        }

        // 2. If player hits a broken syntax. Show Repair UI
        RSRepairUI repairUI = FindFirstObjectByType<RSRepairUI>();

        if (repairUI == null)
        {
            Debug.LogError("RSManager: RSRepairUI was not found! (Is there an RSRepairUI in the scene?)");
            return;
        }

        isRepairing = true;
        repairUI.ShowRepairUI(lineData);
    }

    private void ApplyAdaptiveDifficulty()
    {
        if (!useAdaptiveDifficulty)
        {
            return;
        }

        int difficulty = ChapterDifficulty.GetDifficulty(assessmentChapter);
        string difficultyName;

        if (difficulty == 0)
        {
            difficultyName = "Easy";

            startingTimeSeconds *= 1.5f;
            penaltyFraction *= 0.5f;
            wrongTokenPenaltyFraction *= 0.5f;
        }
        else if (difficulty == 1)
        {
            difficultyName = "Normal";

            startingTimeSeconds *= 1.25f;
            penaltyFraction *= 0.75f;
            wrongTokenPenaltyFraction *= 0.75f;
        }
        else
        {
            difficultyName = "Hard";

            // Keep existing Inspector values. DO NOT CHANGE IT.
        }

        Debug.Log(
            "Repair Syntax | Chapter " + assessmentChapter +
            " | Performance: " +
            ChapterDifficulty.GetPerformance(assessmentChapter).ToString("F1") +
            "% | Difficulty: " + difficultyName +
            " | Starting Time: " + startingTimeSeconds +
            " | Valid-Line Penalty: " + (penaltyFraction * 100f) + "%" +
            " | Wrong-Token Penalty: " +
            (wrongTokenPenaltyFraction * 100f) + "%"
        );
    }


}