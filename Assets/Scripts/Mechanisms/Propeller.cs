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
        goalRb.velocity = new Vector3(goalRb.velocity.x, 0f, goalRb.velocity.z);
        var goalCoefficient = 1 / goalRb.mass * 80;
        goalRb.AddForce(Vector3.up * tossForce * goalRb.mass * goalCoefficient, ForceMode.Impulse);
    }


    void FixedUpdate()
    {
        rotatingHelix.Rotate(0,rotationSpeed,0);
    }
}
