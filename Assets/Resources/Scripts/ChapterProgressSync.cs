using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class ChapterProgressSync : MonoBehaviour
{
    private static ChapterProgressSync instance;

    [Serializable]
    private class ProgressRecord
    {
        public string student_id;
        public int chapter;
        public float chapter_progression;
    }

    [Serializable]
    private class ProgressResponse
    {
        public bool success;
        public string message;
        public int chapter;
        public float chapter_progression;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        SyncCurrentAccount();
    }

    private static ChapterProgressSync GetInstance()
    {
        if (instance == null)
        {
            GameObject host = new GameObject("Chapter Progress Sync");
            instance = host.AddComponent<ChapterProgressSync>();
        }

        return instance;
    }

    public static void Submit(
        string profile,
        int chapter,
        float percentage)
    {
        if (!Application.isPlaying || DBC.IS_GUEST_MODE)
        {
            return;
        }

        string studentId =
            PlayerPrefs.GetString("AccountID", "").Trim();

        if (string.IsNullOrWhiteSpace(studentId) ||
            profile != "student:" + studentId)
        {
            return;
        }

        if (chapter != 1 && chapter != 2)
        {
            return;
        }

        ProgressRecord record = new ProgressRecord
        {
            student_id = studentId,
            chapter = chapter,
            chapter_progression = Mathf.Clamp(percentage, 0f, 100f)
        };

        ChapterProgressSync runner = GetInstance();

        runner.StartCoroutine(
            runner.Upload(JsonUtility.ToJson(record))
        );
    }

    public void SyncCurrentAccount()
    {
        if (DBC.IS_GUEST_MODE)
        {
            return;
        }

        string studentId =
            PlayerPrefs.GetString("AccountID", "").Trim();

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return;
        }

        string profile = "student:" + studentId;

        for (int chapter = 1; chapter <= 2; chapter++)
        {
            Submit(
                profile,
                chapter,
                ChapterProgression.GetPercentage(profile, chapter)
            );
        }
    }

    private IEnumerator Upload(string json)
    {
        string host = "localhost";

        if (!string.IsNullOrWhiteSpace(DBC.IP_ADDRESS))
        {
            host = DBC.IP_ADDRESS.Trim();
        }

        string url =
            "http://" + host +
            "/Explorer/php%20stuffs/save_chapter_progress.php";

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
                    "Progress upload failed. Local milestones are preserved." +
                    "\n" + request.error +
                    "\n" + request.downloadHandler.text,
                    this
                );
                yield break;
            }

            ProgressResponse response = null;

            try
            {
                response = JsonUtility.FromJson<ProgressResponse>(
                    request.downloadHandler.text
                );
            }
            catch (Exception error)
            {
                Debug.LogWarning(
                    "Invalid progression response: " + error.Message,
                    this
                );
            }

            if (response == null || !response.success)
            {
                Debug.LogWarning(
                    "Server did not confirm progression saving." +
                    "\n" + request.downloadHandler.text,
                    this
                );
                yield break;
            }

            Debug.Log(
                "PROGRESSION DATABASE: Chapter " +
                response.chapter + " = " +
                response.chapter_progression.ToString("0.00") + "%",
                this
            );
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}