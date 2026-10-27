using UnityEngine;
using UnityEngine.Events;

public class SledgehammerReturn : MonoBehaviour
{
    [Header("Chapter 2 - Sledgehammer")]
    [SerializeField] private UnityEvent onSledgehammerPassed;
    [SerializeField] private UnityEvent onSledgehammerFail;

    private void Start()
    {
        int result = PlayerPrefs.GetInt("Chapter2_SledgehammerWon", 0);

        if (result == 1)
        {
            Debug.Log("Chapter 2 Sledgehammer passed!");
            onSledgehammerPassed?.Invoke();
        }
        else if (result == 2)
        {
            Debug.Log("Chapter 2 Sledgehammer failed!");
            onSledgehammerFail?.Invoke();
        }

        if (result != 0)
        {
            PlayerPrefs.SetInt("Chapter2_SledgehammerWon", 0);
            PlayerPrefs.Save();
        }
    }
}
