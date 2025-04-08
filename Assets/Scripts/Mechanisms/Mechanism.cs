using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Mechanism : MonoBehaviour
{

    //Type of interaction
    enum Interaction {Trigger, Lever, Wheel};
    [SerializeField] Interaction interaction;
    [SerializeField] GameObject actionImplemention;


    void Start()
    {
        var implementScr = actionImplemention.GetComponent<IInteractable>();
        switch(interaction)
        {
            case Interaction.Trigger:
                
            break;
        }
    }



}
