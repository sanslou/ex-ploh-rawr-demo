using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
public class MinigameResultRecorder : MonoBehaviour
{
    [SerializeField] private string minigameId;
    [SerializeField, Range(1, 2)] private int chapterId = 1;

    [Serializable]
    private class MinigameRecord
    {
        public string attempt_id;
        public string student_id;
        public int chapter_id;
        public string minigame_id;
        public int score;
        public int max_score;
        public string outcome;
        public string completed_at_utc;
    }

    [Serializable]
    private class MinigameHistory
    {
        public List<MinigameRecord> attempts =
            new List<MinigameRecord>();
    }

    private string studentId;
    private string attemptId;
    private bool recorded;

    [Serializable]
    private class UploadResponse
    {
        public bool success;
        public string message;
    }

    private IEnumerator UploadResult(string json)
    {
        string host = "localhost";

        if (!string.IsNullOrWhiteSpace(DBC.IP_ADDRESS))
        {
            host = DBC.IP_ADDRESS.Trim();
        }

        string url =
            "http://" + host +
            "/Explorer/php%20stuffs/save_minigame_result.php";

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
                    "Minigame upload failed. Local history is preserved." +
                    "\nError: " + request.error +
                    "\nResponse: " + request.downloadHandler.text,
                    this
                );

                yield break;
            }

            UploadResponse response = null;

            try
            {
                response = JsonUtility.FromJson<UploadResponse>(
                    request.downloadHandler.text
                );
            }
            catch (Exception error)
            {
                Debug.LogWarning(
                    "Invalid minigame upload response: " + error.Message,
                    this
                );
            }

            if (response == null || !response.success)
            {
                Debug.LogWarning(
                    "Server did not confirm minigame saving." +
                    "\nResponse: " + request.downloadHandler.text,
                    this
                );

                yield break;
            }

            Debug.Log(
                "MINIGAME DATABASE: " + response.message,
                this
            );
        }
    }

    private void Awake()
    {
        BeginAttempt();
    }

    // Call this again only if restarting a minigame
    // without reloading its scene.
    public void BeginAttempt()
    {
        recorded = false;
        attemptId = Guid.NewGuid().ToString("N");
        studentId = "";

        if (!DBC.IS_GUEST_MODE)
        {
            studentId =
                PlayerPrefs.GetString("AccountID", "").Trim();
        }
    }

    public void Record(int score, int maxScore, string outcome)
    {
        if (recorded)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(studentId))
        {
            Debug.Log("Minigame recording skipped: no student account.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(minigameId))
        {
            Debug.LogError("Assign the Minigame ID in the Inspector.", this);
            return;
        }

        if (maxScore < 0 || string.IsNullOrWhiteSpace(outcome))
        {
            Debug.LogError("Invalid minigame result.", this);
            return;
        }

        MinigameRecord record = new MinigameRecord
        {
            attempt_id = attemptId,
            student_id = studentId,
            chapter_id = chapterId,
            minigame_id = minigameId.Trim(),
            score = score,
            max_score = maxScore,
            outcome = outcome,
            completed_at_utc = DateTime.UtcNow.ToString("o")
        };

        string key = "EXplorer_MinigameHistory_v1_" +
            Uri.EscapeDataString(studentId);

        try
        {
            MinigameHistory history = new MinigameHistory();

            if (PlayerPrefs.HasKey(key))
            {
                history = JsonUtility.FromJson<MinigameHistory>(
                    PlayerPrefs.GetString(key)
                );

                if (history == null || history.attempts == null)
                {
                    Debug.LogError(
                        "Invalid saved minigame history. Existing data preserved.",
                        this
                    );
                    return;
                }
            }

            // Prevent recording this attempt twice.
            foreach (MinigameRecord existing in history.attempts)
            {
                if (existing.attempt_id == attemptId)
                {
                    recorded = true;
                    return;
                }
            }

            history.attempts.Add(record);

            PlayerPrefs.SetString(key, JsonUtility.ToJson(history));
            PlayerPrefs.Save();
            recorded = true;
            StartCoroutine(
    UploadResult(JsonUtility.ToJson(record))
);


            Debug.Log(
                "MINIGAME RESULT — SAVED LOCALLY:\n" +
                JsonUtility.ToJson(record, true),
                this
            );
        }
        catch (Exception error)
        {
            Debug.LogError(
                "Could not save minigame result: " + error.Message,
                this
            );
        }
    }
}