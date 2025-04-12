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


    //Send action about current colisions with ground
    void OnCollisionStay(Collision collision)
    {
        if(((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            OnGroundNormalChanged?.Invoke(collision.contacts);
        }
    }

}
