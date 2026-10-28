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

        string link = (DBC.IP_ADDRESS == "") ? ("http://localhost/Explorer/php%20stuffs/game_login_student.php") : ($"http://{DBC.IP_ADDRESS}/Explorer/php%20stuffs/login_student.php");
        //Debug.Log(link);

        UnityWebRequest www = UnityWebRequest.Post(link, form);
        www.certificateHandler = new AcceptAllCertificatesHandler();
        yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + www.error);
                textWrong.text = noConn;
                textWrong.gameObject.SetActive(true);
            }
            else
            {
                string state = www.downloadHandler.text;
                Debug.Log(state);
                if (state != "FAIL")
                {
                    //Debug.Log("Login Success");

                    if (PlayerPrefs.GetInt("isAccountSaved") != 1) // not saved; aka first time logging in or log-out at some point
                    {
                        string[] fields = state.Split('|');
                        Debug.Log(fields.Length);
                        string getID, getPwd, getPN;
                        getID = fields[0];
                        getPwd = fields[1];
                        getPN = fields[2];
                        

                        PlayerPrefs.SetString("AccountID", getID);
                        PlayerPrefs.SetString("AccountPwd", getPwd);
                        PlayerPrefs.SetString("PlayerName", getPN);

                        
                        PlayerPrefs.SetInt("isAccountSaved", 1);
                        PlayerPrefs.Save();
                    }
                    
                    btnLogin.gameObject.SetActive(false);
                    textWrong.gameObject.SetActive(false);
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
