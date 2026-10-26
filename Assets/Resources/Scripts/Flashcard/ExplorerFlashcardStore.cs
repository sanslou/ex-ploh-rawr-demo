
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Local storage. No scene object or DontDestroyOnLoad required.
public static class ExplorerFlashcardStore
{
    // Keep this key to retain existing saved flashcards.
    private const string SaveKey = "EXplorer_Flashcards_v1";

    [Serializable]
    public class FailureRecord
    {
        public int chapter;
        public string assessmentType;
        public string term;
        public string definition;
    }

    [Serializable]
    private class SaveData
    {
        public List<ExplorerFlashcardPanel.Card> cards =
            new List<ExplorerFlashcardPanel.Card>();

        public List<FailureRecord> failures =
            new List<FailureRecord>();
    }

    private static SaveData data;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        data = null;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(
        Scene scene, LoadSceneMode mode)
    {
        RegisterLoadedQuizzes();
    }

    private static void Load()
    {
        if (data != null)
        {
            return;
        }

        data = new SaveData();

        string json = PlayerPrefs.GetString(SaveKey, "");

        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            SaveData saved = JsonUtility.FromJson<SaveData>(json);

            if (saved != null)
            {
                data = saved;

                if (data.cards == null)
                {
                    data.cards =
                        new List<ExplorerFlashcardPanel.Card>();
                }

                if (data.failures == null)
                {
                    data.failures = new List<FailureRecord>();
                }
            }
        }
        catch (Exception error)
        {
            Debug.LogError(
                "Could not read flashcards: " + error.Message
            );
        }
    }

    private static void Save()
    {
        PlayerPrefs.SetString(
            SaveKey,
            JsonUtility.ToJson(data)
        );

        PlayerPrefs.Save();
    }

    private static bool TryGetFlashcardContent(
        QA qa,
        out string term,
        out string definition)
    {
        term = "";
        definition = "";

        if (qa == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(qa.QUESTION) ||
            string.IsNullOrWhiteSpace(qa.ANSWER))
        {
            return false;
        }

        term = qa.ANSWER;
        definition = qa.QUESTION;

        if (qa is Trivia)
        {
            bool correct;

            if (!bool.TryParse(
                qa.ANSWER.Trim(), out correct))
            {
                Debug.LogWarning(
                    "Flashcard skipped: invalid True/False answer."
                );
                return false;
            }

            term = qa.QUESTION;

            if (correct)
            {
                definition = "This is true";
            }
            else
            {
                definition = "This is false";
            }
        }

        return true;
    }

    private static bool Add(
        int chapter,
        QA qa,
        bool collected)
    {
        string term;
        string definition;

        if (!TryGetFlashcardContent(
            qa, out term, out definition))
        {
            return false;
        }

        foreach (ExplorerFlashcardPanel.Card card in data.cards)
        {
            if (card == null)
            {
                continue;
            }

            if (card.chapter != chapter ||
                card.term != term ||
                card.definition != definition)
            {
                continue;
            }

            if (collected && !card.collected)
            {
                card.collected = true;
                return true;
            }

            return false;
        }

        data.cards.Add(new ExplorerFlashcardPanel.Card
        {
            chapter = chapter,
            term = term,
            definition = definition,
            collected = collected
        });

        return true;
    }

    public static void RegisterLoadedQuizzes()
    {
        Load();

        bool changed = false;

        OBJ_CMD[] quizzes =
            UnityEngine.Object.FindObjectsByType<OBJ_CMD>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (OBJ_CMD quiz in quizzes)
        {
            if (quiz.QAs == null)
            {
                continue;
            }

            foreach (QA qa in quiz.QAs)
            {
                if (Add(
                    Mathf.Max(1, quiz.flashcardChapter),
                    qa,
                    false))
                {
                    changed = true;
                }
            }
        }

        if (changed)
        {
            Save();
        }
    }

    public static void RegisterQuestions(
        int chapter,
        IEnumerable<QA> questions)
    {
        Load();

        if (questions == null)
        {
            return;
        }

        bool changed = false;

        foreach (QA qa in questions)
        {
            if (Add(Mathf.Max(1, chapter), qa, false))
            {
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
    }

    // Retained for compatibility with existing callers.
    public static void CollectFailures(
        int chapter,
        IEnumerable<QA> questions)
    {
        Load();

        if (questions == null)
        {
            return;
        }

        bool changed = false;

        foreach (QA qa in questions)
        {
            if (Add(Mathf.Max(1, chapter), qa, true))
            {
                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
    }

    // New universal assessment failure storage.
    public static void RecordAssessmentFailures(
        int chapter,
        OBJ_CMD.QuizType type,
        IEnumerable<QA> questions)
    {
        Load();

        if (questions == null)
        {
            return;
        }

        if (type == OBJ_CMD.QuizType.Regular)
        {
            return;
        }

        chapter = Mathf.Max(1, chapter);

        bool changed = false;

        foreach (QA qa in questions)
        {
            string term;
            string definition;

            if (!TryGetFlashcardContent(
                qa, out term, out definition))
            {
                continue;
            }

            // PreTest and encounters add cards for review.
            // PostTest failures remain separate.
            if (type == OBJ_CMD.QuizType.PreTest ||
                type == OBJ_CMD.QuizType.RandomEncounter)
            {
                if (Add(chapter, qa, true))
                {
                    changed = true;
                }
            }
            else
            {
                if (Add(chapter, qa, false))
                {
                    changed = true;
                }
            }

            bool exists = false;

            foreach (FailureRecord record in data.failures)
            {
                if (record.chapter == chapter &&
                    record.assessmentType == type.ToString() &&
                    record.term == term &&
                    record.definition == definition)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                data.failures.Add(new FailureRecord
                {
                    chapter = chapter,
                    assessmentType = type.ToString(),
                    term = term,
                    definition = definition
                });

                changed = true;
            }
        }

        if (changed)
        {
            Save();
        }
    }

    public static List<FailureRecord> GetAssessmentFailures(
        int chapter,
        OBJ_CMD.QuizType type)
    {
        Load();

        List<FailureRecord> results =
            new List<FailureRecord>();

        foreach (FailureRecord record in data.failures)
        {
            if (record.chapter == chapter &&
                record.assessmentType == type.ToString())
            {
                results.Add(new FailureRecord
                {
                    chapter = record.chapter,
                    assessmentType = record.assessmentType,
                    term = record.term,
                    definition = record.definition
                });
            }
        }

        return results;
    }

    public static List<ExplorerFlashcardPanel.Card> GetCards()
    {
        Load();

        return new List<ExplorerFlashcardPanel.Card>(
            data.cards
        );
    }
}
