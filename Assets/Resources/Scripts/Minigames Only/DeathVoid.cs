
using UnityEngine;
using UnityEngine.Events;
public class DeathVoid : MonoBehaviour
{
    [SerializeField] private GameObject player;

    private Transform playerTransform;
    private bool hasDied;
    [SerializeField] private GameObject resultsScreen;
    private Results resultsScript;
    [Header("Death Events")]
    [SerializeField] private UnityEvent onDeath;

    private void Start()
    {
        if (player == null)
        {
            Debug.LogError("DeathVoid: Player GameObject is not assigned in the inspector.");
            return;
        }

        playerTransform = player.transform;
        resultsScript = resultsScreen.GetComponent<Results>();
    }

    private void Update()
    {
        if (playerTransform == null || hasDied)
            return;

        if (playerTransform.position.y <= transform.position.y) // if player is beneath this game object's y position, commence death
        {
            hasDied = true;
            Debug.Log("Player has fallen into the void!");

            resultsScript.displayResults(0, 0, true);
            // Allow the player to restart the minigame.
            resultsScript.ShowRetry();

            onDeath?.Invoke();

            Time.timeScale = 0f; // Freeze
        }
    }


    

}
