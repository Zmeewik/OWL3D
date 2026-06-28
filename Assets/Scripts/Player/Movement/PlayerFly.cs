using UnityEngine;

public class PlayerFly : MonoBehaviour
{
     [SerializeField] private PlayerMovement playerMovement;
     [HideInInspector] public float currentFallTime;
     [HideInInspector] public float currentDownFallTime;

     public void HandleFallTime()
     {
         if (playerMovement.rb.velocity.y < 0)
             currentDownFallTime += Time.deltaTime;
         else
             currentDownFallTime = 0;
         currentFallTime += Time.deltaTime;
     }

     // //Ground Check events
    //Change grounded state
    public void OnFly()
    {
        if (playerMovement.isGrounded == PlayerMovement.IsGrounded.InAir && playerMovement.CurrentState != PlayerMovement.BodyState.WallRunning)
            return;

        //If climb higher clif slow down speed
        if (playerMovement.playerWallRun.currentWallState == PlayerWallRun.WallState.Climbing && playerMovement.CurrentState == PlayerMovement.BodyState.WallRunning)
            playerMovement.rb.velocity = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z);

        currentFallTime = 0;
        playerMovement.rb.useGravity = true;
        playerMovement.isGrounded = PlayerMovement.IsGrounded.InAir;
        playerMovement.playerSurface.groundNormal = Vector3.up;
        playerMovement.CurrentState = PlayerMovement.BodyState.InAir;
        playerMovement.justLanded = true;
        if (playerMovement.lastState == PlayerMovement.BodyState.WallRunning)
            if (playerMovement.features.enableLifeCamera)
                playerMovement.OnLifeCamera("fall", new float[1] { 0 });
    }


    public void OnLand()
    {
        //Check up for a hang up state
        if (playerMovement.playerWallRun.currentWallState == PlayerWallRun.WallState.HangUp && playerMovement.CurrentState == PlayerMovement.BodyState.WallRunning)
            return;

        if (playerMovement.isGrounded == PlayerMovement.IsGrounded.Grounded)
            return;
        playerMovement.isGrounded = PlayerMovement.IsGrounded.Grounded;

        //Null normals and jumps
        playerMovement.playerSurface.wallNormal = Vector3.zero;

        //Wall run
        playerMovement.playerWallRun.wallJumpCounter = 0;
        playerMovement.playerWallRun.wallrunCounter = 0;
        playerMovement.justLanded = false;
        playerMovement.playerWallRun.wallReferenceSaved = null;

        //Start animation
        if (currentFallTime >= playerMovement.playerMovementConfig.minFallTime)
            playerMovement.OnLifeCamera("land", new float[1] { currentFallTime - playerMovement.playerMovementConfig.minFallTime });
        else
            playerMovement.OnLifeCamera("none", new float[1] { (playerMovement.currentMaxSpeed - playerMovement.playerMovementConfig.maxLowSpeed) / playerMovement.maxSpeedDifference });
    }
}