
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExplorerPlayerName : MonoBehaviour
{
    [Header("Name Input UI")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Canvas nameCanvas;

    [Header("Final Results")]
    [SerializeField] private ExplorerFinalResults finalResults;

    private const string PlayerNameKey = "Explorer_PlayerName";

    private void Start()
    {

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(ConfirmName);
        }

        if (nameInput != null)
        {
            nameInput.text = PlayerPrefs.GetString(PlayerNameKey, "");
        }
    }

    public void ConfirmName()
    {
        if (nameInput == null)
        {
            Debug.LogError("ExplorerPlayerName: Name input is missing.");
            return;
        }

        string playerName = nameInput.text.Trim();

        if (string.IsNullOrEmpty(playerName))
        {
            playerName = "Anonymous";
        }

        PlayerPrefs.SetString(PlayerNameKey, playerName);
        PlayerPrefs.Save();

        Debug.Log("ExplorerPlayerName: Saved name.");

        if (nameCanvas != null)
        {
            nameCanvas.enabled = false;
        }

        if (finalResults != null)
        {
            finalResults.RefreshResults();
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmName);
        }
    }
}
