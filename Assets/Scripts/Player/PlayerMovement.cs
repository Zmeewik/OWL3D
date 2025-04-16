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
    [SerializeField] SurfaceHandler surfaceHandler;
    
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


    [Header("Slide")]
    [SerializeField] float maxSlideSpeed;
    [SerializeField] float slideSpeed;
    int counterNormal = 0;
    Vector3 savedSlideNormal = Vector3.zero;


    [Header("Wall run")]
    [SerializeField] float wallSpeedX;
    [SerializeField] float wallSpeedY;
    [SerializeField] float wallSlideMaxSpeed;
    Vector3 savedNormal = Vector3.zero;

    //Grounded check
    enum IsGrounded {Grounded, InAir};
    IsGrounded isGrounded = IsGrounded.Grounded;


    
    //State handle
    enum BodyState {Moving, Dashing, WallRunning, Crouching, Sliding, InAir};
    BodyState currentState = BodyState.Moving;



    //Surface handle
    Vector3 groundNormal = Vector3.up;
    Vector3 wallNormal;




    //Start settings
    public void Start()
    {
        //Subscribe events
        //Events at ground change state
        collisionScr.OnGrounded += OnLand;
        collisionScr.OnNotGrounded += OnFly;
        collisionScr.OnGroundNormalChanged += OnGroundCollide;

    }
    public void OnDisable()
    {
        //Unsubscribe events
        collisionScr.OnGrounded -= OnLand;
        collisionScr.OnNotGrounded -= OnFly;
        collisionScr.OnGroundNormalChanged -= OnGroundCollide;
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
                WallRun();
                WallSlide();
            break;
            case BodyState.Crouching:
            break;
            case BodyState.Sliding:
                Moving();
                RotateBody();
                Drag();
                CounterMovement();
                Slide();
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
            //Trajectory projection at the ground surface
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;

            rb.AddForce(surfaceRight * moveVector.x * acceleration * airMoltiplyer, ForceMode.Acceleration);
            rb.AddForce(surfaceForward * moveVector.y * acceleration * airMoltiplyer, ForceMode.Acceleration);
        }
    }


    //Grag
    void Drag()
    {
        if(moveVector == Vector2.zero)
        {
            Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            horizontalVel = Vector3.ProjectOnPlane(horizontalVel, groundNormal);

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
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if(currentState == BodyState.Sliding || currentState == BodyState.Moving)
            horizontalVel = new Vector3(rb.velocity.x, rb.velocity.y, rb.velocity.z);
        horizontalVel = Vector3.ProjectOnPlane(horizontalVel, groundNormal);

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


    //Slide auto movement
    private void Slide()
    {
        //Getting vector down
        Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
        //Get current slope velocity
        float currentSpeedOnSlope = Vector3.Dot(rb.velocity, slopeDir);
        // Если мы не превысили максимальную скорость — добавим силу
        if (currentSpeedOnSlope < maxSlideSpeed)
        {
            rb.AddForce(slopeDir * -Physics.gravity.y * slideSpeed, ForceMode.Acceleration);
            rb.AddForce(groundNormal.normalized * 5, ForceMode.Acceleration);
        }
    }

    //Movement close to the wall
    //Wall run at different directions
    private void WallRun()
    {
        //Upward movement

        //Left/Right movement

    }

    //Wall slide when attached to the wall
    private void WallSlide()
    {
        //Slow sliding at the wall
        print(rb.velocity.y);
        if(rb.velocity.y < -wallSlideMaxSpeed)
        {
            rb.AddForce(Vector3.up * 40, ForceMode.Acceleration);
        }
    }





    //Input handle
    //Standard moving
    public void OnJump()
    {
        //print(isGrounded);
        if(isGrounded == IsGrounded.Grounded && currentState != BodyState.Sliding)
        {
            currentState = BodyState.InAir;
            rb.AddForce(Vector2.up * jumpForce * rb.mass, ForceMode.Impulse);
        }
        //If on the wall go a little forward 
        else if (currentState == BodyState.WallRunning && savedNormal != wallNormal)
        {
            savedNormal = wallNormal;
            var lookDirection = cameraFront.forward * moveVector.y + cameraFront.right * moveVector.x;
            if(lookDirection == Vector3.zero)
                lookDirection = cameraFront.forward;

            //Projecting vector to xz plane
            Vector3 lookDirectionXZ = new Vector3(lookDirection.x, 0f, lookDirection.z).normalized;

            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.AddForce(lookDirectionXZ * acceleration, ForceMode.Impulse);
            rb.AddForce(Vector2.up * jumpForce * rb.mass, ForceMode.Impulse);
        }
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












    //Event handle
    //Ground Check events
    void OnLand()
    {
        if(currentState == BodyState.InAir)
            currentState = BodyState.Moving;
        isGrounded = IsGrounded.Grounded;
        var massCoefficient = 1 / rb.mass * 80;
        rb.velocity = new Vector3(rb.velocity.x, -10 * massCoefficient, rb.velocity.z);
        savedNormal = Vector3.zero;
        //print("Grounded");
    }

    void OnFly()
    {
        if(currentState != BodyState.InAir)
            currentState = BodyState.InAir;
        isGrounded = IsGrounded.InAir;
        groundNormal = Vector3.zero;
        rb.useGravity = true;
        //print("Not Grounded");
    }

    //Handle collision
    void OnGroundCollide(ContactPoint[] points)
    {
        if(points.Length == 0)
        {
            rb.useGravity = true;

            if(currentState == BodyState.WallRunning)
                currentState = BodyState.InAir;
            return;
        }

        var ground = SurfaceHandler.SurfaceType.None;

        var wallNormal = Vector3.zero;
        int wallCount = 0;
        bool isMainGround = false;
        var mainNormal = Vector3.zero;

        // Check for every collision is there walls or floors
        foreach(var p in points)
        {
            var type = surfaceHandler.GetSurfaceType(p.normal, false);
            switch(type)
            {
                case SurfaceHandler.SurfaceType.Ground:
                    ground = SurfaceHandler.SurfaceType.Ground;
                    groundNormal = p.normal;
                    mainNormal = p.normal;
                    isMainGround = true;
                    break;
                case SurfaceHandler.SurfaceType.Wall:
                    wallNormal = p.normal;
                    wallCount += 1;
                    break;
                case SurfaceHandler.SurfaceType.Ceiling:
                    break;
                case SurfaceHandler.SurfaceType.Slope:
                    ground = SurfaceHandler.SurfaceType.Slope;
                    groundNormal = p.normal;
                    break;
            }
        }
        //If move than 1 geound choose main ground
        if(isMainGround)
        {
            groundNormal = mainNormal;
            ground = SurfaceHandler.SurfaceType.Ground;
        }

        this.wallNormal = Vector3.zero;
        rb.useGravity = true;

        // Palyer is on the ground
        if(ground != SurfaceHandler.SurfaceType.None)
        {
            if(ground == SurfaceHandler.SurfaceType.Ground)
            {
                rb.useGravity = false;
                currentState = BodyState.Moving;
            }
            else if(ground == SurfaceHandler.SurfaceType.Slope)
            {
                print(groundNormal);
                if(savedSlideNormal == groundNormal)
                    counterNormal++;
                else
                    counterNormal = 0;
                if(counterNormal > 3)
                {
                    counterNormal = 0;
                    currentState = BodyState.Sliding;
                    rb.useGravity = true;
                }
                savedSlideNormal = groundNormal;
            }
        }
        // Player is close to wall
        else if (wallCount != 0 && groundNormal == Vector3.zero)
        {
            currentState = BodyState.WallRunning;
            rb.useGravity = true;
            this.wallNormal = wallNormal;
        }

        //print(currentState);
        // Player is in the air
    }
}
