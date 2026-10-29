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

    private static string GetKey(string profile, string milestoneId)
    {
        return "EXplorer_Chapter1Progress_" +
            Uri.EscapeDataString(profile) + "_" +
            milestoneId;
    }

    public static void Complete(string profile, string milestoneId)
    {
        if (string.IsNullOrWhiteSpace(profile) ||
            string.IsNullOrWhiteSpace(milestoneId))
        {
            return;
        }

        if (Array.IndexOf(chapter1Milestones, milestoneId) < 0)
        {
            Debug.LogWarning(
                "Unknown Chapter 1 milestone: " + milestoneId
            );
            return;
        }

        string key = GetKey(profile, milestoneId);

        if (PlayerPrefs.GetInt(key, 0) == 1)
        {
            return;
        }

        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        Debug.Log(
            "Chapter 1 progression: " +
            GetPercentage(profile).ToString("0.00") + "%"
        );
    }

    public static float GetPercentage(string profile)
    {
        int completed = 0;

        foreach (string milestone in chapter1Milestones)
        {
            if (PlayerPrefs.GetInt(GetKey(profile, milestone), 0) == 1)
            {
                completed++;
            }
        }

        return completed * 100f / chapter1Milestones.Length;
    }
}