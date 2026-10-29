using UnityEngine;
using UnityEngine.SceneManagement;

public class LogoutButton : MonoBehaviour
{
    [SerializeField] private string loginSceneName = "Login";

    public void Logout()
    {
        PlayerPrefs.DeleteKey("AccountID");
        PlayerPrefs.DeleteKey("AccountPwd");
        PlayerPrefs.DeleteKey("AccountFN");
        PlayerPrefs.DeleteKey("AccountMN");
        PlayerPrefs.DeleteKey("AccountLN");
        PlayerPrefs.DeleteKey("AccountSec");
        PlayerPrefs.DeleteKey("PlayerName");
        PlayerPrefs.DeleteKey("PlayerGender");
        PlayerPrefs.DeleteKey("isAccountSaved");
        PlayerPrefs.Save();

        DBC.IS_GUEST_MODE = false;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        SceneManager.LoadScene(loginSceneName);
    }
}