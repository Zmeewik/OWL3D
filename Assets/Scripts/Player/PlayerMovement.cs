using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Rigidbody)), RequireComponent(typeof(Collider))]
public class PlayerMovement : MonoBehaviour, IMovable, IWeaponCommand
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
    public FeatureFlags features = new FeatureFlags();

    [Header("References")]
    public Rigidbody rb;
    public Transform front;
    public Transform cameraFront;
    public CollisionCheck collisionScr;
    public SurfaceHandler surfaceHandler;
    public PlayerMovementConfig playerMovementConfig;
    public static Action<string, float[]> OnLifeCameraAction;

    

    // move
    //Move handle
    public Vector2 moveVector = Vector2.zero;


    // slide
    public int counterNormal = 0;
    public Vector3 savedSlideNormal = Vector3.zero;


    // wallrun
    Vector3 wallrunDirection;
    float wallRunStartTime;
    
    //wall climb
    float wallClimbStartTime;
    
    //wall slide
    public int checkWallCounter = 0;

    //Wall states
    public enum WallState { Sliding, Running, Climbing, HangUp }
    [HideInInspector] public WallState currentWallState = WallState.Sliding;
    [HideInInspector] public Transform wallReferenceSaved = null;
    [HideInInspector] public Transform wallReference = null;
    [HideInInspector] public bool runnedAlready;
    [HideInInspector] public bool stoppedByWall;
    [HideInInspector] public int wallrunCounter = 0;
    //Check for jump from the wall
    [HideInInspector] public int wallJumpCounter = 0;

    
    // hangup
    float currentFinalHangUpTime = 0;
    Vector3 hangUpStartPos;
    Vector3 hangUpControlPos;
    Vector3 nextHangUpPosition;
    float currentHangUpTime = 0f;
    float upwardHangUpRelation = 0;
    bool hangUpImpulseUp = false;
    bool hangUpImpulseForward = false;
    bool hangUpImpulseApplied = false;
    bool animatingArmsPutAway = false;



    // common fields
    [Header("Common Fields")]
    public LayerMask groundLayer;
    

    //Grounded check
    public enum IsGrounded { Grounded, InAir };
    [HideInInspector] public IsGrounded isGrounded = IsGrounded.Grounded;
    [HideInInspector] public bool justLanded = false;


    //State handle
    public enum BodyState { Moving, Dashing, WallRunning, Sliding, InAir };

    public BodyState CurrentState {
        get
        {
            return currentState;
        }
        set
        {
            currentState = value;
        }
    }
    BodyState currentState = BodyState.Moving;
    public BodyState lastState = BodyState.Moving;


    


    //Speed up system
    [HideInInspector] public float maxSpeedDifference;
    [HideInInspector] public float currentMaxSpeed;
    [HideInInspector] public float momentum = 0;
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
    public Action<string> OnWeaponCommand {get; set;}

    
    //Movement scripts
    [Header("SYSTEMS")]
    [SerializeField] public PlayerCrouch playerCrouch;
    [SerializeField] public PlayerDash playerDash;
    [SerializeField] public PlayerSurface playerSurface;
    [SerializeField] public PlayerJump playerJump;
    [SerializeField] public PlayerFly playerFly;

    //Start settings
    public void Start()
    {
        //Subscribe events
        //Events at ground change state
        collisionScr.OnGroundNormalChanged += playerSurface.OnSurfaceCollide;
        collisionScr.OnObjectNormalChanged += playerSurface.HandleCollisionWithObjects;
        maxSpeedDifference = playerMovementConfig.maxTopSpeed - playerMovementConfig.maxLowSpeed;
        currentMaxSpeed = playerMovementConfig.maxLowSpeed;

        //List to dictionary
        speedPoints = speedPointList.ToDictionary(entry => entry.key, entry => entry.value);

        playerCrouch.StartFunc();

        //Set particles to instance on scene
        particles = FindObjectOfType<Particles>();
        debugOutput = FindObjectOfType<DebugOutput>();
    }
    public void OnDisable()
    {
        //Unsubscribe events
        collisionScr.OnGroundNormalChanged -= playerSurface.OnSurfaceCollide;
        collisionScr.OnObjectNormalChanged -= playerSurface.HandleCollisionWithObjects;
    }





    //Physics handle
    private void FixedUpdate()
    {
        Vector3 horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        debugOutput.Output("Скорость: " + horizontalVel.magnitude.ToString("F2"), 1);
        debugOutput.Output("Максимальная скорость: " + (currentMaxSpeed * playerCrouch.CrouchMultiplyer()).ToString("F2"), 2);

        //Speed always goes down
        if (playerCrouch.IsPlayerCrouching() && horizontalVel != Vector3.zero)
            BuildSpeed("crouch");
        else if (horizontalVel != Vector3.zero || currentState == BodyState.WallRunning)
            BuildSpeed("none");
        else if (horizontalVel == Vector3.zero)
            BuildSpeed("not moving");

        //Handle crouch check
        if (playerCrouch.IsStandingUp())
        {
            OnCrouch(false);
        }
        playerCrouch.HandleCrouch();


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
                playerFly.HandleFallTime();
                if (features.enableMovement)
                {
                    Moving(playerMovementConfig.airControlMultiplier);
                    RotateBody();
                    CounterMovement();
                }
                break;
        }

        //Handle visual effects
        if (features.enableParticles)
        {
            if (currentMaxSpeed > playerMovementConfig.maxLowSpeed + maxSpeedDifference / 2)
            {
                //print("move fast");
                var alpha = (currentMaxSpeed - playerMovementConfig.maxLowSpeed - maxSpeedDifference / 2) / maxSpeedDifference * 2;
                var col = new Color[1] { new Color(1, 1, 1, alpha) };
                //print(col);
                particles.ChangeColor("MovementLines", col);
                particles.StartEffect("MovementLines");
                ChangeFOV(alpha);
            }
            else if (currentState == BodyState.InAir && rb.velocity.magnitude > playerMovementConfig.maxLowSpeed)
            {
                //print("move air");
                var alpha = Mathf.Min(playerFly.currentFallTime / playerMovementConfig.flyMaxParticleTime, 1);
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
            if (features.enableLifeCamera && playerCrouch.IsPlayerCrouching() && moveVector != Vector2.zero)
                OnLifeCamera("movement", new float[1] { 0 });
            else if (features.enableLifeCamera && moveVector != Vector2.zero)
                OnLifeCamera("movement", new float[1] { (currentMaxSpeed - playerMovementConfig.maxLowSpeed) / maxSpeedDifference });
            else if (features.enableLifeCamera && moveVector == Vector2.zero && state != "jump" && state != "land" && state != "hangUp" && state != "dash")
            {
                OnLifeCamera("none", new float[1] { (currentMaxSpeed - playerMovementConfig.maxLowSpeed) / maxSpeedDifference });
            }
        }
        else
        {
            if (playerFly.currentFallTime >= playerMovementConfig.minFallTime && features.enableLifeCamera)
                OnLifeCamera("fall", new float[1] { playerFly.currentDownFallTime });
        }

        if (features.enableMovement && moveVector != Vector2.zero)
        {
            //Trajectory projection at the ground surface
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, playerSurface.groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, playerSurface.groundNormal).normalized;

            rb.AddForce(surfaceRight * moveVector.x * playerMovementConfig.acceleration * airMultiplyer * playerCrouch.CrouchMultiplyer(), ForceMode.Acceleration);
            rb.AddForce(surfaceForward * moveVector.y * playerMovementConfig.acceleration * airMultiplyer * playerCrouch.CrouchMultiplyer(), ForceMode.Acceleration);
        }
    }


    //Grag
    void Drag()
    {
        if (features.enableMovement && moveVector == Vector2.zero)
        {
            Vector3 horizontalVel = new Vector3(rb.velocity.x, 0f, rb.velocity.z);
            horizontalVel = Vector3.ProjectOnPlane(horizontalVel, playerSurface.groundNormal);

            if (horizontalVel.magnitude > 0.5f)
            {
                Vector3 drag = -horizontalVel.normalized * playerMovementConfig.decceleration;
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
        horizontalVel = Vector3.ProjectOnPlane(horizontalVel, playerSurface.groundNormal);


        if (horizontalVel.magnitude > currentMaxSpeed * playerCrouch.CrouchMultiplyer() && currentState != BodyState.InAir)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * (playerMovementConfig.acceleration * 1.1f);
            rb.AddForce(counterForce, ForceMode.Acceleration);
        }
        // Counter force in the air
        else if (horizontalVel.magnitude > currentMaxSpeed * playerCrouch.CrouchMultiplyer() && currentState == BodyState.InAir)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * (playerMovementConfig.acceleration * 1.1f);
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


    //Slide auto movement
    private void Slide()
    {
        if (!features.enableSlide) return;

        //Getting vector down
        Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, playerSurface.groundNormal).normalized;
        //Get current slope velocity
        float currentSpeedOnSlope = Vector3.Dot(rb.velocity, slopeDir);
        //Add force until max
        if (currentSpeedOnSlope < playerMovementConfig.maxSlideSpeed)
        {
            rb.AddForce(slopeDir * -Physics.gravity.y * playerMovementConfig.slideSpeed, ForceMode.Acceleration);
        }
        //Sticking player to ground while sloping
        rb.AddForce(-playerSurface.groundNormal.normalized * 30, ForceMode.Acceleration);

        //Start animation
        if (features.enableLifeCamera)
            OnLifeCamera("slide", new float[1] { playerFly.currentDownFallTime });
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
            Vector3 wallRight = Vector3.Cross(Vector3.up, playerSurface.wallNormal).normalized;
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
        Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, playerSurface.groundNormal).normalized;
        Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, playerSurface.groundNormal).normalized;
        var currentMoveInputDirection = surfaceForward * moveVector.y + surfaceRight * moveVector.x;
        var moveDirectionDot = Vector3.Dot(currentMoveInputDirection, -playerSurface.wallNormal);
        print(moveDirectionDot);

        var rbMoveVector = transform.forward;
        var dot = Vector3.Dot(rbMoveVector, -playerSurface.wallNormal);
        Vector3 wallRight = Vector3.Cross(Vector3.up, playerSurface.wallNormal).normalized;
        Vector3 wallLeft = -wallRight;

        var dotRight = Vector3.Dot(rbMoveVector, wallRight);

        //Upward movement
        if (dot > 0.7f && moveDirectionDot > 0.7f && features.enableWallClimb)
        {
            print("start climb!");
            //Add maximum of continueing wall climb
            if (wallrunCounter >= playerMovementConfig.wallrunMaxCount)
                return;
            wallrunCounter++;

            wallClimbStartTime = Time.time;
            currentWallState = WallState.Climbing;
            runnedAlready = true;

            OnWeaponCommand?.Invoke("block_end");
            OnWeaponCommand?.Invoke("left_attack_cancel");
            OnWeaponCommand?.Invoke("right_attack_cancel");

            // Start animations
            if (features.enableLifeCamera)
                OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 1, dotRight });
            if (Physics.Raycast(cameraFront.position, -playerSurface.wallNormal, out RaycastHit wallHit, 1f, groundLayer))
            {
                OnAnimating("H_Arms_ClimbUp", "r_arm", false);
                OnAnimating("H_Arms_ClimbUp", "l_arm", false);
                animatingArmsPutAway = false;
            }
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
            Invoke("DeactivateWallRun", playerMovementConfig.wallrunTime);
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
        float t = Mathf.Clamp01(timeSinceStart / playerMovementConfig.wallClimbTime);
        //Multiplyer
        float forceMultiplier = Mathf.SmoothStep(1f, 0f, t);
        //Climb force
        float climbForce = playerMovementConfig.wallClimbForce * forceMultiplier;
        if (rb.velocity.y < playerMovementConfig.wallClimbForce * forceMultiplier)
            rb.AddForce(Vector3.up * climbForce * rb.mass, ForceMode.Impulse);
        else
        {
            //If overflow normalize speed
            var normSpeed = new Vector3(0, rb.velocity.y, 0).normalized * playerMovementConfig.wallClimbForce * forceMultiplier;
            rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z) + normSpeed;
        }

        //Check for hang up
        HangUpCheck();

        //Prevent animating arms if player turns away
        var turnPercent = Vector3.Dot(cameraFront.forward, -playerSurface.wallNormal);
        if (turnPercent < 0.5f && !animatingArmsPutAway)
        {
            OnAnimating("H_Arms_Boxing_PutAway", "r_arm", false);
            OnAnimating("H_Arms_Boxing_PutAway", "l_arm", false);
            animatingArmsPutAway = true;
        }

        //End climb after time
        if (timeSinceStart >= playerMovementConfig.wallClimbTime)
        {
            currentWallState = WallState.Sliding;
            return;
        }
    }

    //Add climb horizontal movement
    void HorizontalWallRun()
    {
        //Horizontal side wall run
        var speedH = new Vector3(rb.velocity.x, 0, rb.velocity.z);
        //Handle smooth movement 
        float timeSinceStart = Time.time - wallRunStartTime;
        float t = Mathf.Clamp01(timeSinceStart / playerMovementConfig.wallrunTime);
        float xMultiplier = Mathf.SmoothStep(1f, 0f, t);
        float horizontalForce = playerMovementConfig.wallrunForceX * xMultiplier;

        //Horizontal movement
        if (speedH.magnitude < playerMovementConfig.wallrunMaxForceX)
            rb.AddForce(wallrunDirection * horizontalForce * rb.mass, ForceMode.Acceleration);
        else
        {
            //If overflow onrmalize speed
            var normSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).normalized * playerMovementConfig.wallrunMaxForceX;
            rb.velocity = new Vector3(0, rb.velocity.y, 0) + normSpeed;
        }

        //Horizontal up wall run
        var speedV = rb.velocity.y;
        //Handle smooth movement in arch
        float yMultiplier = Mathf.Cos(t * Mathf.PI);
        float verticalForce = playerMovementConfig.wallRunForceY * Math.Abs(xMultiplier);

        //Vertical movement
        if (timeSinceStart < playerMovementConfig.wallrunTime / 2)
        {
            if (speedV < playerMovementConfig.wallrunMaxForceY * yMultiplier)
                rb.AddForce(Vector3.up * verticalForce * rb.mass, ForceMode.Acceleration);
            else
            {
                //If overflow onrmalize speed
                var normSpeed = new Vector3(0, rb.velocity.y, 0).normalized * playerMovementConfig.wallrunMaxForceY;
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
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, playerSurface.groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, playerSurface.groundNormal).normalized;
            Vector3 surfaceMoveDir = (surfaceRight * moveVector.x + surfaceForward * moveVector.y).normalized;
            //Getting current direction
            Vector3 wallRight = Vector3.Cross(Vector3.up, playerSurface.wallNormal).normalized;
            Vector3 wallLeft = -wallRight;

            //Getting wall move vector projection
            Vector3 wallMoveDir = Vector3.ProjectOnPlane(surfaceMoveDir, playerSurface.wallNormal);
            float wallMoveSpeed = wallMoveDir.magnitude;

            var dotRight = Vector3.Dot(surfaceMoveDir, wallRight);
            if (dotRight > 0)
                rb.AddForce(wallRight * (playerMovementConfig.acceleration * wallMoveSpeed * playerMovementConfig.airControlMultiplier) / 2, ForceMode.Acceleration);
            else if (dotRight < 0)
                rb.AddForce(wallLeft * (playerMovementConfig.acceleration * wallMoveSpeed * playerMovementConfig.airControlMultiplier) / 2, ForceMode.Acceleration);


            //Counter movement
            var horizontalVel = new Vector3(rb.velocity.x, 0, rb.velocity.z);
            if (horizontalVel.magnitude > currentMaxSpeed)
            {
                //Getting direction of movement
                Vector3 moveDir = horizontalVel.normalized;
                //Force to counter movement
                Vector3 counterForce = -moveDir * (playerMovementConfig.acceleration * playerMovementConfig.airControlMultiplier);
                rb.AddForce(counterForce, ForceMode.Acceleration);
            }
        }



        //Slow sliding at the wall
        if (rb.velocity.y < -playerMovementConfig.wallSlideMaxSpeed)
        {
            rb.AddForce(Vector3.up * 40, ForceMode.Acceleration);
        }
    }




    //Input handle
    //Standard moving
    public void OnJump()
    {
        playerJump.OnJump();
    }

    public void OnMove(Vector2 vector)
    {
        moveVector = vector;
    }

    public void OnDash()
    {
        playerDash.OnDash();
    }

    public void OnCrouch(bool isCrouching)
    {
        playerCrouch.OnCrouch(isCrouching);
    }


    //Check for hang up state
    void HangUpCheck()
    {
        if (!Physics.Raycast(transform.position + new Vector3(0, playerMovementConfig.hangUpHeight, 0), -playerSurface.wallNormal, out var hit, 1f, groundLayer))
        {
            //Find point on the clif where I need to be at end
            Vector3 rayOrigin = transform.position + -playerSurface.wallNormal * 0.7f + Vector3.up * playerMovementConfig.hangUpHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit downHit, playerMovementConfig.hangUpHeight * 2, groundLayer))
            {
                nextHangUpPosition = downHit.point + playerSurface.wallNormal * playerMovementConfig.forwardOffset + 3f * Vector3.up;
            }
            else
            {
                currentWallState = WallState.Sliding;
                return;
            }

            print("Start hang up!");
            print(nextHangUpPosition);

            //Start hang up
            hangUpStartPos = transform.position;
            currentHangUpTime = 0f;
            hangUpImpulseApplied = false;
            currentFinalHangUpTime = UnityEngine.Random.Range(playerMovementConfig.minHangUpTime, playerMovementConfig.maxHangUpTime);
            currentWallState = WallState.HangUp;

            //Start animation
            if (features.enableHangUp)
            {
                if (Physics.Raycast(transform.position, -playerSurface.wallNormal, out RaycastHit wallHit, 1f, groundLayer))
                {
                    OnLifeCamera("hangup", new float[2] { currentFinalHangUpTime, 1f });
                }
                else
                {
                    OnLifeCamera("hangup", new float[2] { currentFinalHangUpTime, 0.3f });
                }
            }

            //Hang up animation
            if (!isAlreadyAnimating)
            {
                isAlreadyAnimating = true;

                //Check for the wall in front of player to start get up animation
                if (Physics.Raycast(transform.position, -playerSurface.wallNormal, out RaycastHit wallHit, 1f, groundLayer))
                {
                    OnAnimating("H_Arms_Get_Up", "l_arm", false);
                    OnAnimating("H_Arms_Get_Up", "r_arm", false);
                }
            }
        }
    }

    //Change hang up position
    public void OnHangUp()
    {
        currentHangUpTime += Time.deltaTime;
        float t = currentHangUpTime / currentFinalHangUpTime;
        t = Mathf.Clamp01(t);

        if (t < 0.8f && !hangUpImpulseUp)
        {
            print("Go up!");
            //Getting next position vector
            Vector3 start = hangUpStartPos;
            Vector3 end = new Vector3(hangUpStartPos.x, nextHangUpPosition.y, hangUpStartPos.z);
            Vector3 displacement = end - start;

            float g = Mathf.Abs(Physics.gravity.y);
            float time = currentFinalHangUpTime * 0.8f;

            //Counting paraboloid movement velocity
            Vector3 velocity = new Vector3(
                displacement.x / time,
                (displacement.y / time) + (0.5f * g * time),
                displacement.z / time
            );

            rb.velocity = Vector3.zero;
            rb.AddForce(velocity, ForceMode.VelocityChange);

            hangUpImpulseUp = true;
        }
        else if (t >= 0.8f && !hangUpImpulseForward)
        {
            print("Go forward!");
            // Движение вперёд
            //Getting next position vector
            Vector3 start = transform.position;
            Vector3 end = nextHangUpPosition;
            Vector3 displacement = end - start;

            float g = Mathf.Abs(Physics.gravity.y);
            float time = currentFinalHangUpTime * 0.2f;

            //Counting paraboloid movement velocity
            Vector3 velocity = -new Vector3(
                displacement.x / time,
                (displacement.y / time) + (0.5f * g * time),
                displacement.z / time
            );

            print(velocity);

            rb.AddForce(velocity, ForceMode.VelocityChange);

            hangUpImpulseForward = true;
        }

        if (t >= 1f)
        {
            isAlreadyAnimating = false;
            hangUpImpulseUp = false;
            hangUpImpulseForward = false;
            currentWallState = WallState.Sliding;
            lastState = currentState;
            OnFly();
        }
    }




    //Speed system
    public void BuildSpeed(string type)
    {
        if (!features.enableSpeedSystem) return;

        momentum += speedPoints[type];
        if (momentum > playerMovementConfig.maxMomentum) momentum = playerMovementConfig.maxMomentum;
        if (momentum < 0) momentum = 0;
        currentMaxSpeed = Math.Max(playerMovementConfig.maxLowSpeed, playerMovementConfig.maxLowSpeed + momentum / playerMovementConfig.maxMomentum * maxSpeedDifference);
    }


    //Get something from children


    // //Event handle
    
    // Ground control
    public void OnFly()
    {
        playerFly.OnFly();
    }
    
    public void OnLand()
    {
        playerFly.OnLand();
    }
    
    //Send command to life camera
    public void OnLifeCamera(string type, float[] parameters = null)
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