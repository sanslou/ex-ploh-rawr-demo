using System;
using UnityEngine;

public static class ChapterProgression
{
    private static readonly string[] chapter1Milestones =
    {
        "intro",
        "after_pretest",
        "pretest_outcome",
        "anyways",
        "encounter_intro",
        "reached_map_end",
        "tower_outcome",
        "posttest_outcome",
        "after_posttest"
    };

    private static readonly string[] chapter2Milestones =
    {
        "intro",
        "pretest_outcome",
        "after_pretest",
        "encounter_pots_vases",
        "encounter_podium",
        "encounter_crown",
        "encounter_tools",
        "all_encounters_notification",
        "all_encounters_finished",
        "minigame_win"
    };

    private static string[] GetMilestones(int chapter)
    {
        if (chapter == 1)
        {
            return chapter1Milestones;
        }

        if (chapter == 2)
        {
            return chapter2Milestones;
        }

        return null;
    }

    private static string GetKey(
        string profile,
        int chapter,
        string milestoneId)
    {
        return "EXplorer_Chapter" + chapter + "Progress_" +
            Uri.EscapeDataString(profile) + "_" +
            milestoneId;
    }

    public static void Complete(string profile, string milestoneId)
    {
        Complete(profile, 1, milestoneId);
    }

    public static void Complete(
        string profile,
        int chapter,
        string milestoneId)
    {
        if (string.IsNullOrWhiteSpace(profile) ||
            string.IsNullOrWhiteSpace(milestoneId))
        {
            return;
        }

        milestoneId = milestoneId.Trim();
        string[] milestones = GetMilestones(chapter);

        if (milestones == null ||
            Array.IndexOf(milestones, milestoneId) < 0)
        {
            Debug.LogWarning(
                "Unknown Chapter " + chapter +
                " milestone: " + milestoneId
            );
            return;
        }

        string key = GetKey(profile, chapter, milestoneId);

        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            ChapterProgressSync.Submit(
                profile,
                chapter,
                GetPercentage(profile, chapter)
            );

            return;
        }

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        ChapterProgressSync.Submit(
    profile,
    chapter,
    GetPercentage(profile, chapter)
);

        Debug.Log(
            "Chapter " + chapter + " progression: " +
            GetPercentage(profile, chapter).ToString("0.00") + "%"
        );
    }

    public static float GetPercentage(string profile)
    {
        return GetPercentage(profile, 1);
    }

    public static float GetPercentage(string profile, int chapter)
    {
        string[] milestones = GetMilestones(chapter);

        if (milestones == null ||
            string.IsNullOrWhiteSpace(profile))
        {
            return 0f;
        }

        int completed = 0;

        foreach (string milestone in milestones)
        {
            if (PlayerPrefs.GetInt(
                GetKey(profile, chapter, milestone), 0) == 1)
            {
                completed++;
            }
        }

        return completed * 100f / milestones.Length;
    }

    public static void DebugMilestones(string profile)
    {
        DebugMilestones(profile, 1);
    }

    public static void DebugMilestones(string profile, int chapter)
    {
        string[] milestones = GetMilestones(chapter);

        if (milestones == null ||
            string.IsNullOrWhiteSpace(profile))
        {
            return;
        }

        foreach (string milestone in milestones)
        {
            bool completed = PlayerPrefs.GetInt(
                GetKey(profile, chapter, milestone), 0) == 1;

            if (completed)
            {
                Debug.Log(
                    "Chapter " + chapter + " COMPLETE: " + milestone
                );
            }
            else
            {
                Debug.LogWarning(
                    "Chapter " + chapter + " MISSING: " + milestone
                );
            }
        }
    }
}