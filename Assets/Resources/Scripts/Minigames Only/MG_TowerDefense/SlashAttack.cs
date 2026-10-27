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


    private void OnTriggerEnter(Collider other)
    {
        if (hit)
        {
            return;
        }

        if (!other.CompareTag("Enemy_TD"))
        {
            return;
        }

        Enemy_TD enemy = other.GetComponent<Enemy_TD>();

        if (enemy == null)
        {
            Debug.LogWarning("SlashAttack: Enemy_TD script missing!");
            return;
        }

        hit = true;

        bool isAlly = !enemy.isEvil;

        Debug.Log("SlashAttack hit: " + other.name);

        enemy.takeDamage();

        if (isAlly)
        {
            towerScript.towerHealth -= towerScript.allySlainDmg;
            towerScript.updateHpBar();

            Debug.LogWarning("You've hit an ALLY! OUCH!");
        }
        else
        {
            Debug.Log("You've hit an enemy.");

            GameObject sfxGah = Resources.Load<GameObject>(
                "Prefabs/Minigames Only/SFX_Gah"
            );

            if (sfxGah != null)
            {
                GameObject spawnedSFX = Instantiate(sfxGah);
                Destroy(spawnedSFX, 3f);
            }
        }
    }





}
