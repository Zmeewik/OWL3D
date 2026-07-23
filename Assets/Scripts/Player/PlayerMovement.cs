using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

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
        public bool enableSlideGround = true;
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
    public Vector2 moveVector => playerWalking.moveVector;

    // common fields
    [Header("Common Fields")]
    public LayerMask groundLayer;
    //Grounded check
    public enum IsGrounded { Grounded, InAir };
    [HideInInspector] public IsGrounded isGrounded = IsGrounded.Grounded;
    [HideInInspector] public bool justLanded = false;


    //State handle
    public enum BodyState { Moving, Dashing, WallRunning, Sliding, InAir, SlidingGround };

    public BodyState CurrentState {
        get
        {
            return currentState;
        }
        set
        {
            /*UnityEngine.Debug.Log($"STATE {currentState} -> {value}");
            UnityEngine.Debug.Log(new StackTrace());*/
            currentState = value;
        }
    }
    BodyState currentState = BodyState.Moving;
    public BodyState lastState = BodyState.Moving;
    
    // Momentum
    public float currentMaxSpeed => playerMomentum.currentMaxSpeed;
    public float maxSpeedDifference => playerMomentum.maxSpeedDifference;

    //Animations
    Particles particles;
    DebugOutput debugOutput;
    [HideInInspector] public bool isAlreadyAnimating;
    public static Action<string, bool> OnPlayAnimationLArm;
    public static Action<string, bool> OnPlayAnimationRArm;
    public static Action<string, bool> OnPlayAnimationRLeg;
    public static Action<string, float[]> OnArmsMove;
    public Action<string> OnWeaponCommand {get; set;}
    [HideInInspector] public bool animatingArmsPutAway = false;

    
    //Movement scripts
    [Header("SYSTEMS")] [SerializeField] public PlayerWalking playerWalking;
    [SerializeField] public PlayerCrouch playerCrouch;
    [SerializeField] public PlayerDash playerDash;
    [SerializeField] public PlayerSurface playerSurface;
    [SerializeField] public PlayerJump playerJump;
    [SerializeField] public PlayerFly playerFly;
    [SerializeField] public PlayerSlide playerSlide;
    [SerializeField] public PlayerWallRun playerWallRun;
    [SerializeField] public PlayerHangUp playerHangUp;
    [SerializeField] public PlayerMomentum playerMomentum;

    //Start settings
    public void Start()
    {
        //Subscribe events
        //Events at ground change state
        collisionScr.OnGroundNormalChanged += playerSurface.OnSurfaceCollide;
        collisionScr.OnObjectNormalChanged += playerSurface.HandleCollisionWithObjects;

        // Start Setup functions
        playerMomentum.StartFunc();
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
        var dot = Vector2.Dot(
            moveVector.normalized,
            Vector2.up);
        if (playerCrouch.IsPlayerCrouching() && currentState != BodyState.InAir && currentState != BodyState.SlidingGround && horizontalVel != Vector3.zero)
            BuildSpeed("crouch");
        else if(horizontalVel.magnitude < 0.3f * currentMaxSpeed && isGrounded == IsGrounded.Grounded)
            BuildSpeed("not moving");
        // If moves forward
        else if (dot > 0.707f && !playerCrouch.IsPlayerCrouching() && horizontalVel.magnitude > 0.5f * currentMaxSpeed && playerFly)
            BuildSpeed("move_forward");
        else if (moveVector != Vector2.zero || currentState == BodyState.WallRunning)
            BuildSpeed("none");
        else if (moveVector == Vector2.zero && isGrounded == IsGrounded.Grounded && currentState != BodyState.SlidingGround)
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
                    playerWalking.Moving();
                    playerWalking.RotateBody();
                    playerWalking.Drag();
                    playerWalking.CounterMovement();
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
                    playerWallRun.WallRun();
                    playerWalking.RotateBody();
                }
                else
                    currentState = BodyState.InAir;
                break;
            case BodyState.Sliding:
                if (features.enableSlide)
                {
                    playerWalking.RotateBody();
                    playerSlide.Slide();
                }
                break;
            case BodyState.InAir:
                //Counting fall time
                playerFly.HandleFallTime();
                if (features.enableMovement)
                {
                    playerWalking.Moving(playerMovementConfig.airControlMultiplier);
                    playerWalking.RotateBody();
                    playerWalking.CounterMovement();
                }
                break;
            case BodyState.SlidingGround:
                if (features.enableSlideGround)
                {
                    playerSlide.SlideGround();
                }
                break;
        }
        
        //UnityEngine.Debug.Log(currentState);

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
    

    //Input handle
    //Standard moving
    public void OnJump()
    {
        playerJump.OnJump();
    }

    public void OnMove(Vector2 vector)
    {
        playerWalking.OnMove(vector);
    }

    public void OnDash()
    {
        playerDash.OnDash();
    }
    
    public void OnCrouch(bool isCrouching)
    {
        var currentMaxspeed_temp =
            currentMaxSpeed * (playerCrouch.IsPlayerCrouching() ? playerCrouch.CrouchMultiplyer() : 1);
        
        // Crouch handle
        if (currentState != BodyState.SlidingGround)
            playerCrouch.OnCrouch(isCrouching);
        else
            playerCrouch.savedCrouch = isCrouching;
        
        // Slide handle
        if (currentState != BodyState.SlidingGround)
        {
            var currentSpeedMagnitude = (currentMaxspeed_temp - playerMovementConfig.maxLowSpeed) /
                                        (playerMovementConfig.maxTopSpeed - playerMovementConfig.maxLowSpeed);
            //print(currentSpeedMagnitude);
            if (isCrouching &&
                isGrounded == IsGrounded.Grounded &&
                currentSpeedMagnitude > playerMovementConfig.minSlideGroundMovementSpeed)
            {
                //print("Sliding");
                playerSlide.StartSlideGround(currentMaxspeed_temp);
            }
            else
            {
                //print("Not sliding");
            }
        }
    }


   
    // Momentum control
     //Speed system
    public void BuildSpeed(string type)
    {
        playerMomentum.BuildSpeed(type);
    }

    //Hang up control
    public void HangUpCheck()
    {
        playerHangUp.HangUpCheck();
    }
    public void OnHangUp()
    {
        playerHangUp.OnHangUp();
    }

    ////Event handle
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
    public void OnAnimating(string clipName, string bodyPart, bool loop)
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

    // Camera FOV and LifeCamera
    public void ChangeFOV(float num)
    {
        if (features.enableLifeCamera)
            features.cameraScrReference.ChangeFOV(num);
    }

    public string GetCurrentLifeCameraState()
    {
        if (features.enableLifeCamera)
            return features.cameraScrReference.GetCurrentState();
        else
            return "-1";
    }
}