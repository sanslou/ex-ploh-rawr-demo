
using UnityEngine;

public class ButtonSmash : MonoBehaviour
{
    [Header("Sledgehammer VFX")]
    [SerializeField] private GameObject smashPrefab;

    [Header("Player References")]
    [SerializeField] private Transform playerSprite;
    [SerializeField] private Transform attackPivot;

    [Header("VFX Position Spawn")]
    [SerializeField] private Vector3 vfxPos = new Vector3(0.5f, 0.0f, 0.02f);

    [Header("VFX Settings")]
    [SerializeField, Min(0.1f)] private float vfxLifetime = .217f;

    public void Smash()
    {
        if (smashPrefab == null)
        {
            Debug.LogWarning("ButtonSmash: Smash Prefab is not assigned.", this);
            return;
        }

        Transform player = GameObject.FindWithTag("Player")?.transform;

        if (player == null)
        {
            Debug.LogError("ButtonSmash: Player tag not found.", this);
            return;
        }

        
        if (playerSprite == null)
        {
            playerSprite = player.Find("Player Sprite"); 
        }
            

        if (attackPivot == null)
        {
            attackPivot = player.Find("AttackPivot");
        }

        Transform parent;

        if (attackPivot != null) // Based on attackpivot within player model
        {
            parent = attackPivot;
        }
        else
        {
            parent = player;
        }
        GameObject spawnedVFX = Instantiate(smashPrefab);
        GameObject sfx_swing = Resources.Load<GameObject>("Prefabs/Minigames Only/SFX_MCSwordHeavy");
        GameObject spawnedSFX = Instantiate(sfx_swing);

        spawnedVFX.transform.SetParent(parent, false); // Set parent to attack pivot.

        
        spawnedVFX.transform.localPosition = new Vector3(  // Positions the vfx. adjust xyz accordingly for polishing.
            vfxPos.x,
            vfxPos.y,
            vfxPos.z
        );

       
        spawnedVFX.transform.localRotation = Quaternion.identity;  // Makes it so that there is a lack of movement-based rotation.  

        Destroy(spawnedVFX, vfxLifetime);
        Destroy(spawnedSFX, 3f);
    }
}
