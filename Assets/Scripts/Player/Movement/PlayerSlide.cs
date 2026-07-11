using UnityEngine;

public class PlayerSlide: MonoBehaviour
{
    [SerializeField] PlayerMovement playerMovement;
    private Vector3 slideDirectionSaved = Vector3.zero;
    private float slideSpeed = 0;
    //private float slopeSlideTimer = 0;
    
    private float currentSlideTimer = 0;

    public void StartSlide()
    {
        //slopeSlideTimer = 0;
    }

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
        {
            //slopeSlideTimer += Time.deltaTime;
            playerMovement.OnLifeCamera("slide", new float[1] { 0, /*slopeSlideTimer*/ });
        }
    }

    // Start slide at ground
    public void StartSlideGround(float speed)
    {
        Debug.Log("Start Slide");
        
        //Get forward direction
        slideDirectionSaved = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z).normalized;
        currentSlideTimer = playerMovement.playerMovementConfig.slideGroundTime;
        
        //Speed to slide speed
        slideSpeed = Mathf.Lerp(playerMovement.playerMovementConfig.minSlideGroundSpeed, playerMovement.playerMovementConfig.maxSlideGroundSpeed, speed);
        playerMovement.CurrentState = PlayerMovement.BodyState.SlidingGround;
        
        //Save crouch state
        playerMovement.playerCrouch.savedCrouch = true;
    }

    public void SlideGround()
    {
        if (!playerMovement.features.enableSlideGround) return;
        if (playerMovement.playerMovementConfig.slideGroundTime == 0) return;

        Debug.Log("Sliding Ground!!!");
        
        //Current move speed
        var time = 1 - (currentSlideTimer / playerMovement.playerMovementConfig.slideGroundTime);
        var forceSpeed = Mathf.Lerp(slideSpeed, 0, time);
        
        // Move to direction
        playerMovement.rb.AddForce(slideDirectionSaved * forceSpeed, ForceMode.Acceleration);
        currentSlideTimer -= Time.deltaTime;
        
        //Start animation
        if (playerMovement.features.enableLifeCamera)
            playerMovement.OnLifeCamera("slide", new float[1] { 0/*1 - time*/ });

        // End slide
        if (currentSlideTimer <= 0)
        {
            currentSlideTimer = 0;
            playerMovement.CurrentState = PlayerMovement.BodyState.Moving;
            playerMovement.OnCrouch(playerMovement.playerCrouch.savedCrouch);
            playerMovement.OnLifeCamera("jump", new float[1] { time });
            playerMovement.playerMomentum.BuildSpeed("slide_ground");
        }
    }
}