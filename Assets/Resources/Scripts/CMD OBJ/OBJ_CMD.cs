using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Networking;

public class OBJ_CMD : NPC
{
    // Inspector elements
    public bool isTimed = false;
    /*
        When Timed:
            Choices = 7.5s
            Identify = 10s
            Fact = 5s
     */
    public enum QuizType
    {
        Regular,
        PreTest,
        RandomEncounter,
        PostTest
    }

    [Header("Quiz Ready Screen")]
    [SerializeField] private bool showCloseButton = true;
    [Header("Universal Quiz Configuration")]
    [SerializeField] private QuizType quizType = QuizType.Regular;

    [Header("Flashcards")]
    [Min(1)] public int flashcardChapter = 1; // Flashcards automatically gather every OBJ_CMD GameObject's inspector elements for the entire scene. 

    private readonly List<QA> incorrectFlashcardQuestions = new List<QA>();

    private readonly List<OBJ_CMD> encounterConsoles = new List<OBJ_CMD>();
    private int nextConsoleIndex = 0;



    [Header("Check this if console is post-test or pre-test")]
    [SerializeField] private UnityEvent onQuizFinished;

    [Header("Random Encounter Recording")]
    [SerializeField] private string encounterId;

    [System.Serializable]
    private class RandomEncounterScoreRecord
    {
        public string attempt_id;
        public string student_id;
        public int chapter_id;
        public string encounter_id;
        public int correct_answers;
        public int total_questions;
        public string completed_at_utc;
        public string quiz_type;
    }

    [System.Serializable]
    private class AssessmentHistory
    {
        public List<RandomEncounterScoreRecord> attempts =
            new List<RandomEncounterScoreRecord>();
    }

    private RandomEncounterScoreRecord currentEncounterRecord;
    private int completedQuestionCount;


    public static bool IsQuizOngoing { get; private set; }

    [SerializeReference]
    [SubclassSelector]
    public List<QA> QAs = new List<QA>();


    // Don't touch it
    public int score = 0;
 
    public new void Start()
    {
        isEntity = false;
        ExplorerFlashcardStore.RegisterQuestions(flashcardChapter, QAs);
    }

    public new void Update() {
        transform.Find("Square").GetComponent<SpriteRenderer>().transform.rotation = Quaternion.LookRotation(PlayerInteract.PLAYER_CAMERA.transform.forward);
    }

   TaskCompletionSource<bool> quizRespond = new TaskCompletionSource<bool>();

    public async override void Interact() {
        if (!QuestionsReady)
        {
            Debug.LogWarning("Questions are still loading.");
            return;
        }
        IsQuizOngoing = true;
        if (quizType == QuizType.PreTest ||
            quizType == QuizType.PostTest)
        {
            LoadChapterQuestions();
        }

        Canvas player = GameObject.FindGameObjectWithTag("Canvas_Player").GetComponent<Canvas>();
        player.enabled = false;
        Canvas readypage = GameObject.Find("Terminal Ready").GetComponent<Canvas>();
        readypage.enabled = true;
        Button buttonReady = readypage.transform.Find("Button Ready").GetComponent<Button>();

        Transform closeTransform = readypage.transform.Find("Button Close");

        if (closeTransform != null)
        {
            Button buttonClose = closeTransform.GetComponent<Button>();

            if (buttonClose != null)
            {
                buttonClose.gameObject.SetActive(showCloseButton);
            }
        }

        TMP_Text count = readypage.transform.Find("Count Value").GetComponent<TMP_Text>();
        TMP_Text timed = readypage.transform.Find("Timed Value").GetComponent<TMP_Text>(); // timed question
        TMP_Text score = readypage.transform.Find("Saved Score").GetComponent<TMP_Text>();
        count.text = $"{QAs.Count}";
        timed.text = (isTimed) ? ("YES") : ("NO");
        //score.text = $"Saved Score {this.score}/{QAs.Count}";
        score.text = $"High Score {GetHighScore()}/{QAs.Count}"; // high score system: GetHighScoreKey(), GetHighScore(), SaveHighScore()

        bool isReady = await ReadyQuiz(); // ask before proceeding
        if (isReady)
        {
            await CloseReady();
            await QuizTime(); // take the quiz
        }
        else {
            await CloseReady();
            IsQuizOngoing = false;
        }
    }

    private void BeginEncounterRecord()
    {
        currentEncounterRecord = null;
        completedQuestionCount = 0;

        if (quizType == QuizType.Regular)
        {
            return;
        }

        if (DBC.IS_GUEST_MODE)
        {
            Debug.Log("Guest encounter: no student score record prepared.");
            return;
        }

        string studentId =
            PlayerPrefs.GetString("AccountID", "").Trim();

        if (string.IsNullOrEmpty(studentId) ||
            string.IsNullOrWhiteSpace(encounterId))
        {
            Debug.LogWarning(
                "Cannot prepare encounter record: missing AccountID or Encounter ID.",
                this
            );
            return;
        }

        currentEncounterRecord = new RandomEncounterScoreRecord
        {
            attempt_id = System.Guid.NewGuid().ToString("N"),
            student_id = studentId,
            chapter_id = flashcardChapter,
            encounter_id = encounterId.Trim(),
            quiz_type = quizType.ToString(),
            total_questions = QAs.Count
        };
    }

    [System.Serializable]
    private class AssessmentUploadResponse
    {
        public bool success;
        public string message;
    }

    private IEnumerator UploadAssessment(string json)
    {
        string host = "localhost";

        if (!string.IsNullOrWhiteSpace(DBC.IP_ADDRESS))
        {
            host = DBC.IP_ADDRESS.Trim();
        }

        string url =
            "http://" + host +
            "/Explorer/php%20stuffs/save_assessment_result.php";

        using (UnityWebRequest request =
            new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(
                System.Text.Encoding.UTF8.GetBytes(json)
            );

            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 20;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    "Assessment upload failed. Local history is preserved." +
                    "\nError: " + request.error +
                    "\nResponse: " + request.downloadHandler.text,
                    this
                );

                yield break;
            }

            AssessmentUploadResponse response = null;

            try
            {
                response =
                    JsonUtility.FromJson<AssessmentUploadResponse>(
                        request.downloadHandler.text
                    );
            }
            catch (System.Exception)
            {
                Debug.LogWarning(
                    "Assessment server returned invalid JSON. " +
                    "Local history is preserved.",
                    this
                );
            }

            if (response == null || !response.success)
            {
                Debug.LogWarning(
                    "Assessment server did not confirm saving." +
                    "\nResponse: " + request.downloadHandler.text,
                    this
                );

                yield break;
            }

            Debug.Log(
                "ASSESSMENT DATABASE: " + response.message,
                this
            );
        }
    }

    private void FinishEncounterRecord()
    {
        if (currentEncounterRecord == null)
        {
            return;
        }

        if (currentEncounterRecord.total_questions <= 0 ||
            completedQuestionCount != currentEncounterRecord.total_questions)
        {
            Debug.LogWarning(
                "Encounter record not prepared: not all questions were completed.",
                this
            );

            currentEncounterRecord = null;
            return;
        }

        currentEncounterRecord.correct_answers = score;
        currentEncounterRecord.completed_at_utc =
            System.DateTime.UtcNow.ToString("o");

        string json = JsonUtility.ToJson(currentEncounterRecord, true);

        Debug.Log(
    "ASSESSMENT RECORD — PREVIEW ONLY:\n" + json,
    this
);

        // Later: pass this record to the database submission system.
        //currentEncounterRecord = null;
        if (SaveAssessmentLocally(currentEncounterRecord))
        {
            StartCoroutine(UploadAssessment(json));
            currentEncounterRecord = null;
        }
        else
        {
            Debug.LogWarning(
                "Assessment was not saved. Record remains in memory for now.",
                this
            );
        }
    }

    [ContextMenu("Debug Saved Assessment History")]
    private void DebugSavedAssessmentHistory()
    {
        string studentId =
            PlayerPrefs.GetString("AccountID", "").Trim();

        if (DBC.IS_GUEST_MODE || string.IsNullOrEmpty(studentId))
        {
            Debug.LogWarning("Log in as a student first.");
            return;
        }

        string key = "EXplorer_AssessmentHistory_v1_" +
            System.Uri.EscapeDataString(studentId);

        if (!PlayerPrefs.HasKey(key))
        {
            Debug.Log("No assessment history saved for this student.");
            return;
        }

        Debug.Log(
            "SAVED ASSESSMENT HISTORY:\n" +
            PlayerPrefs.GetString(key),
            this
        );
    }
    private string GetHighScoreKey()
    {
        return GetQuizSaveKey() + "_" + quizType + "_HighScore";
    }

    private int GetHighScore()
    {
        return PlayerPrefs.GetInt(GetHighScoreKey(), 0);
    }

    private void SaveHighScore()
    {
        int previousHighScore = GetHighScore();

        if (score > previousHighScore)
        {
            PlayerPrefs.SetInt(GetHighScoreKey(), score);
            PlayerPrefs.Save();

            Debug.Log(
                "OBJ_CMD: New high score! " +
                score + "/" + QAs.Count
            );
        }
    }

    private void RecordFlashcardFailure(QA qa)
    {
        if (quizType == QuizType.Regular)
        {
            return;
        }

        if (qa == null)
        {
            return;
        }

        if (!incorrectFlashcardQuestions.Contains(qa))
        {
            incorrectFlashcardQuestions.Add(qa);
        }
    }


    public void StartAssessment()
    {
        Debug.Log("Starting assessment: " + gameObject.name);

        if (!QuestionsReady)
        {
            Debug.LogWarning("Assessment questions are still loading.");
            return;
        }

        if (quizType != QuizType.PreTest &&
            quizType != QuizType.PostTest)
        {
            Debug.LogWarning(
                "OBJ_CMD: StartAssessment() requires PreTest or PostTest."
            );
            return;
        }

        LoadChapterQuestions();

        if (QAs.Count <= 0)
        {
            Debug.LogWarning(
                "OBJ_CMD: No questions available for " +
                gameObject.scene.name
            );
            return;
        }

        Interact();
    }
    /*
    public void StartPostTest() // DESPITE ITS NAME, IT IS ALSO USED FOR PRETEST. (isPretest must be true and loadallquizzes is true too)
    {
        Debug.Log("StartPostTest called on: " + gameObject.name);

        if (QAs.Count <= 0)
        {
            Debug.LogWarning("Post-test: No questions available!");
            return;
        }

        Interact();
    }*/

    public void ChooseOneREConsole()
    {
        if (IsQuizOngoing)
        {
            return;
        }

        // Read completed encounters for the current student.
        HashSet<string> completedEncounters = new HashSet<string>();

        string studentId =
            PlayerPrefs.GetString("AccountID", "").Trim();

        bool studentMode =
            !DBC.IS_GUEST_MODE &&
            !string.IsNullOrEmpty(studentId);

        if (studentMode)
        {
            string historyKey =
                "EXplorer_AssessmentHistory_v1_" +
                System.Uri.EscapeDataString(studentId);

            if (PlayerPrefs.HasKey(historyKey))
            {
                try
                {
                    AssessmentHistory history =
                        JsonUtility.FromJson<AssessmentHistory>(
                            PlayerPrefs.GetString(historyKey)
                        );

                    if (history == null || history.attempts == null)
                    {
                        Debug.LogError(
                            "RE: Saved assessment history is invalid.",
                            this
                        );
                        return;
                    }

                    foreach (RandomEncounterScoreRecord attempt
                        in history.attempts)
                    {
                        if (attempt == null)
                        {
                            continue;
                        }

                        if (attempt.student_id == studentId &&
                            attempt.quiz_type ==
                                QuizType.RandomEncounter.ToString() &&
                            attempt.total_questions > 0 &&
                            !string.IsNullOrWhiteSpace(
                                attempt.encounter_id))
                        {
                            completedEncounters.Add(
                                attempt.chapter_id + ":" +
                                attempt.encounter_id.Trim()
                            );
                        }
                    }
                }
                catch (System.Exception error)
                {
                    Debug.LogError(
                        "RE: Cannot read assessment history: " +
                        error.Message,
                        this
                    );
                    return;
                }
            }
        }

        List<OBJ_CMD> unfinished = new List<OBJ_CMD>();
        List<OBJ_CMD> allConsoles = new List<OBJ_CMD>();

        GameObject[] roots =
            gameObject.scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            OBJ_CMD[] consoles =
                root.GetComponentsInChildren<OBJ_CMD>(true);

            foreach (OBJ_CMD console in consoles)
            {
                if (console.quizType != QuizType.RandomEncounter)
                {
                    continue;
                }

                allConsoles.Add(console);

                bool completed = false;

                if (studentMode)
                {
                    string completionId =
                        console.flashcardChapter + ":" +
                        (console.encounterId ?? "").Trim();

                    completed =
                        completedEncounters.Contains(completionId);
                }
                else
                {
                    completed = PlayerPrefs.GetInt(
                        console.GetQuizSaveKey(), 0
                    ) == 1;
                }

                if (!completed)
                {
                    unfinished.Add(console);
                }
            }
        }

        // Prioritize unfinished consoles.
        List<OBJ_CMD> candidates = allConsoles;

        if (unfinished.Count > 0)
        {
            candidates = unfinished;
        }

        List<OBJ_CMD> readyConsoles = new List<OBJ_CMD>();

        foreach (OBJ_CMD console in candidates)
        {
            if (console.QuestionsReady &&
                console.QAs != null &&
                console.QAs.Count > 0)
            {
                readyConsoles.Add(console);
            }
        }

        if (readyConsoles.Count == 0)
        {
            Debug.LogWarning(
                "RE: No eligible consoles have loaded questions.",
                this
            );
            return;
        }

        OBJ_CMD selectedConsole = readyConsoles[
            Random.Range(0, readyConsoles.Count)
        ];

        Debug.Log(
            "RE: Selected " + selectedConsole.name +
            " | Encounter ID: " + selectedConsole.encounterId +
            " | Unfinished consoles: " + unfinished.Count,
            selectedConsole
        );

        selectedConsole.Interact();
    }

    private void ShuffleREConsoles()
    {
        for (int i = encounterConsoles.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            OBJ_CMD temporary = encounterConsoles[i];
            encounterConsoles[i] = encounterConsoles[randomIndex];
            encounterConsoles[randomIndex] = temporary;
        }
    }

    private void LoadChapterQuestions()
    {
        List<QA> collectedQuestions = new List<QA>();

        GameObject[] roots = gameObject.scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            OBJ_CMD[] consoles =
                root.GetComponentsInChildren<OBJ_CMD>(true);

            foreach (OBJ_CMD console in consoles)
            {
                if (console == this)
                {
                    continue;
                }

                if (console.quizType != QuizType.RandomEncounter)
                {
                    continue;
                }

                if (console.QAs == null)
                {
                    continue;
                }

                foreach (QA qa in console.QAs)
                {
                    if (qa == null)
                    {
                        continue;
                    }

                    if (!collectedQuestions.Contains(qa))
                    {
                        collectedQuestions.Add(qa);
                    }
                }
            }
        }

        QAs.Clear();
        QAs.AddRange(collectedQuestions);

        ExplorerFlashcardStore.RegisterQuestions(
            flashcardChapter,
            QAs
        );

        Debug.Log(
            "OBJ_CMD: " + gameObject.scene.name +
            " | Quiz Type: " + quizType +
            " | Loaded Questions: " + QAs.Count
        );
    }


    private void SaveChapterAssessmentScore()
    {
        if (QAs.Count <= 0)
        {
            return;
        }

        if (quizType == QuizType.PreTest)
        {
            ChapterDifficulty.SavePretest(
                flashcardChapter,
                score,
                QAs.Count
            );
        }
        else if (quizType == QuizType.RandomEncounter)
        {
            ChapterDifficulty.SaveRandomEncounter(
                flashcardChapter,
                score,
                QAs.Count
            );
        }
        else if (quizType == QuizType.PostTest)
        {
            string key = "Chapter" + flashcardChapter + "_PostTest_";

            PlayerPrefs.SetInt(key + "Correct", score);
            PlayerPrefs.SetInt(key + "Total", QAs.Count);
            PlayerPrefs.Save();
        }
    }


    public Task<bool> ReadyQuiz()
    {
        GameObject ready = GameObject.Find("Button Ready");

        if (ready == null)
        {
            Debug.LogError("OBJ_CMD: Button Ready not found.");
            return Task.FromResult(false);
        }

        Button readyButton = ready.GetComponent<Button>();

        Button closeButton = null;

        if (showCloseButton)
        {
            GameObject close = GameObject.Find("Button Close");

            if (close != null)
            {
                closeButton = close.GetComponent<Button>();
            }
        }

        quizRespond = new TaskCompletionSource<bool>();

        void OnClick(bool isReady)
        {
            readyButton.onClick.RemoveAllListeners();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
            }

            quizRespond.TrySetResult(isReady);
        }

        readyButton.onClick.AddListener(() => OnClick(true));

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() => OnClick(false));
        }

        return quizRespond.Task;
    }


    public Task CloseReady()
    {
        Canvas readypage = GameObject.Find("Terminal Ready").GetComponent<Canvas>();
        readypage.enabled = false;
        Canvas player = GameObject.FindGameObjectWithTag("Canvas_Player").GetComponent<Canvas>();
        player.enabled = true;
        return Task.CompletedTask;
    }

    /**
     * Don't get lost, here's this and that
     *  SetMultipleQuestion() : ChooseAnswer()
     *  SetIdentifyQuestion() : IdentifyAnswer()
     *  SetTriviaQuestion()   : FactAnswer()
     * 
     */

    // Starting the quiz
    public async Task QuizTime()
    {
        IsQuizOngoing = true;
        score = 0;
        BeginEncounterRecord();

        incorrectFlashcardQuestions.Clear();
        ExplorerFlashcardStore.RegisterQuestions(flashcardChapter, QAs);
        animator.SetBool("isAnswering", true);

        Canvas ui = null;
        Canvas player = GameObject.FindGameObjectWithTag("Canvas_Player").GetComponent<Canvas>();
        player.enabled = false;

        foreach (QA qa in QAs) {
            Debug.Log(
                "Processing question: " +
                (qa == null ? "NULL" : qa.QUESTION) +
                " | Type: " +
                (qa == null ? "NULL" : qa.GetType().Name)
            );
            //Debug.Log(qa.QUESTION);
            //question.text = qa.QUESTION;

            if (qa.GetType() == typeof(MultipleChoices)) {
                ui = GameObject.Find("Terminal Multiple").GetComponent<Canvas>();
                await SetMultipleQuestion(qa);
            } else if (qa.GetType() == typeof(Trivia)) {
                ui = GameObject.Find("Terminal Trivia").GetComponent<Canvas>();
                await SetTriviaQuestion(qa);
            } else if (qa.GetType() == typeof(Identify)) {
                ui = GameObject.Find("Terminal Identify").GetComponent<Canvas>();
                await SetIdentifyQuestion(qa);
            } else {
                Debug.Log("No questions asked!");
                ui = null;
                break;
            }
            completedQuestionCount++;

            ui.enabled = true;

            await Awaitable.WaitForSecondsAsync(1.0f);
            ui.enabled = false;
        }

        animator.SetBool("isAnswering", false);
        //Debug.Log(score >= (QAs.Count / 2));
        animator.SetBool("isPassed", this.score > (QAs.Count / 2));

        FinishEncounterRecord();

        SaveQuizEncounter();
        SaveHighScore();
        ShowResult();
        //Debug.Log("Quiz Done");
        //if (ui != null) { ui.enabled = false; }
        //player.enabled = true;
    }

    public bool hasFailed = false;
    public void ShowResult() {

        ExplorerFlashcardStore.RecordAssessmentFailures(
            flashcardChapter,
            quizType,
            incorrectFlashcardQuestions
        );


        incorrectFlashcardQuestions.Clear();
        Canvas res = GameObject.Find("Terminal Results").GetComponent<Canvas>();
        res.enabled = true;

        TMP_InputField grade = res.transform.Find("Result Score").GetComponent<TMP_InputField>();
        grade.text = $"{score}/{QAs.Count}";

        TMP_InputField state = res.transform.Find("Result State").GetComponent<TMP_InputField>();
        if (this.score > (QAs.Count / 2)) {
            state.text = "PASSED";
            hasFailed = false;
        } else
        {
            state.text = "FAILED";
            hasFailed = true;
        }

            Button done = res.transform.Find("Button Done").GetComponent<Button>();
        done.onClick.RemoveAllListeners();
        done.onClick.AddListener(DoneResult);


        // TODO: Adjust future difficulties of minigames according to scores
    }

    public void HideCloseButton()
    {
        GameObject readyPage = GameObject.Find("Terminal Ready");

        if (readyPage == null)
        {
            Debug.LogWarning("Terminal Ready was not found.", this);
            return;
        }

        Transform closeTransform =
            readyPage.transform.Find("Button Close");

        if (closeTransform == null)
        {
            Debug.LogWarning(
                "Button Close was not found inside Terminal Ready.",
                this
            );
            return;
        }

        Button button = closeTransform.GetComponent<Button>();

        if (button != null)
        {
            button.interactable = false;
        }

        Graphic[] graphics =
            closeTransform.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            graphic.enabled = false;
        }
    }

    public void ShowCloseButton()
    {
        if (!showCloseButton)
        {
            return;
        }

        GameObject readyPage = GameObject.Find("Terminal Ready");

        if (readyPage == null)
        {
            return;
        }

        Transform closeButton = readyPage.transform.Find("Button Close");

        if (closeButton == null)
        {
            return;
        }

        Button button = closeButton.GetComponent<Button>();

        if (button != null)
        {
            button.interactable = true;
        }

        Image image = closeButton.GetComponent<Image>();

        if (image != null)
        {
            image.enabled = true;
        }

        TextMeshProUGUI text =
            closeButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (text != null)
        {
            text.enabled = true;
        }
    }


    private string GetQuizSaveKey()
    {
        string path = "";
        Transform current = transform;
        while (current != null)
        {
            path = "/" + current.GetSiblingIndex() + "_" +current.name + path;
            current = current.parent;
        }
        return "Chapter" + flashcardChapter + "_EncounteredQuiz_" + path;
    }

    private void SaveQuizEncounter()
    {
        PlayerPrefs.SetInt(GetQuizSaveKey(), 1);
        PlayerPrefs.Save();
    }
    public void DoneResult() {
        IsQuizOngoing = false;
        Canvas res = GameObject.Find("Terminal Results").GetComponent<Canvas>();
        Canvas player = GameObject.FindGameObjectWithTag("Canvas_Player").GetComponent<Canvas>();

        GameObject encounterArea = GameObject.Find("EncounterArea");
        GameObject chapterManager = GameObject.Find("Chapter1Manager");

        Transform closeTransform = GameObject.Find("Terminal Ready").transform.Find("Button Close");
        Transform playerUI = player.transform;
        Transform joystick = playerUI.Find("Joystick BG");
        Transform interact = playerUI.Find("Button Interact");
        res.enabled = false;
        player.enabled = true;

        //Debug.Log("Done result");

        if (showCloseButton && closeTransform != null)
        {
            closeTransform.gameObject.SetActive(true);
            ShowCloseButton();
        }
        if (encounterArea != null)
        {
            AudioSource audio = encounterArea.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.Stop();
            }
        }
        /*if (chapterManager != null)
        {
            AudioSource audio = chapterManager.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.Play();
            }
        }*/
        if (joystick != null)
        {
            joystick.gameObject.SetActive(true);
        }
        if (interact != null)
        {
            interact.gameObject.SetActive(true);
        }
        SaveChapterAssessmentScore();
        onQuizFinished?.Invoke();
    }

    // IN CHRONOLOGICAL ORDER (sorry, I don't have time to change these method names anymore.)
    // 1. StartCutscene() is called after the pre-test
    // 2. PlayNextCutscene() is called after this cutscene: Cutscene_AfterPretest 
    // 3. PlayDeterminePostTestCutscene is called after the chapter 1 post-test results.
    public void StartCutscene() // FOR AFTER PRE-TEST. 
    {
        GameObject cutscene2 = GameObject.Find("Cutscene_AfterPretest");
        Cutscene cutsceneScript = cutscene2.GetComponent<Cutscene>();

        Cutscene.CutsceneAction action = cutsceneScript.GetAction(0);
        if (hasFailed)
        {
            action.dialogue = "* You fumbled and struggled to get the words out! You bit your tongue in the process. How pathetic.";
            Debug.Log("hasFailed: " + action.dialogue);
        } 

        if (!GameObject.Find("Cutscenes").GetComponent<CutsceneManager>().cutscenesFinished)
        {
            cutsceneScript.PlayCutscene();
            cutscene2.transform.Find("AftPreMusic").GetComponent<AudioSource>().Play();
            Debug.Log("Cutscene called");
        }
        
    }

    public void Ch2BadGoodCutscenePlay()
    {
        GameObject csBad = null;
        GameObject csGood = null;

        // Find cutscenes, including inactive GameObjects.
        Transform[] objects = FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (Transform obj in objects)
        {
            if (obj.name == "Cutscene_BadAfterPreTest")
            {
                csBad = obj.gameObject;
            }
            else if (obj.name == "Cutscene_GoodAfterPreTest")
            {
                csGood = obj.gameObject;
            }
        }

        Debug.Log("Bad cutscene found: " + (csBad != null));
        Debug.Log("Good cutscene found: " + (csGood != null));

        // Select the cutscene based on the quiz result.
        GameObject selectedCutscene;

        if (hasFailed)
        {
            selectedCutscene = csBad;
        }
        else
        {
            selectedCutscene = csGood;
        }

        if (selectedCutscene == null)
        {
            Debug.LogError(
                "Chapter 2 cutscene not found! HasFailed: " + hasFailed
            );
            return;
        }

        Cutscene cutscene = selectedCutscene.GetComponent<Cutscene>();

        if (cutscene == null)
        {
            Debug.LogError(
                "Cutscene component missing on: " + selectedCutscene.name
            );
            return;
        }

        // Inactive GameObjects must be activated before their cutscene runs.
        if (!selectedCutscene.activeInHierarchy)
        {
            selectedCutscene.SetActive(true);
        }

        cutscene.PlayCutscene();
    }

    public void PlayDeterminePostTestCutscene() // for chapter 1 posttest
    {
        string cutsceneName;

        if (hasFailed)
        {
            cutsceneName = "Cutscene_Bad_PostTestFinished";
        }
        else
        {
            cutsceneName = "Cutscene_Good_PostTestFinished";
        }

        GameObject cutsceneObject = GameObject.Find(cutsceneName);

        if (cutsceneObject == null)
        {
            Debug.LogError("Cutscene not found: " + cutsceneName);
            return;
        }

        Cutscene cutscene = cutsceneObject.GetComponent<Cutscene>();

        if (cutscene != null)
        {
            cutscene.PlayCutscene();
        }
    }

    public void PlayNextCutscene() // to be called by Cutscene_AfterPretest after finishing
    {
        GameObject cutsceneBad = GameObject.Find("Cutscene_BadAfterCrying");
        GameObject cutsceneGood = GameObject.Find("Cutscene_GoodAfterCrying");
        if (hasFailed)
        {
            Debug.Log("Bad Cutscene");
            cutsceneBad.GetComponent<Cutscene>().PlayCutscene();
        } else
        {
            Debug.Log("Good Cutscene");
            cutsceneGood.GetComponent<Cutscene>().PlayCutscene();
        }
    }

    

    // forgot what it is but it looks important for later... unused and unaffected
    //IEnumerator RevealAnswer() {
    //    yield return new WaitForSeconds(5.0f);
    //    Debug.Log("Finised");
    //}

    /* Initialize each question */
    public async Task SetMultipleQuestion(QA qa) {
        Canvas ui = GameObject.Find("Terminal Multiple").GetComponent<Canvas>();
        ui.enabled = true;

        TMP_InputField question = ui.transform.Find("Question").GetComponent<TMP_InputField>();
        Button b1 = ui.transform.Find("Choice A").GetComponent<Button>();
        Button b2 = ui.transform.Find("Choice B").GetComponent<Button>();
        Button b3 = ui.transform.Find("Choice C").GetComponent<Button>();
        Button b4 = ui.transform.Find("Choice D").GetComponent<Button>();
        question.text = qa.QUESTION;

        Button[] buttons = new Button[4];
        buttons[0] = b1;
        buttons[1] = b2;
        buttons[2] = b3;
        buttons[3] = b4;

        b1.interactable = true;
        b2.interactable = true;
        b3.interactable = true;
        b4.interactable = true;
        b1.GetComponent<Image>().color = Color.white;
        b2.GetComponent<Image>().color = Color.white;
        b3.GetComponent<Image>().color = Color.white;
        b4.GetComponent<Image>().color = Color.white;
        b1.transform.Find("Answer 1").GetComponent<TMP_Text>().text = ((MultipleChoices)qa).choices[0];
        b2.transform.Find("Answer 2").GetComponent<TMP_Text>().text = ((MultipleChoices)qa).choices[1];
        b3.transform.Find("Answer 3").GetComponent<TMP_Text>().text = ((MultipleChoices)qa).choices[2];
        b4.transform.Find("Answer 4").GetComponent<TMP_Text>().text = ((MultipleChoices)qa).choices[3];

        string ans = await ChooseAnswer(((MultipleChoices)qa).choices);
        if (qa.Answer(ans)) 
        { 
            score++; 
        } else
        {
            RecordFlashcardFailure(qa);
        }

        foreach (Button b in buttons) {
            if      (b.GetComponentInChildren<TMP_Text>().text == qa.ANSWER) b.GetComponent<Image>().color = Color.green;
            else if (b.GetComponentInChildren<TMP_Text>().text == ans)       b.GetComponent<Image>().color = Color.red;
            else                                                             b.GetComponent<Image>().color = Color.white;
        }
    }

    public async Task SetIdentifyQuestion(QA qa) {
        Canvas ui = GameObject.Find("Terminal Identify").GetComponent<Canvas>();
        //Debug.Log(ui.name);
        ui.enabled = true;

        TMP_InputField question = ui.transform.Find("Question").GetComponent<TMP_InputField>();
        TMP_InputField identify = ui.transform.Find("Identify Answer").GetComponent<TMP_InputField>();
        Button submit = ui.transform.Find("Submit Answer").GetComponent<Button>();
        question.text = qa.QUESTION;
        identify.text = string.Empty;
        submit.GetComponent<Image>().color = Color.white;

        string ans = await IdentifyAnswer();

        if (((Identify) qa).AnswerIdentity(ans)) {
            score++;
            submit.GetComponent<Image>().color = Color.green;
        } else {
            RecordFlashcardFailure(qa);
            submit.GetComponent<Image>().color = Color.red;
        }
    }
    public async Task SetTriviaQuestion(QA qa) {
        Canvas ui = GameObject.Find("Terminal Trivia").GetComponent<Canvas>();
        ui.enabled = true;

        TMP_InputField question = ui.transform.Find("Question").GetComponent<TMP_InputField>();
        question.text = qa.QUESTION;
        Button bt = ui.transform.Find("True Answer").GetComponent<Button>();
        Button bf = ui.transform.Find("False Answer").GetComponent<Button>();
        bt.GetComponent<Image>().color = Color.white;
        bf.GetComponent<Image>().color = Color.white;

        string ans = await FactAnswer();
        if (((Trivia)qa).AnswerFact(ans))
        {
            score++;
            if      (qa.savedAnswer == "true")  bt.GetComponent<Image>().color = Color.green;
            else if (qa.savedAnswer == "false") bf.GetComponent<Image>().color = Color.green;
        } else {
            RecordFlashcardFailure(qa);
            if (qa.savedAnswer == "true") {
                bt.GetComponent<Image>().color = Color.red;
                bf.GetComponent<Image>().color = Color.green;
            } else {
                bt.GetComponent<Image>().color = Color.green;
                bf.GetComponent<Image>().color = Color.red;
            }
        }             
    }

    /* Getting the answer */
    TaskCompletionSource<string> buttonClickTask;

    public Task<string> ChooseAnswer(string[] choices) {
        buttonClickTask = new TaskCompletionSource<string>();

        void OnClick(Button btn) {
            string text = btn.GetComponentInChildren<TMP_Text>().text;
            GameObject.Find("Choice A").GetComponent<Button>().onClick.RemoveAllListeners();
            GameObject.Find("Choice B").GetComponent<Button>().onClick.RemoveAllListeners();
            GameObject.Find("Choice C").GetComponent<Button>().onClick.RemoveAllListeners();
            GameObject.Find("Choice D").GetComponent<Button>().onClick.RemoveAllListeners();
            buttonClickTask.SetResult(text);
        }

        GameObject.Find("Choice A").GetComponent<Button>().onClick.AddListener(() => OnClick(GameObject.Find("Choice A").GetComponent<Button>()));
        GameObject.Find("Choice B").GetComponent<Button>().onClick.AddListener(() => OnClick(GameObject.Find("Choice B").GetComponent<Button>()));
        GameObject.Find("Choice C").GetComponent<Button>().onClick.AddListener(() => OnClick(GameObject.Find("Choice C").GetComponent<Button>()));
        GameObject.Find("Choice D").GetComponent<Button>().onClick.AddListener(() => OnClick(GameObject.Find("Choice D").GetComponent<Button>()));
        return buttonClickTask.Task;
    }

    public Task<string> IdentifyAnswer() {
        buttonClickTask = new TaskCompletionSource<string>();

        void OnClick(Button btn) {
            string text = GameObject.Find("Identify Answer").GetComponent<TMP_InputField>().text;
            GameObject.Find("Submit Answer").GetComponent<Button>().onClick.RemoveAllListeners();
            buttonClickTask.SetResult(text);
        }
        GameObject.Find("Submit Answer").GetComponent<Button>().onClick.AddListener(() => OnClick(GameObject.Find("Submit Answer").GetComponent<Button>()));
        return buttonClickTask.Task;
    }

    public Task<string> FactAnswer()
    {
        buttonClickTask = new TaskCompletionSource<string>();

        void OnClick(Button btn)
        {
            GameObject.Find("True Answer").GetComponent<Button>().onClick.RemoveAllListeners();
            GameObject.Find("False Answer").GetComponent<Button>().onClick.RemoveAllListeners();
            buttonClickTask.SetResult(btn.GetComponentInChildren<TMP_Text>().text);
        }
        GameObject.Find("True Answer").GetComponent<Button>().onClick.AddListener(() => OnClick(GameObject.Find("True Answer").GetComponent<Button>()));
        GameObject.Find("False Answer").GetComponent<Button>().onClick.AddListener(() => OnClick(GameObject.Find("False Answer").GetComponent<Button>()));
        return buttonClickTask.Task;
    }

    private List<OBJ_CMD> GetAllRandomEncounterConsoles()
    {
        List<OBJ_CMD> consoles = new List<OBJ_CMD>();

        GameObject[] roots = gameObject.scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            OBJ_CMD[] allCmds =
                root.GetComponentsInChildren<OBJ_CMD>(true);

            foreach (OBJ_CMD cmd in allCmds)
            {
                if (cmd.quizType == QuizType.RandomEncounter)
                {
                    consoles.Add(cmd);
                }
            }
        }

        return consoles;
    }
    //save assessment attempts locally
    private bool SaveAssessmentLocally(
    RandomEncounterScoreRecord record)
    {
        if (record == null ||
            string.IsNullOrWhiteSpace(record.student_id))
        {
            return false;
        }

        string key = "EXplorer_AssessmentHistory_v1_" +
            System.Uri.EscapeDataString(record.student_id);

        AssessmentHistory history;

        try
        {
            if (PlayerPrefs.HasKey(key))
            {
                history = JsonUtility.FromJson<AssessmentHistory>(
                    PlayerPrefs.GetString(key)
                );

                if (history == null || history.attempts == null)
                {
                    Debug.LogError(
                        "Cannot read assessment history. Existing save preserved."
                    );
                    return false;
                }
            }
            else
            {
                history = new AssessmentHistory();
            }

            foreach (RandomEncounterScoreRecord attempt in history.attempts)
            {
                if (attempt != null &&
                    attempt.attempt_id == record.attempt_id)
                {
                    return true;
                }
            }

            history.attempts.Add(record);

            PlayerPrefs.SetString(
                key,
                JsonUtility.ToJson(history)
            );

            PlayerPrefs.Save();

            Debug.Log(
                "Assessment saved locally: " +
                record.encounter_id + " | " +
                record.correct_answers + "/" +
                record.total_questions + " | " +
                "Saved attempts: " + history.attempts.Count,
                this
            );

            return true;
        }
        catch (System.Exception error)
        {
            Debug.LogError(
                "Could not save assessment history: " + error.Message,
                this
            );

            return false;
        }
    }

    //PHP AND DATABASE FETCHING STUFF FOR DISTRIBUTING RANDOM ENCOUNTERS
    public QuizType ConfiguredQuizType
    {
        get { return quizType; }
    }

    public bool QuestionsReady { get; set; } = true;
}
