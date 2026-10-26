using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class WallSpawner : MonoBehaviour
{
    // REFERENCES
    [Header("Wall Prefab")]
    [SerializeField] private WallObstacle wallPrefab;
    [Header("Results Screen")]
    [SerializeField] private GameObject resultsScreen;

    [Header("Wave Data")]
    [SerializeField] private WallWaveData[] waves;

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

    private float spawnTimer;
    private float currentSpeedMultiplier = 1f;

    private bool isSpawning;
    private bool isWaitingForNextWave;
    private Results resultsScript;



    private void Start()
    {
        // TODO: Measure wall spawner - FIN
        // TODO: Divide walls after measurement - FINN
        score = 0;
        maxScore = 0;

        ApplyAdaptiveDifficulty();

        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        if (randomizeWaves) // randomize waves if the option is enabled
        {
            ShuffleWaves();
        }

        if (!LoadWave(0)) // load first wave, if it fails, disable the spawner
        {
            enabled = false;
            return;
        }

        isSpawning = true;
        spawnTimer = 0f;
        resultsScript = resultsScreen?.GetComponent<Results>();
    }

    private WallWaveData CurrentWave
    {
        get
        {
            if (waves == null ||
                currentWaveIndex < 0 ||
                currentWaveIndex >= waves.Length)
                return null;

            return waves[currentWaveIndex];
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

            spawnInterval = 7f;
            baseWallSpeed = 2f;
            wrongHitSpeedMultiplier = 1.10f;
            maximumSpeedMultiplier = 2f;
        }
        else if (difficulty == 1)
        {
            difficultyName = "Normal";

            spawnInterval = 6f;
            baseWallSpeed = 2.5f;
            wrongHitSpeedMultiplier = 1.15f;
            maximumSpeedMultiplier = 3f;
        }
        else
        {
            difficultyName = "Hard";

            // Your tested Hard settings.
            spawnInterval = 5f;
            baseWallSpeed = 3f;
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
        if (index < 0 || index >= waves.Length)
            return false;

        WallWaveData wave = waves[index];

        if (wave == null || !wave.HasValidAnswers())
        {
            Debug.LogError("WallSpawner: Wave " + (index+1) + " has invalid data. Each wave needs exactly one correct answer.");

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
            Debug.LogError($"Wave {index + 1} needs at least one incorrect answer.");

            return false;
        }

        currentWaveIndex = index;
        formationsThisWave = 0;

        if (questionText != null)
        {
            questionText.text = wave.question;
        }
            

        if (waveText != null)
        {
            waveText.text = $"Wave {index + 1}";
        }
            

        Debug.Log($"Started {wave.waveName}");
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
        GameObject spawnedSFX = Instantiate(sfx);
        formationsThisWave++;

        if (formationsThisWave >= wave.spawnsRequired)
        {
            AdvanceWave();
        }
        Destroy(spawnedSFX, 3f);
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

        if (nextWave >= waves.Length)
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
            score += pointsPerCorrectHit; // increase score for correct answer
            Debug.Log("Correct answer!");
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
}
