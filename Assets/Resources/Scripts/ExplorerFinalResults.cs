
using UnityEngine;
using TMPro;
using System.Text;
using System.Collections.Generic;

public class ExplorerFinalResults : MonoBehaviour
{
    [Header("Final Results UI")]
    [SerializeField] private TMP_Text resultsText;

    [Header("Chapter 1 Keys")]
    [SerializeField] private string c1PreCorrect = "C1_PretestCorrect";
    [SerializeField] private string c1PreTotal = "C1_PretestTotal";
    [SerializeField] private string c1EncounterCorrect = "C1_EncounterCorrect";
    [SerializeField] private string c1EncounterTotal = "C1_EncounterTotal";

    private void Start()
    {
        RefreshResults();
    }


    private string GetMinigameResult(string prefix)
    {
        if (!PlayerPrefs.HasKey(prefix + "_Score") ||
            !PlayerPrefs.HasKey(prefix + "_MaxScore"))
        {
            return "N/A";
        }

        int score = PlayerPrefs.GetInt(prefix + "_Score");
        int maxScore = PlayerPrefs.GetInt(prefix + "_MaxScore");
        bool failed = PlayerPrefs.GetInt(prefix + "_Failed", 0) == 1;

        if (failed || maxScore <= 0)
        {
            return score + "/" + maxScore + " (F)";
        }

        float percentage = (float)score / maxScore * 100f;

        string grade = "F";

        if (percentage >= 100f)
        {
            grade = "S+";
        }
        else if (percentage >= 90f)
        {
            grade = "A";
        }
        else if (percentage >= 80f)
        {
            grade = "B";
        }
        else if (percentage >= 70f)
        {
            grade = "C";
        }
        else if (percentage >= 60f)
        {
            grade = "D";
        }

        return score + "/" + maxScore +
            " (" + percentage.ToString("F1") +
            "%) - " + grade;
    }

    public void RefreshResults()
    {
        if (resultsText == null)
        {
            Debug.LogError("ExplorerFinalResults: Missing TMP Text.");
            return;
        }

        StringBuilder text = new StringBuilder();

        text.AppendLine("EXPLORER - FINAL RESULTS");
        text.AppendLine();

        text.AppendLine("CHAPTER 1");
        text.AppendLine("Pre-Test: " +
            GetScore(c1PreCorrect, c1PreTotal));

        text.AppendLine("Random Encounters: " +
            GetScore(c1EncounterCorrect, c1EncounterTotal));

        text.AppendLine("Post-Test: " +
            GetScore("Chapter1_PostTest_Correct",
                     "Chapter1_PostTest_Total"));

        text.AppendLine("Tower Defense: " +
    GetMinigameResult("Chapter1_TowerDefense"));

        text.AppendLine();

        text.AppendLine("CHAPTER 2");
        text.AppendLine("Pre-Test: " +
            GetScore("Chapter2_PretestCorrect",
                     "Chapter2_PretestTotal"));

        text.AppendLine("Sledgehammer: " +
    GetMinigameResult("Chapter2_Sledgehammer"));

        text.AppendLine("Repair Syntax: " +
            GetMinigameResult("Chapter2_RepairSyntax"));

        text.AppendLine("Random Encounters: " +
            GetScore("Chapter2_RandomEncounterCorrect",
                     "Chapter2_RandomEncounterTotal"));

        text.AppendLine("Post-Test: " +
            GetScore("Chapter2_PostTest_Correct",
                     "Chapter2_PostTest_Total"));

        text.AppendLine();
        text.AppendLine("FLASHCARDS COLLECTED");

        int chapter1Cards = 0;
        int chapter2Cards = 0;

        List<ExplorerFlashcardPanel.Card> cards =
            ExplorerFlashcardStore.GetCards();

        foreach (ExplorerFlashcardPanel.Card card in cards)
        {
            if (card == null || !card.collected)
            {
                continue;
            }

            if (card.chapter == 1)
            {
                chapter1Cards++;
            }
            else if (card.chapter == 2)
            {
                chapter2Cards++;
            }
        }

        text.AppendLine("Chapter 1: " + chapter1Cards);
        text.AppendLine("Chapter 2: " + chapter2Cards);
        text.AppendLine("Total: " +
            (chapter1Cards + chapter2Cards));

        resultsText.text = text.ToString();
    }

    private string GetScore(string correctKey, string totalKey)
    {
        if (!PlayerPrefs.HasKey(correctKey) ||
            !PlayerPrefs.HasKey(totalKey))
        {
            return "N/A";
        }

        int correct = PlayerPrefs.GetInt(correctKey);
        int total = PlayerPrefs.GetInt(totalKey);

        if (total <= 0)
        {
            return correct + "/" + total + " (N/A)";
        }

        float percentage = (float)correct / total * 100f;

        return correct + "/" + total +
            " (" + percentage.ToString("F1") + "%)";
    }
}
