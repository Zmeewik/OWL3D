using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider))]
public class PlayerMovement : MonoBehaviour, IMovable, IRotatable
{


    [Header("References")]
    [SerializeField] Rigidbody rb;
    
    [Header("Rotation")]
    [SerializeField] float speedRotation;
    [SerializeField] float sensitivity;
    public float Sensitivity => sensitivity;

    [Header("Movement")]
    [SerializeField] float acceleration;
    [SerializeField] float maxSpeed;
    [SerializeField] float decceleration;

    [Header("Jump")]
    [SerializeField] float jumpForce;


    [Header("Dash")]
    

    //Vectors of body
    Vector3 frontDirection = Vector3.forward;
    Vector3 rightDirection = Vector3.right;
    Vector2 moveVector = Vector2.zero;
    Vector2 rotationVector = Vector2.zero;

    
    //State handle
    enum BodyState {Moving, Dashing, WallRunning, AirMoving, Crouching, Sliding};
    BodyState currentState = BodyState.Moving;

    private void FixedUpdate()
    {
        switch(currentState)
        {
            case BodyState.Moving:
                Moving();
                RotateBody();
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

    //Movement states
    void Moving()
    {

        //Handle movement of player
        //Adding force to object until reaching max speed
        if(moveVector != Vector2.zero)
        {
            rb.AddForce(rightDirection * moveVector.x * acceleration, ForceMode.Force);
            rb.AddForce(frontDirection * moveVector.y * acceleration, ForceMode.Force);
            
            print(rb.velocity);
            if(Math.Abs(rb.velocity.x) > maxSpeed)
            {
                rb.velocity = new Vector3(rb.velocity.x < 0 ? -1 : 1 * maxSpeed, rb.velocity.y, rb.velocity.z);
            }
            if(Math.Abs(rb.velocity.z) > maxSpeed)
            {
                rb.velocity = new Vector3(rb.velocity.x, rb.velocity.y, rb.velocity.z < 0 ? -1 : 1 * maxSpeed);
            }
        }
        //Reduce force to object until stop
        else
        {
            Vector3 horizontalVelocity = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            Vector3 decelerationForce = -horizontalVelocity * decceleration;
            rb.AddForce(decelerationForce, ForceMode.Force);
        }
    }



    //Rotating methond
    void RotateBody()
    {
        Quaternion deltaRotation = Quaternion.Euler(0f, rotationVector.x * speedRotation * sensitivity, 0f);
        frontDirection = transform.forward;
        rightDirection = transform.right;
        rb.MoveRotation(rb.rotation * deltaRotation);
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
