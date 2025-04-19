using UnityEngine;
using static PlayerMovement;

public class SurfaceHandler : MonoBehaviour
{

    [Header("Standing")]
    [SerializeField] float groundAngle;
    [SerializeField] float slopeAngle;
    [SerializeField] float wallAngle;

    [Header("Crouching")]
    [SerializeField] float groundAngleCrouch;

    public enum SurfaceType { Ground, Slope,  Wall, Ceiling, None }

    //Check for normal angle to up to get type of surface
    public SurfaceType GetSurfaceType(Vector3 normal, IsCrouching isCrouching)
    {
        float angle = Vector3.Angle(normal, Vector3.up);
        
        if(isCrouching == IsCrouching.Crouching)
        {
            if(angle < groundAngleCrouch)
                return SurfaceType.Ground;
            else if (angle < slopeAngle)
                return SurfaceType.Slope;
            else if (angle < wallAngle)
                return SurfaceType.Wall;
            else
                return SurfaceType.Ceiling;
        }
        else
        {
            if (angle < groundAngle)
                return SurfaceType.Ground;
            else if (angle < slopeAngle)
                return SurfaceType.Slope;
            else if (angle < wallAngle)
                return SurfaceType.Wall;
            else
                return SurfaceType.Ceiling;
        }
    }

    public float GetAngleFromNormal(Vector3 normal)
    {
        return Vector3.Angle(normal, Vector3.up);
    }


}