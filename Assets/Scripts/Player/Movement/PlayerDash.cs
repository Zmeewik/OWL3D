using UnityEngine;
public class PlayerDash : MonoBehaviour
{
    [SerializeField] PlayerMovement playerMovement;
    // dash
    float dashVelocity = 0;
    float currentDashTime = 0;
    Vector3 dashDirection = Vector3.zero;
    
    public void OnDash()
    {
        //Check if dashing right now
        if (playerMovement.CurrentState == PlayerMovement.BodyState.Dashing || !playerMovement.features.enableDash)
            return;

        //Nullifying x, z speed
        playerMovement.rb.velocity = new Vector3(0f, playerMovement.rb.velocity.y, 0f);

        //Get current Look Direction
        var lookDirection = playerMovement.cameraFront.forward * playerMovement.moveVector.y + playerMovement.cameraFront.right * playerMovement.moveVector.x;
        if (lookDirection == Vector3.zero)
            lookDirection = playerMovement.cameraFront.forward;

        //If going down go little up instead
        //Up dashing
        playerMovement.rb.AddForce(playerMovement.playerMovementConfig.dashUpForce * Vector3.up, ForceMode.Impulse);
        Vector3 lookDirectionXZ = new Vector3(lookDirection.x, 0f, lookDirection.z).normalized;
        lookDirection = lookDirectionXZ;
        dashDirection = lookDirection;

        //Get needed velocity
        dashVelocity = playerMovement.playerMovementConfig.dashDistance / playerMovement.playerMovementConfig.dashTime;
        Vector3 dash = dashDirection.normalized * dashVelocity;
        playerMovement.rb.velocity = new Vector3(dash.x, playerMovement.rb.velocity.y, dash.z);

        //Dash player
        currentDashTime = 0;
        timeOfDash = transform.position;

        //End dash afrter time
        playerMovement.CurrentState = PlayerMovement.BodyState.Dashing;
        Invoke("EndDash", playerMovement.playerMovementConfig.dashTime);
        playerMovement.OnCrouch(false);
        if (playerMovement.features.enableLifeCamera)
            playerMovement.OnLifeCamera(LifeCameraCue.Dash);
    }
    
    //End dash state
    Vector3 timeOfDash = Vector3.zero;
    public void EndDash()
    {
        if (playerMovement.CurrentState == PlayerMovement.BodyState.Dashing)
        {
            playerMovement.BuildSpeed("dash");
            //print(Vector3.Distance(transform.position, timeOfDash));
            var normVel = new Vector3(playerMovement.rb.velocity.x, 0, playerMovement.rb.velocity.z).normalized;
            playerMovement.rb.velocity = normVel * playerMovement.playerMovementConfig.maxSpeed * playerMovement.playerMovementConfig.airControlMultiplier;
            playerMovement.CurrentState = PlayerMovement.BodyState.InAir;
            playerMovement.OnFly();
        }
    }
}