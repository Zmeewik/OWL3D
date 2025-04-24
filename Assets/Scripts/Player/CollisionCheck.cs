using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class CollisionCheck : MonoBehaviour
{
    [Header("Ground check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform groundCheck; // Пустой объект под игроком
    [SerializeField] private float groundDistance = 0.2f;
    [SerializeField] private float wallDistance = 0.5f;
    bool isGrounded;
    int groundContacts = 0;
    public Action<ContactPoint[]> OnGroundNormalChanged;
    public Action<Vector3> OnWallNormalChanged;

    //[Header("Wall check")]


    void FixedUpdate()
    {

    }


    //Collision detection


    //
    // void OnCollisionEnter(Collision collision)
    // {
    //     if((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
    //     {
    //         OnGroundNormalChanged(collision.contacts);
    //     }
    // }

    //
    void OnCollisionStay(Collision collision)
    {
        if ((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            OnGroundNormalChanged(collision.contacts);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if ((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            OnGroundNormalChanged(collision.contacts);
        }
    }

}
