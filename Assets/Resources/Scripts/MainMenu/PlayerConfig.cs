using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerConfig : MonoBehaviour
{

    Animator maleAnim, femaleAnim, nonBinaryAnim;
    SpriteRenderer maleMarker, femaleMarker, nonbinaryMarker;
    SpriteRenderer shad1, shad2, shad3;
    void Start()
    {
        maleAnim = GameObject.Find("Select Male").GetComponentInChildren<Animator>();
        femaleAnim = GameObject.Find("Select Female").GetComponentInChildren<Animator>();
        nonBinaryAnim = GameObject.Find("Select Nonbinary").GetComponentInChildren<Animator>();
        maleMarker = transform.GetChild(6).GetComponent<SpriteRenderer>();
        femaleMarker = transform.GetChild(7).GetComponent<SpriteRenderer>();
        nonbinaryMarker = transform.GetChild(8).GetComponent<SpriteRenderer>();
        shad1 = transform.GetChild(9).GetComponent<SpriteRenderer>();
        shad2 = transform.GetChild(10).GetComponent<SpriteRenderer>();
        shad3 = transform.GetChild(11).GetComponent<SpriteRenderer>();

        maleMarker.enabled = PlayerPrefs.GetString("PlayerGender") == "M";
        femaleMarker.enabled = PlayerPrefs.GetString("PlayerGender") == "F";
        nonbinaryMarker.enabled = PlayerPrefs.GetString("PlayerGender") != "M" && PlayerPrefs.GetString("PlayerGender") != "F";
        shad1.enabled = PlayerPrefs.GetString("PlayerGender") == "F";
        shad2.enabled = PlayerPrefs.GetString("PlayerGender") == "M";
        shad3.enabled = PlayerPrefs.GetString("PlayerGender") != "M" && PlayerPrefs.GetString("PlayerGender") != "F";


        //maleAnim.runtimeAnimatorController = (PlayerPrefs.GetString("PlayerGender") == "M") ? (Resources.Load<RuntimeAnimatorController>("Animations/PlayerMale_Controller")) : (null);
        //femaleAnim.runtimeAnimatorController = (PlayerPrefs.GetString("PlayerGender") == "F") ? (Resources.Load<RuntimeAnimatorController>("Animations/PlayerFemale_Controller")) : (null);
        //nonBinaryAnim.runtimeAnimatorController = (PlayerPrefs.GetString("PlayerGender") != "M" && PlayerPrefs.GetString("PlayerGender") != "F") ? (Resources.Load<RuntimeAnimatorController>("Animations/PlayerNonbinary_Controller")) : (null);

        // FIXES GENDER BUG THAT DISALLOWS US TO CHANGE
        string selectedGender = PlayerPrefs.GetString("PlayerGender", "N");

        maleAnim.runtimeAnimatorController = null;
        femaleAnim.runtimeAnimatorController = null;
        nonBinaryAnim.runtimeAnimatorController = null;

        if (selectedGender == "M")
        {
            maleAnim.runtimeAnimatorController =
                Resources.Load<RuntimeAnimatorController>(
                    "Animations/PlayerMale_Controller");
        }
        else if (selectedGender == "F")
        {
            femaleAnim.runtimeAnimatorController =
                Resources.Load<RuntimeAnimatorController>(
                    "Animations/PlayerFemale_Controller");
        }
        else
        {
            nonBinaryAnim.runtimeAnimatorController =
                Resources.Load<RuntimeAnimatorController>(
                    "Animations/PlayerNonbinary_Controller");
        }

    }

    // Update is called once per frame
    void Update() {}

    public void ClickBack() {
        SceneManager.LoadScene("MainMenu");
    }

    public void SelectMale() {
        //Debug.Log("Male Selected!");
        PlayerPrefs.SetString("PlayerGender", "M");
        PlayerPrefs.Save();

        maleAnim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerMale_Controller");
        femaleAnim.runtimeAnimatorController = null;
        nonBinaryAnim.runtimeAnimatorController = null;
        maleMarker.enabled = true;
        femaleMarker.enabled = false;
        nonbinaryMarker.enabled = false;
        shad1.enabled = false;
        shad2.enabled = true;
        shad3.enabled = false;
    }

    public void SelectFemale()
    {
        //Debug.Log("Female Selected!");
        PlayerPrefs.SetString("PlayerGender", "F");
        PlayerPrefs.Save();
        maleAnim.runtimeAnimatorController = null;
        femaleAnim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerFemale_Controller");
        nonBinaryAnim.runtimeAnimatorController = null;
        maleMarker.enabled = false;
        femaleMarker.enabled = true;
        nonbinaryMarker.enabled = false;
        shad1.enabled = true;
        shad2.enabled = false;
        shad3.enabled = false;
    }

    public void SelectNonbinary()
    {
        //Debug.Log("Nonbinary Selected!");
        PlayerPrefs.SetString("PlayerGender", "N");
        PlayerPrefs.Save();

        maleAnim.runtimeAnimatorController = null;
        femaleAnim.runtimeAnimatorController = null;
        nonBinaryAnim.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerNonbinary_Controller");
        maleMarker.enabled = false;
        femaleMarker.enabled = false;
        nonbinaryMarker.enabled = true;
        shad1.enabled = false;
        shad2.enabled = false;
        shad3.enabled = true;
    }

}
