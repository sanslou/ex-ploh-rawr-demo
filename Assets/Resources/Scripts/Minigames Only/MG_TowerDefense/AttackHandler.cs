using UnityEngine;
using UnityEngine.UI;

public class AttackHandler : MonoBehaviour
{
    // This script is attached to the player and handles the attack logic. It enables the attack button when the tower defense minigame is ongoing,
    // and calls the slash() method when the button is pressed.

    [Header("References")]
    [SerializeField] private Transform playerChest;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private MobileController mobileController;

    private Tower towerScript;
    private Enemy_TD enemyScript;
    private Button buttonAttack;

    [SerializeField] private float slashOffset;
    private Vector3 lastMoveDirection = Vector3.forward;

    [SerializeField] private Transform attackPivot;
    

    void Start()
    {
        buttonAttack = GameObject.FindGameObjectWithTag("UI_Attack").GetComponent<Button>();
        towerScript = FindObjectOfType<Tower>();
        slashOffset = 1.5f;
    }

    

    private void Update()
    {
        if (mobileController == null)
            return;

        Vector3 input = new Vector3(
            mobileController.Horizontal(),
            0f,
            mobileController.Vertical()
        );

        if (input.sqrMagnitude > 0.01f)
            lastMoveDirection = input.normalized;
    }


    void OnTriggerStay(Collider other) { 
        if (other.CompareTag("Enemy_TD") && towerScript.isOngoing) {   // Why is it OnTriggerEnter() and not put it in Start()? This keeps the slash system from multiplying its attack and keep only one.
            enemyScript = other.GetComponent<Enemy_TD>(); 
            buttonAttack.interactable = true; 
            buttonAttack.onClick.AddListener(slash); 
            Debug.Log("ONTRIGGERENTER: Tower Defense minigame is ongoing. Attack button enabled."); 
        } 
    }


    public void slash()
    {
        GameObject vfx =
            Resources.Load<GameObject>("Prefabs/Minigames Only/VFX_DefaultSlash");

        GameObject sfx =
            Resources.Load<GameObject>("Prefabs/Minigames Only/SFX_MCSword");

        Transform player = GameObject.FindWithTag("Player").transform;

        playerChest = player.Find("Player Sprite");
        attackPivot = player.Find("AttackPivot");

        if (attackPivot == null)
        {
            Debug.LogError("AttackPivot not found!");
            return;
        }

        // SLASH ROTATION

        float angle = 0f;

        if (lastMoveDirection.x > 0 && lastMoveDirection.y > 0)
            angle = 45f;       // NE
        else if (lastMoveDirection.x > 0 && lastMoveDirection.y < 0)
            angle = 135f;      // SE
        else if (lastMoveDirection.x < 0 && lastMoveDirection.y < 0)
            angle = 225f;      // SW
        else if (lastMoveDirection.x < 0 && lastMoveDirection.y > 0)
            angle = 315f;      // NW
        else if (lastMoveDirection.x > 0)
            angle = 90f;       // E
        else if (lastMoveDirection.x < 0)
            angle = 270f;      // W
        else if (lastMoveDirection.y > 0)
            angle = 0f;        // N
        else if (lastMoveDirection.y < 0)
            angle = 180f;      // S

        // SPAWN VFX

        GameObject spawnedVFX = Instantiate(vfx);

        // Make VFX follow AttackPivot
        spawnedVFX.transform.SetParent(attackPivot, false);

        // Position relative to AttackPivot
        spawnedVFX.transform.localPosition = new Vector3(
            lastMoveDirection.x * slashOffset,
            0f,
            lastMoveDirection.y * slashOffset
        );

        // Rotate relative to AttackPivot
        spawnedVFX.transform.localEulerAngles = new Vector3(
            90f,
            angle,
            0f
        );

        // SPAWN SFX

        GameObject spawnedSFX = Instantiate(
            sfx
        );

        Destroy(spawnedVFX, 0.12f);
        Destroy(spawnedSFX, 2f);
    }



}
