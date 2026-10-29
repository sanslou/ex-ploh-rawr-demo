using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ExplainerInteract : NPC
{
    [SerializeField]
    [TextArea(2,5)]
    public List<String> dialogue = new List<String>();
    [SerializeField] private float nextDialogueCooldown = 2.5f;
    private int index;
    private float nextDialogueTime;
    new void Start()
    {
        //npcSprite = GetComponent<SpriteRenderer>();
        //animator = GetComponent<Animator>();

        SpriteRenderer explainerSprite = transform.Find("Sprite").GetComponent<SpriteRenderer>();
        explainerSprite.GetComponent<Animator>().runtimeAnimatorController = animatorController;
    }
    new void Update() {
        transform.rotation = Quaternion.LookRotation(PlayerInteract.PLAYER_CAMERA.transform.forward);
    }

    public override void Interact()
    {
        base.Interact();

        // First-Line
        index = 0;
        Speak(dialogue[index], true);
        nextDialogueTime = Time.unscaledTime + nextDialogueCooldown;
        //Debug.Log(index);

        PlayerInteract.UI_INTERACT.gameObject.SetActive(false);
        PlayerInteract.UI_DIALOGUE_PREV.gameObject.SetActive(false);
        PlayerInteract.UI_DIALOGUE_NEXT.gameObject.SetActive(true);
    }


    public void SayNext() {
        if (Time.unscaledTime < nextDialogueTime)
        {
            return;
        }
        if (index >= dialogue.Count - 1) { return; }

        index++;
        Speak(dialogue[index], true);
        nextDialogueTime = Time.unscaledTime + nextDialogueCooldown;

        if (index >= dialogue.Count - 1)
        {
            PlayerInteract.UI_DIALOGUE_NEXT.gameObject.SetActive(false);
            PlayerInteract.UI_DIALOGUE_PREV.gameObject.SetActive(false);
            PlayerInteract.UI_INTERACT.gameObject.SetActive(true);
            PlayerInteract.UI_JOYSTICK.gameObject.SetActive(true);
        }
        else
        {
            PlayerInteract.UI_DIALOGUE_PREV.gameObject.SetActive(true);
        }

        //Debug.Log(index);
    }

    public void SayPrev()
    {
        if (Time.unscaledTime < nextDialogueTime)
        {
            return;
        }
        if (index == 0) { return; }

        index--;
        Speak(dialogue[index], true);
        nextDialogueTime = Time.unscaledTime + nextDialogueCooldown;

        //Debug.Log(index);

        if (index == 0)
        {
            PlayerInteract.UI_DIALOGUE_PREV.gameObject.SetActive(false);
        }
        if (index <= dialogue.Count - 1) {
            PlayerInteract.UI_DIALOGUE_NEXT.gameObject.SetActive(true);
        }
    }
}
