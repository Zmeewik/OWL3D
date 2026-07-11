using UnityEngine;

public class PlayerWalking : MonoBehaviour
{
    [SerializeField] private PlayerMovement playerMovement;
    [HideInInspector] public Vector2 moveVector = Vector2.zero;
    
    //Movement states
    public void Moving(float airMultiplyer = 1)
    {
        //Handle movement of player
        //Adding force to object until reaching max speed
        if (airMultiplyer == 1)
        {
            var horizontalVel = new Vector3(playerMovement.rb.velocity.x, 0f, playerMovement.rb.velocity.z);
            var state = playerMovement.GetCurrentLifeCameraState();
            if (playerMovement.features.enableLifeCamera && playerMovement.playerCrouch.IsPlayerCrouching() && moveVector != Vector2.zero)
                playerMovement.OnLifeCamera("movement", new float[1] { 0 });
            else if (playerMovement.features.enableLifeCamera && moveVector != Vector2.zero && horizontalVel.magnitude > 0.5f * playerMovement.currentMaxSpeed)
                playerMovement.OnLifeCamera("movement", new float[1] { (playerMovement.currentMaxSpeed - playerMovement.playerMovementConfig.maxLowSpeed) / playerMovement.maxSpeedDifference });
            else if (playerMovement.features.enableLifeCamera && moveVector != Vector2.zero)
                playerMovement.OnLifeCamera("none", new float[1] { (playerMovement.currentMaxSpeed - playerMovement.playerMovementConfig.maxLowSpeed) / playerMovement.maxSpeedDifference });
            else if (playerMovement.features.enableLifeCamera && moveVector == Vector2.zero && state != "jump" && state != "land" && state != "hangUp" && state != "dash")
            {
                playerMovement.OnLifeCamera("none", new float[1] { (playerMovement.currentMaxSpeed - playerMovement.playerMovementConfig.maxLowSpeed) / playerMovement.maxSpeedDifference });
            }
        }
        else
        {
            var y_force = Mathf.Abs(playerMovement.rb.velocity.y);
            if (y_force >= playerMovement.playerMovementConfig.minFallForce && playerMovement.features.enableLifeCamera)
                playerMovement.OnLifeCamera("fall", new float[1] { y_force });
        }

        if (playerMovement.features.enableMovement && moveVector != Vector2.zero)
        {
            //Trajectory projection at the ground surface
            Vector3 surfaceForward = Vector3.ProjectOnPlane(transform.forward, playerMovement.playerSurface.groundNormal).normalized;
            Vector3 surfaceRight = Vector3.ProjectOnPlane(transform.right, playerMovement.playerSurface.groundNormal).normalized;

            playerMovement.rb.AddForce(surfaceRight * (moveVector.x * playerMovement.playerMovementConfig.acceleration * airMultiplyer * playerMovement.playerCrouch.CrouchMultiplyer()), ForceMode.Acceleration);
            playerMovement.rb.AddForce(surfaceForward * (moveVector.y * playerMovement.playerMovementConfig.acceleration * airMultiplyer * playerMovement.playerCrouch.CrouchMultiplyer()), ForceMode.Acceleration);
        }
    }


    //Grag
    public void Drag()
    {
        if (playerMovement.features.enableMovement && moveVector == Vector2.zero)
        {
            Vector3 horizontalVel = new Vector3(playerMovement.rb.velocity.x, 0f, playerMovement.rb.velocity.z);
            horizontalVel = Vector3.ProjectOnPlane(horizontalVel, playerMovement.playerSurface.groundNormal);

            if (horizontalVel.magnitude > playerMovement.playerMovementConfig.decceleration / 50)
            {
                Vector3 drag = -horizontalVel.normalized * playerMovement.playerMovementConfig.decceleration;
                playerMovement.rb.AddForce(drag, ForceMode.Acceleration);
            }
            else
            {
                playerMovement.rb.velocity = new Vector3(0f, playerMovement.rb.velocity.y, 0f);
            }
        }
    }


    //Counter movement if player cross the limit speed
    public void CounterMovement()
    {
        if (!playerMovement.features.enableMovement) return;

        //Check for limit overflow
        //Counter force at the ground
        Vector3 horizontalVel = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z);
        if (playerMovement.CurrentState == PlayerMovement.BodyState.Sliding || playerMovement.CurrentState == PlayerMovement.BodyState.Moving)
        {
            horizontalVel = new Vector3(playerMovement.rb.velocity.x, playerMovement.rb.velocity.y, playerMovement.rb.velocity.z);
        }
        horizontalVel = Vector3.ProjectOnPlane(horizontalVel, playerMovement.playerSurface.groundNormal);


        if (horizontalVel.magnitude > playerMovement.currentMaxSpeed * playerMovement.playerCrouch.CrouchMultiplyer() && playerMovement.CurrentState != PlayerMovement.BodyState.InAir)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * (playerMovement.playerMovementConfig.acceleration * 1.1f);
            playerMovement.rb.AddForce(counterForce, ForceMode.Acceleration);
        }
        // Counter force in the air
        else if (horizontalVel.magnitude > playerMovement.currentMaxSpeed * playerMovement.playerCrouch.CrouchMultiplyer() && playerMovement.CurrentState == PlayerMovement.BodyState.InAir)
        {
            //Getting direction of movement
            Vector3 moveDir = horizontalVel.normalized;

            //Force to counter movement
            Vector3 counterForce = -moveDir * (playerMovement.playerMovementConfig.acceleration * 1.1f);
            playerMovement.rb.AddForce(counterForce, ForceMode.Acceleration);
        }

    }

    //Rotating
    public void RotateBody()
    {
        if (!playerMovement.features.enableMovement) return;

        //Getting forward of the camera
        Vector3 forward = playerMovement.cameraFront.forward;
        forward.y = 0f;
        forward.Normalize();

        //Rotating player toward camera
        Quaternion targetRotation = Quaternion.LookRotation(forward);
        playerMovement.rb.MoveRotation(targetRotation);
    }
    
    
    public void OnMove(Vector2 vector)
    {
        moveVector = vector;
    }
}