    using UnityEngine;

    public static class ChapterDifficulty
    {
        private static string GetPrefix(int chapter)
        {
            return "Chapter" + chapter;
        }

        public static void SavePretest(int chapter, int correct, int total)
        {
            // Preserve the existing Tower Defense integration.
            if (chapter == 1)
            {
                Chapter1Difficulty.SavePretest(correct, total);
                return;
            }

            string prefix = GetPrefix(chapter);

            PlayerPrefs.SetInt(prefix + "_PretestCorrect", correct);
            PlayerPrefs.SetInt(prefix + "_PretestTotal", total);

            // Start fresh encounter scores for this chapter run.
            PlayerPrefs.SetInt(prefix + "_RandomEncounterCorrect", 0);
            PlayerPrefs.SetInt(prefix + "_RandomEncounterTotal", 0);

            PlayerPrefs.Save();
        }

        public static void SaveRandomEncounter(int chapter, int correct, int total)
        {
            if (chapter == 1)
            {
                Chapter1Difficulty.SaveRandomEncounter(correct, total);
                return;
            }

            string prefix = GetPrefix(chapter);

            string correctKey = prefix + "_RandomEncounterCorrect";
            string totalKey = prefix + "_RandomEncounterTotal";

            PlayerPrefs.SetInt(
                correctKey,
                PlayerPrefs.GetInt(correctKey, 0) + correct
            );

            PlayerPrefs.SetInt(
                totalKey,
                PlayerPrefs.GetInt(totalKey, 0) + total
            );

            PlayerPrefs.Save();
        }

        public static float GetPerformance(int chapter)
        {
            if (chapter == 1)
            {
                return Chapter1Difficulty.GetPerformance();
            }

            string prefix = GetPrefix(chapter);

            int correct =
                PlayerPrefs.GetInt(prefix + "_PretestCorrect", 0) +
                PlayerPrefs.GetInt(prefix + "_RandomEncounterCorrect", 0);

            int total =
                PlayerPrefs.GetInt(prefix + "_PretestTotal", 0) +
                PlayerPrefs.GetInt(prefix + "_RandomEncounterTotal", 0);

            if (total <= 0)
            {
                return 0f;
            }

            return (float)correct / total * 100f;
        }

        public static int GetDifficulty(int chapter)
        {
            float performance = GetPerformance(chapter);

            if (performance >= 80f)
            {
                return 2; // Hard
            }
            else if (performance >= 50f)
            {
                return 1; // Normal
            }
            else
            {
                return 0; // Easy
            }
        }
    }