using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DescriptorInteract : NPC
{
    [SerializeField]
    [TextArea(2,5)]
    public List<String> dialogue = new List<String>();

    private int index;
    
    new void Start() {
        //npcSprite = GetComponent<SpriteRenderer>();
        //animator = GetComponent<Animator>();

        SpriteRenderer explainerSprite = transform.Find("Sprite").GetComponent<SpriteRenderer>();
        explainerSprite.GetComponent<Animator>().runtimeAnimatorController = animatorController;
    }
    new void Update() { base.Update(); }

    public override void Interact()
    {
        base.Interact();

        if (index >= dialogue.Count) index = 0;
        Speak(dialogue[index]);
        index++;
    }


}
