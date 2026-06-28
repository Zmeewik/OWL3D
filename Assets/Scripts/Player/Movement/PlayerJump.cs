using UnityEngine;

public class PlayerJump: MonoBehaviour
{
    [SerializeField] PlayerMovement playerMovement;
    
    public void OnJump()
    {
        if (!playerMovement.features.enableJump) return;

        //print(isGrounded);
        if (playerMovement.isGrounded == PlayerMovement.IsGrounded.Grounded && playerMovement.CurrentState != PlayerMovement.BodyState.Sliding)
        {
            playerMovement.rb.AddForce(Vector2.up * playerMovement.playerMovementConfig.jumpForce * playerMovement.rb.mass, ForceMode.Impulse);
            playerMovement.BuildSpeed("jump");
            playerMovement. OnCrouch(false);
            if (playerMovement.features.enableLifeCamera)
                playerMovement.OnLifeCamera("jump");
        }
        //If on the wall go a little forward 
        else if (playerMovement.CurrentState == PlayerMovement.BodyState.WallRunning && (playerMovement.features.enableWallRun || playerMovement.features.enableWallClimb || playerMovement.features.enableWallSlide))
        {
            var lookDirection = playerMovement.cameraFront.forward * playerMovement.moveVector.y + playerMovement.cameraFront.right * playerMovement.moveVector.x;
            if (lookDirection == Vector3.zero)
                lookDirection = playerMovement.cameraFront.forward;

            //Projecting vector to xz plane
            Vector3 lookDirectionXZ = new Vector3(lookDirection.x, 0f, lookDirection.z).normalized;

            //Cant jump into the wall
            if (Vector3.Dot(lookDirection, playerMovement.playerSurface.wallNormal) <= 0.1f)
                return;

            playerMovement.rb.velocity = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z);
            playerMovement.rb.AddForce(lookDirectionXZ * playerMovement.playerMovementConfig.acceleration, ForceMode.Impulse);

            //Counter of max wall jump, if overwlow dont use up speed
            if (playerMovement.playerWallRun.wallJumpCounter < playerMovement.playerMovementConfig.wallrunMaxJumps)
                playerMovement.rb.AddForce(Vector2.up * playerMovement.playerMovementConfig.jumpForce * playerMovement.rb.mass, ForceMode.Impulse);
            else { }

            playerMovement.playerWallRun.wallJumpCounter++;
            playerMovement.BuildSpeed("jump");
            if (playerMovement.features.enableLifeCamera)
                playerMovement.OnLifeCamera("jump");
        }
    }
}