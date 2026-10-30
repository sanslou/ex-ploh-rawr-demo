using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;

public class WallSpawner : MonoBehaviour
{

    [System.Serializable]
    private class DatabaseQuestion
    {
        public int id;
        public string question;
        public string[] choices;
        public string answer;
        public string waveName;
        public int spawnsRequired;
    }

    [System.Serializable]
    private class DatabaseResponse
    {
        public bool success;
        public string message;
        public DatabaseQuestion[] questions;
    }

    private readonly List<WallWaveData> runtimeWaves =
        new List<WallWaveData>();

    private string CleanDatabaseAnswer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return System.Text.RegularExpressions.Regex.Replace(
            value.Trim(),
            @"\s+",
            " "
        );
    }
    // REFERENCES
    [Header("Result Recording")]
    [SerializeField] private MinigameResultRecorder resultRecorder;

    [Header("Wall Prefab")]
    [SerializeField] private WallObstacle wallPrefab;
    [Header("Results Screen")]
    [SerializeField] private GameObject resultsScreen;

    [Header("Wave Data")]
    [SerializeField] private WallWaveData[] waves;

    [SerializeField] private bool loadWavesFromResources = true;
    [Header("Wave Settings")]
    [SerializeField, Min(1)] private int numberOfWaves = 5;

    [Header("Question UI")]
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private TMP_Text waveText;

    [Header("Adaptive Difficulty")]
    [SerializeField] private bool useAdaptiveDifficulty = true;
    [SerializeField, Min(1)] private int assessmentChapter = 2;

    // WALL SPAWN LOGIC
    [Header("Formation Spawning")]
    [Min(0.1f)]
    [SerializeField] private float spawnInterval = 3f;

    [Header("Wave Transition")]
    [SerializeField] private float wavePauseDuration = 5f;

    [Tooltip("Wall width in local units. Set this to 0 to use the root BoxCollider.")]
    [SerializeField] private float wallSegmentWidth = 0f;

    [Tooltip("Wall depth in local units. Set to 0 to use the root BoxCollider.")]
    [SerializeField] private float wallSegmentDepth = 0f;

    [Tooltip("Maximum random Z offset as a fraction of wall depth.")]
    [Range(0f, 0.45f)] // this makes a slider
    [SerializeField] private float staggerAmount = 0.4f;

    [Header("Wave Randomization")]
    [SerializeField] private bool randomizeWaves = true;

    [Header("Wall Movement")]
    [SerializeField] private float baseWallSpeed = 3f;

    // Wrong answer consequences

    [Min(1f)]
    [SerializeField] private float wrongHitSpeedMultiplier = 1.25f;

    [Min(1f)]
    [SerializeField] private float maximumSpeedMultiplier = 4f;

    // SCORING SYSTEM
    [Header("Scoring")]
    [SerializeField, Min(1)] private int pointsPerCorrectHit = 1;

    private readonly List<WallObstacle> activeWalls = new();

    private int score;
    private int maxScore;
    private int currentWaveIndex;
    private int formationsThisWave;
    private bool correctWallHitThisWave;
    private bool allFormationsSpawned;
    private int totalWavesToPlay;
    private WallWaveData selectedWave;

    private float spawnTimer;
    private float currentSpeedMultiplier = 1f;

    private bool isSpawning;
    private bool isWaitingForNextWave;
    private Results resultsScript;



    private IEnumerator Start()
    {
        score = 0;
        maxScore = 0;
        isSpawning = false;

        string host = "localhost";

        if (!string.IsNullOrWhiteSpace(DBC.IP_ADDRESS))
        {
            host = DBC.IP_ADDRESS.Trim();
        }

        string url =
            "http://" + host +
            "/Explorer/php%20stuffs/get_sledgehammer_questions.php" +
            "?chapter=" + assessmentChapter;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 20;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "Sledgehammer content download failed: " +
                    request.error,
                    this
                );
                yield break;
            }

            DatabaseResponse response = null;

            try
            {
                response = JsonUtility.FromJson<DatabaseResponse>(
                    request.downloadHandler.text
                );
            }
            catch (System.Exception error)
            {
                Debug.LogError(
                    "Sledgehammer: Invalid server response: " +
                    error.Message,
                    this
                );
            }

            if (response == null ||
                !response.success ||
                response.questions == null ||
                response.questions.Length == 0)
            {
                Debug.LogError(
                    "Sledgehammer: No usable database questions received.",
                    this
                );
                yield break;
            }

            foreach (DatabaseQuestion row in response.questions)
            {
                if (row == null ||
                    row.spawnsRequired < 1 ||
                    string.IsNullOrWhiteSpace(row.question) ||
                    string.IsNullOrWhiteSpace(row.answer) ||
                    row.choices == null ||
                    row.choices.Length != 4
                    )
                {
                    Debug.LogError(
                        "Sledgehammer: Incomplete database question.",
                        this
                    );
                    yield break;
                }

                string correctAnswer = CleanDatabaseAnswer(row.answer);

                WallAnswerData[] answers = new WallAnswerData[4];
                int correctCount = 0;

                for (int i = 0; i < row.choices.Length; i++)
                {
                    string text = CleanDatabaseAnswer(row.choices[i]);

                    if (string.IsNullOrWhiteSpace(text))
                    {
                        Debug.LogError(
                            "Sledgehammer: Empty choice at question " + row.id,
                            this
                        );
                        yield break;
                    }

                    bool isCorrect = string.Equals(
                        text,
                        correctAnswer,
                        System.StringComparison.OrdinalIgnoreCase
                    );

                    if (isCorrect)
                    {
                        correctCount++;
                    }

                    answers[i] = new WallAnswerData
                    {
                        answerText = text,
                        isCorrect = isCorrect
                    };
                }

                if (correctCount != 1)
                {
                    Debug.LogError(
                        "Sledgehammer: Question " + row.id +
                        " must have exactly one choice matching Answer.",
                        this
                    );
                    yield break;
                }

                WallWaveData wave =
                    ScriptableObject.CreateInstance<WallWaveData>();

                wave.name = "Database_Wall_" + row.id;
                wave.waveName = row.waveName;

                if (string.IsNullOrWhiteSpace(wave.waveName))
                {
                    wave.waveName = "Question " + row.id;
                }

                wave.question = row.question.Trim();
                wave.spawnsRequired = row.spawnsRequired;
                wave.answers = answers;

                runtimeWaves.Add(wave);
            }
        }

        waves = runtimeWaves.ToArray();

        Debug.Log(
            "Sledgehammer: Loaded " + waves.Length +
            " database wave questions.",
            this
        );

        ApplyAdaptiveDifficulty();

        if (!ValidateSetup())
        {
            enabled = false;
            yield break;
        }

        totalWavesToPlay = Mathf.Max(1, numberOfWaves);

        if (randomizeWaves)
        {
            ShuffleWaves();
        }

        if (!LoadWave(0))
        {
            enabled = false;
            yield break;
        }

        resultsScript = resultsScreen?.GetComponent<Results>();

        isSpawning = true;
        spawnTimer = 0f;

        Debug.Log(
            "Sledgehammer: Playing " + totalWavesToPlay + " waves.",
            this
        );
    }

    private void OnDestroy()
    {
        foreach (WallWaveData wave in runtimeWaves)
        {
            if (wave != null)
            {
                Destroy(wave);
            }
        }
    }

    private WallWaveData CurrentWave
    {
        get
        {
            return selectedWave;
        }
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

            spawnInterval = 6f;
            baseWallSpeed = 1.5f;
            wrongHitSpeedMultiplier = 1.10f;
            maximumSpeedMultiplier = 2f;
        }
        else if (difficulty == 1)
        {
            difficultyName = "Normal";

            spawnInterval = 4f; // 4
            baseWallSpeed = 4f; //4
            wrongHitSpeedMultiplier = 1.15f;
            maximumSpeedMultiplier = 3f;
        }
        else
        {
            difficultyName = "Hard";

            // Your tested Hard settings.
            spawnInterval = 3f;
            baseWallSpeed = 6f;
            wrongHitSpeedMultiplier = 1.25f;
            maximumSpeedMultiplier = 4f;
        }

        Debug.Log(
            "WallSpawner | Chapter " + assessmentChapter +
            " | Performance: " +
            ChapterDifficulty.GetPerformance(assessmentChapter).ToString("F1") +
            "% | Difficulty: " + difficultyName
        );
    }
    private bool ValidateSetup() // debugging purposes
    {
        if (wallPrefab == null)
        {
            Debug.LogError("WallSpawner: Wall prefab is missing. (Did you attach one to the hierarchy?)", this);
            return false;
        }

        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("WallSpawner: No wave data assigned. (Do you have ScriptableObjects present)", this);
            return false;
        }

        if (!TryGetWallDimensions(out _, out _))
            return false;

        return true;
    }

    private void ShuffleWaves()
    {
        // "Fisher-Yates shuffle"
        for (int i = waves.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            WallWaveData temp = waves[i];
            waves[i] = waves[j];
            waves[j] = temp;
        }
    }


    private bool TryGetWallDimensions(out float width,out float depth)
    {
        width = wallSegmentWidth;
        depth = wallSegmentDepth;

        if (wallPrefab == null)
        {
            Debug.LogError("WallSpawner: Wall Prefab is missing or destroyed. Assign the actual prefab asset from the Project window.");

            return false;
        }

        BoxCollider box = wallPrefab.GetComponent<BoxCollider>();

        if (box != null)
        {
            if (width <= 0f)
                width = box.size.x;

            if (depth <= 0f)
                depth = box.size.z;
        }

        if (width <= 0f || depth <= 0f)
        {
            Debug.LogError("WallSpawner: Set Wall Segment Width and Depth, or add a BoxCollider to the root Wall prefab.", this);

            return false;
        }

        return true;
    }


    private void Update()
    {
        if (!isSpawning || CurrentWave == null)
            return;

        spawnTimer += Time.deltaTime;

        if (spawnTimer >= spawnInterval)
        {
            spawnTimer -= spawnInterval;
            SpawnFormation();
        }
    }


    private bool LoadWave(int index)
    {
        if (waves == null || waves.Length == 0)
        {
            Debug.LogError("WallSpawner: No wave data assigned.");
            return false;
        }

        // Randomly choose among the existing wave assets.
        int randomIndex = Random.Range(0, waves.Length);

        if (waves.Length > 1 && selectedWave != null)
        {
            int previousIndex = System.Array.IndexOf(waves, selectedWave);

            if (previousIndex >= 0)
            {
                randomIndex = Random.Range(0, waves.Length - 1);

                if (randomIndex >= previousIndex)
                {
                    randomIndex++;
                }
            }
        }
        WallWaveData wave = waves[randomIndex];

        if (wave == null || !wave.HasValidAnswers())
        {
            Debug.LogError("WallSpawner: Invalid wave data selected.");
            return false;
        }

        bool hasWrongAnswer = false;

        foreach (WallAnswerData answer in wave.answers)
        {
            if (!answer.isCorrect)
            {
                hasWrongAnswer = true;
                break;
            }
        }

        if (!hasWrongAnswer)
        {
            Debug.LogError("WallSpawner: Wave needs incorrect answers.");
            return false;
        }

        selectedWave = wave;
        currentWaveIndex = index;
        formationsThisWave = 0;

        correctWallHitThisWave = false;
        allFormationsSpawned = false;

        if (questionText != null)
        {
            questionText.text = wave.question;
        }

        if (waveText != null)
        {
            waveText.text = "Wave " + (index + 1) + "/" + numberOfWaves;
        }

        Debug.Log(
            "Started Wave " + (index + 1) +
            ": " + wave.waveName
        );

        return true;
    }


    private void SpawnFormation()
    {
        Debug.Log($"Wall Prefab reference: " + $"{(wallPrefab == null ? "NULL" : wallPrefab.name)}");
        WallWaveData wave = CurrentWave;

        if (wave == null)
            return;

        if (!TryGetWallDimensions(out float localWidth,out float localDepth))
        {
            isSpawning = false;
            return;
        }

       
        float segmentWidth = localWidth * Mathf.Abs(wallPrefab.transform.lossyScale.x);  // Converts the segment dimensions into world dimensions
        float segmentDepth = localDepth * Mathf.Abs(wallPrefab.transform.lossyScale.z);
        float fieldWidth = Mathf.Abs(transform.lossyScale.x);

        if (segmentWidth <= 0f || fieldWidth <= 0f)
        {
            Debug.LogError("WallSpawner: Invalid wall or field width.", this);
            isSpawning = false;
            return;
        }

        
        int segmentCount = Mathf.Max(1,Mathf.CeilToInt(fieldWidth / segmentWidth)); // Makes each wall segment equal to each other

        float spacing = fieldWidth / segmentCount;

        // Finds the correct answer references for this wave
        WallAnswerData correctAnswer = null;
        List<WallAnswerData> wrongAnswers = new();

        foreach (WallAnswerData answer in wave.answers)
        {
            if (answer.isCorrect)
            {
                correctAnswer = answer;
            }
            else
            {
                wrongAnswers.Add(answer);
            }

        }

        
        int correctSegment = Random.Range(0, segmentCount); // This puts the correct answer in one random segment

        // Makes wall segments' Z offsets fall within their collider depth so hopefully they overlap
        float maxOffset = segmentDepth * staggerAmount;

        for (int i = 0; i < segmentCount; i++)
        {
            float localX = -fieldWidth * 0.5f + spacing * (i + 0.5f);
            float randomZOffset = Random.Range(-maxOffset, maxOffset);
            Vector3 position = new Vector3(transform.position.x + localX, transform.position.y, transform.position.z + randomZOffset);

            WallAnswerData answer;

            if (i == correctSegment)
            {
                answer = correctAnswer;
            }
            else
            {
                answer = wrongAnswers[
                    Random.Range(0, wrongAnswers.Count)
                ];
            }

            WallObstacle wall = Instantiate(wallPrefab, position, wallPrefab.transform.rotation);

            wall.Initialize(this, answer, baseWallSpeed * currentSpeedMultiplier);

            // Increases max score for each correct answer wall spawned
            if (answer.isCorrect) 
            {
                maxScore += pointsPerCorrectHit;
            }

            activeWalls.Add(wall);
        }
        GameObject sfx = Resources.Load<GameObject>("Prefabs/Minigames Only/SFX_MovingStone");

        if (sfx != null)
        {
            GameObject spawnedSFX = Instantiate(sfx);
            Destroy(spawnedSFX, 3f);
        }
        else
        {
            Debug.LogWarning("WallSpawner: SFX_MovingStone prefab not found in Resources.");
        }

        formationsThisWave++;

        if (formationsThisWave >= wave.spawnsRequired)
        {
            allFormationsSpawned = true;

            if (correctWallHitThisWave)
            {
                isSpawning = false;
                if (allFormationsSpawned)
                {   
                    isSpawning = false;
                }
                TryAdvanceWave();
            }
            // Otherwise, continue spawning the same wave.
        }
    }

    private void TryAdvanceWave()
    {
        if (!allFormationsSpawned)
        {
            return;
        }

        if (!correctWallHitThisWave)
        {
            return;
        }

        if (isWaitingForNextWave)
        {
            return;
        }

        AdvanceWave();
    }
    private void AdvanceWave() // Per wave.
    {
        if (isWaitingForNextWave) return;

        isSpawning = false;
        isWaitingForNextWave = true;

        StartCoroutine(AdvanceWaveAfterPause());
    }


    private IEnumerator AdvanceWaveAfterPause()
    {
        // Wait before transitioning from the completed wave
        yield return new WaitForSeconds(wavePauseDuration);

        int nextWave = currentWaveIndex + 1;

        if (nextWave >= numberOfWaves)
        {
            Debug.Log("All waves completed. Waiting for remaining walls...");
            isSpawning = false; // Stop spawning new walls if all waves are completed

            
            while (activeWalls.Count > 0) // wait until every active wall has despawned or been destroyed
            {
                
                activeWalls.RemoveAll(wall => wall == null); // Remove destroyed wall references, if any remains

                if (activeWalls.Count > 0)
                    yield return null;
            }

            Debug.Log("All walls cleared. Displaying results.");

            if (resultRecorder != null)
            {
                string outcome = "Fail";

                if (maxScore > 0)
                {
                    float percentage = (float)score / maxScore * 100f;

                    if (percentage >= 60f)
                    {
                        outcome = "Win";
                    }
                }

                resultRecorder.Record(
                    score,
                    maxScore,
                    outcome
                );
            }

            isWaitingForNextWave = false;

            if (resultsScreen != null)
            {
                resultsScreen.SetActive(true);
            }

            if (resultsScript != null)
            {
                resultsScript.displayResults(score, maxScore);
            }
            else
            {
                Debug.LogError(
                    "WallSpawner: Results component is missing!"
                );
            }

            yield break;
        }

        if (LoadWave(nextWave))
        {
            spawnTimer = 0f;
            isSpawning = true;
        }

        isWaitingForNextWave = false;
    }


    public void OnWallHit(WallObstacle wall, bool isCorrect)
    {
        if (isCorrect)
        {
            score += pointsPerCorrectHit;

            correctWallHitThisWave = true;

            Debug.Log("Correct answer! Wave requirement fulfilled.");

            TryAdvanceWave();

            return;
        }

        currentSpeedMultiplier = Mathf.Min(currentSpeedMultiplier * wrongHitSpeedMultiplier,maximumSpeedMultiplier); // failure means more speed increased

        // This updates every active wall that's currently active
        for (int i = activeWalls.Count - 1; i >= 0; i--)
        {
            if (activeWalls[i] == null)
            {
                activeWalls.RemoveAt(i);
                continue;
            }

            activeWalls[i].MoveSpeed =
                baseWallSpeed * currentSpeedMultiplier;
        }
        score -= pointsPerCorrectHit; //  subtracts the score for wrong answer each time wrong wall is hit
        Debug.Log($"Wrong answer! Wall speed increased: " + $"{currentSpeedMultiplier:F2}x");
    }

    public void UnregisterWall(WallObstacle wall)
    {
        activeWalls.Remove(wall);
    }

    public void RecordDeathResult()
    {
        isSpawning = false;
        StopAllCoroutines();

        if (resultRecorder != null)
        {
            resultRecorder.Record(
                score,
                maxScore,
                "Fail"
            );
        }
        else
        {
            Debug.LogError(
                "WallSpawner: Result Recorder is not assigned.",
                this
            );
        }
    }
}
