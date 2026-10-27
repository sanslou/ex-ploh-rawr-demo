using UnityEngine;
using UnityEngine.Events;

public class Chapter2Manager : MonoBehaviour
{
    [Header("Chapter 2 Exploration")]
    [SerializeField] private GameObject childrenParent;
    [SerializeField] private UnityEvent onAllChildrenInactive = new UnityEvent();

    private bool allChildrenEventInvoked;
    [Header("Chapter 2 Intro Cutscenes")]
    [SerializeField] private OBJ_CMD preTestConsole;

    [SerializeField] private Cutscene goodAfterPreTest;
    [SerializeField] private Cutscene badAfterPreTest;

    public void OnPreTestFinished()
    {
        if (preTestConsole == null)
        {
            Debug.LogError(
                "Chapter2Manager: PreTest console is missing!"
            );
            return;
        }

        Cutscene selectedCutscene;

        if (preTestConsole.hasFailed)
        {
            selectedCutscene = badAfterPreTest;
        }
        else
        {
            selectedCutscene = goodAfterPreTest;
        }

        if (selectedCutscene == null)
        {
            Debug.LogError(
                "Chapter2Manager: Selected cutscene is missing!"
            );
            return;
        }

        if (!selectedCutscene.gameObject.activeInHierarchy)
        {
            selectedCutscene.gameObject.SetActive(true);
        }

        selectedCutscene.PlayCutscene();
    }

    public void ProceedToChapter3()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("AT1");
    }
    private void Update()
    {
        if (childrenParent == null)
            return;

        if (childrenParent.transform.childCount == 0)
            return;

        foreach (Transform child in childrenParent.transform)
        {
            if (child.gameObject.activeSelf)
            {
                allChildrenEventInvoked = false;
                return;
            }
        }

        if (!allChildrenEventInvoked)
        {
            allChildrenEventInvoked = true;
            onAllChildrenInactive.Invoke();
        }
    }
}