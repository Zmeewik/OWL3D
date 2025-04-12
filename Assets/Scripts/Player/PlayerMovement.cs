using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider))]
public class PlayerMovement : MonoBehaviour, IMovable
{


    [Header("References")]
    [SerializeField] Rigidbody rb;
    [SerializeField] Transform front;
    [SerializeField] Transform cameraFront;
    [SerializeField] CollisionCheck collisionScr;
    
    [Header("Rotation")]
    [SerializeField] float speedRotation;
    [SerializeField] float sensitivity;
    public float Sensitivity => sensitivity;

    [Header("Movement")]
    [SerializeField] float acceleration;
    [SerializeField] float decceleration;
    [SerializeField] float maxSpeed;
    //Move handle
    Vector2 moveVector = Vector2.zero;

    [Header("Jump")]
    [SerializeField] float jumpForce;


    [Header("Dash")]
    [SerializeField] float dashDistance;
    [SerializeField] float dashTime;


    [Header("Air")]
    [SerializeField, Range(0, 1)] float airControlMultiplier;
    [SerializeField] float airMaxSpeed;


    //Grounded check
    enum IsGrounded {Grounded, InAir};
    IsGrounded isGrounded = IsGrounded.Grounded;


    
    //State handle
    enum BodyState {Moving, Dashing, WallRunning, Crouching, Sliding, InAir};
    BodyState currentState = BodyState.Moving;





    //Start settings
    public void Start()
    {
        //Subscribe events
        //Events at ground change state
        collisionScr.OnGrounded += OnLand;
        collisionScr.OnNotGrounded += OnFly;


    }
    public void OnDisable()
    {
        //Unsubscribe events
        collisionScr.OnGrounded -= OnLand;
        collisionScr.OnNotGrounded -= OnFly;
    }





    //Physics handle
    private void FixedUpdate()
    {
        switch(currentState)
        {
            case BodyState.Moving:
                Moving();
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
            case BodyState.InAir:
                Moving(airControlMultiplier);
                RotateBody();
                CounterMovement(airMaxSpeed);
            break;
        }
    }


    //Movement states
    void Moving(float airMoltiplyer = 1)
    {
        //Handle movement of player
        //Adding force to object until reaching max speed
        if(moveVector != Vector2.zero)
        {
            //Speed limit
            rb.AddForce(transform.right * moveVector.x * acceleration * airMoltiplyer, ForceMode.Acceleration);
            rb.AddForce(transform.forward * moveVector.y * acceleration * airMoltiplyer, ForceMode.Acceleration);
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
    void CounterMovement(float airMaxSpeed = 0)
    {
        //Check for limit overflow
        //Counter force at the ground
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
        if (horizontalVel.magnitude > maxSpeed && airMaxSpeed == 0)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * acceleration;
            rb.AddForce(counterForce, ForceMode.Acceleration);
        }
        // Counter force in the air
        else if(horizontalVel.magnitude > airMaxSpeed && airMaxSpeed != 0)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * acceleration;
            rb.AddForce(counterForce, ForceMode.Acceleration);
        }
        print(horizontalVel.magnitude);

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


    //End dash state
    public void EndDash()
    {
        if(currentState == BodyState.Dashing)
            if(isGrounded == IsGrounded.Grounded)
                currentState = BodyState.Moving;
            else
                currentState = BodyState.InAir;
    }



    //Input handle
    //Standard moving
    public void OnJump()
    {
        if(isGrounded == IsGrounded.Grounded)
            rb.AddForce(Vector2.up * jumpForce * rb.mass, ForceMode.Impulse);
    }
    
    public void OnMove(Vector2 vector)
    {
        moveVector = vector;
    }

    public void OnDash()
    {
        //Check if dashing right now
        if(currentState == BodyState.Dashing)
            return;
        
        //Nullifying y speed
        rb.velocity = new Vector3(0f, rb.velocity.y, 0f);

        //Get current Look Direction
        var lookDirection = cameraFront.forward * moveVector.y + cameraFront.right * moveVector.x;
        if(lookDirection == Vector3.zero)
            lookDirection = cameraFront.forward;

        //If going down go little up instead
        //Up dashing
        Vector3 lookDirectionXZ = new Vector3(lookDirection.x, 0f, lookDirection.z).normalized;
        lookDirectionXZ = new Vector3(lookDirectionXZ.x, 0.2f, lookDirectionXZ.z).normalized;
        lookDirection = lookDirectionXZ;
        

        //Get needed velocity
        var dashVelocity = dashTime * (dashDistance / dashTime);

        //Dash player and change its state
        rb.AddForce(lookDirection * dashVelocity * rb.mass, ForceMode.Impulse);

        //End dash afrter time
        currentState = BodyState.Dashing;
        Invoke("EndDash", dashTime);
    }










    //Ground Check events
    void OnLand()
    {
        if(currentState == BodyState.InAir)
            currentState = BodyState.Moving;
        isGrounded = IsGrounded.Grounded;
        var massCoefficient = 1 / rb.mass * 80;
        rb.velocity = new Vector3(rb.velocity.x, -10 * massCoefficient, rb.velocity.z);
        print("Grounded");
    }

    void OnFly()
    {
        if(currentState == BodyState.Moving)
            currentState = BodyState.InAir;
        isGrounded = IsGrounded.InAir;
        print("Not Grounded");
    }
}
