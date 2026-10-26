using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
    [Header("Flashcards")] // Flashcards automatically gather every OBJ_CMD GameObject's inspector elements for the entire scene. as long as "collectIncorrectFlashcards" is enabled in the Unity hierarchy
    [Min(1)] public int flashcardChapter = 1;
    [SerializeField] private bool collectIncorrectFlashcards = false; 
    private readonly List<QA> incorrectFlashcardQuestions = new List<QA>();

    [SerializeField] private bool isChapter1Pretest = false;

    [Header("Universal Chapter Assessments (For Ch.2 and Beyond)")]
    [SerializeField] private bool isPretest = false;
    [SerializeField] private bool isRandomEncounter = false;

    [Header("Random Encounter (Uncheck ALL if Ch.2 and Beyond)")]
    [SerializeField] private bool isChapter1RandomEncounter = false;
    public bool isRandomEncounterConsole = false;
    private readonly List<OBJ_CMD> encounterConsoles = new List<OBJ_CMD>();
    private int nextConsoleIndex = 0;

    [Header("Check this if console is post-test or pre-test")]
    [SerializeField] private bool loadAllQuizzes = false; // if false, load the encountered/collected questions only.
    [SerializeField] private UnityEvent onQuizFinished;

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
        IsQuizOngoing = true;
        if (isPretest && loadAllQuizzes)
        {
            LoadChapterQuestions(); // force to load
        }
        Canvas player = GameObject.FindGameObjectWithTag("Canvas_Player").GetComponent<Canvas>();
        player.enabled = false;
        Canvas readypage = GameObject.Find("Terminal Ready").GetComponent<Canvas>();
        readypage.enabled = true;
        Button buttonReady = readypage.transform.Find("Button Ready").GetComponent<Button>();
        Button buttonClose = readypage.transform.Find("Button Close").GetComponent<Button>();

        TMP_Text count = readypage.transform.Find("Count Value").GetComponent<TMP_Text>();
        TMP_Text timed = readypage.transform.Find("Timed Value").GetComponent<TMP_Text>(); // timed question
        TMP_Text score = readypage.transform.Find("Saved Score").GetComponent<TMP_Text>();
        count.text = $"{QAs.Count}";
        timed.text = (isTimed) ? ("YES") : ("NO");
        score.text = $"Saved Score {this.score}/{QAs.Count}";

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

    private void RecordFlashcardFailure(QA qa)
    {
        if (!collectIncorrectFlashcards &&
            !isChapter1Pretest &&
            !isChapter1RandomEncounter &&
            !isPretest &&
            !isRandomEncounter)
        {
            return;
        }

        if (qa != null && !incorrectFlashcardQuestions.Contains(qa))
        {
            incorrectFlashcardQuestions.Add(qa);
        }
    }
    public void StartPostTest() // DESPITE ITS NAME, IT IS ALSO USED FOR PRETEST. (isPretest must be true and loadallquizzes is true too)
    {
        Debug.Log("StartPostTest called on: " + gameObject.name);

        if (QAs.Count <= 0)
        {
            Debug.LogWarning("Post-test: No questions available!");
            return;
        }

        Interact();
    }

    public void ChooseOneREConsole()
    {
        // Gather consoles in the scene's hierarchy order.
        if (encounterConsoles.Count == 0)
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();

            System.Array.Sort(
                roots,
                (a, b) => a.transform.GetSiblingIndex()
                    .CompareTo(b.transform.GetSiblingIndex())
            );

            foreach (GameObject root in roots)
            {
                OBJ_CMD[] consoles = root.GetComponentsInChildren<OBJ_CMD>(true);

                foreach (OBJ_CMD console in consoles)
                {
                    if (console.isRandomEncounterConsole)
                    {
                        encounterConsoles.Add(console);
                    }
                }
            }

            nextConsoleIndex = 0;
        }

        // Remove any consoles destroyed since the previous encounter.
        for (int i = encounterConsoles.Count - 1; i >= 0; i--)
        {
            if (encounterConsoles[i] == null)
            {
                encounterConsoles.RemoveAt(i);

                if (i < nextConsoleIndex)
                {
                    nextConsoleIndex--;
                }
            }
        }

        if (encounterConsoles.Count == 0)
        {
            Debug.LogWarning("No random encounter consoles found in this scene.");
            return;
        }

        // All consoles used: shuffle before starting another round.
        if (nextConsoleIndex >= encounterConsoles.Count)
        {
            ShuffleREConsoles();
            nextConsoleIndex = 0;
        }

        OBJ_CMD selectedConsole = encounterConsoles[nextConsoleIndex];
        nextConsoleIndex++;

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
        Debug.Log("LoadChapterQuestions called! Chapter: " + flashcardChapter);
        ExplorerFlashcardStore.RegisterLoadedQuizzes();
        QAs.Clear();

        OBJ_CMD[] quizzes = FindObjectsByType<OBJ_CMD>(FindObjectsInactive.Include,FindObjectsSortMode.None);

        foreach (OBJ_CMD quiz in quizzes)
        {
            Debug.Log(
                "Found OBJ_CMD: " + quiz.gameObject.name +
                " | Chapter: " + quiz.flashcardChapter +
                " | Questions: " + quiz.QAs.Count +
                " | Random Encounter: " + quiz.isRandomEncounterConsole
            );
            if (quiz == this || quiz.flashcardChapter != flashcardChapter) 
            {
                continue;
            }

            if (quiz.QAs == null || quiz.QAs.Count == 0)
            {
                continue;
            }

            if (loadAllQuizzes)
            {
                QAs.AddRange(quiz.QAs);
            }
            else
            {
                if (PlayerPrefs.GetInt(quiz.GetQuizSaveKey(), 0) == 1)
                {
                    QAs.AddRange(quiz.QAs);
                }
            }
        }

        Debug.Log("Post-test loaded " + QAs.Count +" questions. Load All: " + loadAllQuizzes);
    }
    private void SaveChapter1AssessmentScore()
    {
        if (QAs.Count <= 0)
        {
            return;
        }

        if (isPretest)
        {
            ChapterDifficulty.SavePretest(
                flashcardChapter,
                score,
                QAs.Count
            );
        }
        else if (isRandomEncounter)
        {
            ChapterDifficulty.SaveRandomEncounter(
                flashcardChapter,
                score,
                QAs.Count
            );
        }
    }
    public Task<bool> ReadyQuiz()
    {
        GameObject ready = GameObject.Find("Button Ready");
        GameObject close = GameObject.Find("Button Close");
        Button readyButton = ready.GetComponent<Button>();
        Button closeButton = close.GetComponent<Button>();

        quizRespond = new TaskCompletionSource<bool>();
        void OnClick(Button btn) {
            readyButton.onClick.RemoveAllListeners();
            closeButton.onClick.RemoveAllListeners();
            if (btn.GetComponentInChildren<TMP_Text>().text == "START") quizRespond.SetResult(true);
            else                                                        quizRespond.SetResult(false);
        }
        readyButton.onClick.AddListener(() => OnClick(GameObject.Find("Button Ready").GetComponent<Button>()));
        closeButton.onClick.AddListener(() => OnClick(GameObject.Find("Button Close").GetComponent<Button>()));
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
            ui.enabled = true;
            
            await Awaitable.WaitForSecondsAsync(1.0f);
            ui.enabled = false;
        }

        animator.SetBool("isAnswering", false);
        //Debug.Log(score >= (QAs.Count / 2));
        animator.SetBool("isPassed", this.score > (QAs.Count / 2));
        
        SaveQuizEncounter();
        ShowResult();

        //Debug.Log("Quiz Done");
        //if (ui != null) { ui.enabled = false; }
        //player.enabled = true;
    }

    public bool hasFailed = false;
    public void ShowResult() {
        ExplorerFlashcardStore.CollectFailures(flashcardChapter,incorrectFlashcardQuestions);

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
    
    public void HideCloseButton() // Only exists here to be called by the cutscene.
    {
        GameObject readyPage = GameObject.Find("Terminal Ready");

        if (readyPage == null)
            return;

        Transform closeButton = readyPage.transform.Find("Button Close");

        if (closeButton != null)
        closeButton.GetComponentInParent<Button>().interactable = false;
        closeButton.GetComponentInParent<Image>().enabled = false;
        closeButton.GetChild(0).GetComponent<TextMeshProUGUI>().enabled = false;

    }
    public void ShowCloseButton() //
    {
        GameObject readyPage = GameObject.Find("Terminal Ready");

        if (readyPage == null)
            return;

        Transform closeButton = readyPage.transform.Find("Button Close");

        if (closeButton != null)
            closeButton.GetComponentInParent<Button>().interactable = true;
        closeButton.GetComponentInParent<Image>().enabled = true;
        closeButton.GetComponentInChildren<TextMeshProUGUI>().enabled = true;

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
        
        GameObject cutscenesObject = GameObject.Find("Cutscenes");

        if (cutscenesObject != null)
        {
            CutsceneManager manager = cutscenesObject.GetComponent<CutsceneManager>();
            if (manager != null)
            {
                if (flashcardChapter == 1 &&
                (isChapter1Pretest || isPretest) &&
                !manager.cutscenesFinished)
                {
                    StartCutscene();
                    Debug.Log("Playing next cutscene..");
                }
            }
        }

        ShowCloseButton();
        if (closeTransform != null)
        {
            closeTransform.gameObject.SetActive(true);

            Button closeBut = closeTransform.GetComponent<Button>();

            if (closeBut != null)
            {
                closeBut.interactable = true;

                Image closeImage = closeBut.GetComponent<Image>();

                if (closeImage != null)
                {
                    closeImage.enabled = true;
                }
            }
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
        SaveChapter1AssessmentScore();
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

}
