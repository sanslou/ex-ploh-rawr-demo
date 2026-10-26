using UnityEngine;
using TMPro;
using System.Collections;

public class CodeLine : MonoBehaviour
{
    // Note: This class is responsible for an individual CodeLine object that the player must hit inside the game world.
    [Header("Code Line Data")]
    [SerializeField] private RepairSyntaxLineData lineData;

    [Header("Repair Feedback")]
    [SerializeField] private Color repairedColor = Color.green;
    [SerializeField] private float repairedColorDuration = 3f;

    // REFERENCES 
    private TMP_Text codeText;
    private Coroutine colorCoroutine;
    private Color defaultTextColor;

    private bool hasBeenHit;

    void Awake()
    {
        codeText = GetComponent<TMP_Text>();

        if (codeText != null)
        {
            defaultTextColor = codeText.color; // Remembers original color 
        }
        else
        {
            Debug.LogError($"CodeLine: No TextMeshPro component found on {gameObject.name}! (Is the TMP text on the root object?)");
        }
    }

    public void Initialize(RepairSyntaxLineData data) // Called by RSLineSpawner right after this line is spawned
    {
        lineData = data;
        hasBeenHit = false;

        if (codeText == null)
        {
            codeText = GetComponent<TMP_Text>();
        }

        // Shows the broken version if this line has a syntax error. Otherwise, show the original.
        if (codeText != null && lineData != null)
        {
            if (lineData.hasSyntaxError)
            {
                codeText.text = lineData.brokenCode;
            }
            else
            {
                codeText.text = lineData.originalCode;
            }
        }
    }

    public void ShowRepairedFeedback() // Turns the text green for a few seconds after the player fixes this line
    {
        if (codeText == null)
        {
            codeText = GetComponent<TMP_Text>();
        }

        if (codeText == null)
            return;

        if (colorCoroutine != null)
        {
            StopCoroutine(colorCoroutine);
        }

        colorCoroutine = StartCoroutine(RepairedColorRoutine());
    }

    IEnumerator RepairedColorRoutine()
    {
        codeText.color = repairedColor;

        yield return new WaitForSeconds(repairedColorDuration);

        codeText.color = defaultTextColor;
        colorCoroutine = null;
    }

    public void Hit() // Called when the player hits this line. Only counts once
    {
        if (hasBeenHit)
            return;

        RSManager manager = FindFirstObjectByType<RSManager>();

        if (manager == null)
        {
            Debug.LogError("CodeLine: RSManager was not found! (Is there an RSManager in the scene?)");
            return;
        }

        if (manager.IsRepairing)
        {
            return; // hasBeenHit stays false so this line can still be hit later
        }
            

        hasBeenHit = true;
        manager.HandleCodeLineHit(lineData);
    }

    public void SetCorrectedCode(string correctedCode) // Replaces the broken text with the fixed code
    {
        if (codeText == null)
        {
            codeText = GetComponent<TMP_Text>();
        }

        if (codeText == null)
            return;

        codeText.text = correctedCode;

        GameObject sfx_writing = Resources.Load<GameObject>("Prefabs/Minigames Only/SFX_Writing");
        GameObject spawnedSFX = Instantiate(sfx_writing);
        Destroy(spawnedSFX, 3f);
    }

    public bool IsUsingData(RepairSyntaxLineData data) // Lets RSRepairUI find which physical line matches its data
    {
        return lineData == data;
    }
}