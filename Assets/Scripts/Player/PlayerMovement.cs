using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] float wallrunForceX;
    [SerializeField] float wallRunForceY;
    [SerializeField] float wallrunMaxForceX;
    [SerializeField] float wallrunMaxForceY;
    [SerializeField] float wallrunTime;
    [SerializeField] int wallrunMaxCount;
    Vector3 wallrunDirection;
    float wallRunStartTime;
    [Header("Wall climb")]
    [SerializeField] float wallClimbForce;
    [SerializeField] float wallClimbMaxSpeed;
    [SerializeField] float wallClimbTime;
    float wallClimbStartTime;
    [Header("Wall slide")]
    [SerializeField] float wallSlideMaxSpeed;
    Vector3 savedNormal = Vector3.zero;
    Vector3 lastWallNormal = Vector3.zero;
    int checkWallCounter = 0;

    //Wall states
    enum WallState {Sliding, Running, Climbing}
    WallState currentWallState = WallState.Sliding;
    Transform wallReferenceSaved = null;
    Transform wallReference = null;
    bool runnedAlready;
    bool stoppedByWall;
    int wallrunCounter = 0;
    //Check for jump from the wall
    int wallJumpCounter = 0;


    [Header("Crouching")]
    [SerializeField] float crouchHeight;
    [SerializeField] float crouchSpeedMultiplyer;
    //Crouch handle
    public enum IsCrouching {Crouching, Standing}
    IsCrouching isCrouching = IsCrouching.Standing;



    //Grounded check
    enum IsGrounded {Grounded, InAir};
    IsGrounded isGrounded = IsGrounded.Grounded;
    private bool justLanded = false;

    
    //State handle
    enum BodyState {Moving, Dashing, WallRunning, Sliding, InAir};
    BodyState currentState = BodyState.Moving;



    //Surface handle
    Vector3 groundNormal = Vector3.up;
    Vector3 wallNormal;



    //Speed up system
    [SerializeField] float maxTopSpeed;
    [SerializeField] float maxLowSpeed;
    float maxSpeedDifference;
    float currentMaxSpeed;
    int momentum = 0;
    Dictionary<string, int> speedPoints = new Dictionary<string, int>()
    {
        {"wallrun", 3},
        {"jump", 1},
        {"slide", 1}
    };



    //Start settings
    public void Start()
    {
        //Subscribe events
        //Events at ground change state
        collisionScr.OnGroundNormalChanged += OnSurfaceCollide;
        
    }
    public void OnDisable()
    {
        //Unsubscribe events
        collisionScr.OnGroundNormalChanged -= OnSurfaceCollide;
    }





    //Physics handle
    private void FixedUpdate()
    {
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        DebugOutput.Instance.Output("Скорость: " + horizontalVel.magnitude.ToString("F2"), 1);
        //Current states of movement
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
                CounterMovement();
            break;
        }

        //Check collisions
        //collisionScr.WallRaycast(rb.velocity);
    }


    //Movement states
    void Moving(float airMultiplyer = 1)
    {
        //Handle movement of player
        //Adding force to object until reaching max speed

        if(moveVector != Vector2.zero)
        {
            //Trajectory projection at the ground surface
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;

            rb.AddForce(surfaceRight * moveVector.x * acceleration * airMultiplyer, ForceMode.Acceleration);
            rb.AddForce(surfaceForward * moveVector.y * acceleration * airMultiplyer, ForceMode.Acceleration);
        }
    }


    //Grag
    void Drag()
    {
        if(moveVector == Vector2.zero)
        {
            Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            horizontalVel = Vector3.ProjectOnPlane(horizontalVel, groundNormal);

            if (horizontalVel.magnitude > 0.5f)
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
        //Counter force at the ground
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if(currentState == BodyState.Sliding || currentState == BodyState.Moving)
        {
            horizontalVel = new Vector3(rb.velocity.x, rb.velocity.y, rb.velocity.z);
        }
        horizontalVel = Vector3.ProjectOnPlane(horizontalVel, groundNormal);


        if (horizontalVel.magnitude > maxSpeed && currentState != BodyState.InAir)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * acceleration;
            rb.AddForce(counterForce, ForceMode.Acceleration);
        }
        // Counter force in the air
        else if(horizontalVel.magnitude > airMaxSpeed && currentState == BodyState.InAir)
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
        //Activate wall run at first collision
        if(!runnedAlready)
            WallRunStart();

        //Main movement handle
        switch(currentWallState)
        {
            case WallState.Climbing:
                ClimbWallRun();
            break;
            case WallState.Running:
                HorizontalWallRun();
            break;
            case WallState.Sliding:
                WallSlide();
            break;
        }

    }

    //Start wall run direction
    void WallRunStart()
    {
        //Calculating vectors

        var rbMoveVector = transform.forward;
        var dot = Vector3.Dot(rbMoveVector, -wallNormal);
        Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;
        Vector3 wallLeft = -wallRight;
        
        var dotRight = Vector3.Dot(rbMoveVector, wallRight);
        print(rbMoveVector);
        print(dot);
        
        //Upward movement
        if(dot > 0.7f)
        {
            //Add maximum of continueing wall climb
            if(wallrunCounter >= wallrunMaxCount)
                return;
            wallrunCounter++;

            wallClimbStartTime = Time.time;
            Invoke("DeactivateWallRun", wallClimbTime);
            currentWallState = WallState.Climbing;
            
            runnedAlready = true;
        }
        //Left/Right movement
        else if(dot > -0.5f)
        {

            //Going right
            if(dotRight > 0)
            {
                wallrunDirection = wallRight;
            }
            //Going left
            else
            {
                wallrunDirection = wallLeft;
            }
            Invoke("DeactivateWallRun", wallrunTime);
            wallRunStartTime = Time.time;
            currentWallState = WallState.Running;
            runnedAlready = true;
        }
        else
        {
            currentWallState = WallState.Sliding;
        }

        if(!stoppedByWall)
        {
            //Nullifying start speed
            stoppedByWall = true;
            rb.velocity = new Vector3(0, 0, 0);
        }

    }

    //Add climb vertical movement
    void ClimbWallRun()
    {
        //Smooth movement handle
        //Timer from climb start
        float timeSinceStart = Time.time - wallClimbStartTime;
        //Progress percent
        float t = Mathf.Clamp01(timeSinceStart / wallClimbTime);
        //Multiplyer
        float forceMultiplier = Mathf.SmoothStep(1f, 0f, t);
        //Climb force
        float climbForce = wallClimbForce * forceMultiplier;
        if(rb.velocity.y < wallClimbMaxSpeed)
            rb.AddForce(Vector3.up * climbForce * rb.mass, ForceMode.Impulse);
        else
        {
            //If overflow normalize speed
            var normSpeed = new Vector3(0, rb.velocity.y, 0).normalized * wallrunMaxForceY;
            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z) + normSpeed;
        }
    }

    //Add climb horizontal movement
    void HorizontalWallRun()
    {
        //Horizontal side wall run
        var speedH = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        //Handle smooth movement 
        float timeSinceStart = Time.time - wallRunStartTime;
        float t = Mathf.Clamp01(timeSinceStart / wallrunTime);
        float xMultiplier = Mathf.SmoothStep(1f, 0f, t);
        float horizontalForce = wallrunForceX * xMultiplier;
        print(xMultiplier);
        
        //Horizontal movement
        if(speedH.magnitude < wallrunMaxForceX)
            rb.AddForce(wallrunDirection * horizontalForce * rb.mass, ForceMode.Acceleration);
        else
        {
            //If overflow onrmalize speed
            var normSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).normalized * wallrunMaxForceX;
            rb.velocity = new Vector3(0, rb.velocity.y, 0) + normSpeed;
        }

        //Horizontal up wall run
        var speedV = rb.velocity.y;
        //Handle smooth movement in arch
        float yMultiplier = Mathf.Cos(t * Mathf.PI);
        float verticalForce = wallRunForceY * Math.Abs(xMultiplier);
        print(yMultiplier);

        //Vertical movement
        if(speedV < wallrunMaxForceY * yMultiplier)
            rb.AddForce(Vector3.up * verticalForce * rb.mass, ForceMode.Acceleration);
        else
        {
            //If overflow onrmalize speed
            var normSpeed = new Vector3(0, rb.velocity.y, 0).normalized * wallrunMaxForceY;
            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z) + normSpeed;
        }
    }

    void DeactivateWallRun()
    {
        print("Deactivated");
        currentWallState = WallState.Sliding;
    }

    //Wall slide when attached to the wall
    private void WallSlide()
    {
        if(moveVector != null)
        {
            //Finding direction of movement
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
            Vector3 surfaceMoveDir = (surfaceRight * moveVector.x + surfaceForward * moveVector.y).normalized;
            //Getting current direction
            Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;
            Vector3 wallLeft = -wallRight;
            var dotRight = Vector3.Dot(surfaceMoveDir, wallRight);
            if(dotRight > 0)
                rb.AddForce(wallRight * acceleration * airControlMultiplier / 2, ForceMode.Acceleration);
            else if (dotRight < 0)
                rb.AddForce(-wallRight * acceleration * airControlMultiplier / 2, ForceMode.Acceleration);


            //Counter movement
            var horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            if (horizontalVel.magnitude > maxSpeed)
            {
                //Getting direction of movement
                Vector3 moveDir = horizontalVel.normalized;
                //Force to counter movement
                Vector3 counterForce = -moveDir * acceleration * airControlMultiplier;
                rb.AddForce(counterForce, ForceMode.Acceleration);
            }
        }



        //Slow sliding at the wall
        if(rb.velocity.y < -wallSlideMaxSpeed)
        {
            print(rb.velocity.y);
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
            rb.AddForce(Vector2.up * jumpForce * rb.mass, ForceMode.Impulse);
        }
        //If on the wall go a little forward 
        else if (currentState == BodyState.WallRunning)
        {
            savedNormal = wallNormal;
            var lookDirection = cameraFront.forward * moveVector.y + cameraFront.right * moveVector.x;
            if(lookDirection == Vector3.zero)
                lookDirection = cameraFront.forward;

            //Projecting vector to xz plane
            Vector3 lookDirectionXZ = new Vector3(lookDirection.x, 0f, lookDirection.z).normalized;

            //Cant jump into the wall
            if(Vector3.Dot(lookDirection, wallNormal) <= 0.1f)
                return;

            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.AddForce(lookDirectionXZ * acceleration, ForceMode.Impulse);
            
            print(wallJumpCounter);
            //Counter of max wall jump - 3, if overwlow dont use up speed
            if(wallJumpCounter < 3)
                rb.AddForce(Vector2.up * jumpForce * rb.mass, ForceMode.Impulse);
            else {}

            wallJumpCounter++;
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












    // //Event handle
    // //Ground Check events
    //Change grounded state
    void OnFly()
    {
            if(isGrounded == IsGrounded.InAir && currentState != BodyState.WallRunning)
                return;
            else
            {
                print("In the air");
                rb.useGravity = true;
                isGrounded = IsGrounded.InAir;
                groundNormal = Vector3.up;
                currentState = BodyState.InAir;
                justLanded = true;
            }
    }

    void OnLand()
    {
            if(isGrounded == IsGrounded.Grounded)
                return;
            print("At the ground");
            isGrounded = IsGrounded.Grounded;

            //Additional land force
            var dot = Vector3.Dot(groundNormal, Vector3.up);
            if(dot > 0.8f)
            {
                var massCoefficient = 1 / rb.mass * 80;
                rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z) - groundNormal * massCoefficient * 10;
            }
            else
            {
                var massCoefficient = 1 / rb.mass * 80;
                rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z) - groundNormal * massCoefficient * 3;
            }
            //Null normals and jumps
            savedNormal = Vector3.zero;
            wallNormal = Vector3.zero;
            //Wall run
            wallJumpCounter = 0;
            wallrunCounter = 0;
            justLanded = false;
            wallReferenceSaved = null;
    }


    //Handle collisions with surfaces
    void OnSurfaceCollide(ContactPoint[] contacts)
    {

        //If there is ono contact object is flying
        if (contacts.Length == 0)
        {
            print("zero contacts");
            OnFly();
            return;
        }

        //Handling and counting multiple contacts
        int[] contactSurfaces = {0,0,0,0};
        List<int> indexesGround = new List<int>();
        List<int> indexesSlope = new List<int>();
        List<int> indexesCeiling = new List<int>();
        List<int> indexesWall = new List<int>();
        for(int i=0; i < contacts.Length; i++)
        {
            var surfaceType = surfaceHandler.GetSurfaceType(contacts[i].normal, isCrouching);
            switch (surfaceType)
            {
                case SurfaceHandler.SurfaceType.Ground:
                    contactSurfaces[0]++;
                    indexesGround.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Slope:
                    contactSurfaces[1]++;
                    indexesSlope.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Ceiling:
                    contactSurfaces[2]++;
                    indexesCeiling.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Wall:
                    contactSurfaces[3]++;
                    indexesWall.Add(i);
                    break;
            }
        }

        //Ground contact main
        if(contactSurfaces[0] > 0)
        {
            //Find main surface: closest to vector.up
            var curnormal = FindClosestToVectorUp(indexesGround, contacts);

            //Handle main logic
            HandleGround(curnormal);
            OnLand();
        }
        //Slope contact main
        else if(contactSurfaces[1] > 0)
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestTo90(indexesSlope, contacts);

            //Handle main logic
            HandleSlope(curnormal);
            OnLand();
        }
        //Wall contact main
        else if(contactSurfaces[3] > 0)
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestTo90(indexesWall, contacts);

            //Handle main logic
            HandleWall(curnormal, contacts[indexesWall[0]]);
        }
        //Ceiling contact main
        else if(contactSurfaces[2] > 0)
        {
            //Handle main logic
            OnFly();
        }
        
        //Clear all lists
        indexesGround.Clear();
        indexesSlope.Clear();
        indexesCeiling.Clear();
        indexesWall.Clear();
    }

    //Find main surface: closest to 90 degrees
    Vector3 FindClosestTo90(List<int> indexes, ContactPoint[] contacts)
    {
            var closestNormal = Vector3.zero;
            var closestAngle = 0f;
            foreach(var i in indexes)
            {
                var currentNormal = contacts[i].normal;
                var angle = Vector3.Angle(currentNormal, Vector3.up);
                if(angle > closestAngle)
                {
                    closestNormal = currentNormal;
                    closestAngle = angle;
                }
            }
            return closestNormal;
    }

    //Find main surface: closest to vector.up
    Vector3 FindClosestToVectorUp(List<int> indexes, ContactPoint[] contacts)
    {
            var closestNormal = Vector3.zero;
            var closestDot = 0f;
            foreach(var i in indexes)
            {
                var currentNormal = contacts[i].normal;
                var dot = Vector3.Dot(currentNormal, Vector3.up);
                if(dot > closestDot)
                {
                    closestNormal = currentNormal;
                    closestDot = dot;
                }
            }
            return closestNormal;
    }

    void HandleGround(Vector3 normal)
    {
        groundNormal = normal;
        if (currentState != BodyState.Moving)
        {
            rb.useGravity = false;
            currentState = BodyState.Moving;
        }
    }

    void HandleSlope(Vector3 normal)
    {
        groundNormal = normal;

        //Check for consistent slope
        if (savedSlideNormal == groundNormal)
            counterNormal++;
        else
            counterNormal = 0;

        if (counterNormal > 3)
        {
            if (currentState != BodyState.Sliding)
            {
                counterNormal = 0;
                currentState = BodyState.Sliding;
                rb.useGravity = true;
            }
        }

        savedSlideNormal = groundNormal;
    }

    void HandleWall(Vector3 normal, ContactPoint contact)
    {
        if (isGrounded == IsGrounded.InAir)
        {
            //Check for consistent wall
            if (lastWallNormal == normal)
                checkWallCounter++;
            else
                checkWallCounter = 0;

            if (checkWallCounter > 3)
            {
                currentState = BodyState.WallRunning;
                rb.useGravity = true;
                wallNormal = normal;

                //Check for the same wall for additional wall run possibility
                wallReference = contact.otherCollider.transform;
                if(wallReference != wallReferenceSaved)
                {
                    runnedAlready = false;
                    stoppedByWall = true;
                }
                wallReferenceSaved = wallReference;
            }

            lastWallNormal = normal;
        }
    }
}

















