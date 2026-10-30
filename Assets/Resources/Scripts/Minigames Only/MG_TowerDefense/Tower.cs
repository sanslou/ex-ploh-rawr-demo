using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class Tower : MonoBehaviour
{
    // This class is responsible for the tower and its properties, as well as enemy spawn properties.
    public bool isOngoing = true;
    [SerializeField] private bool gameOver = false;
    [Header("Result Recording")]
    [SerializeField] private MinigameResultRecorder resultRecorder;

    [Header("Tower Properties")]
    [Tooltip("Tower's health in float values")]
    public float towerHealth; //500;
    public float towerMaxHealth;

    [Tooltip("Number of seconds until next enemy wave spawns AFTER a wave is defeated")]
    public float enemySpawnRate; // 20.0f;

    [Tooltip("Number of enemies to summon per wave")]
    public int enemiesPerWave; // 4;

    [Tooltip("The total number of waves before the minigame's completion")]
    public int numberOfWaves; // 3;
    public int currentWavesLeft;

    //
    [Header("Individual Enemy Properties")]
    [Tooltip("A singular enemy's tower damage")]
    public float enemyDamage = 15.0f;
    [Tooltip("A singular enemy's tower damage multiplier whenever a critical chance is a success")]
    public float enemyDamageCritMultiplier = 2.0f;
    [Tooltip("Chance to crit (out of 100).")]
    public float enemyDamageCritChance = 10f;
    [Tooltip("Enemy's attack speed (per second).")]
    public float enemyDamageTickRate = 2.0f;
    [Tooltip("Enemy walking speed")]
    public float enemyWalkSpeed = 3.5f;

    [Header("Ally (fake enemy) Properties")]
    [Tooltip("An fake enemy (ally)'s heal amount to the tower when it gets consumed by the tower")]
    public float allyHeal = 5f; // = 5 for balanced. Three ally heals to compensate for the enemy damage, which is 15.
    [Tooltip("If an ally is slain, this is how much damage it deals against the tower as punishment")]
    public float allySlainDmg = 15f;

    [Header("Player Properties")]
    [Tooltip("Player's minimum damage against enemies not yet influenced by critical strikes.")]
    public float playerDamage = 26.0f;

    [Header("For scoring system")]
    public int totalEnemiesSpawned = 0;
    public int enemiesSlain = 0;
    public int totalHostileSpawned = 0;
    public int hostileEnemiesSlain = 0;

    /* <REQUIRES IMPLEMENTATION>

    [Tooltip("Player's attack speed per second")] 
    public float playerAtkSpeed = 1.0f; 
    [Tooltip("Player speed boost increase after killing an enemy (current player speed + this much amount)")]
    public float speedIncrease = 10f; // placeholder. should be toggleable or added immediately after killing an enemy?
    [Tooltip("Player speed boost duration in seconds")]
    public float speedDuration = 1f; 
    */

    //
    [Header("Miscellaneous")]
    public int nextEnemyID = 0;
    public int enemiesAlive = 0;
    
    

    // <Private>
    [Header("Serialized References")] // Debugging purposes.
    [SerializeField] private Transform spawnLocation;
    [SerializeField] private Transform[] spawnChildren;
    [SerializeField] private GameObject enemy;
    [SerializeField] private Enemy_TD enemyScript;
    [SerializeField] private NametagManager nametagScript;
    [SerializeField] private Results resultScript;
    [SerializeField] private TextMeshProUGUI td_question; // UI
    [SerializeField] private GameObject invisWall;
    [SerializeField] private TextMeshProUGUI towerUI;
    [SerializeField] private CanvasGroup towerCG;
    public Slider towerHpBar;
    private Button buttonAttack;
    private List<int> selectedIndexes = new List<int>(); // attachNametag(): Lists all selected indexes to avoid duplicates
    // </Private>


    void Start()
    {
        spawnLocation = GameObject.Find("enemySpawns").transform;
        buttonAttack = GameObject.FindGameObjectWithTag("UI_Attack").GetComponent<Button>();
        nametagScript = GetComponent<NametagManager>();
        currentWavesLeft = numberOfWaves;
        towerUI = GameObject.Find("BossUI").GetComponent<TextMeshProUGUI>(); 
        resultScript = GameObject.Find("Terminal Results").GetComponent<Results>();
        invisWall = transform.parent.Find("Invisible").gameObject;
        towerCG = GameObject.Find("BossUI").GetComponent<CanvasGroup>();

        towerCG.alpha = 0;
        if (isOngoing)
        {
            buttonAttack.gameObject.SetActive(true);
            buttonAttack.interactable = true;
            Debug.Log("Tower Defense minigame is ongoing. Attack button enabled.");
            invisWall.SetActive(true);
            towerUI.enabled = true;

            towerHpBar = GameObject.Find("BossHPBar").GetComponent<Slider>();   
            td_question = GameObject.Find("BossUI").GetComponent<TextMeshProUGUI>();

            towerCG.alpha = 1;
            
        } else
        {
            return;
        }

            towerHpBar.value = towerHealth;
        towerHpBar.maxValue = towerHealth;



        foreach (Transform child in spawnLocation) // Get all spawn locations
        {
            spawnChildren = spawnLocation.GetComponentsInChildren<Transform>().Where(t => t != spawnLocation).ToArray();
            // .Where() ensures that the parent object is not included in the array
            //Debug.Log("Spawn location: " + child.name);
        }
        enemy = Resources.Load<GameObject>("Prefabs/Minigames Only/Evil Bun");

        ApplyAdaptiveDifficulty();
        StartCoroutine(WaitForContentAndStart());
    }

    private void Update()
    {
        if (isOngoing && !gameOver && towerHealth <= 0f)
        {
            ForceResults();
        }
    }

    private IEnumerator WaitForContentAndStart()
    {
        if (nametagScript == null)
        {
            Debug.LogError("Tower: NametagManager is missing.", this);
            isOngoing = false;
            yield break;
        }

        yield return new WaitUntil(
            () => nametagScript.IsLoaded || nametagScript.LoadFailed
        );

        if (nametagScript.LoadFailed)
        {
            isOngoing = false;

            Debug.LogError(
                "Tower Defense cannot start: content failed to load.",
                this
            );

            yield break;
        }

        StartCoroutine(Loop());
    }
    private void ApplyAdaptiveDifficulty()
    {
        int difficulty = Chapter1Difficulty.GetDifficulty();

        if (difficulty == 2) // Hard
        {
            enemyDamage = 25f; // 4 hits before death
            enemyDamageCritChance = 10f;
            enemyDamageCritMultiplier = 1.5f;
            allySlainDmg = 35f;
        }
        else if (difficulty == 1) // Normal
        {
            enemyDamage = 15f; // 7 hits before death
            enemyDamageCritChance = 7.5f;
            enemyDamageCritMultiplier = 1.5f;
            allySlainDmg = 25f;
        }
        else // Easy
        {
            enemyDamage = 10f; // 10 hits before death
            enemyDamageCritChance = 5f;
            enemyDamageCritMultiplier = 1.5f;
            allySlainDmg = 17f;
        }

        Debug.Log(
            "Tower Defense Difficulty (2 is hardest): " + difficulty +
            " | Enemy Damage: " + enemyDamage +
            " | Crit Chance: " + enemyDamageCritChance + "%" +
            " | Crit Multiplier: " + enemyDamageCritMultiplier
        );
    }
    public void updateHpBar()
    {
        towerHealth = Mathf.Clamp(towerHealth, 0f, towerMaxHealth);
        towerHpBar.value = towerHealth;
    }

    void spawnEnemy(Transform location) // Spawns an enemy at a location
    {
        GameObject spawnedEnemy = Instantiate(
            enemy,
            location.position,
            location.rotation
        );
        
        Enemy_TD enemyScript = spawnedEnemy.GetComponent<Enemy_TD>(); // Communicates to the Enemy script

        UnityEngine.AI.NavMeshAgent agent =
        spawnedEnemy.GetComponent<UnityEngine.AI.NavMeshAgent>();

        if (agent != null)
        {
            agent.speed = enemyWalkSpeed;
        }

        if (enemyScript != null)
        {
            enemyScript.enemyID = nextEnemyID;
            spawnedEnemy.name = "EvilBun_" + nextEnemyID; // IDs each spawned enemy to the enemyScript
            nextEnemyID++;
            enemiesAlive++;
            totalEnemiesSpawned++;
            //Debug.Log("Spawned Evil Bun ID: " + enemyScript.enemyID);
        }

        attachNametag(spawnedEnemy, enemyScript);
        if (enemyScript != null)
        {
            if (enemyScript.isEvil)
            {
                totalHostileSpawned++;
            }
        }
        updateHeader(nametagScript.GetWinningSO().question);
    }

    public void RecordHostileKill()
    {
        if (gameOver || !isOngoing)
        {
            return;
        }

        hostileEnemiesSlain++;

        Debug.Log("Hostile enemies slain: " +hostileEnemiesSlain + "/" + totalHostileSpawned);
    }

    void updateHeader(string txt)
    {
        td_question.text = "Category: \n" + txt;
    }

    public void enemyDefeated()
    {
        enemiesAlive--;
        enemiesSlain++;
        Debug.Log("Enemy defeated. Enemies remaining: " + enemiesAlive);
    }
    
    
    void attachNametag(GameObject enemy, Enemy_TD es)
    {
        TextMeshPro nametag = enemy.GetComponentInChildren<TextMeshPro>();

        if (nametag == null)
        {
            Debug.LogError(enemy.name + ": Nametag not loaded.");
            return;
        }

        if (nametagScript == null)
        {
            Debug.LogError("NametagManager not loaded.");
            return;
        }

        string[] correctAnswers = nametagScript.GetWinningSO().correctAnswers;
        string[] wrongAnswers = nametagScript.GetWinningSO().wrongAnswers;

        string[] answers = correctAnswers.Concat(wrongAnswers).ToArray();

        if (answers.Length == 0)
        {
            Debug.LogError("No answers available.");
            return;
        }

        // Clears the list if everything has already been selected
        if (selectedIndexes.Count >= answers.Length)
        {
            selectedIndexes.Clear();
            Debug.Log("All nametag answers used. Resetting selected indexes.");
        }

        int winningIndex;

        do
        {
            winningIndex = Random.Range(0, answers.Length);
        }
        while (selectedIndexes.Contains(winningIndex));

        // Assign nametag
        nametag.text = answers[winningIndex];

        // Determine if the answer came from wrongAnswers
        if (winningIndex >= correctAnswers.Length)
        {
            es.isEvil = true;

            Debug.Log($"{enemy.name} (aka {answers[winningIndex]}) is EVIL!");

            //nametag.text = "EVIL: " + nametag.text;
        }
        else
        {
            es.isEvil = false;

            Debug.Log($"{enemy.name} (aka {answers[winningIndex]}) is an ally");
        }

        // Remember this answer
        selectedIndexes.Add(winningIndex);

    }

    public float takeDamage() // this is for the Tower object, not an individual enemy!
    {
        
        float rollDice = UnityEngine.Random.Range(0f, 100f);
        if (rollDice <= (enemyDamageCritChance)) 
        {
            // TODO: Insert game logic feedback here for when enemy crit damage happens
            return (float)(enemyDamage * enemyDamageCritMultiplier);
        } else
        {
            return (float)enemyDamage;
        }

        updateHpBar();
    }

    public float takeDamage(float dmg) // overload. custom damage
    {
        float rollDice = UnityEngine.Random.Range(0f, 100f);
        if (rollDice <= (enemyDamageCritChance))
        {
            // TODO: Insert game logic feedback here for when enemy crit damage happens
            return dmg * enemyDamageCritMultiplier;
        }
        else
        {
            return dmg;
        }

        updateHpBar();
    }

    public void ForceResults() // can be used to force fail results
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;

        Debug.Log("Tower destroyed! Forcing results.");

        StopAllCoroutines();
        isOngoing = false;
        towerHealth = 0f;

        updateHpBar();
        updateHeader("Tower is destroyed...");

        buttonAttack.interactable = false;
        PlayerPrefs.SetInt("Chapter1_TowerDefenseWon", 2);
        PlayerPrefs.Save();
        DisplayFinalScore(true);      // must be in this
        resultScript.ShowRetry(); // order to avoid breaking the game
        
    }

    private void DisplayFinalScore(bool failed = false)
    {
        int maxScore = Mathf.Max(1, totalHostileSpawned);

        if (resultRecorder != null)
        {
            string outcome = "Win";

            if (failed)
            {
                outcome = "Fail";
            }

            resultRecorder.Record(
                hostileEnemiesSlain,
                maxScore,
                outcome
            );
        }

        resultScript.displayResults(hostileEnemiesSlain,maxScore,failed);

        float percentage =(float)hostileEnemiesSlain / maxScore * 100f;

        Debug.Log("Tower Defense Final Score: " + hostileEnemiesSlain + "/" + totalHostileSpawned + " (" + percentage.ToString("F1") + "%)");
    }
    IEnumerator Loop()
    {
        for (int wave = 1; wave <= numberOfWaves; wave++)
        {
            if (!isOngoing)
                yield break;
            if (towerHealth <= 0)
            {
                updateHeader("Tower is destroyed...");
                yield break;    
            }

            Debug.Log("Wave " + wave + " incoming!");

            enemiesAlive = 0;

            // Spawn ALL enemies in this wave at once
            for (int i = 0; i < enemiesPerWave; i++)
            {
                spawnEnemy(spawnChildren[i]);

                Debug.Log(
                    "Spawned enemy " + (i + 1) +
                    " of wave " + wave
                );
            }

            Debug.Log("Wave " + wave + " started with " + enemiesAlive + " enemies.");

            // WAIT until every enemy has been defeated
            yield return new WaitUntil(() => enemiesAlive <= 0);

            Debug.Log("Wave " + wave + " finished!");

            // Don't roll another set after the final wave
            if (wave < numberOfWaves)
            {
                nametagScript.rollNewSet();
                selectedIndexes.Clear();

                updateHeader(nametagScript.GetWinningSO().question);

                Debug.Log("New nametag set loaded!");

                // Optional delay before next wave
                yield return new WaitForSeconds(enemySpawnRate);
            }
        }

        if (towerHealth <= 0f)
        {
            ForceResults();
            yield break;
        }

        if (gameOver)
        {
            yield break;
        }

        gameOver = true;
        isOngoing = false;

        DisplayFinalScore();
        PlayerPrefs.SetInt("Chapter1_TowerDefenseWon", 1);
        PlayerPrefs.Save();
        Debug.Log("All waves completed! Victory!");
    }




}
    