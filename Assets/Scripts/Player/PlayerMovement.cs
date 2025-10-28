using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider))]
public class PlayerMovement : MonoBehaviour, IMovable
{

    [System.Serializable]
    public class FeatureFlags
    {
        public bool enableMovement = true;
        public bool enableJump = true;
        public bool enableDash = true;
        public bool enableSlide = true;
        public bool enableWallRun = true;
        public bool enableWallClimb = true;
        public bool enableWallSlide = true;
        public bool enableHangUp = true;
        public bool enableCrouch = true;
        public bool enableSpeedSystem = true;
        public bool enableLifeCamera = true;
        public PlayerCamera cameraScrReference;
        public bool enableParticles = true;
    }


    [Header("Feature Flags")]
    [SerializeField] FeatureFlags features = new FeatureFlags();

    [Header("References")]
    [SerializeField] Rigidbody rb;
    [SerializeField] Transform front;
    [SerializeField] Transform cameraFront;
    [SerializeField] CollisionCheck collisionScr;
    [SerializeField] SurfaceHandler surfaceHandler;
    public static Action<string, float[]> OnLifeCameraAction; 

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
    [SerializeField] float dashUpForce;
    [SerializeField] float dashTime;
    [SerializeField] float dashAcceleration;
    float dashVelocity = 0;
    float currentDashTime = 0;
    Vector3 dashDirection = Vector3.zero;


    [Header("Air")]
    [SerializeField, Range(0, 1)] float airControlMultiplier;
    [SerializeField] float airMaxSpeed;
    [SerializeField] float minFallTime;
    [SerializeField] float minFallCheck;
    [SerializeField] float flyMaxParticleTime;
    float currentFallTime;
    float currentDownFallTime;
    int counterOfAirFrames = 0;


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
    Vector3 closestWallContact = Vector3.zero;
    [Header("Wall slide")]
    [SerializeField] float wallSlideMaxSpeed;
    Vector3 savedNormal = Vector3.zero;
    Vector3 lastWallNormal = Vector3.zero;
    int checkWallCounter = 0;

    //Wall states
    enum WallState { Sliding, Running, Climbing, HangUp }
    WallState currentWallState = WallState.Sliding;
    Transform wallReferenceSaved = null;
    Transform wallReference = null;
    bool runnedAlready;
    bool stoppedByWall;
    int wallrunCounter = 0;
    //Check for jump from the wall
    int wallJumpCounter = 0;

    [Header("Hang Up")]
    [SerializeField] float minHangUpTime;
    [SerializeField] float maxHangUpTime;
    float currentFinalHangUpTime = 0;
    Vector3 hangUpStartPos;
    Vector3 hangUpControlPos;
    Vector3 nextHangUpPosition;
    float currentHangUpTime = 0f;
    float upwardHangUpRelation = 0;
    bool hangUpImpulseUp = false;
    bool hangUpImpulseForward = false;


    [Header("Crouching")]
    [SerializeField] float crouchHeadOffset;
    [SerializeField] float crouchMaxMultiplyer;
    [SerializeField] Transform headPosition;
    Vector3 headStartPosition;
    [SerializeField] Vector3 headCrouchPosition;
    [SerializeField] CapsuleCollider crouchCollider;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] float crouchTime;
    bool isStandingUp = false;
    float currentCrouchTime = 0;
    //Handle smooth crouching
    enum isChangingCrouchState { Crouching, Standing, None };
    isChangingCrouchState currentCrouchState = isChangingCrouchState.None;
    float crouchMultiplyer = 1;
    //Crouch handle
    public enum IsCrouching { Crouching, Standing }
    IsCrouching isCrouching = IsCrouching.Standing;



    //Grounded check
    enum IsGrounded { Grounded, InAir };
    IsGrounded isGrounded = IsGrounded.Grounded;
    private bool justLanded = false;


    //State handle
    enum BodyState { Moving, Dashing, WallRunning, Sliding, InAir };
    BodyState currentState = BodyState.Moving;
    BodyState lastState = BodyState.Moving;


    //Surface handle
    Vector3 groundNormal = Vector3.up;
    Vector3 wallNormal;


    //Speed up system
    [Header("Speed up system")]
    [SerializeField] float maxTopSpeed;
    [SerializeField] float maxLowSpeed;
    [SerializeField] float maxMomentum;
    float maxSpeedDifference;
    float currentMaxSpeed;
    float momentum = 0;
    [System.Serializable]
    public class DictionaryDummy
    {
        public string key;
        public float value;
    }
    [Header("Speed Point Values")]
    public List<DictionaryDummy> speedPointList = new List<DictionaryDummy>()
    {
        new DictionaryDummy(){key = "wallrun", value = 3f},
        new DictionaryDummy(){key = "jump", value = 1f},
        new DictionaryDummy(){key = "slide", value = 1f},
        new DictionaryDummy(){key = "not moving", value = -1f},
        new DictionaryDummy(){key = "none", value = -0.01f},
    };
    Dictionary<string, float> speedPoints;


    //Animations
    Particles particles;
    DebugOutput debugOutput;
    bool isAlreadyAnimating;
    public static Action<string, bool> OnPlayAnimationLArm;
    public static Action<string, bool> OnPlayAnimationRArm;
    public static Action<string, bool> OnPlayAnimationRLeg;
    public static Action<string, float[]> OnArmsMove;




    //Start settings
    public void Start()
    {
        //Subscribe events
        //Events at ground change state
        collisionScr.OnGroundNormalChanged += OnSurfaceCollide;
        collisionScr.OnObjectNormalChanged += HandleCollisionWithObjects;
        maxSpeedDifference = maxTopSpeed - maxLowSpeed;
        currentMaxSpeed = maxLowSpeed;

        //List to dictionary
        speedPoints = speedPointList.ToDictionary(entry => entry.key, entry => entry.value);

        //Set start head position
        headStartPosition = headPosition.localPosition;

        //Set particles to instance on scene
        particles = FindObjectOfType<Particles>();
        debugOutput = FindObjectOfType<DebugOutput>();
    }
    public void OnDisable()
    {
        //Unsubscribe events
        collisionScr.OnGroundNormalChanged -= OnSurfaceCollide;
        collisionScr.OnObjectNormalChanged -= HandleCollisionWithObjects;
    }





    //Physics handle
    private void FixedUpdate()
    {
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        debugOutput.Output("Скорость: " + horizontalVel.magnitude.ToString("F2"), 1);
        debugOutput.Output("Максимальная скорость: " + (currentMaxSpeed * crouchMultiplyer).ToString("F2"), 2);

        //Speed always goes down
        if (isCrouching == IsCrouching.Crouching && horizontalVel != Vector3.zero)
            BuildSpeed("crouch");
        else if (horizontalVel != Vector3.zero || currentState == BodyState.WallRunning)
            BuildSpeed("none");
        else if (horizontalVel == Vector3.zero)
            BuildSpeed("not moving");

        //Handle crouch check
        if (isStandingUp)
        {
            OnCrouch(false);
        }
        HandleCrouch();


        //Current states of movement
        switch (currentState)
        {
            case BodyState.Moving:
                if (features.enableMovement)
                {
                    Moving();
                    RotateBody();
                    Drag();
                    CounterMovement();
                }
                break;
            case BodyState.Dashing:
                if (features.enableDash)
                {

                }
                break;
            case BodyState.WallRunning:
                if (features.enableWallRun || features.enableWallClimb || features.enableWallSlide)
                {
                    WallRun();
                    RotateBody();
                }
                else
                    currentState = BodyState.InAir;
                break;
            case BodyState.Sliding:
                if (features.enableSlide)
                {
                    RotateBody();
                    Slide();
                }
                break;
            case BodyState.InAir:
                //Counting fall time
                if (rb.velocity.y < 0)
                    currentDownFallTime += Time.deltaTime;
                else
                    currentDownFallTime = 0;
                currentFallTime += Time.deltaTime;
                if (features.enableMovement)
                {
                    Moving(airControlMultiplier);
                    RotateBody();
                    CounterMovement();
                }
                break;
        }

        //Handle animations
        //Animate idle
        OnAnimating("H_Arms_Boxing_Idle", "r_arm", true);
        OnAnimating("H_Arms_Boxing_Idle", "l_arm", true);

        //Handle visual effects
        if (features.enableParticles)
        {
            if (currentMaxSpeed > maxLowSpeed + maxSpeedDifference / 2)
            {
                //print("move fast");
                var alpha = (currentMaxSpeed - maxLowSpeed - maxSpeedDifference / 2) / maxSpeedDifference * 2;
                var col = new Color[1] { new Color(1, 1, 1, alpha) };
                print(col);
                particles.ChangeColor("MovementLines", col);
                particles.StartEffect("MovementLines");
                ChangeFOV(alpha);
            }
            else if (currentState == BodyState.InAir && rb.velocity.magnitude > maxLowSpeed)
            {
                //print("move air");
                var alpha = Mathf.Min(currentFallTime / flyMaxParticleTime, 1);
                var col = new Color[1] { new Color(1, 1, 1, alpha) };
                //print(col);
                particles.ChangeColor("MovementLines", col);
                particles.StartEffect("MovementLines");
                ChangeFOV(alpha);
            }
            else
            {
                //print("move slow");
                particles.StopEffect("MovementLines");
                ChangeFOV(0);
            }
        }
    }


    //Movement states
    void Moving(float airMultiplyer = 1)
    {
        //Handle movement of player
        //Adding force to object until reaching max speed
        if (airMultiplyer == 1)
        {
            var state = GetCurrentLifeCameraState();
            if (features.enableLifeCamera && isCrouching == IsCrouching.Crouching && moveVector != Vector2.zero)
                OnLifeCamera("movement", new float[1] { 0 });
            else if (features.enableLifeCamera && moveVector != Vector2.zero)
                OnLifeCamera("movement", new float[1] { (currentMaxSpeed - maxLowSpeed) / maxSpeedDifference });
            else if (features.enableLifeCamera && moveVector == Vector2.zero && state != "jump" && state != "land" && state != "hangUp" && state != "dash")
            {
                OnLifeCamera("none", new float[1] { (currentMaxSpeed - maxLowSpeed) / maxSpeedDifference });
            }
        }
        else
        {
            if (currentFallTime >= minFallTime && features.enableLifeCamera)
                OnLifeCamera("fall", new float[1] { currentDownFallTime });
        }

        if (features.enableMovement && moveVector != Vector2.zero)
        {
            //Trajectory projection at the ground surface
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;

            rb.AddForce(surfaceRight * moveVector.x * acceleration * airMultiplyer * crouchMultiplyer, ForceMode.Acceleration);
            rb.AddForce(surfaceForward * moveVector.y * acceleration * airMultiplyer * crouchMultiplyer, ForceMode.Acceleration);
        }
    }


    //Grag
    void Drag()
    {
        if (features.enableMovement && moveVector == Vector2.zero)
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
        if (!features.enableMovement) return;

        //Check for limit overflow
        //Counter force at the ground
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        if (currentState == BodyState.Sliding || currentState == BodyState.Moving)
        {
            horizontalVel = new Vector3(rb.velocity.x, rb.velocity.y, rb.velocity.z);
        }
        horizontalVel = Vector3.ProjectOnPlane(horizontalVel, groundNormal);


        if (horizontalVel.magnitude > currentMaxSpeed * crouchMultiplyer && currentState != BodyState.InAir)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * acceleration * 1.1f;
            rb.AddForce(counterForce, ForceMode.Acceleration);
        }
        // Counter force in the air
        else if (horizontalVel.magnitude > currentMaxSpeed * crouchMultiplyer && currentState == BodyState.InAir)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * acceleration * 1.1f;
            rb.AddForce(counterForce, ForceMode.Acceleration);
        }

    }

    //Rotating
    void RotateBody()
    {
        if (!features.enableMovement) return;

        //Getting forward of the camera
        Vector3 forward = cameraFront.forward;
        forward.y = 0f;
        forward.Normalize();

        //Rotating player toward camera
        Quaternion targetRotation = Quaternion.LookRotation(forward);
        rb.MoveRotation(targetRotation);
    }

    //End dash state
    Vector3 timeOfDash = Vector3.zero;
    public void EndDash()
    {
        if (currentState == BodyState.Dashing)
        {
            BuildSpeed("dash");
            print(Vector3.Distance(transform.position, timeOfDash));
            var normVel = new Vector3(rb.velocity.x, 0, rb.velocity.z).normalized;
            rb.velocity = normVel * maxSpeed * airControlMultiplier;
            currentState = BodyState.InAir;
            OnFly();
        }
    }


    //Slide auto movement
    private void Slide()
    {
        if (!features.enableSlide) return;

        //Getting vector down
        Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
        //Get current slope velocity
        float currentSpeedOnSlope = Vector3.Dot(rb.velocity, slopeDir);
        //Add force until max
        if (currentSpeedOnSlope < maxSlideSpeed)
        {
            rb.AddForce(slopeDir * -Physics.gravity.y * slideSpeed, ForceMode.Acceleration);
        }
        //Sticking player to ground while sloping
        rb.AddForce(-groundNormal.normalized * 30, ForceMode.Acceleration);

        //Start animation
        if (features.enableLifeCamera)
            OnLifeCamera("slide", new float[1] { currentDownFallTime });
    }

    //Movement close to the wall
    //Wall run at different directions
    private void WallRun()
    {
        //Activate wall run at first collision
        if (!runnedAlready)
            WallRunStart();

        //Starting calculations and change life camera state
        if (features.enableLifeCamera)
        {
            //Calculate vectore
            var rbMoveVector = transform.forward;
            Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;
            var dotRight = Vector3.Dot(rbMoveVector, wallRight);

            //Change life camera state
            switch (currentWallState)
            {
                case WallState.Climbing:
                    if (features.enableWallClimb)
                        OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 1, dotRight });
                    else
                        OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 2, dotRight });
                    break;
                case WallState.Running:
                    if (features.enableWallRun)
                        OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 0, dotRight });
                    else
                        OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 2, dotRight });
                    break;
                case WallState.Sliding:
                    if (features.enableWallSlide)
                        OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 2, dotRight });
                    break;
            }
        }
        
        //Main movement handle
        switch (currentWallState)
        {
            case WallState.Climbing:
                if (features.enableWallClimb)
                    ClimbWallRun();
                else
                    currentWallState = WallState.Sliding;
                break;
            case WallState.Running:
                if (features.enableWallRun)
                    HorizontalWallRun();
                else
                    currentWallState = WallState.Sliding;
                break;
            case WallState.Sliding:
                if (features.enableWallSlide)
                    WallSlide();
                else
                    OnFly();
                break;
            case WallState.HangUp:
                if (features.enableHangUp)
                    OnHangUp();
                else
                    currentWallState = WallState.Climbing;
                break;
        }

    }

    //Start wall run direction
    void WallRunStart()
    {
        //Calculating vectors
        Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
        var currentMoveInputDirection = surfaceForward * moveVector.y + surfaceRight * moveVector.x;
        var moveDirectionDot = Vector3.Dot(currentMoveInputDirection, -wallNormal);
        print(moveDirectionDot);

        var rbMoveVector = transform.forward;
        var dot = Vector3.Dot(rbMoveVector, -wallNormal);
        Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;
        Vector3 wallLeft = -wallRight;

        var dotRight = Vector3.Dot(rbMoveVector, wallRight);

        //Upward movement
        if (dot > 0.7f && moveDirectionDot > 0.7f && features.enableWallClimb)
        {
            //Add maximum of continueing wall climb
            if (wallrunCounter >= wallrunMaxCount)
                return;
            wallrunCounter++;

            wallClimbStartTime = Time.time;
            Invoke("DeactivateWallRun", wallClimbTime);
            currentWallState = WallState.Climbing;
            runnedAlready = true;
            if (features.enableLifeCamera)
                OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 1, dotRight });
        }
        //Left/Right movement
        else if (dot < 0.7f && dot > -0.5f && moveDirectionDot < 0.7f && moveDirectionDot > 0f && features.enableWallRun)
        {
            //Going right
            if (dotRight > 0)
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
            BuildSpeed("wallrun");
            runnedAlready = true;
            if (features.enableLifeCamera)
                OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 0, dotRight });
        }
        else
        {
            if (features.enableLifeCamera)
                OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 2, dotRight });
            currentWallState = WallState.Sliding;
        }

        if (!stoppedByWall)
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
        if (rb.velocity.y < wallClimbMaxSpeed)
            rb.AddForce(Vector3.up * climbForce * rb.mass, ForceMode.Impulse);
        else
        {
            //If overflow normalize speed
            var normSpeed = new Vector3(0, rb.velocity.y, 0).normalized * wallrunMaxForceY;
            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z) + normSpeed;
        }

        //Activate put away animation
        OnAnimating("H_Arms_Boxing_PutAway", "l_arm", false);
        OnAnimating("H_Arms_Boxing_PutAway", "r_arm", false);

        //Check for hang up
        HangUpCheck();
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

        //Horizontal movement
        if (speedH.magnitude < wallrunMaxForceX)
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

        //Vertical movement
        if (timeSinceStart < wallrunTime / 2)
        {
            if (speedV < wallrunMaxForceY * yMultiplier)
                rb.AddForce(Vector3.up * verticalForce * rb.mass, ForceMode.Acceleration);
            else
            {
                //If overflow onrmalize speed
                var normSpeed = new Vector3(0, rb.velocity.y, 0).normalized * wallrunMaxForceY;
                rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z) + normSpeed;
            }
        }
    }

    void DeactivateWallRun()
    {
        if (currentWallState != WallState.HangUp)
        {
            currentWallState = WallState.Sliding;
        }
    }

    //Wall slide when attached to the wall
    private void WallSlide()
    {
        if (moveVector != Vector2.zero)
        {
            //Finding direction of movement
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
            Vector3 surfaceMoveDir = (surfaceRight * moveVector.x + surfaceForward * moveVector.y).normalized;
            //Getting current direction
            Vector3 wallRight = Vector3.Cross(Vector3.up, wallNormal).normalized;
            Vector3 wallLeft = -wallRight;

            //Getting wall move vector projection
            Vector3 wallMoveDir = Vector3.ProjectOnPlane(surfaceMoveDir, wallNormal);
            float wallMoveSpeed = wallMoveDir.magnitude;

            var dotRight = Vector3.Dot(surfaceMoveDir, wallRight);
            if (dotRight > 0)
                rb.AddForce(wallRight * acceleration * wallMoveSpeed * airControlMultiplier / 2, ForceMode.Acceleration);
            else if (dotRight < 0)
                rb.AddForce(wallLeft * acceleration * wallMoveSpeed * airControlMultiplier / 2, ForceMode.Acceleration);


            //Counter movement
            var horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            if (horizontalVel.magnitude > currentMaxSpeed)
            {
                //Getting direction of movement
                Vector3 moveDir = horizontalVel.normalized;
                //Force to counter movement
                Vector3 counterForce = -moveDir * acceleration * airControlMultiplier;
                rb.AddForce(counterForce, ForceMode.Acceleration);
            }
        }



        //Slow sliding at the wall
        if (rb.velocity.y < -wallSlideMaxSpeed)
        {
            rb.AddForce(Vector3.up * 40, ForceMode.Acceleration);
        }
    }




    //Input handle
    //Standard moving
    public void OnJump()
    {
        if (!features.enableJump) return;

        //print(isGrounded);
        if (isGrounded == IsGrounded.Grounded && currentState != BodyState.Sliding)
        {
            rb.AddForce(Vector2.up * jumpForce * rb.mass, ForceMode.Impulse);
            BuildSpeed("jump");
            OnCrouch(false);
            if (features.enableLifeCamera)
                OnLifeCamera("jump");
        }
        //If on the wall go a little forward 
        else if (currentState == BodyState.WallRunning && (features.enableWallRun || features.enableWallClimb || features.enableWallSlide))
        {
            savedNormal = wallNormal;
            var lookDirection = cameraFront.forward * moveVector.y + cameraFront.right * moveVector.x;
            if (lookDirection == Vector3.zero)
                lookDirection = cameraFront.forward;

            //Projecting vector to xz plane
            Vector3 lookDirectionXZ = new Vector3(lookDirection.x, 0f, lookDirection.z).normalized;

            //Cant jump into the wall
            if (Vector3.Dot(lookDirection, wallNormal) <= 0.1f)
                return;

            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            rb.AddForce(lookDirectionXZ * acceleration, ForceMode.Impulse);

            //Counter of max wall jump - 3, if overwlow dont use up speed
            if (wallJumpCounter < 3)
                rb.AddForce(Vector2.up * jumpForce * rb.mass, ForceMode.Impulse);
            else { }

            wallJumpCounter++;
            BuildSpeed("jump");
            if (features.enableLifeCamera)
                OnLifeCamera("jump");
        }
    }

    public void OnMove(Vector2 vector)
    {
        moveVector = vector;
    }

    public void OnDash()
    {
        //Check if dashing right now
        if (currentState == BodyState.Dashing || !features.enableDash)
            return;

        //Nullifying x, z speed
        rb.velocity = new Vector3(0f, rb.velocity.y, 0f);

        //Get current Look Direction
        var lookDirection = cameraFront.forward * moveVector.y + cameraFront.right * moveVector.x;
        if (lookDirection == Vector3.zero)
            lookDirection = cameraFront.forward;

        //If going down go little up instead
        //Up dashing
        rb.AddForce(dashUpForce * Vector3.up, ForceMode.Impulse);
        Vector3 lookDirectionXZ = new Vector3(lookDirection.x, 0f, lookDirection.z).normalized;
        lookDirection = lookDirectionXZ;
        dashDirection = lookDirection;

        //Get needed velocity
        dashVelocity = dashDistance / dashTime;
        Vector3 dash = dashDirection.normalized * dashVelocity;
        rb.velocity = new Vector3(dash.x, rb.velocity.y, dash.z);

        //Dash player
        currentDashTime = 0;
        timeOfDash = transform.position;

        //End dash afrter time
        currentState = BodyState.Dashing;
        Invoke("EndDash", dashTime);
        OnCrouch(false);
        if (features.enableLifeCamera)
            OnLifeCamera("dash");
    }


    //Crouch action
    public void OnCrouch(bool isCrouch)
    {
        if (!features.enableCrouch)
            return;

        if (isCrouch)
            //Switching standing up under low ceiling and end checkout
            isStandingUp = false;

        //Scale player
        if (isCrouch && isCrouching != IsCrouching.Crouching)
        {
            //Start croudhing
            isCrouching = IsCrouching.Crouching;
            crouchMultiplyer = crouchMaxMultiplyer;
            currentCrouchState = isChangingCrouchState.Crouching;
            if (currentCrouchTime != 0)
                currentCrouchTime = crouchTime - currentCrouchTime;
        }
        else if (!isCrouch && isCrouching != IsCrouching.Standing)
        {
            //Start standing
            if (Physics.Raycast(headPosition.position, Vector3.up, 2.3f, groundLayer))
            {
                //Switching standing up under low ceiling and starting checkout
                print("Cannot stand!");
                isStandingUp = true;
                return;
            }
            //Switching standing up under low ceiling and end checkout
            isStandingUp = false;
            isCrouching = IsCrouching.Standing;
            crouchMultiplyer = 1f;

            currentCrouchState = isChangingCrouchState.Standing;
            if (currentCrouchTime != 0)
                currentCrouchTime = crouchTime - currentCrouchTime;
        }
    }


    //Check for hang up state
    void HangUpCheck()
    {
        if (!Physics.Raycast(cameraFront.position, -wallNormal, out var hit, 1f, groundLayer))
        {
            //Start hang up
            currentHangUpTime = 0f;
            currentWallState = WallState.HangUp;
            rb.useGravity = false;

            //Finding closect contact point
            var differenceY = cameraFront.position.y - closestWallContact.y;

            //Setting up next position
            hangUpStartPos = transform.position;
            hangUpControlPos = hangUpStartPos + Vector3.up * (2.6f - differenceY);
            nextHangUpPosition = transform.position - wallNormal + Vector3.up * (2.6f - differenceY);

            //Getting final relation between distances
            var sumDistance = 2.6f - differenceY + 1;
            var distToMax = sumDistance / 3.6f;
            currentFinalHangUpTime = Mathf.Lerp(minHangUpTime, maxHangUpTime, distToMax);
            upwardHangUpRelation = (2.6f - differenceY) / sumDistance;
            print(distToMax);
            print(upwardHangUpRelation);

            hangUpImpulseUp = false;
            hangUpImpulseForward = false;

            //Start animation
            if (features.enableHangUp)
                OnLifeCamera("hangup", new float[1] { currentFinalHangUpTime });
        }
    }

    //Change hang up position
    void OnHangUp()
    {
        currentHangUpTime += Time.deltaTime;
        float t = currentHangUpTime / currentFinalHangUpTime;
        t = Mathf.Clamp01(t);

        if (t < upwardHangUpRelation && !hangUpImpulseUp)
        {
            print("Go up!");
            // Движение вверх: 70% времени
            Vector3 upDistance = hangUpControlPos - hangUpStartPos;
            float duration = currentFinalHangUpTime * upwardHangUpRelation;
            Vector3 velocity = upDistance / duration;

            rb.velocity = Vector3.zero;
            rb.AddForce(velocity, ForceMode.VelocityChange);

            hangUpImpulseUp = true;
            
            //Hang up animation
            if (!isAlreadyAnimating)
            {
                isAlreadyAnimating = true;
                OnAnimating("H_Arms_Get_Up", "l_arm", false);
                OnAnimating("H_Arms_Get_Up", "r_arm", false);
            }
        }
        else if (t >= upwardHangUpRelation && !hangUpImpulseForward)
        {
            print("Go forward!");
            // Движение вперёд: оставшиеся 30% времени
            Vector3 forwardDistance = nextHangUpPosition - hangUpControlPos;
            float duration = currentFinalHangUpTime * (1 - upwardHangUpRelation);
            Vector3 velocity = forwardDistance / duration;

            rb.velocity = Vector3.zero;
            rb.AddForce(velocity, ForceMode.VelocityChange);

            hangUpImpulseForward = true;
        }

        if (t >= 1f)
        {
            isAlreadyAnimating = false;
            currentWallState = WallState.Sliding;
            lastState = currentState;
            OnFly();
        }
    }




    //Speed system
    void BuildSpeed(string type)
    {
        if (!features.enableSpeedSystem) return;

        momentum += speedPoints[type];
        if (momentum > maxMomentum) momentum = maxMomentum;
        if (momentum < 0) momentum = 0;
        currentMaxSpeed = Math.Max(maxLowSpeed, maxLowSpeed + momentum / maxMomentum * maxSpeedDifference);
    }


    //Crouch system
    //Handle smooth standing and crouching
    void SmoothCrouch(bool isCrouching)
    {
        currentCrouchTime += Time.deltaTime;
        float t = Mathf.Clamp01(currentCrouchTime / crouchTime);

        if (currentCrouchTime < crouchTime)
        {
            //handle crouch changes
            if (isCrouching)
            {
                // Interpolate to crouch
                //Scale collider down
                crouchCollider.height = Mathf.Lerp(2f, 2 * crouchHeadOffset, t);
                crouchCollider.center = Vector3.Lerp(Vector3.up * 0f, 2 * crouchHeadOffset / 2 * Vector3.up - Vector3.up, t);
                //Offset head
                headPosition.localPosition = Vector3.Lerp(headStartPosition, headCrouchPosition, t);
            }
            else
            {
                // Interpolate to stand
                //Scale collider up
                if (Physics.Raycast(headPosition.position, Vector3.up, 2.3f, groundLayer))
                {
                    //Switching standing up under low ceiling and starting checkout
                    OnCrouch(true);
                    print("Cannot stand!");
                    isStandingUp = true;
                    return;
                }
                crouchCollider.height = Mathf.Lerp(2 * crouchHeadOffset, 2f, t);
                crouchCollider.center = Vector3.Lerp(2 * crouchHeadOffset / 2 * Vector3.up - Vector3.up, Vector3.up * 0f, t);
                //Offset head
                headPosition.localPosition = Vector3.Lerp(headCrouchPosition, headStartPosition, t);
            }
        }
        else
        {
            //Handle final crouch/stand
            currentCrouchState = isChangingCrouchState.None;
            currentCrouchTime = 0;
            if (isCrouching)
            {
                //Crouch
                crouchCollider.height = 2 * crouchHeadOffset;
                crouchCollider.center = 2 * crouchHeadOffset / 2 * Vector3.up - Vector3.up;
                headPosition.localPosition = headCrouchPosition;
            }
            else
            {
                //Stand
                crouchCollider.height = 2f;
                crouchCollider.center = Vector3.up * 0f;
                headPosition.localPosition = headStartPosition;
            }
        }
    }

    //Crouch state mashine
    void HandleCrouch()
    {
        switch (currentCrouchState)
        {
            case isChangingCrouchState.Crouching:
                SmoothCrouch(true);
                break;
            case isChangingCrouchState.Standing:
                SmoothCrouch(false);
                break;
            case isChangingCrouchState.None:
                break;
        }
    }







    // //Event handle
    // //Ground Check events
    //Change grounded state
    void OnFly()
    {
        if (isGrounded == IsGrounded.InAir && currentState != BodyState.WallRunning)
            return;

        currentFallTime = 0;
        rb.useGravity = true;
        isGrounded = IsGrounded.InAir;
        groundNormal = Vector3.up;
        currentState = BodyState.InAir;
        justLanded = true;
        if (lastState == BodyState.WallRunning)
            if (features.enableLifeCamera)
                OnLifeCamera("fall", new float[1] { 0 });
    }


    void OnLand()
    {
        if (isGrounded == IsGrounded.Grounded)
            return;
        isGrounded = IsGrounded.Grounded;

        //Null normals and jumps
        savedNormal = Vector3.zero;
        wallNormal = Vector3.zero;

        //Wall run
        wallJumpCounter = 0;
        wallrunCounter = 0;
        justLanded = false;
        wallReferenceSaved = null;

        //Start animation
        if (currentFallTime >= minFallTime)
            OnLifeCamera("land", new float[1] { currentFallTime - minFallTime });
        else
            OnLifeCamera("none", new float[1] { (currentMaxSpeed - maxLowSpeed) / maxSpeedDifference });
    }


    //Handle collisions with surfaces
    void OnSurfaceCollide(ContactPoint[] contacts)
    {
        if (currentState == BodyState.Dashing)
            return;

        if (currentWallState == WallState.HangUp)
        {
            currentState = BodyState.WallRunning;
            return;
        }

        //If there is ono contact object is flying
        if (contacts.Length == 0)
        {
            lastState = currentState;
            OnFly();
            return;
        }
        counterOfAirFrames = 0;

        //Handling and counting multiple contacts
        int[] contactSurfaces = { 0, 0, 0, 0 };
        List<int> indexesGround = new List<int>();
        List<int> indexesSlope = new List<int>();
        List<int> indexesCeiling = new List<int>();
        List<int> indexesWall = new List<int>();
        for (int i = 0; i < contacts.Length; i++)
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
        if (contactSurfaces[0] > 0)
        {
            //Find main surface: closest to vector.up
            var curnormal = FindClosestToVector(indexesGround, contacts, Vector3.up);

            //Handle main logic
            HandleGround(curnormal);
            OnLand();
        }
        //Slope contact main
        else if (contactSurfaces[1] > 0 && features.enableSlide)
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestTo90(indexesSlope, contacts, out var index);

            //Handle main logic
            HandleSlope(curnormal, contacts[index].otherCollider.tag);
            OnLand();
        }
        //Wall contact main
        else if (contactSurfaces[3] > 0 && (features.enableWallRun || features.enableWallClimb || features.enableWallSlide))
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestTo90(indexesWall, contacts, out var index);
            closestWallContact = FindClosestToObjectContatct(indexesWall, contacts, transform.position);

            //Handle main logic
            HandleWall(curnormal, contacts[index]);
        }
        //Ceiling contact main
        else if (contactSurfaces[2] > 0)
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestToVector(indexesGround, contacts, Vector3.down);

            //Handle main logic
            HandleCeiling(curnormal);
        }

        //Clear all lists
        indexesGround.Clear();
        indexesSlope.Clear();
        indexesCeiling.Clear();
        indexesWall.Clear();
    }

    //Find main surface: closest to 90 degrees
    Vector3 FindClosestTo90(List<int> indexes, ContactPoint[] contacts, out int objIndex)
    {
        var closestNormal = Vector3.zero;
        var closestAngle = 0f;
        var num = 0;
        foreach (var i in indexes)
        {
            var currentNormal = contacts[i].normal;
            var angle = Vector3.Angle(currentNormal, Vector3.up);
            if (angle > closestAngle)
            {
                closestNormal = currentNormal;
                closestAngle = angle;
                num = i;
            }
        }
        objIndex = num;
        return closestNormal;
    }

    //Find main surface: closest to vector.up
    Vector3 FindClosestToVector(List<int> indexes, ContactPoint[] contacts, Vector3 vec)
    {
        var closestNormal = Vector3.zero;
        var closestDot = 0f;
        foreach (var i in indexes)
        {
            var currentNormal = contacts[i].normal;
            var dot = Vector3.Dot(currentNormal, vec);
            if (dot > closestDot)
            {
                closestNormal = currentNormal;
                closestDot = dot;
            }
        }
        return closestNormal;
    }

    Vector3 FindClosestToObjectContatct(List<int> indexes, ContactPoint[] contacts, Vector3 pos)
    {
        var closestPosition = Vector3.positiveInfinity;
        var closestDistance = Mathf.Infinity;
        foreach (var i in indexes)
        {
            var currentPosition = contacts[i].point;
            var dist = Vector3.Distance(currentPosition, pos);
            if (dist < closestDistance)
            {
                closestPosition = currentPosition;
                closestDistance = dist;
            }
        }
        return closestPosition;
    }




    void HandleCollisionWithObjects(ContactPoint[] contacts)
    {

        if (currentWallState == WallState.HangUp)
        {
            currentState = BodyState.WallRunning;
            return;
        }

        //If there is ono contact object is flying
        if (contacts.Length == 0)
        {
            OnFly();
            return;
        }

        //Handling and counting multiple contacts
        int[] contactSurfaces = { 0, 0, 0, 0 };
        List<int> indexesGround = new List<int>();
        List<int> indexesSlope = new List<int>();
        List<int> indexesCeiling = new List<int>();
        List<int> indexesWall = new List<int>();
        for (int i = 0; i < contacts.Length; i++)
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
        if (contactSurfaces[0] > 0)
        {
            //Find main surface: closest to vector.up
            var curnormal = FindClosestToVector(indexesGround, contacts, Vector3.up);

            //Handle main logic
            HandleGround(curnormal);
            OnLand();
            rb.useGravity = true;
        }
        //Slope contact main
        else if (contactSurfaces[1] > 0 && features.enableSlide)
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestTo90(indexesSlope, contacts, out var index);

            //Handle main logic
            HandleSlope(curnormal, contacts[index].otherCollider.tag);
            OnLand();
        }

        //Clear all lists
        indexesGround.Clear();
        indexesSlope.Clear();
        indexesCeiling.Clear();
        indexesWall.Clear();
    }


    void HandleGround(Vector3 normal)
    {

        groundNormal = normal;
        if (currentState != BodyState.Moving)
        {
            rb.useGravity = false;
            //Impulse when touching the ground after slope
            if (savedSlideNormal != Vector3.zero && groundNormal == Vector3.up && lastState == BodyState.Sliding)
            {
                var forceOfSlide = new Vector3(savedSlideNormal.x, 0, savedSlideNormal.z).normalized;
                rb.AddForce(forceOfSlide * currentMaxSpeed * 100, ForceMode.Impulse);
            }
            currentState = BodyState.Moving;
            savedSlideNormal = Vector3.zero;

        }
    }

    void HandleSlope(Vector3 normal, string tag)
    {

        groundNormal = normal;
        //Check for consistent slope
        if (savedSlideNormal != groundNormal && tag == "Slope")
            rb.velocity = Vector3.zero;

        if (savedSlideNormal == groundNormal)
            counterNormal++;
        else
            counterNormal = 0;

        if (counterNormal > 3)
        {
            BuildSpeed("slide");
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
                if (wallReference != wallReferenceSaved)
                {
                    runnedAlready = false;
                    stoppedByWall = true;
                }
                wallReferenceSaved = wallReference;
                OnCrouch(false);
            }

            lastWallNormal = normal;
        }
    }


    void HandleCeiling(Vector3 normal)
    {
        if (currentState == BodyState.Dashing)
            return;
        //rb.AddForce(normal * 100f, ForceMode.Impulse);
    }













    //Send command to life camera
    void OnLifeCamera(string type, float[] parameters = null)
    {
        if (!features.enableLifeCamera)
            return;
        if (parameters == null)
            parameters = new float[0];
        OnLifeCameraAction?.Invoke(type, parameters);
    }

    //Send command to animation
    void OnAnimating(string clipName, string bodyPart, bool loop)
    {
        switch (bodyPart)
        {
            case "l_arm":
                OnPlayAnimationLArm?.Invoke(clipName, loop);
                break;
            case "r_arm":
                OnPlayAnimationRArm?.Invoke(clipName, loop);
                break;
            case "leg":
                OnPlayAnimationRLeg?.Invoke(clipName, loop);
                break;
        }
    }


    void ChangeFOV(float num)
    {
        if (features.enableLifeCamera)
            features.cameraScrReference.ChangeFOV(num);
    }

    string GetCurrentLifeCameraState()
    {
        if (features.enableLifeCamera)
            return features.cameraScrReference.GetCurrentState();
        else
            return "-1";
    }
}

















