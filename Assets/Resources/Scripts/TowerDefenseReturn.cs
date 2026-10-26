
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;


public class TowerDefenseReturn : MonoBehaviour
{
    [Header("Chapter 1")]
    [SerializeField] private UnityEvent onTowerDefensePassed;
    [SerializeField] private UnityEvent onTowerDefenseFail;

    private void Start()
    {
        int towerDefenseWon = PlayerPrefs.GetInt("Chapter1_TowerDefenseWon", 0);

        if (towerDefenseWon == 1)
        {
            Debug.Log("Tower Defense passed! Invoking UnityEvent.");
            onTowerDefensePassed?.Invoke();
        }
        else if (towerDefenseWon == 2)
        {
            Debug.Log("Tower Defense failed! Invoking UnityEvent.");
            onTowerDefenseFail?.Invoke();
        }

        if (towerDefenseWon != 0)
        {
            PlayerPrefs.SetInt("Chapter1_TowerDefenseWon", 0);
            PlayerPrefs.Save();
        }
    }

    public void ProceedToChapter2()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("LTV2");
    }
}
