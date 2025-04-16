using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class CollisionCheck : MonoBehaviour
{
    [Header("Ground check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck; // Пустой объект под игроком
    [SerializeField] private float groundRadius = 0.3f;
    bool isGrounded;
    int groundContacts = 0;
    public event Action OnGrounded;
    public event Action OnNotGrounded;
    public Action<ContactPoint[]> OnGroundNormalChanged;

    //[Header("Wall check")]


    void FixedUpdate()
    {
        GroundCheck();
    }

    //Check for a ground touch
    void GroundCheck()
    {
        //Check for a ground collider in point radius
        var wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, groundRadius, groundLayer);

        //If ground (not) found first time call event
        if(isGrounded != wasGrounded)
        {
            if(isGrounded)
                OnGrounded?.Invoke();
            else
                OnNotGrounded?.Invoke();
        }
    }

    //Cast raycast
    public void WallRaycast()
    {
        // Cast a ray from the camera into the scene
        // Vector3 movement = Vector3.ProjectOnPlane(moveVector, groundNormal).normalized;

        // Ray ray = ;
        // RaycastHit hit;
        // //Vector3 targetDirection = Vector3.forward;
        // Vector3 targetDirection = ray.direction;
        // Vector3 hitPosition = Vector3.zero;
        // var hit = Physics.Raycast();
    }

    void OnCollisionEnter(Collision collision)
    {
        if(((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            groundContacts++;
            OnGroundNormalChanged?.Invoke(collision.contacts);
        }
    }

    //Send action about current colisions with ground
    void OnCollisionStay(Collision collision)
    {
        if(((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            OnGroundNormalChanged?.Invoke(collision.contacts);
        }
    }

    //Send action when exit wall
    void OnCollisionExit(Collision collision)
    {
        if(((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            groundContacts--;
            //print(groundContacts);
            OnGroundNormalChanged?.Invoke(collision.contacts);
        }
    }

}
