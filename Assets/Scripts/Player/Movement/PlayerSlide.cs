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
        // Keep the slide going at max speed rather than gradually re-accelerating via
        // AddForce: a momentary surface-normal hiccup used to reset velocity to zero
        // mid-slide (see PlayerSurface.HandleSlope), and ramping back up slowly made that
        // read as "the slide stopped". Snapping the slope-direction speed straight to
        // maxSlideSpeed each tick (while preserving whatever lateral/other-axis velocity
        // the player already has) means a hiccup is never visible as a slowdown.
        if (currentSpeedOnSlope < playerMovement.playerMovementConfig.maxSlideSpeed)
        {
            Vector3 lateralVelocity = playerMovement.rb.velocity - slopeDir * currentSpeedOnSlope;
            playerMovement.rb.velocity = lateralVelocity + slopeDir * playerMovement.playerMovementConfig.maxSlideSpeed;
        }
        //Sticking player to ground while sloping
        playerMovement.rb.AddForce(-playerMovement.playerSurface.groundNormal.normalized * 30, ForceMode.Acceleration);

        //Start animation
        if (playerMovement.features.enableLifeCamera)
        {
            //slopeSlideTimer += Time.deltaTime;
            playerMovement.OnLifeCamera(LifeCameraCue.Slide, new float[1] { 0, /*slopeSlideTimer*/ });
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

    // Carry an in-progress slope slide straight on into a ground slide once the player reaches
    // flat ground (called from PlayerSurface.HandleGround while the slide button is held).
    // Unlike StartSlideGround, which lerps between min/max slide speed for a standing start,
    // this always starts at full slide speed: the player already arrives carrying the slope's
    // momentum, so scaling it down would read as the slide dying at the bottom of the hill.
    public void ContinueSlideOnGround()
    {
        // Keep travelling the way the slope was already carrying the player. Falls back to the
        // body's facing if horizontal velocity is ~zero, so the slide can never latch onto a
        // zero vector and stall on the spot.
        var horizontalVelocity = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z);
        slideDirectionSaved = horizontalVelocity.sqrMagnitude > 0.01f
            ? horizontalVelocity.normalized
            : new Vector3(playerMovement.transform.forward.x, 0, playerMovement.transform.forward.z).normalized;

        currentSlideTimer = playerMovement.playerMovementConfig.slideGroundTime;
        slideSpeed = playerMovement.playerMovementConfig.maxSlideGroundSpeed;

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
            playerMovement.OnLifeCamera(LifeCameraCue.Slide, new float[1] { 0/*1 - time*/ });

        // End slide
        if (currentSlideTimer <= 0)
        {
            Debug.Log("End slide!");
            currentSlideTimer = 0;
            // The slope descent this slide may have come from is done with; don't let a later
            // landing continue it (see PlayerSurface.slideContinuationArmed).
            playerMovement.playerSurface.DisarmSlideContinuation();
            playerMovement.CurrentState = PlayerMovement.BodyState.Moving;
            playerMovement.OnCrouch(playerMovement.playerCrouch.savedCrouch);
            playerMovement.OnLifeCamera(LifeCameraCue.Jump, new float[1] { time });
            playerMovement.playerMomentum.BuildSpeed("slide_ground");
        }
    }
}