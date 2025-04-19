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

    // //Cast raycast
    // public void WallRaycast(Vector3 moveVector)
    // {
    //     //Cast a ray from the playes in move vector and down
    //     moveVector = new Vector3(moveVector.x, 0, moveVector.z);
    //     Vector3 movement = Vector3.ProjectOnPlane(moveVector, Vector3.up).normalized;
    //     var dir = Vector3.down;

    //     //Ground detection with down vector
    //     if (Physics.Raycast(groundCheck.position, dir, out RaycastHit hitGround, groundDistance, groundLayer))
    //     {
    //         Debug.DrawRay(groundCheck.position, dir, Color.red, groundDistance);
    //         Debug.Log($"Hit ground layer: {hitGround.collider.name} in direction {dir}");
    //         OnGroundNormalChanged?.Invoke(hitGround.normal);
    //     }
    //     else
    //         OnGroundNormalChanged?.Invoke(Vector3.zero);
        
    //     //Wall detection with move vector
    //     if (Physics.Raycast(groundCheck.position, movement, out RaycastHit hitWall, wallDistance, groundLayer))
    //     {
    //         Debug.DrawRay(groundCheck.position, movement, Color.green, wallDistance);
    //         Debug.Log($"Hit wall layer: {hitWall.collider.name} in direction {movement}");
    //         OnWallNormalChanged?.Invoke(hitWall.normal);
    //     }
    //     else
    //         OnWallNormalChanged?.Invoke(Vector3.zero);
    // }


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
        if((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            OnGroundNormalChanged(collision.contacts);
        }
    }

        void OnCollisionExit(Collision collision)
    {
        if((groundLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            OnGroundNormalChanged(collision.contacts);
        }
    }

}
