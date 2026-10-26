using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerInteract : MonoBehaviour
{
    public static GameObject PLAYER;
    public static string GENDER;

    [SerializeField] private bool isExplorationScene = false;


    private GameObject target;
    private NPC NPCscript;
    private bool hasTriggered = false;
    
    private Button buttonInteract;
    private TMP_InputField inputField;

    // <!Static>
    public static Button UI_INTERACT;
    public static TMP_InputField UI_DIALOGUE;
    public static Image UI_JOYSTICK;
    public static Camera PLAYER_CAMERA;
    public static Button UI_DIALOGUE_PREV, UI_DIALOGUE_NEXT;
    // </Static>


    // <Movement>
    public float SPEED = 5.0f;
    public bool isMirrored;
    Vector3 moveVector;
    public CharacterController characterController;
    public MobileController mobileController;
    public Animator animator;
    // </Movement>

    void Start()
    {
        PlayerInteract.PLAYER = gameObject;

        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (mobileController == null)
        {
            mobileController = GetComponent<MobileController>();

            if (mobileController == null)
            {
                mobileController = FindObjectOfType<MobileController>();
            }
        }

        GENDER = PlayerPrefs.GetString("PlayerGender");
        animator = transform.GetChild(1).GetComponent<Animator>();
       

             if (GENDER == "M") animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerMale_Controller");      //Debug.Log("MALE!!");
        else if (GENDER == "F") animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerFemale_Controller");    //Debug.Log("FEMALE!!");
        else                    animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Animations/PlayerNonbinary_Controller"); //Debug.Log("NONBINARY!!");

        if (SceneManager.GetActiveScene().name == "InGame")
        {
            float x = PlayerPrefs.GetFloat("PlayerX");
            float y = PlayerPrefs.GetFloat("PlayerY");
            float z = PlayerPrefs.GetFloat("PlayerZ");
            if (y < 0) { x = 0; y = 0; z = 0; }
            transform.position = new Vector3(x, y, z);
        }

        // only in Non-Minigame, InGame-Scene

        if (SceneManager.GetActiveScene().name == "InGame" ||
    SceneManager.GetActiveScene().name == "Tutorial" ||
    SceneManager.GetActiveScene().name == "LTV2")
        {

            string[] requiredTags =
{
    "UI_Interact",
    "UI_Dialogue",
    "UI_Joystick",
    "MainCamera",
    "UI_DialoguePrev",
    "UI_DialogueNext",
    "Canvas_Player"
};

            foreach (string tagName in requiredTags)
            {
                GameObject found = GameObject.FindGameObjectWithTag(tagName);

                if (found == null)
                {
                    Debug.LogError("PlayerInteract: Missing GameObject with tag: " + tagName);
                }
                else
                {
                    Debug.Log("PlayerInteract: Found " + tagName + " -> " + found.name);
                }
            }

            string[] requiredObjects =
            {
    "Terminal Multiple",
    "Terminal Identify",
    "Terminal Trivia",
    "Terminal Ready",
    "Terminal Results"
};

            foreach (string objectName in requiredObjects)
            {
                GameObject found = GameObject.Find(objectName);

                if (found == null)
                {
                    Debug.LogError("PlayerInteract: Missing GameObject: " + objectName);
                }
            }

            buttonInteract = GameObject.FindGameObjectWithTag("UI_Interact").GetComponent<Button>();
            inputField = GameObject.FindGameObjectWithTag("UI_Dialogue").GetComponent<TMP_InputField>();

            PlayerInteract.UI_INTERACT = buttonInteract;
            PlayerInteract.UI_DIALOGUE = inputField;
            PlayerInteract.UI_JOYSTICK = GameObject.FindGameObjectWithTag("UI_Joystick").GetComponent<Image>();
            VerifyPlayerMainCamera();

            PlayerInteract.UI_DIALOGUE_PREV = GameObject.FindGameObjectWithTag("UI_DialoguePrev").GetComponent<Button>();
            PlayerInteract.UI_DIALOGUE_NEXT = GameObject.FindGameObjectWithTag("UI_DialogueNext").GetComponent<Button>();

            //GameObject.FindGameObjectWithTag("Canvas_Terminal").GetComponent<Canvas>().enabled = false;

            // do we need terminals at the Tutorial-Scene? if so, copy from the InGame-Scene
            if (SceneManager.GetActiveScene().name == "InGame" || SceneManager.GetActiveScene().name == "LTV2")
            {
                GameObject.Find("Terminal Multiple").GetComponent<Canvas>().enabled = false;
                GameObject.Find("Terminal Identify").GetComponent<Canvas>().enabled = false;
                GameObject.Find("Terminal Trivia").GetComponent<Canvas>().enabled = false;
                GameObject.Find("Terminal Ready").GetComponent<Canvas>().enabled = false;
                GameObject.Find("Terminal Results").GetComponent<Canvas>().enabled = false;
                PlayerInteract.UI_DIALOGUE_PREV.gameObject.SetActive(false);
                PlayerInteract.UI_DIALOGUE_NEXT.gameObject.SetActive(false);
            }
        }
        
        GameObject.FindGameObjectWithTag("Canvas_Player").GetComponent<Canvas>().enabled = true;

    }

    void Update()
    {

        if (PLAYER_CAMERA != null) // FIX: keeps player movement active when player switches to another scene.
        {
            Transform playerSprite = transform.Find("Player Sprite");

            if (playerSprite != null)
            {
                playerSprite.rotation = Quaternion.LookRotation(PLAYER_CAMERA.transform.forward);
            }
        }

        CharacterMove();
        //Debug.Log(target);
    }

    private void VerifyPlayerMainCamera()
    {
        PlayerInteract.PLAYER_CAMERA = null;
        GameObject[] mainCameras = GameObject.FindGameObjectsWithTag("MainCamera");

        if (mainCameras.Length > 1)
        {
            Debug.LogError(
                "PlayerInteract: Multiple active GameObjects have the MainCamera tag! Found "
                + mainCameras.Length + ". Only the gameplay camera should use this tag."
            );
        }

        if (mainCameras.Length == 0)
        {
            Debug.LogError("PlayerInteract: No active MainCamera found!");
        }
        else
        {
            GameObject selectedCamera = null;

            foreach (GameObject cameraObject in mainCameras)
            {
                if (cameraObject.name == "Main Camera")
                {
                    selectedCamera = cameraObject;
                    break;
                }
            }

            if (selectedCamera == null)
            {
                selectedCamera = mainCameras[0];
            }

            PlayerInteract.PLAYER_CAMERA = selectedCamera.GetComponent<Camera>();

            if (PlayerInteract.PLAYER_CAMERA == null)
            {
                Debug.LogError("PlayerInteract: Selected MainCamera has no Camera component!");
            }
        }
    }
    private void CharacterMove()
    {
        moveVector = Vector3.zero;
        if (mobileController != null)
        {
            moveVector.x = mobileController.Horizontal() * SPEED;
            moveVector.z = mobileController.Vertical() * SPEED;
        }

        if (characterController != null)
        {
            characterController.SimpleMove(moveVector); 
        }

        //characterController.Move(moveVector * Time.deltaTime); // without gravity
        //characterController.SimpleMove(moveVector); // with gravity

        // Save location
        if (SceneManager.GetActiveScene().name == "InGame") // saves the position ONLY inside ingame
        {
            PlayerPrefs.SetFloat("PlayerX", transform.position.x);
            PlayerPrefs.SetFloat("PlayerY", transform.position.y);
            PlayerPrefs.SetFloat("PlayerZ", transform.position.z);
            //PlayerPrefs.Save();
        }

        Transform playerSprite = transform.Find("Player Sprite");

        if (playerSprite != null)
        {
            SpriteRenderer spriteRenderer =
                playerSprite.GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                if (moveVector.x > 0)
                {
                    isMirrored = false;
                }
                else if (moveVector.x < 0)
                {
                    isMirrored = true;
                }

                spriteRenderer.flipX = isMirrored;
            }
        }

        if (animator != null)
        {
            if (moveVector.x != 0 || moveVector.z != 0)
            {
                animator.SetBool("isMoving", true);
            }
            else
            {
                animator.SetBool("isMoving", false);
            }
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("NPC"))
        {
            return;
        }

        if (hasTriggered)
        {
            return;
        }

        hasTriggered = true;
        //Debug.Log(other);

        if (other.gameObject.CompareTag("NPC") && target == null)
        {
            target = other.gameObject;
            //Debug.Log(target.name);
            NPCscript = target.GetComponent<NPC>();
            if (target.GetComponent<NPC>() != null) NPCscript = target.GetComponent<NPC>();

            buttonInteract.onClick.AddListener(NPCscript.Interact);
            buttonInteract.interactable = true;

            // When it's Explainer; important NPC-type
            if (NPCscript.GetType() == typeof(ExplainerInteract)) {
                Button interact = GameObject.FindGameObjectWithTag("UI_Interact").GetComponent<Button>();
                interact.enabled = true;

                UI_DIALOGUE_PREV.onClick.AddListener( ((ExplainerInteract) NPCscript).SayPrev);
                UI_DIALOGUE_NEXT.onClick.AddListener( ((ExplainerInteract) NPCscript).SayNext);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (NPCscript == null)
        {
            return;
        }

        if (other.gameObject != target)
        {
            return;
        }

        if (NPCscript.GetType() == typeof(ExplainerInteract))
        {
            UI_DIALOGUE_PREV.onClick.RemoveListener(((ExplainerInteract) NPCscript).SayPrev);
            UI_DIALOGUE_NEXT.onClick.RemoveListener(((ExplainerInteract) NPCscript).SayNext);
            UI_INTERACT.gameObject.SetActive(true);

            UI_DIALOGUE_PREV.gameObject.SetActive(false);
            UI_DIALOGUE_NEXT.gameObject.SetActive(false);
        }


        target = null;
        buttonInteract.interactable = false;

        if (NPCscript != null) buttonInteract.onClick.RemoveListener(NPCscript.Interact);
        NPCscript = null;
        hasTriggered = false;

        var dialogue = GameObject.Find("Dialogue Text");
        var content = dialogue.GetComponent<TMP_InputField>();
        content.text = "";
        var image = dialogue.GetComponent<Image>();
        image.enabled = false;
        content.enabled = false;

    }

    public void ForceExit()
    {         // This function is called by the Teleporter script to force the player to exit an NPC interaction
        target = null;
        buttonInteract.interactable = false;
        if (NPCscript != null)
        {
            buttonInteract.onClick.RemoveListener(NPCscript.Interact);
        }
        NPCscript = null;
        hasTriggered = false;
        var dialogue = GameObject.Find("Dialogue Text");
        var content = dialogue.GetComponent<TMP_InputField>();
        content.text = "";
        var image = dialogue.GetComponent<Image>();
        image.enabled = false;
        content.enabled = false;
    }


    public void ForceSpeak(string msg) {
        PlayerInteract.UI_DIALOGUE.text = msg;
    }

    public void ToggleInteraction(bool here) {
        PlayerInteract.UI_INTERACT.gameObject.SetActive(here);
    }

    public void TogglePlayerUI(bool here) {
        
    }


}
