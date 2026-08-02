using UnityEngine;

/// <summary>
/// Drives the actual first-person view camera: mouse-look rotation, attaching to the rig, FOV,
/// plus the shared procedural "life camera" shake/kick effects from <see cref="ProceduralCameraEffects"/>.
/// </summary>
public class PlayerCamera : ProceduralCameraEffects, IRotatable
{
    [Header("References")]
    [SerializeField] private Transform cameraOffsetObject;
    [SerializeField] private Transform cameraPosition;
    private Camera cameraRef;

    [Header("Rotation")]
    [SerializeField] private float speedRotation;
    [SerializeField] private float sensitivity;
    public float Sensitivity => sensitivity;

    [Header("Other settings")]
    [SerializeField] private float FOVOffset;
    [SerializeField] private float changeFOVTime;
    private float targetFOV;
    private float currentFOVVelocity = 0f;
    private float savedFov = 0;

    protected override Transform TargetTransform => cameraOffsetObject;

    protected override void Start()
    {
        base.Start();
        cameraRef = cameraObj.transform.GetChild(0).GetChild(0).GetComponent<Camera>();
        savedFov = cameraRef.fieldOfView;
    }

    // Update camera position and rotation at late update
    public override void LateUpdate()
    {
        if (rotationVector != Vector2.zero)
            FirstPerson();
        AttachCamera();
        base.LateUpdate();
        if (isCameraOn == IsCameraOn.On)
            ChangeFOV();
    }

    // Attach camera to an object
    public void AttachCamera()
    {
        transform.position = cameraPosition.position;
    }

    // Rotate camera with mouse
    public void FirstPerson()
    {
        // Find current look rotation
        Vector3 rot = cameraObj.transform.rotation.eulerAngles;
        var desiredX = rot.y + rotationVector.x * speedRotation * sensitivity;

        // Rotate and limit y axis
        xRotation -= rotationVector.y * speedRotation * sensitivity;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);

        // Perform the rotations
        cameraObj.transform.rotation = Quaternion.Euler(xRotation, desiredX, 0);
    }

    // Change camera FOV smoothly
    private void ChangeFOV()
    {
        // Go down FOV slow
        if (Mathf.Abs(cameraRef.fieldOfView - targetFOV) > 0.01f && cameraRef.fieldOfView > targetFOV)
        {
            cameraRef.fieldOfView = Mathf.SmoothDamp(cameraRef.fieldOfView, targetFOV, ref currentFOVVelocity, changeFOVTime);
        }
        // Go up FOV fast
        else if (Mathf.Abs(cameraRef.fieldOfView - targetFOV) > 0.01f && cameraRef.fieldOfView < targetFOV)
        {
            cameraRef.fieldOfView = Mathf.SmoothDamp(cameraRef.fieldOfView, targetFOV, ref currentFOVVelocity, changeFOVTime / 4);
        }
    }

    // Change camera rotation
    public void DeltaRotation(Vector2 delta)
    {
        rotationVector = new Vector2(delta.x, delta.y);
    }

    public void ChangeFOV(float num)
    {
        targetFOV = savedFov + num * FOVOffset;
    }
}
