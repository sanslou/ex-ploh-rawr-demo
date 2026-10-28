using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class DBC : MonoBehaviour
{
    public string connectionString; // Deprecated; PHP will have the connection-string instead


    /*
     * For PlayerPrefs, saving credentials for auto-login
     * 
     * AccountID
     * AccountPwd
     * AccountFN
     * AccountMN
     * AccountLN
     * AccountSec
     * PlayerName
     * PlayerGender
     * ---
     * isAccountSaved (not an option, just there to keep track of)
     * MusicVolume
     * SFXVolume
     * ---
     * IPaddress
     * 
     */

    // Local Only
    public static string IP_ADDRESS = "";

    // Is Guest-Mode
    public static bool IS_GUEST_MODE;
    public static bool IS_REMEMBERING; // after login, saved credentials

    // connect
    public static string STUDENT_ID;
    public static string FIRST_NAME;
    public static string LAST_NAME;
    public static string USER_NAME;
    public static string PASSWORD;


    // local, used for saved login
    public static string LOCAL_STUDENT_ID;
    public static string LOCAL_USER_NAME;
    public static string LOCAL_PASSWORD;

    void Start()
    {
        //UnityWebRequest request = UnityWebRequest.Get("http://localhost/Explorer/home.html");
        IP_ADDRESS = "";
        StartCoroutine(Connect());
    }
 
    void Update(){}

    IEnumerator Connect() {
        WWWForm form = new WWWForm();

        // http://localhost/Explorer/game_script.php
        // http://192.168.1.8/Explorer/login_student.php

        string link = (IP_ADDRESS == "") ? ("http://localhost/Explorer/php%20stuffs/game_login_student.php") : ($"http://{IP_ADDRESS}/Explorer/php%20stuffs/game_login_student.php");

        using (UnityWebRequest www = UnityWebRequest.Post(link, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + www.error);
            }
            else
            {
                Debug.Log("Server Response: " + www.downloadHandler.text);
                ParseSQLResult(www.downloadHandler.text);
            }
        }
    }


    void ParseSQLResult(string data) {
        string[] lines = data.Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines) {
            string[] fields = line.Split(',');
            if (fields.Length >= 4) {
                string activity = fields[0];
                string quiz = fields[1];
                string exam = fields[2];
                string answer = fields[3];
                Debug.Log($"Activity: {activity}, Quiz: {quiz}, Exam: {exam}, Answer: {answer}");
            }
            
        }
    }


    public static void ParseQuestions(string data)
    {
        string[] lines = data.Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string[] fields = line.Split(',');
            if (fields.Length >= 5)
            {
                string index = fields[0];
                string activity = fields[1];
                string quiz = fields[2];
                string exam = fields[3];
                string answer = fields[4];
                //string tag = fields[5];
                Debug.Log($"Index: {index}, Activity: {activity}, Quiz: {quiz}, Exam: {exam}, Answer: {answer}");
            }
        }
    }


    // Check if its saved after logging successfully
    public void LoadLocalCredentials() {
        string getID, getPwd, getUser, getFirstName, getMidName, getLastName;

        getID = PlayerPrefs.GetString("AccountID");
        getPwd = PlayerPrefs.GetString("AccountPwd");
        getUser = PlayerPrefs.GetString("PlayerName");
        getFirstName = PlayerPrefs.GetString("AccountFN");
        getMidName = PlayerPrefs.GetString("AccountMN");
        getLastName = PlayerPrefs.GetString("AccountLN");

        if (getID == "") { // doesn't have any 
            SceneManager.LoadScene("Login");
        }
    }

    public void SaveLocalCredentials() {
        string getID, getPwd, getUser, getFirstName, getMidName, getLastName;

        
    }

    IEnumerator LoadCredentials()
    {
        WWWForm form = new WWWForm();

        // http://localhost/Explorer/game_script.php
        using (UnityWebRequest www = UnityWebRequest.Post("http://localhost/Explorer/login_student.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error: " + www.error);
            }
            else
            {
                Debug.Log("Server Response: " + www.downloadHandler.text);
                ParseSQLResult(www.downloadHandler.text);
            }
        }
    }



}
