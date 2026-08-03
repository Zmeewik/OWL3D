using UnityEngine;
public class PlayerHangUp : MonoBehaviour
{        
    [SerializeField] private PlayerMovement playerMovement;
    
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
    
    //Check for hang up state
    public void HangUpCheck()
    {
        if (!Physics.Raycast(transform.position + new Vector3(0, playerMovement.playerMovementConfig.hangUpHeight, 0), -playerMovement.playerSurface.wallNormal, out var hit, 1f, playerMovement.groundLayer))
        {
            //Find point on the clif where I need to be at end
            Vector3 rayOrigin = transform.position + -playerMovement.playerSurface.wallNormal * 0.7f + Vector3.up * playerMovement.playerMovementConfig.hangUpHeight;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit downHit, playerMovement.playerMovementConfig.hangUpHeight * 2, playerMovement.groundLayer))
            {
                nextHangUpPosition = downHit.point + playerMovement.playerSurface.wallNormal * playerMovement.playerMovementConfig.forwardOffset + 3f * Vector3.up;
            }
            else
            {
                playerMovement.playerWallRun.currentWallState = PlayerWallRun.WallState.Sliding;
                return;
            }

            //print("Start hang up!");
            //print(nextHangUpPosition);

            //Start hang up
            hangUpStartPos = transform.position;
            currentHangUpTime = 0f;
            hangUpImpulseApplied = false;
            currentFinalHangUpTime = UnityEngine.Random.Range(playerMovement.playerMovementConfig.minHangUpTime, playerMovement.playerMovementConfig.maxHangUpTime);
            playerMovement.playerWallRun.currentWallState = PlayerWallRun.WallState.HangUp;

            //Start animation
            if (playerMovement.features.enableHangUp)
            {
                if (Physics.Raycast(transform.position, -playerMovement.playerSurface.wallNormal, out RaycastHit wallHit, 1f, playerMovement.groundLayer))
                {
                    playerMovement.OnLifeCamera(LifeCameraCue.HangUp, new float[2] { currentFinalHangUpTime, 1f });
                }
                else
                {
                    playerMovement.OnLifeCamera(LifeCameraCue.HangUp, new float[2] { currentFinalHangUpTime, 0.3f });
                }
            }

            //Hang up animation
            if (!playerMovement.isAlreadyAnimating)
            {
                playerMovement.isAlreadyAnimating = true;

                //Check for the wall in front of player to start get up animation
                if (Physics.Raycast(transform.position, -playerMovement.playerSurface.wallNormal, out RaycastHit wallHit, 1f, playerMovement.groundLayer))
                {
                    playerMovement.OnAnimating("H_Arms_GetUp", "l_arm", false);
                    playerMovement.OnAnimating("H_Arms_GetUp", "r_arm", false);
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
            //print("Go up!");
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

            playerMovement.rb.velocity = Vector3.zero;
            playerMovement.rb.AddForce(velocity, ForceMode.VelocityChange);

            hangUpImpulseUp = true;
        }
        else if (t >= 0.8f && !hangUpImpulseForward)
        {
            //print("Go forward!");
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

            //print(velocity);

            playerMovement.rb.AddForce(velocity, ForceMode.VelocityChange);

            hangUpImpulseForward = true;
        }

        if (t >= 1f)
        {
            playerMovement.isAlreadyAnimating = false;
            hangUpImpulseUp = false;
            hangUpImpulseForward = false;
            playerMovement.playerWallRun.currentWallState = PlayerWallRun.WallState.Sliding;
            playerMovement.lastState = playerMovement.CurrentState;
            playerMovement.OnFly();
        }
    }
}