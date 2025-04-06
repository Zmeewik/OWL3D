using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider))]
public class PlayerMovement : MonoBehaviour, IMovable
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
    [SerializeField] float decceleration;
    [SerializeField] float maxSpeed;
    [SerializeField] public float dragOnGround;

    [Header("Jump")]
    [SerializeField] float jumpForce;


    [Header("Dash")]
    [SerializeField] float dashForce;

    [Header("Air")]
    [SerializeField, Range(0, 1)] float airControlMultiplier;


    //Move handle
    Vector2 moveVector = Vector2.zero;


    //Bool
    enum IsGrounded {Grounded, InAir};
    IsGrounded isGrounded = IsGrounded.Grounded;

    
    //State handle
    enum BodyState {Moving, Dashing, WallRunning, Crouching, Sliding};
    BodyState currentState = BodyState.Moving;

    private void FixedUpdate()
    {
        switch(currentState)
        {
            case BodyState.Moving:
                Moving();
                //ApplyDrag();
                RotateBody();
                Drag();
                CounterMovement();
            break;
            case BodyState.Dashing:
            break;
            case BodyState.WallRunning:
            break;
            case BodyState.Crouching:
            break;
            case BodyState.Sliding:
            break;
        }
    }

    //Movement states
    void Moving()
    {

        //Handle movement of player
        //Adding force to object until reaching max speed
        if(moveVector != Vector2.zero)
        {
            //Speed limit
            rb.AddForce(transform.right * moveVector.x * acceleration, ForceMode.Acceleration);
            rb.AddForce(transform.forward * moveVector.y * acceleration, ForceMode.Acceleration);

            print(rb.velocity);
        }
    }


    //Grag
    void Drag()
    {
        if(moveVector == Vector2.zero)
        {
            Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            if (horizontalVel.magnitude > 0.3f)
            {
                Vector3 drag = -horizontalVel.normalized * decceleration;
                rb.AddForce(drag, ForceMode.Acceleration);
            }
            else
            {
                rb.velocity = new Vector3(0f, rb.velocity.y, 0f);
            }
        }
    }


    //Counter movement if player cross the limit speed
    void CounterMovement()
    {
        //Check for limit overflow
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (horizontalVel.magnitude > maxSpeed)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * acceleration;
            rb.AddForce(counterForce, ForceMode.Acceleration);
        }

    }

    //Rotating
    void RotateBody()
    {
        //Getting forward of the camera
        Vector3 forward = cameraFront.forward;
        forward.y = 0f;
        forward.Normalize();

        //Rotating player toward camera
        Quaternion targetRotation = Quaternion.LookRotation(forward);
        rb.MoveRotation(targetRotation);
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
}
