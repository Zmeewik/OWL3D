using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider))]
public class PlayerMovement : MonoBehaviour, IMovable, IRotatable
{


    [Header("References")]
    [SerializeField] Rigidbody rb;
    [SerializeField] Transform front;
    [SerializeReference] Transform cameraFront;
    
    [Header("Rotation")]
    [SerializeField] float speedRotation;
    [SerializeField] float sensitivity;
    public float Sensitivity => sensitivity;

    [Header("Movement")]
    [SerializeField] float acceleration;
    [SerializeField] float maxSpeed;
    [SerializeField] public float dragOnGround;
    public float airControlMultiplier;

    [Header("Jump")]
    [SerializeField] float jumpForce;


    [Header("Dash")]
    

    //Vectors of body
    Vector3 frontDirection = Vector3.forward;
    Vector3 rightDirection = Vector3.right;


    //Move handle
    Vector2 moveVector = Vector2.zero;



    //Rotation handle
    float xRotation;
    Vector2 rotationVector = Vector2.zero;



    //Bool
    enum IsGrounded {Grounded, InAir};
    IsGrounded isGrounded = IsGrounded.Grounded;

    
    //State handle
    enum BodyState {Moving, Dashing, WallRunning, AirMoving, Crouching, Sliding};
    BodyState currentState = BodyState.Moving;

    private void FixedUpdate()
    {
        switch(currentState)
        {
            case BodyState.Moving:
                Moving();
                ApplyDrag();
            break;
            case BodyState.Dashing:
            break;
            case BodyState.WallRunning:
            break;
            case BodyState.AirMoving:
            break;
            case BodyState.Crouching:
            break;
            case BodyState.Sliding:
            break;
        }
    }

    private void LateUpdate()
    {
        RotateBody();
    }

    //Movement states
    void Moving()
    {

        //Handle movement of player
        //Adding force to object until reaching max speed
        if(moveVector != Vector2.zero)
        {
            //Speed limit
            Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (horizontalVel.magnitude < maxSpeed)
            {
                rb.AddForce(transform.right * moveVector.x * acceleration, ForceMode.Acceleration);
                rb.AddForce(transform.forward * moveVector.y * acceleration, ForceMode.Acceleration);
            }
        }
    }



    //Rotating methond
    void RotateBody()
    {
        //Rotating player
        //Find current look rotation
        //Vector3 rot = transform.rotation.eulerAngles;
        //var desiredX = rot.y + rotationVector.x * speedRotation * sensitivity;

        //Perform the rotations
        //transform.rotation = Quaternion.Euler(0, desiredX, 0);


        Vector3 forward = cameraFront.forward;
        forward.y = 0f;
        forward.Normalize();

        Quaternion targetRotation = Quaternion.LookRotation(forward);
        rb.MoveRotation(targetRotation);
    }


    //Applying drag if player is on the ground
    void ApplyDrag()
    {
        rb.drag = isGrounded == IsGrounded.Grounded ? dragOnGround : 0f;
    }



    //Standard moving
    public void Jump()
    {
        rb.AddForce(Vector2.up * jumpForce, ForceMode.Impulse);
    }
    public void OnMove(Vector2 vector)
    {
        moveVector = vector;
    }


    //Standart rotating
    public void DeltaRotation(Vector2 delta)
    {
        rotationVector = delta;
    }
}
