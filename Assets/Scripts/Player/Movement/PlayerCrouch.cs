using UnityEngine;

public class PlayerCrouch : MonoBehaviour
{
    public PlayerMovement playerMovement;
    // crouch
    [SerializeField] Transform headPosition;
    Vector3 headStartPosition;
    [SerializeField] Vector3 headCrouchPosition;
    [SerializeField] CapsuleCollider crouchCollider;
    bool isStandingUp = false;
    float currentCrouchTime = 0;
    //Handle smooth crouching
    enum isChangingCrouchState { Crouching, Standing, None };
    isChangingCrouchState currentCrouchState = isChangingCrouchState.None;
    float crouchMultiplyer = 1;
    //Crouch handle
    private enum IsCrouching { Crouching, Standing }
    IsCrouching isCrouching = IsCrouching.Standing;

    [HideInInspector] public bool savedCrouch;

    public void StartFunc()
    {
        //Set start head position
        headStartPosition = headPosition.localPosition;
    }

    //Crouch system
    //Handle smooth standing and crouching
    public void SmoothCrouch(bool isCrouching)
    {
        currentCrouchTime += Time.deltaTime;
        float t = Mathf.Clamp01(currentCrouchTime / playerMovement.playerMovementConfig.crouchTime);

        if (currentCrouchTime < playerMovement.playerMovementConfig.crouchTime)
        {
            //handle crouch changes
            if (isCrouching)
            {
                // Interpolate to crouch
                //Scale collider down
                crouchCollider.height = Mathf.Lerp(2f, 2 * playerMovement.playerMovementConfig.crouchHeadOffset, t);
                crouchCollider.center = Vector3.Lerp(Vector3.up * 0f, 2 * playerMovement.playerMovementConfig.crouchHeadOffset / 2 * Vector3.up - Vector3.up, t);
                //Offset head
                headPosition.localPosition = Vector3.Lerp(headStartPosition, headCrouchPosition, t);
            }
            else
            {
                // Interpolate to stand
                //Scale collider up
                if (Physics.Raycast(headPosition.position, Vector3.up, 2.3f, playerMovement.groundLayer))
                {
                    //Switching standing up under low ceiling and starting checkout
                    OnCrouch(true);
                    print("Cannot stand!");
                    isStandingUp = true;
                    return;
                }
                crouchCollider.height = Mathf.Lerp(2 * playerMovement.playerMovementConfig.crouchHeadOffset, 2f, t);
                crouchCollider.center = Vector3.Lerp(2 * playerMovement.playerMovementConfig.crouchHeadOffset / 2 * Vector3.up - Vector3.up, Vector3.up * 0f, t);
                //Offset head
                headPosition.localPosition = Vector3.Lerp(headCrouchPosition, headStartPosition, t);
            }
        }
        else
        {
            //Handle final crouch/stand
            currentCrouchState = isChangingCrouchState.None;
            currentCrouchTime = 0;
            if (isCrouching)
            {
                //Crouch
                crouchCollider.height = 2 * playerMovement.playerMovementConfig.crouchHeadOffset;
                crouchCollider.center = 2 * playerMovement.playerMovementConfig.crouchHeadOffset / 2 * Vector3.up - Vector3.up;
                headPosition.localPosition = headCrouchPosition;
            }
            else
            {
                //Stand
                crouchCollider.height = 2f;
                crouchCollider.center = Vector3.up * 0f;
                headPosition.localPosition = headStartPosition;
            }
        }
    }

    //Crouch state mashine
    public void HandleCrouch()
    {
        switch (currentCrouchState)
        {
            case isChangingCrouchState.Crouching:
                SmoothCrouch(true);
                break;
            case isChangingCrouchState.Standing:
                SmoothCrouch(false);
                break;
            case isChangingCrouchState.None:
                break;
        }
    }
    
    //Crouch action
    public void OnCrouch(bool isCrouch)
    {
        print("Crouching: " + isCrouch);
        if (!playerMovement.features.enableCrouch)
            return;

        if (isCrouch)
            //Switching standing up under low ceiling and end checkout
            isStandingUp = false;

        //Scale player
        if (isCrouch && isCrouching != IsCrouching.Crouching)
        {
            //Start croudhing
            isCrouching = IsCrouching.Crouching;
            crouchMultiplyer = playerMovement.playerMovementConfig.crouchMaxMultiplyer;
            currentCrouchState = isChangingCrouchState.Crouching;
            if (currentCrouchTime != 0)
                currentCrouchTime = playerMovement.playerMovementConfig.crouchTime - currentCrouchTime;
        }
        else if (!isCrouch && isCrouching != IsCrouching.Standing)
        {
            //Start standing
            // Checkout if player can stand sending 5 rays up
            float standCheckDistance = 2.3f;
            float standCheckRadius = 0.5f;
            Vector3 center = headPosition.position;
            Vector3[] rayOrigins =
            {
                center,
                center + transform.forward * standCheckRadius,
                center - transform.forward * standCheckRadius,
                center + transform.right * standCheckRadius,
                center - transform.right * standCheckRadius
            };
            bool cannotStand = false;
            foreach (Vector3 origin in rayOrigins)
            {
                if (Physics.Raycast(
                        origin,
                        Vector3.up,
                        standCheckDistance,
                        playerMovement.groundLayer))
                {
                    cannotStand = true;
                    break;
                }
            }
            if (cannotStand)
            {
                print("Cannot stand!");
                isStandingUp = true;
                return;
            }
            //Switching standing up under low ceiling and end checkout
            isStandingUp = false;
            isCrouching = IsCrouching.Standing;
            crouchMultiplyer = 1f;

            currentCrouchState = isChangingCrouchState.Standing;
            if (currentCrouchTime != 0)
                currentCrouchTime = playerMovement.playerMovementConfig.crouchTime - currentCrouchTime;
        }
    }
    
    // Methods of interaction
    public bool IsPlayerCrouching()
    {
        return isCrouching == IsCrouching.Crouching;
    }
    
    public bool IsStandingUp()
    {
        return isStandingUp;
    }

    public float CrouchMultiplyer()
    {
        return crouchMultiplyer;
    }

}