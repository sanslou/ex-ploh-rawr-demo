using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IPconfig : MonoBehaviour
{
    TMP_InputField inputIP;

    void Start()
    {
        DBC.IP_ADDRESS = PlayerPrefs.GetString("IPaddress");
        inputIP = GameObject.Find("InputField IP").GetComponent<TMP_InputField>();
        inputIP.text = DBC.IP_ADDRESS;
    }

    void Update(){}

    public void ButtonCancel() {
        DBC.IP_ADDRESS = "";
        SceneManager.LoadScene("Login");
    }


    public void ButtonSave() {
        DBC.IP_ADDRESS = inputIP.text;
        PlayerPrefs.SetString("IPaddress", inputIP.text);
        SceneManager.LoadScene("Login");
    }
}
