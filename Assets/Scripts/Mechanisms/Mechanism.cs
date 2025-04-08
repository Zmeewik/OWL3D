using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Mechanism : MonoBehaviour
{

    //Type of interaction
    enum Interaction {Trigger, Lever, Wheel};
    [SerializeField] Interaction interaction;
    [SerializeField] GameObject actionImplemention;
    IInteractable mechanismScript;


    void Start()
    {
        var implementScr = actionImplemention.GetComponent<IInteractable>();
        mechanismScript = implementScr;
        var colComponent = actionImplemention.GetComponent<Collider>();
        
        //Change states of interaction and disable unneaded
        switch(interaction)
        {
            case Interaction.Trigger:
                if(colComponent is not null)
                    colComponent.enabled = true;
            break;
            case Interaction.Lever:
                if(colComponent is not null)
                    colComponent.enabled = false;
                break;
        }
    }


    void OnTriggerEnter(Collider other)
    {
        mechanismScript.Activate(other);
    }

}
