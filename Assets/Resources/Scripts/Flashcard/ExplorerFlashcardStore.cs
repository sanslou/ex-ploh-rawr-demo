
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

        public List<ExplorerFlashcardPanel.Card> generalCards =
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

                if (data.generalCards == null)
                {
                    data.generalCards = new List<ExplorerFlashcardPanel.Card>();
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

    // Rebuild each loaded chapter only after ALL its encounter consoles are ready.
    // Other chapters retain their last successfully loaded General pool.
    public static void RegisterLoadedQuizzes()
    {
        Load();
        OBJ_CMD[] quizzes = UnityEngine.Object.FindObjectsByType<OBJ_CMD>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Dictionary<int, List<OBJ_CMD>> chapters = new Dictionary<int, List<OBJ_CMD>>();
        foreach (OBJ_CMD quiz in quizzes)
        {
            if (!quiz.IsFlashcardRandomEncounter) continue;
            int chapter = Mathf.Max(1, quiz.flashcardChapter);
            if (!chapters.ContainsKey(chapter))
                chapters.Add(chapter, new List<OBJ_CMD>());
            chapters[chapter].Add(quiz);
        }

        bool changed = false;
        foreach (var entry in chapters)
        {
            bool ready = true;
            List<ExplorerFlashcardPanel.Card> fresh =
                new List<ExplorerFlashcardPanel.Card>();
            foreach (OBJ_CMD quiz in entry.Value)
            {
                if (!quiz.FlashcardQuestionsReady || quiz.QAs == null || quiz.QAs.Count == 0)
                {
                    ready = false;
                    break;
                }
                foreach (QA qa in quiz.QAs)
                {
                    string term, definition;
                    if (!TryGetFlashcardContent(qa, out term, out definition)) continue;
                    if (fresh.Exists(c => c.term == term && c.definition == definition)) continue;
                    fresh.Add(new ExplorerFlashcardPanel.Card
                    {
                        chapter = entry.Key,
                        term = term,
                        definition = definition
                    });
                }
            }
            // Empty/failed loads must not erase the last valid pool.
            if (!ready || fresh.Count == 0) continue;
            List<ExplorerFlashcardPanel.Card> old =
                data.generalCards.FindAll(c => c != null && c.chapter == entry.Key);
            bool same = old.Count == fresh.Count;
            if (same)
            {
                foreach (var card in fresh)
                    if (!old.Exists(c => c.term == card.term && c.definition == card.definition))
                    { same = false; break; }
            }
            if (same) continue;
            data.generalCards.RemoveAll(c => c == null || c.chapter == entry.Key);
            data.generalCards.AddRange(fresh);
            changed = true;
        }
        if (changed) Save();
    }

    // Existing OBJ_CMD calls remain valid. The source of General is the live
    // encounter consoles, never an accumulating history of registered questions.
    public static void RegisterQuestions(int chapter, IEnumerable<QA> questions)
    {
        RegisterLoadedQuizzes();
    }

    public static bool RemoveCollectedCard(int chapter, string term, string definition)
    {
        Load();
        bool changed = false;
        foreach (var card in data.cards)
        {
            if (card != null && card.chapter == chapter &&
                card.term == term && card.definition == definition && card.collected)
            {
                card.collected = false;
                changed = true;
            }
        }
        // Assessment failure history is retained; review completion is separate.
        if (changed) Save();
        return changed;
    }

    public static List<ExplorerFlashcardPanel.Card> GetGeneralCards()
    {
        Load();
        return new List<ExplorerFlashcardPanel.Card>(data.generalCards);
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

            if (Add(chapter, qa, true))
            {
                changed = true;
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
