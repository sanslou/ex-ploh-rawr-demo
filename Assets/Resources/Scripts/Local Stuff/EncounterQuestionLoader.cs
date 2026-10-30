using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class EncounterQuestionLoader : MonoBehaviour
{
    [Header("PHP Endpoint")]
    [SerializeField]
    private string endpointUrl =
        "http://localhost/explorer/get_encounter_questions.php";

    [Header("Inclusive database ID range")]
    [Min(1)]
    [SerializeField] private int firstQuestionId = 1;

    [Min(1)]
    [SerializeField] private int lastQuestionId = 20;

    [Header("RandomEncounter consoles, in distribution order")]
    [SerializeField]
    private List<OBJ_CMD> targetConsoles = new List<OBJ_CMD>();

    [Header("Start the assessment here if it should run after loading")]
    [SerializeField] private UnityEvent onQuestionsLoaded;

    public bool IsLoaded { get; private set; }

    private bool isLoading;

    private readonly List<OBJ_CMD> gatedConsoles =
        new List<OBJ_CMD>();

    [Serializable]
    private class QuestionRow
    {
        public int id;
        public string question;
        public string[] choices;
        public string answer;
    }

    [Serializable]
    private class QuestionResponse
    {
        public bool success;
        public string message;
        public QuestionRow[] questions;
    }

    private void Awake()
    {
        // Block this scene's assessments and encounter consoles
        // until their database content is ready.
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            OBJ_CMD[] consoles =
                root.GetComponentsInChildren<OBJ_CMD>(true);

            foreach (OBJ_CMD console in consoles)
            {
                if (console.ConfiguredQuizType == OBJ_CMD.QuizType.Regular)
                {
                    continue;
                }

                console.QuestionsReady = false;
                gatedConsoles.Add(console);
            }
        }
    }

    private void Start()
    {
        LoadQuestions();
    }

    // Can also be called by a Retry button after a failed request.
    public void LoadQuestions()
    {
        if (isLoading || IsLoaded)
        {
            return;
        }

        StartCoroutine(FetchAndDistribute());
    }

    private IEnumerator FetchAndDistribute()
    {
        if (!ValidateConfiguration())
        {
            yield break;
        }

        isLoading = true;

        string host = "localhost";

        if (!string.IsNullOrWhiteSpace(DBC.IP_ADDRESS))
        {
            host = DBC.IP_ADDRESS.Trim();
        }

        string url =
            "http://" + host +
            "/Explorer/php%20stuffs/get_encounter_questions.php" +
            "?first=" + firstQuestionId +
            "&last=" + lastQuestionId;

        Debug.Log("EncounterQuestionLoader: Requesting " + url);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 20;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "EncounterQuestionLoader: Request failed. " +
                    request.error
                );

                isLoading = false;
                yield break;
            }

            Debug.Log(
                "RAW QUESTION RESPONSE:\n" +
                request.downloadHandler.text
            );

            QuestionResponse response;

            try
            {
                response = JsonUtility.FromJson<QuestionResponse>(
                    request.downloadHandler.text
                );
            }
            catch (Exception error)
            {
                Debug.LogError(
                    "EncounterQuestionLoader: Invalid JSON. " +
                    error.Message
                );

                isLoading = false;
                yield break;
            }

            if (response == null || !response.success)
            {
                Debug.LogError(
                    "EncounterQuestionLoader: Server could not load questions."
                );

                isLoading = false;
                yield break;
            }

            if (response.questions == null ||
                response.questions.Length < targetConsoles.Count)
            {
                Debug.LogError(
                    "EncounterQuestionLoader: Not enough questions. " +
                    "Each console needs at least one question."
                );

                isLoading = false;
                yield break;
            }

            // Validate and construct all questions before replacing anything.
            List<QA> loadedQuestions = BuildMixedQuestions(response.questions);

            if (loadedQuestions == null)
            {
                isLoading = false;
                yield break;
            }

            int questionsPerConsole =
                loadedQuestions.Count / targetConsoles.Count;

            int remainder =
                loadedQuestions.Count % targetConsoles.Count;

            int questionIndex = 0;

            for (int i = 0; i < targetConsoles.Count; i++)
            {
                OBJ_CMD console = targetConsoles[i];

                int count = questionsPerConsole;

                if (i < remainder)
                {
                    count++;
                }

                if (console.QAs == null)
                {
                    console.QAs = new List<QA>();
                }

                console.QAs.Clear();

                for (int j = 0; j < count; j++)
                {
                    console.QAs.Add(loadedQuestions[questionIndex]);
                    questionIndex++;
                }

                ExplorerFlashcardStore.RegisterQuestions(
                    console.flashcardChapter,
                    console.QAs
                );

                Debug.Log(
                    "EncounterQuestionLoader: Assigned " + count +
                    " questions to " + console.name
                );
            }

            foreach (OBJ_CMD console in gatedConsoles)
            {
                if (console != null)
                {
                    console.QuestionsReady = true;
                }
            }

            IsLoaded = true;
            isLoading = false;

            Debug.Log(
                "EncounterQuestionLoader: Loaded " +
                loadedQuestions.Count + " questions."
            );

            onQuestionsLoaded?.Invoke();
        }
    }

    private bool ValidateConfiguration()
    {
        if (firstQuestionId < 1 ||
            lastQuestionId < firstQuestionId ||
            targetConsoles.Count == 0)
        {
            Debug.LogError(
                "EncounterQuestionLoader: Check URL, range, and consoles."
            );

            return false;
        }

        HashSet<OBJ_CMD> uniqueConsoles = new HashSet<OBJ_CMD>();

        foreach (OBJ_CMD console in targetConsoles)
        {
            if (console == null ||
                console.gameObject.scene != gameObject.scene ||
                console.ConfiguredQuizType !=
                    OBJ_CMD.QuizType.RandomEncounter ||
                !uniqueConsoles.Add(console))
            {
                Debug.LogError(
                    "EncounterQuestionLoader: Assign unique " +
                    "RandomEncounter consoles from this scene."
                );

                return false;
            }
        }

        return true;
    }

    private List<QA> BuildMixedQuestions(QuestionRow[] rows)
    {
        if (rows == null || rows.Length == 0)
        {
            Debug.LogError("EncounterQuestionLoader: No questions received.");
            return null;
        }

        int multipleCount = Mathf.RoundToInt(rows.Length * 0.70f);

        int triviaCount = Mathf.Min(
            Mathf.RoundToInt(rows.Length * 0.20f),
            rows.Length - multipleCount
        );

        int identifyCount = rows.Length - multipleCount - triviaCount;

        // 0 = multiple choice, 1 = true/false, 2 = identification.
        List<int> questionTypes = new List<int>();

        for (int i = 0; i < multipleCount; i++)
        {
            questionTypes.Add(0);
        }

        for (int i = 0; i < triviaCount; i++)
        {
            questionTypes.Add(1);
        }

        for (int i = 0; i < identifyCount; i++)
        {
            questionTypes.Add(2);
        }

        ShuffleList(questionTypes);

        List<QA> questions = new List<QA>();

        for (int i = 0; i < rows.Length; i++)
        {
            QuestionRow row = rows[i];

            if (row == null ||
                string.IsNullOrWhiteSpace(row.question) ||
                string.IsNullOrWhiteSpace(row.answer))
            {
                Debug.LogError(
                    "EncounterQuestionLoader: Missing question or answer at row " + i
                );
                return null;
            }

            string question = row.question.Trim();
            string correctAnswer = CleanAnswerText(row.answer);

            // Identification only needs the question and correct answer.
            if (questionTypes[i] == 2)
            {
                questions.Add(
                    new Identify(question, correctAnswer, false)
                );
                continue;
            }

            if (row.choices == null || row.choices.Length != 4)
            {
                Debug.LogError(
                    "Question ID " + row.id + ": Expected four database choices."
                );
                return null;
            }

            List<string> choices = new List<string>();
            List<string> wrongAnswers = new List<string>();

            HashSet<string> uniqueChoices =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool answerFound = false;

            foreach (string rawChoice in row.choices)
            {
                string choice = CleanAnswerText(rawChoice);

                if (string.IsNullOrWhiteSpace(choice))
                {
                    Debug.LogError(
                        "Question ID " + row.id + ": An answer choice is empty."
                    );
                    return null;
                }

                if (!uniqueChoices.Add(choice))
                {
                    Debug.LogWarning(
                        "Question ID " + row.id +
                        ": Duplicate database choice: " + choice
                    );
                }

                if (string.Equals(
                    choice,
                    correctAnswer,
                    StringComparison.OrdinalIgnoreCase))
                {
                    // Use identical text for matching and feedback.
                    choice = correctAnswer;
                    answerFound = true;
                }
                else
                {
                    wrongAnswers.Add(choice);
                }

                choices.Add(choice);
            }

            if (!answerFound)
            {
                Debug.LogError(
                    "Question ID " + row.id +
                    ": Answer does not match any database choice."
                );
                return null;
            }

            if (questionTypes[i] == 0)
            {
                ShuffleList(choices);

                int correctIndex = choices.IndexOf(correctAnswer);

                questions.Add(
                    new MultipleChoices(
                        question,
                        choices.ToArray(),
                        correctIndex
                    )
                );
            }
            else
            {
                if (wrongAnswers.Count == 0)
                {
                    Debug.LogError(
                        "Question ID " + row.id +
                        ": No incorrect choice available for true/false."
                    );
                    return null;
                }

                bool isTrue = UnityEngine.Random.Range(0, 2) == 0;
                string proposedAnswer = correctAnswer;

                if (!isTrue)
                {
                    int wrongIndex =
                        UnityEngine.Random.Range(0, wrongAnswers.Count);

                    proposedAnswer = wrongAnswers[wrongIndex];
                }

                string statement =
                    question +
                    "\n\nProposed answer: " + proposedAnswer +
                    "\n\nIs the proposed answer correct?";

                questions.Add(new Trivia(statement, isTrue));
            }
        }

        Debug.Log(
            "EncounterQuestionLoader: Generated " +
            multipleCount + " multiple-choice, " +
            triviaCount + " true/false, " +
            identifyCount + " identification questions."
        );

        return questions;
    }

    private string CleanAnswerText(string value)
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

    private void ShuffleList<T>(List<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);

            T temporary = items[i];
            items[i] = items[randomIndex];
            items[randomIndex] = temporary;
        }
    }
}