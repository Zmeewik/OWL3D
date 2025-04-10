using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Mechanism))]
public class Propeller : MonoBehaviour, IInteractable
{

    [SerializeField] float tossForce;
    [SerializeField] Transform rotatingHelix;
    [SerializeField] float rotationSpeed;

    public void Activate(Collider goal)
    {
        var goalRb = goal.GetComponent<Rigidbody>();
        goalRb.velocity = new Vector3(goalRb.velocity.x, 0f, goalRb.velocity.y);
        goalRb.AddForce(Vector3.up * tossForce, ForceMode.Impulse);
    }


    void FixedUpdate()
    {
        rotatingHelix.Rotate(0,rotationSpeed,0);
    }
}
