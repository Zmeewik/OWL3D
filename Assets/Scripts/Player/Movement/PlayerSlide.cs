using UnityEngine;

public class PlayerSlide: MonoBehaviour
{
    [SerializeField] PlayerMovement playerMovement;
    
    //Slide auto movement
    public void Slide()
    {
        if (!playerMovement.features.enableSlide) return;

        //Getting vector down
        Vector3 slopeDir = Vector3.ProjectOnPlane(Vector3.down, playerMovement.playerSurface.groundNormal).normalized;
        //Get current slope velocity
        float currentSpeedOnSlope = Vector3.Dot(playerMovement.rb.velocity, slopeDir);
        //Add force until max
        if (currentSpeedOnSlope < playerMovement.playerMovementConfig.maxSlideSpeed)
        {
            playerMovement.rb.AddForce(slopeDir * -Physics.gravity.y * playerMovement.playerMovementConfig.slideSpeed, ForceMode.Acceleration);
        }
        //Sticking player to ground while sloping
        playerMovement.rb.AddForce(-playerMovement.playerSurface.groundNormal.normalized * 30, ForceMode.Acceleration);

        //Start animation
        if (playerMovement.features.enableLifeCamera)
            playerMovement.OnLifeCamera("slide", new float[1] { playerMovement.playerFly.currentDownFallTime });
    }
}