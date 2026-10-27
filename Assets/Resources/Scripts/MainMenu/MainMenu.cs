using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.UI;
using TMPro;


public class MainMenu : MonoBehaviour
{
    private GameObject SoundHandler;
    private AudioSource audioSource;
    private Canvas settings;
    private Canvas main;
    private Canvas play;
    private SpriteRenderer genderSprite;
    private SpriteRenderer shadowblob;

    private bool isPlaying = false;

    // i know its spaghetti code because this script is being used from different GameObjects, but it works (and regret later)

    void Start()
    {
        
        if (PlayerPrefs.GetString("AccountID") == "")
        {
            DBC.IS_GUEST_MODE = true;

            // FIXES LACK OF GENDER BUG
            if (!PlayerPrefs.HasKey("PlayerGender"))
            {
                PlayerPrefs.SetString("PlayerGender", "N");
            }

            if (!PlayerPrefs.HasKey("PlayerName"))
            {
                PlayerPrefs.SetString("PlayerName", "");
            }

            PlayerPrefs.Save();
        }
        else {
            DBC.IS_GUEST_MODE = false;
        }

            string gender = PlayerPrefs.GetString("PlayerGender");
        Animator animator = GameObject.Find("Gender Sprite").GetComponent<Animator>();

             if (gender == "M") animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerMale_Controller");
        else if (gender == "F") animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerFemale_Controller");
        else                    animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerNonbinary_Controller");

        Debug.Log("MainMenu: Loaded gender = " + PlayerPrefs.GetString("PlayerGender", "N"));
    }
    
    void Update(){}

    void Awake()
    {
        //sound
        SoundHandler = GameObject.Find("SoundHandler");
        audioSource = SoundHandler.GetComponent<AudioSource>();

        //ui
        settings = GameObject.Find("SettingsCanvas").GetComponent<Canvas>();
        main = GameObject.Find("MainCanvas").GetComponent<Canvas>();
        play = GameObject.Find("ContinueCanvas").GetComponent<Canvas>();
        genderSprite = GameObject.Find("Gender Sprite").GetComponent<SpriteRenderer>();
        shadowblob = GameObject.Find("Shadow Blob").GetComponent<SpriteRenderer>();
    }

    // Cancel Tutorial/Continue Page
    public void ClickCancel() {
        genderSprite.gameObject.SetActive(true);
        main.enabled = true;
        play.enabled = false;
    }

    
    public void ClickPlay() {
        genderSprite.gameObject.SetActive(false);
        main.enabled = false;
        play.enabled = true;
    }

    public void ClickTutorial() {
        Debug.Log("Clicked Tutorial");
        //SceneManager.LoadScene("Tutorial");

    }

    public void ClickContinue()
    {
        Debug.Log("Clicked Continue");
        if (!isPlaying) {
            audioSource.Play();
            StartCoroutine(DramaticWait());
        }
    }

    private IEnumerator DramaticWait() //coroutine
    {
        Debug.Log("Waiting for 1 second before loading the scene...");
        audioSource.Play();
        yield return new WaitForSeconds(1f);
        SceneManager.LoadScene("InGame");
    }

    public void ClickProgress() {
        Debug.Log("Clicked Progress");
        audioSource.Play();
    }

    public void ClickQuit()
    {
        // research about how to make the app quit based on OS (Android/iOS/etc.)

        Debug.Log("Clicked Quit");
        audioSource.Play();
        Application.Quit(); // violates Apple's HCI, they prefer via home gestures
        // Activity.moveTaskToBack for Android

    }

    public void ClickSettings() {
        settings.enabled = true;
        initiateMainMenu(false);

        audioSource.Play();
        Debug.Log("Clicked Settings");
    }

    public void ResetPlayerPrefs()
    {
        ResetFlashcards();
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();

        Debug.Log("All PlayerPrefs have been reset!");
    }
    public static void ResetFlashcards()
    {
        PlayerPrefs.DeleteKey("EXplorer_Flashcards_v1");
        PlayerPrefs.Save();

        // Also reset the in-memory flashcard collection
        // using the actual field names in ExplorerFlashcardStore.
    }
    public void ClickBack() {
        audioSource.Play();
        settings.enabled = false;
        initiateMainMenu(true);

        Debug.Log("Returned to main menu");
    }

    public void ClickFlashcards()
    {
        Debug.Log("Clicked Flashcards");
        audioSource.Play();
    }


    public void ClickPlayer()
    {
        SceneManager.LoadScene("Player");
        audioSource.Play();
        Debug.Log("Edit Player");

    }

    public void playButtonAudio()
    {
        audioSource.Play();
    }

    public void initiateMainMenu(bool a)
    {
        main.enabled = a;
        genderSprite.enabled = a;
        shadowblob.enabled = a;
    }
}
