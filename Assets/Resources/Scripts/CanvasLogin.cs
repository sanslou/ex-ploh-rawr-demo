using System.Collections;
using System.Net;
using TMPro;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CanvasLogin : MonoBehaviour
{
    TMP_InputField inputID, inputPwd;
    TMP_Text textWrong;
    Button btnLogin;

    string wrongCreds = "Wrong ID or Password!";
    string noneCreds = "Enter your ID and Password!";
    string noConn = "No connection, try again!";


    void Start(){
        //if (PlayerPrefs.GetInt("isAccountSaved") == 1) {
        //    SceneManager.LoadScene("MainMenu");
        //    return;
        //}


        inputID = transform.Find("InputField ID").GetComponent<TMP_InputField>();
        inputPwd = transform.Find("InputField Password").GetComponent<TMP_InputField>();
        textWrong = transform.Find("Text Wrong").GetComponent<TMP_Text>();
        btnLogin = transform.Find("Button Login").GetComponent<Button>();

        textWrong.gameObject.SetActive(false);

    }
    void Update(){}
        public void ButtonLogin() {
        //Debug.Log(inputID + " | " + inputPwd.text);

        if (inputID.text == "" || inputPwd.text == "")
        {
            textWrong.text = noneCreds;
            textWrong.gameObject.SetActive(true);

        }
        else
        {
            StartCoroutine(ConnectAndLogin());
            textWrong.text = wrongCreds;
        }
    }

    public void ButtonGuest() {
        DBC.IS_GUEST_MODE = true;

        PlayerPrefs.SetString("AccountID", "");
        PlayerPrefs.SetString("AccountPwd", "");
        PlayerPrefs.SetString("AccountFN", "");
        PlayerPrefs.SetString("AccountMN", "");
        PlayerPrefs.SetString("AccountLN", "");
        PlayerPrefs.SetString("AccountSec", "");
        PlayerPrefs.SetString("PlayerName", "");
        PlayerPrefs.SetString("PlayerGender", "");
        PlayerPrefs.SetInt("isAccountSaved", 1);
        PlayerPrefs.Save();

        SceneManager.LoadScene("MainMenu");
    }

    public void ButtonExit() {
        Application.Quit();
    }

    public void OnChangeInput() {
        textWrong.gameObject.SetActive(false);
    }


    IEnumerator ConnectAndLogin()
    {
        WWWForm form = new WWWForm();
        
        form.AddField("id", inputID.text);
        form.AddField("pwd", inputPwd.text);

        // Old format (only applicable to Editor)
        // http://localhost/Explorer/game_script.php
        // New format (now applies to )
        // http://192.168.1.8/Explorer/php%20stuffs/login_student.php
        // http://{IP.ADDRESS}/Explorer/php%20/game_login_student_php

        string host = "localhost";

        if (!string.IsNullOrWhiteSpace(DBC.IP_ADDRESS))
        {
            host = DBC.IP_ADDRESS.Trim();
        }

        string link = "http://" + host +
            "/Explorer/php%20stuffs/game_login_student.php";

        Debug.Log("Login URL: " + link);
        //Debug.Log(link);

        UnityWebRequest www = UnityWebRequest.Post(link, form);
        www.certificateHandler = new AcceptAllCertificatesHandler();
        yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + www.error);  
                textWrong.text = noConn + " \nError: " + www.error;
            textWrong.gameObject.SetActive(true);
            }
            else
            {
            string state = www.downloadHandler.text.Trim();
            if (state.Contains("<") ||
                state.Contains("Warning:") ||
                state.Contains("Fatal error:"))
            {
                textWrong.text = "The login server returned an error.";
                textWrong.gameObject.SetActive(true);

                Debug.LogError(
                    "Login response contains server errors. Account data was not saved."
                );

                yield break;
            }
                if (state != "FAIL")
                {
                    //Debug.Log("Login Success");

                    
                string[] fields = state.Trim().Split('|');

                if (fields.Length != 8 ||
                    string.IsNullOrWhiteSpace(fields[0]) ||
                    fields[0].Contains("<"))
                {
                    textWrong.text = "Invalid login response from server.";
                    textWrong.gameObject.SetActive(true);
                    yield break;
                }

                DBC.IS_GUEST_MODE = false;

                PlayerPrefs.SetString("AccountID", fields[0].Trim());
                PlayerPrefs.SetString("AccountPwd", fields[1]);
                PlayerPrefs.SetString("AccountFN", fields[2]);
                PlayerPrefs.SetString("AccountMN", fields[3]);
                PlayerPrefs.SetString("AccountLN", fields[4]);
                PlayerPrefs.SetString("AccountSec", fields[5]);
                PlayerPrefs.SetString("PlayerName", fields[6]);
                PlayerPrefs.SetString("PlayerGender", fields[7].Trim());

                PlayerPrefs.SetInt("isAccountSaved", 1);
                PlayerPrefs.Save();

                Debug.Log("Login account details refreshed.");
                
                    
                btnLogin.gameObject.SetActive(false);
                textWrong.gameObject.SetActive(false);

                Debug.Log(
                "Before MainMenu: AccountID = " +
                PlayerPrefs.GetString("AccountID", "MISSING") +
                " | PlayerGender = " +
                PlayerPrefs.GetString("PlayerGender", "MISSING")
                );

                SceneManager.LoadScene("MainMenu");
                }
                else {
                    Debug.Log("Login FAILED");
                    textWrong.gameObject.SetActive(true);
                }

        }
    }


    // Only Local and debugging
    public void ButtonSettings() {
        SceneManager.LoadScene("IP");
    }
}
