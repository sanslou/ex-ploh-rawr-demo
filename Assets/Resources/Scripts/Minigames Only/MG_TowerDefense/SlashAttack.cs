using UnityEngine;

public class SlashAttack : MonoBehaviour
{
    // This script is attached to the SlashAttack prefab, which is spawned when the player slashes an enemy. It detects collisions with enemies and destroys them.

    private bool hit;
    private Enemy_TD enemyScript;
    private GameObject target;
    private Tower towerScript;
    
    void Start()
    {
        hit = false;
        towerScript = GameObject.FindWithTag("Tower").GetComponent<Tower>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy_TD"))   
        {
            if (hit) { return; }
            Debug.Log("SlashAttack hit an Enemy_TD! " + other.name);
            target = other.gameObject;
            enemyScript = target.GetComponent<Enemy_TD>();

            enemyScript.takeDamage(); // Call the TakeDamage method on the Enemy_TD script
            

            if (!enemyScript.isEvil && hit)
            {
                towerScript.towerHealth -= towerScript.allySlainDmg; // the tower takes damage as punishment
                towerScript.updateHpBar();
                Debug.LogWarning("You've hit an ALLY! OUCH!");
            }
            else
            {
                Debug.Log("You've hit an enemy.");
                GameObject sfx_gah = Resources.Load<GameObject>("Prefabs/Minigames Only/SFX_Gah");
                GameObject spawnedSFX = Instantiate(sfx_gah);
                Destroy(spawnedSFX, 3f);
            }
            hit = true;
        }
    }

    


}
