using System;
using UnityEngine;

public class PlayerWallRun: MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    
    // wallrun
    Vector3 wallrunDirection;
    float wallRunStartTime;
    
    //wall climb
    float wallClimbStartTime;
    
    //wall slide
    [HideInInspector] public int checkWallCounter = 0;

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
    
    //Movement close to the wall
    //Wall run at different directions
    public void WallRun()
    {
        //Activate wall run at first collision
        if (!runnedAlready)
            WallRunStart();

        //Starting calculations and change life camera state
        if (playerMovement.features.enableLifeCamera)
        {
            //Calculate vectore
            var rbMoveVector = transform.forward;
            Vector3 wallRight = Vector3.Cross(Vector3.up, playerMovement.playerSurface.wallNormal).normalized;
            var dotRight = Vector3.Dot(rbMoveVector, wallRight);

            //Change life camera state
            switch (currentWallState)
            {
                case WallState.Climbing:
                    if (playerMovement.features.enableWallClimb)
                        playerMovement.OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 1, dotRight });
                    else
                        playerMovement.OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 2, dotRight });
                    break;
                case WallState.Running:
                    if (playerMovement.features.enableWallRun)
                        playerMovement.OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 0, dotRight });
                    else
                        playerMovement.OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 2, dotRight });
                    break;
                case WallState.Sliding:
                    if (playerMovement.features.enableWallSlide)
                        playerMovement.OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 2, dotRight });
                    break;
            }
        }

        //Main movement handle
        switch (currentWallState)
        {
            case WallState.Climbing:
                if (playerMovement.features.enableWallClimb)
                    ClimbWallRun();
                else
                    currentWallState = WallState.Sliding;
                break;
            case WallState.Running:
                if (playerMovement.features.enableWallRun)
                    HorizontalWallRun();
                else
                    currentWallState = WallState.Sliding;
                break;
            case WallState.Sliding:
                if (playerMovement.features.enableWallSlide)
                    WallSlide();
                else
                    playerMovement.OnFly();
                break;
            case WallState.HangUp:
                if (playerMovement.features.enableHangUp)
                    playerMovement.OnHangUp();
                else
                    currentWallState = WallState.Climbing;
                break;
        }

    }

    //Start wall run direction
    void WallRunStart()
    {
        //Calculating vectors
        Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, playerMovement.playerSurface.groundNormal).normalized;
        Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, playerMovement.playerSurface.groundNormal).normalized;
        var currentMoveInputDirection = surfaceForward * playerMovement.moveVector.y + surfaceRight * playerMovement.moveVector.x;
        var moveDirectionDot = Vector3.Dot(currentMoveInputDirection, -playerMovement.playerSurface.wallNormal);
        print(moveDirectionDot);

        var rbMoveVector = transform.forward;
        var dot = Vector3.Dot(rbMoveVector, -playerMovement.playerSurface.wallNormal);
        Vector3 wallRight = Vector3.Cross(Vector3.up, playerMovement.playerSurface.wallNormal).normalized;
        Vector3 wallLeft = -wallRight;

        var dotRight = Vector3.Dot(rbMoveVector, wallRight);

        //Upward movement
        if (dot > 0.7f && moveDirectionDot > 0.7f && playerMovement.features.enableWallClimb)
        {
            print("start climb!");
            //Add maximum of continueing wall climb
            if (wallrunCounter >= playerMovement.playerMovementConfig.wallClimbMaxJumps)
                return;
            wallrunCounter++;

            wallClimbStartTime = Time.time;
            currentWallState = WallState.Climbing;
            runnedAlready = true;

            playerMovement.OnWeaponCommand?.Invoke("block_end");
            playerMovement.OnWeaponCommand?.Invoke("left_attack_cancel");
            playerMovement.OnWeaponCommand?.Invoke("right_attack_cancel");

            // Start animations
            if (playerMovement.features.enableLifeCamera)
                playerMovement.OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 1, dotRight });
            if (Physics.Raycast(playerMovement.cameraFront.position, -playerMovement.playerSurface.wallNormal, out RaycastHit wallHit, 1f, playerMovement.groundLayer))
            {
                playerMovement.OnAnimating("H_Arms_ClimbUp", "r_arm", false);
                playerMovement.OnAnimating("H_Arms_ClimbUp", "l_arm", false);
                playerMovement.animatingArmsPutAway = false;
            }
        }
        //Left/Right movement
        else if (dot < 0.7f && dot > -0.5f && moveDirectionDot < 0.7f && moveDirectionDot > 0f && playerMovement.features.enableWallRun)
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
            Invoke("DeactivateWallRun", playerMovement.playerMovementConfig.wallrunTime);
            wallRunStartTime = Time.time;
            currentWallState = WallState.Running;
            playerMovement.BuildSpeed("wallrun");
            runnedAlready = true;
            if (playerMovement.features.enableLifeCamera)
                playerMovement.OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 0, dotRight });
        }
        else
        {
            if (playerMovement.features.enableLifeCamera)
                playerMovement.OnLifeCamera("wallrun", new float[3] { dotRight > 0 ? 1 : 0, 2, dotRight });
            currentWallState = WallState.Sliding;
        }

        if (!stoppedByWall)
        {
            //Nullifying start speed
            stoppedByWall = true;
            playerMovement.rb.velocity = new Vector3(0, 0, 0);
        }

    }

    //Add climb vertical movement
    void ClimbWallRun()
    {
        //Smooth movement handle
        //Timer from climb start
        float timeSinceStart = Time.time - wallClimbStartTime;
        //Progress percent
        float t = Mathf.Clamp01(timeSinceStart / playerMovement.playerMovementConfig.wallClimbTime);
        //Multiplyer
        float forceMultiplier = Mathf.SmoothStep(1f, 0f, t);
        //Climb force
        float climbForce = playerMovement.playerMovementConfig.wallClimbForce * forceMultiplier;
        if (playerMovement.rb.velocity.y < playerMovement.playerMovementConfig.wallClimbForce * forceMultiplier)
            playerMovement.rb.AddForce(Vector3.up * (climbForce * playerMovement.rb.mass), ForceMode.Impulse);
        else
        {
            //If overflow normalize speed
            var normSpeed = new Vector3(0, playerMovement.rb.velocity.y, 0).normalized * (playerMovement.playerMovementConfig.wallClimbForce * forceMultiplier);
            playerMovement.rb.velocity = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z) + normSpeed;
        }

        //Check for hang up
        playerMovement.HangUpCheck();

        //Prevent animating arms if player turns away
        var turnPercent = Vector3.Dot(playerMovement.cameraFront.forward, -playerMovement.playerSurface.wallNormal);
        if (turnPercent < 0.5f && !playerMovement.animatingArmsPutAway)
        {
            playerMovement.OnAnimating("H_Arms_Boxing_PutAway", "r_arm", false);
            playerMovement.OnAnimating("H_Arms_Boxing_PutAway", "l_arm", false);
            playerMovement.animatingArmsPutAway = true;
        }

        //End climb after time
        if (timeSinceStart >= playerMovement.playerMovementConfig.wallClimbTime)
        {
            currentWallState = WallState.Sliding;
            return;
        }
    }

    //Add climb horizontal movement
    void HorizontalWallRun()
    {
        //Horizontal side wall run
        var speedH = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z);
        //Handle smooth movement 
        float timeSinceStart = Time.time - wallRunStartTime;
        float t = Mathf.Clamp01(timeSinceStart / playerMovement.playerMovementConfig.wallrunTime);
        float xMultiplier = Mathf.SmoothStep(1f, 0f, t);
        float horizontalForce = playerMovement.playerMovementConfig.wallrunForceX * xMultiplier;

        //Horizontal movement
        if (speedH.magnitude < playerMovement.playerMovementConfig.wallrunMaxForceX)
            playerMovement.rb.AddForce(wallrunDirection * horizontalForce * playerMovement.rb.mass, ForceMode.Acceleration);
        else
        {
            //If overflow onrmalize speed
            var normSpeed = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z).normalized * playerMovement.playerMovementConfig.wallrunMaxForceX;
            playerMovement.rb.velocity = new Vector3(0, playerMovement.rb.velocity.y, 0) + normSpeed;
        }

        //Horizontal up wall run
        var speedV = playerMovement.rb.velocity.y;
        //Handle smooth movement in arch
        float yMultiplier = Mathf.Cos(t * Mathf.PI);
        float verticalForce = playerMovement.playerMovementConfig.wallRunForceY * Math.Abs(xMultiplier);

        //Vertical movement
        if (timeSinceStart < playerMovement.playerMovementConfig.wallrunTime / 2)
        {
            if (speedV < playerMovement.playerMovementConfig.wallrunMaxForceY * yMultiplier)
                playerMovement.rb.AddForce(Vector3.up * verticalForce * playerMovement.rb.mass, ForceMode.Acceleration);
            else
            {
                //If overflow onrmalize speed
                var normSpeed = new Vector3(0, playerMovement.rb.velocity.y, 0).normalized * playerMovement.playerMovementConfig.wallrunMaxForceY;
                playerMovement.rb.velocity = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z) + normSpeed;
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
        if (playerMovement.moveVector != Vector2.zero)
        {
            //Finding direction of movement
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, playerMovement.playerSurface.groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, playerMovement.playerSurface.groundNormal).normalized;
            Vector3 surfaceMoveDir = (surfaceRight * playerMovement.moveVector.x + surfaceForward * playerMovement.moveVector.y).normalized;
            //Getting current direction
            Vector3 wallRight = Vector3.Cross(Vector3.up, playerMovement.playerSurface.wallNormal).normalized;
            Vector3 wallLeft = -wallRight;

            //Getting wall move vector projection
            Vector3 wallMoveDir = Vector3.ProjectOnPlane(surfaceMoveDir, playerMovement.playerSurface.wallNormal);
            float wallMoveSpeed = wallMoveDir.magnitude;

            var dotRight = Vector3.Dot(surfaceMoveDir, wallRight);
            if (dotRight > 0)
                playerMovement.rb.AddForce(wallRight * (playerMovement.playerMovementConfig.acceleration * wallMoveSpeed * playerMovement.playerMovementConfig.airControlMultiplier) / 2, ForceMode.Acceleration);
            else if (dotRight < 0)
                playerMovement.rb.AddForce(wallLeft * (playerMovement.playerMovementConfig.acceleration * wallMoveSpeed * playerMovement.playerMovementConfig.airControlMultiplier) / 2, ForceMode.Acceleration);


            //Counter movement
            var horizontalVel = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z);
            if (horizontalVel.magnitude > playerMovement.currentMaxSpeed)
            {
                //Getting direction of movement
                Vector3 moveDir = horizontalVel.normalized;
                //Force to counter movement
                Vector3 counterForce = -moveDir * (playerMovement.playerMovementConfig.acceleration * playerMovement.playerMovementConfig.airControlMultiplier);
                playerMovement.rb.AddForce(counterForce, ForceMode.Acceleration);
            }
        }



        //Slow sliding at the wall
        if (playerMovement.rb.velocity.y < -playerMovement.playerMovementConfig.wallSlideMaxSpeed)
        {
            playerMovement.rb.AddForce(Vector3.up * 40, ForceMode.Acceleration);
        }
    }
}