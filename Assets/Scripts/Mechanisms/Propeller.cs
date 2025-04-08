using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Propeller : MonoBehaviour, IInteractable
{


    public void Activate(Collider goal)
    {
        var goalRb = other.GetComponent<Rigidbody>();
    }


}
