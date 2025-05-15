using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class CollisionCheck : MonoBehaviour
{
    [Header("Ground check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private LayerMask objectLayer;
    [SerializeField] private Transform groundCheck; // Пустой объект под игроком
    [SerializeField] private float groundDistance = 0.2f;
    [SerializeField] private float wallDistance = 0.5f;
    bool isGrounded;
    int groundContacts = 0;
    public Action<ContactPoint[]> OnGroundNormalChanged;
    public Action<ContactPoint[]> OnObjectNormalChanged;
    public Action<Vector3> OnWallNormalChanged;

    //[Header("Wall check")]


    void FixedUpdate()
    {

    }


    //Collision detection
    public bool RaycastGround(Vector3 groundNormal)
    {
        Debug.DrawRay(groundCheck.position, -groundNormal * groundDistance, Color.black, 0.5f);
        if(Physics.Raycast(groundCheck.position, -groundNormal, groundDistance, groundLayer))
        {
            return true;
        }
        return false;
    }

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
        else if((objectLayer.value & (1 << collision.gameObject.layer))!= 0)
        {
            OnObjectNormalChanged(collision.contacts);
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if ((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            OnGroundNormalChanged(collision.contacts);
        }
        else if((objectLayer.value & (1 << collision.gameObject.layer))!= 0)
        {
            OnObjectNormalChanged(collision.contacts);
        }
    }

}
