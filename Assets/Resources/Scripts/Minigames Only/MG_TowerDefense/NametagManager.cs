using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class NametagManager : MonoBehaviour
{
    [SerializeField, Range(1, 2)] private int chapterId = 1;

    private List<NametagsData> nametags =
        new List<NametagsData>();

    private NametagsData winningSO;

    public bool IsLoaded { get; private set; }
    public bool LoadFailed { get; private set; }

    [Serializable]
    private class QuestionRow
    {
        public int id;
        public string question;
        public string[] correctAnswers;
        public string[] wrongAnswers;
    }

    [Serializable]
    private class QuestionResponse
    {
        public bool success;
        public string message;
        public QuestionRow[] questions;
    }

    private IEnumerator Start()
    {
        string host = "localhost";

        if (!string.IsNullOrWhiteSpace(DBC.IP_ADDRESS))
        {
            host = DBC.IP_ADDRESS.Trim();
        }

        string url =
            "http://" + host +
            "/Explorer/php%20stuffs/get_tower_questions.php" +
            "?chapter=" + chapterId;

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 20;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                FailLoading(
                    request.error + "\n" +
                    request.downloadHandler.text
                );
                yield break;
            }

            QuestionResponse response = null;

            try
            {
                response = JsonUtility.FromJson<QuestionResponse>(
                    request.downloadHandler.text
                );
            }
            catch (Exception error)
            {
                FailLoading("Invalid server response: " + error.Message);
            }

            if (LoadFailed)
            {
                yield break;
            }

            if (response == null ||
                !response.success ||
                response.questions == null ||
                response.questions.Length == 0)
            {
                FailLoading("No usable Tower Defense content received.");
                yield break;
            }

            foreach (QuestionRow row in response.questions)
            {
                if (row == null ||
                    string.IsNullOrWhiteSpace(row.question) ||
                    !ValidAnswers(row.correctAnswers) ||
                    !ValidAnswers(row.wrongAnswers))
                {
                    FailLoading("A question has missing content.");
                    yield break;
                }

                HashSet<string> answers =
                    new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase
                    );

                foreach (string answer in row.correctAnswers)
                {
                    answers.Add(answer.Trim());
                }

                foreach (string answer in row.wrongAnswers)
                {
                    if (answers.Contains(answer.Trim()))
                    {
                        FailLoading(
                            "Question " + row.id +
                            ": An answer is marked both correct and wrong."
                        );
                        yield break;
                    }
                }

                NametagsData data =
                    ScriptableObject.CreateInstance<NametagsData>();

                data.name = "Database_TD_" + row.id;
                data.question = row.question.Trim();
                data.correctAnswers = TrimAnswers(row.correctAnswers);
                data.wrongAnswers = TrimAnswers(row.wrongAnswers);

                nametags.Add(data);
            }

            winningSO = GetRandomNametagSO();
            IsLoaded = true;

            Debug.Log(
                "Tower Defense: Loaded " + nametags.Count +
                " database question sets.",
                this
            );
        }
    }

    private bool ValidAnswers(string[] answers)
    {
        if (answers == null || answers.Length == 0)
        {
            return false;
        }

        foreach (string answer in answers)
        {
            if (string.IsNullOrWhiteSpace(answer))
            {
                return false;
            }
        }

        return true;
    }

    private string[] TrimAnswers(string[] answers)
    {
        string[] trimmed = new string[answers.Length];

        for (int i = 0; i < answers.Length; i++)
        {
            trimmed[i] = answers[i].Trim();
        }

        return trimmed;
    }

    private void FailLoading(string message)
    {
        LoadFailed = true;

        Debug.LogError(
            "Tower Defense content loading failed: " + message,
            this
        );
    }

    public void rollNewSet()
    {
        winningSO = GetRandomNametagSO();
    }

    public NametagsData GetRandomNametagSO()
    {
        if (nametags.Count == 0)
        {
            return null;
        }

        return nametags[UnityEngine.Random.Range(0, nametags.Count)];
    }

    public NametagsData GetWinningSO()
    {
        return winningSO;
    }

    private void OnDestroy()
    {
        foreach (NametagsData data in nametags)
        {
            if (data != null)
            {
                Destroy(data);
            }
        }
    }
}