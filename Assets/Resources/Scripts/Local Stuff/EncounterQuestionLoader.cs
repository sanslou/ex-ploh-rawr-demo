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

        string separator = "?";

        if (endpointUrl.Contains("?"))
        {
            separator = "&";
        }

        string url = endpointUrl + separator +
            "first=" + firstQuestionId +
            "&last=" + lastQuestionId;

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
        if (string.IsNullOrWhiteSpace(endpointUrl) ||
            firstQuestionId < 1 ||
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
        // Build a unique answer pool for incorrect choices.
        List<string> answerPool = new List<string>();

        HashSet<string> uniqueAnswers =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (QuestionRow row in rows)
        {
            if (row == null ||
                string.IsNullOrWhiteSpace(row.question) ||
                string.IsNullOrWhiteSpace(row.answer))
            {
                Debug.LogError(
                    "EncounterQuestionLoader: Empty question or answer."
                );

                return null;
            }

            string answer = row.answer.Trim();

            if (uniqueAnswers.Add(answer))
            {
                answerPool.Add(answer);
            }
        }

        int multipleCount = Mathf.RoundToInt(rows.Length * 0.70f);

        int triviaCount = Mathf.Min(
            Mathf.RoundToInt(rows.Length * 0.20f),
            rows.Length - multipleCount
        );

        int identifyCount = rows.Length - multipleCount - triviaCount;

        if (multipleCount > 0 && answerPool.Count < 4)
        {
            Debug.LogError(
                "EncounterQuestionLoader: At least four different answers " +
                "are needed to generate multiple-choice questions."
            );

            return null;
        }


        // 0 = MultipleChoices, 1 = Trivia, 2 = Identify.
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
            string question = rows[i].question.Trim();
            string correctAnswer = rows[i].answer.Trim();

            List<string> wrongAnswers = new List<string>();

            foreach (string candidate in answerPool)
            {
                if (!string.Equals(
                    candidate,
                    correctAnswer,
                    StringComparison.OrdinalIgnoreCase))
                {
                    wrongAnswers.Add(candidate);
                }
            }

            ShuffleList(wrongAnswers);

            if (questionTypes[i] == 0)
            {
                List<string> choices = new List<string>
            {
                correctAnswer,
                wrongAnswers[0],
                wrongAnswers[1],
                wrongAnswers[2]
            };

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
            else if (questionTypes[i] == 1)
            {
                string statement =
                    question +
                    "\n\nThe correct answer is: " +
                    correctAnswer;

                questions.Add(
                    new Trivia(statement, true)
                );
            }
            else
            {
                questions.Add(
                    new Identify(question, correctAnswer, false)
                );
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