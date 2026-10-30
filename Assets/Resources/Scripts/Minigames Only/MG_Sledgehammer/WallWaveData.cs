using System;
using UnityEngine;

[CreateAssetMenu(fileName = "WallWave",menuName = "Scriptable Objects/Wall Wave Data")]
public class WallWaveData : ScriptableObject
{
    [Header("Wave Information")]
    public string waveName;
    public string question;
    public int spawnsRequired = 10;

    [Header("Wall Answers")]
    public WallAnswerData[] answers;


    public bool HasValidAnswers()
    {
        if (answers == null || answers.Length == 0)
        {
            return false;
        }

        int correctCount = 0;

        foreach (WallAnswerData answer in answers)
        {
            if (answer == null)
            {
                continue;
            }

            if (answer.isCorrect)
            {
                correctCount++;
            }

            if (string.IsNullOrWhiteSpace(answer.answerText))
            {
                return false;
            }
                
        }

        return correctCount == 1;
    }
}

[Serializable]
public class WallAnswerData
{
    [TextArea(1, 3)]
    public string answerText;

    public bool isCorrect;
}
