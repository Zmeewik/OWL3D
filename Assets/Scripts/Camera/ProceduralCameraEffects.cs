using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Shared procedural "life camera" effects driven by <see cref="PlayerMovement.OnLifeCameraAction"/>:
/// idle breathing sway, footstep bob, jump/dash/land/fall-cliff/leg-hit kicks, wall-run/climb
/// shake, ledge hang-up roll, falling shake.
///
/// PlayerCamera (the actual view camera) and ArmsOffset (the weapon/arms view-model offset)
/// used to each carry a full, independent copy of this state machine (~90% identical code,
/// already drifted — ArmsOffset alone had FallCliff/LegHit support). This base class holds
/// everything that was duplicated; each subclass supplies only what genuinely differs: which
/// transform it moves (<see cref="TargetTransform"/>), and any extra per-frame behaviour
/// (PlayerCamera also owns mouse-look and FOV).
/// </summary>
public abstract class ProceduralCameraEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] protected GameObject cameraObj;

    protected enum IsCameraOn { On, Off }
    [Header("Life camera")]
    [SerializeField] protected IsCameraOn isCameraOn = IsCameraOn.On;

    [System.Serializable]
    protected class FeatureFlags
    {
        public bool enableBreath = true;
        public bool enableMovement = true;
        public bool enableRotate = true;
        public bool enableJump = true;
        public bool enableDash = true;
        public bool enableLand = true;
        public bool enableSlide = true;
        public bool enableWallrun = true;
        public bool enableClimb = true;
        public bool enableHangup = true;
        public bool enableFall = true;
        public bool enableFallcliff = true;
        public bool enableLeghit = true;
    }
    [SerializeField] protected FeatureFlags featureFlags;

    [SerializeField] protected float smoothSpeed = 5f;
    [SerializeField] protected float smoothRotationSpeed = 5f;

    private Vector3 offsetTarget = Vector3.zero;
    private Vector3 offsetCurrent = Vector3.zero;
    private Quaternion rotationTarget = Quaternion.identity;
    private Quaternion rotationCurrent = Quaternion.identity;

    [Header("Breath")]
    [SerializeField] protected float breathYShake;
    [SerializeField] protected float maxBreathYShake;
    [SerializeField] protected float breathSpeed = 3;
    [SerializeField] protected float maxBreathSpeed;
    [SerializeField] protected float relaxTime;
    private float savedBreathMultiplyer = 0;
    private float breathMultiplyer = 0;
    private float breathPhase;

    [Header("Movement")]
    [SerializeField] protected float walkXShake;
    [SerializeField] protected float walkYShake;
    [SerializeField] protected float runXShake;
    [SerializeField] protected float runYShake;
    [SerializeField] protected float startMoveSpeed;
    [SerializeField] protected float endMoveSpeed;
    private float currentSpeed;
    private float movementPhase = 0;

    [Header("Rotate")]
    [SerializeField] protected float minAngle;
    [SerializeField] protected float maxAngle;
    [SerializeField] protected float rotateAnimSensivity;
    [SerializeField] protected float maxRotationDelta;
    [SerializeField] protected float minRotationDelta;

    [Header("Jump")]
    [SerializeField] protected float jumpYShake;
    [SerializeField] protected float jumpAngle;
    [SerializeField] protected float jumpTime;
    private float angleJumpVelocity;
    private float currentJumpAngle = 0f;

    [Header("Dash")]
    [SerializeField] protected float dashAngle;
    [SerializeField] protected float dashTime;
    private float angleDashVelocity;
    private float currentDashAngle = 0f;

    [Header("Slide")]
    [SerializeField] protected float slideSpeed;
    [SerializeField] protected float slideYAngle;
    [SerializeField] protected float slideZAngle;
    private float slideMultiplier;

    [Header("Wallrun")]
    [SerializeField] protected float wallrunAngle;
    [SerializeField] protected float wallrunXShake;
    [SerializeField] protected float wallrunYShake;
    [SerializeField] protected float wallrunSpeed;
    private float wallrunPhase = 0f;
    private bool isWallrunRight = false;
    private float wallrunRightDot = 0f;
    private int wallrunState = 0;

    [Header("Climb")]
    [SerializeField] protected float wallclimbXShake;
    [SerializeField] protected float wallclimbYShake;
    [SerializeField] protected float wallclimbSpeed;

    [Header("Hungup")]
    [SerializeField] protected float hangupAngle;
    [SerializeField] protected float hangUpTime;
    private float currentHangUpAngle = 0f;
    private float currentHangUpSpeed = 0f;
    private int hangupDirection = 1;
    private float hangupPercent = 0f;

    [Header("Fall")]
    [SerializeField] protected float minFallSpeed;
    [SerializeField] protected float fallSpeed;
    [SerializeField] protected float minFallXShake;
    [SerializeField] protected float fallXShake;
    [SerializeField] protected float minFallYShake;
    [SerializeField] protected float fallYShake;
    [SerializeField] protected float minFallZRotation;
    [SerializeField] protected float fallZRotation;
    [SerializeField] protected float fallToMinTime;
    [SerializeField] protected float fallToMaxTime;
    private float fallMultiplyer = 0;

    [Header("Land")]
    [SerializeField] protected float landOffset;
    [SerializeField] protected float landAngle;
    [SerializeField] protected float landTime;
    [SerializeField] protected float maxLandMultiplyer;
    [SerializeField] protected float minLandMultiplyer;
    [SerializeField] protected float maxSecondsToMaxLandForce;
    private float currentLandForce = 0;
    private float angleLandVelocity;
    private float currentLandAngle = 0f;
    private float currentLandYOffset = 0f;
    private float offsetLandVelocity = 0f;
    private Vector3 initialOffset = Vector3.zero;

    [Header("Fall Cliff")]
    [SerializeField] protected float fallCliffYShake;
    [SerializeField] protected float fallCliffAngle;
    [SerializeField] protected float fallCliffTime;
    private float anglefallCliffVelocity;
    private float currentfallCliffAngle = 0f;

    [Header("Leg hit")]
    [SerializeField] protected float leghitYShake;
    [SerializeField] protected float leghitAngle;
    [SerializeField] protected float leghitTime;
    private float angleleghitVelocity;
    private float currentleghitAngle = 0f;

    private LifeCameraCue currentLifeCameraState = LifeCameraCue.None;
    private string savedState = "";

    // Rotation handle. Only PlayerCamera (IRotatable) ever assigns rotationVector; ArmsOffset
    // never receives look input so this stays (0,0) there — matching original behaviour where
    // ArmsOffset called OnRotate(rotationVector.x) every frame with an always-zero delta.
    protected Vector2 rotationVector;
    protected float xRotation;

    /// <summary>The transform this instance moves (the arms offset object, or the camera offset object).</summary>
    protected abstract Transform TargetTransform { get; }

    /// <summary>
    /// ArmsOffset breathed a half-cycle out of phase with PlayerCamera (Sin(phase + PI) vs
    /// Sin(phase)) — preserved here rather than silently unified, since it was presumably
    /// tuned that way on purpose (arms breathing opposite the camera reads as more natural).
    /// </summary>
    protected virtual float BreathPhaseOffset => 0f;

    protected virtual void Start()
    {
        PlayerMovement.OnLifeCameraAction += ChangeLifeCameraState;
    }

    protected virtual void OnDisable()
    {
        PlayerMovement.OnLifeCameraAction -= ChangeLifeCameraState;
    }

    public virtual void LateUpdate()
    {
        if (isCameraOn == IsCameraOn.On)
        {
            ApplyOffset();
            LifeCamera();
            if (featureFlags.enableRotate)
                OnRotate(rotationVector.x);
        }
    }

    private bool IsFeatureEnabled(LifeCameraCue cue)
    {
        switch (cue)
        {
            case LifeCameraCue.None: return featureFlags.enableBreath;
            case LifeCameraCue.Movement: return featureFlags.enableMovement;
            case LifeCameraCue.Jump: return featureFlags.enableJump;
            case LifeCameraCue.Dash: return featureFlags.enableDash;
            case LifeCameraCue.Land: return featureFlags.enableLand;
            case LifeCameraCue.Slide: return featureFlags.enableSlide;
            case LifeCameraCue.Wallrun: return featureFlags.enableWallrun;
            case LifeCameraCue.HangUp: return featureFlags.enableHangup;
            case LifeCameraCue.Fall: return featureFlags.enableFall;
            case LifeCameraCue.FallCliff: return featureFlags.enableFallcliff;
            case LifeCameraCue.LegHit: return featureFlags.enableLeghit;
            default: return false;
        }
    }

    /// <summary>Life camera cue handler. Was string-keyed with a "does this name exist" guard; now a closed enum.</summary>
    public void ChangeLifeCameraState(LifeCameraCue cue, float[] parameters)
    {
        if (!IsFeatureEnabled(cue))
            return;

        // Don't breathe if a continuous animation is already playing.
        if (cue == LifeCameraCue.None &&
            (currentLifeCameraState == LifeCameraCue.Dash
             || currentLifeCameraState == LifeCameraCue.LegHit
             || currentLifeCameraState == LifeCameraCue.Jump
             || currentLifeCameraState == LifeCameraCue.Land))
            return;

        if (cue != currentLifeCameraState && cue != LifeCameraCue.Movement || (cue == LifeCameraCue.Movement && currentLifeCameraState == LifeCameraCue.None))
        {
            if (cue == LifeCameraCue.HangUp)
            {
                Invoke(nameof(HangUpDelayStart), Mathf.Max(0, parameters[0] - hangUpTime));
                hangupPercent = parameters[1];
            }
            else
            {
                currentLifeCameraState = cue;
                offsetTarget = Vector3.zero;
                rotationTarget = Quaternion.identity;
                if (cue == LifeCameraCue.None)
                    if (breathMultiplyer < parameters[0])
                        breathMultiplyer = parameters[0];
                if (cue == LifeCameraCue.Wallrun)
                    wallrunPhase = 0;
            }
        }

        switch (cue)
        {
            case LifeCameraCue.None:
                break;
            case LifeCameraCue.Dash:
                {
                    Vector3 currentEuler = rotationTarget.eulerAngles;
                    rotationTarget = Quaternion.Euler(currentEuler.x - dashAngle, currentEuler.y, currentEuler.z);
                    currentDashAngle = -dashAngle;
                    angleDashVelocity = 0f;
                    StartCoroutine(NullStateDelayed(cue, dashTime));
                    savedState = cue.ToString();
                    break;
                }
            case LifeCameraCue.Movement:
                currentSpeed = parameters[0];
                if (currentSpeed != 0)
                    savedBreathMultiplyer = currentSpeed;
                break;
            case LifeCameraCue.Jump:
                {
                    Vector3 currentEuler = rotationTarget.eulerAngles;
                    rotationTarget = Quaternion.Euler(currentEuler.x - jumpAngle, currentEuler.y, currentEuler.z);
                    currentJumpAngle = -jumpAngle;
                    angleJumpVelocity = 0f;
                    StartCoroutine(NullStateDelayed(cue, jumpTime));
                    savedState = cue.ToString();
                    break;
                }
            case LifeCameraCue.Wallrun:
                isWallrunRight = parameters[0] != 0;
                wallrunState = (int)parameters[1];
                wallrunRightDot = parameters[2];
                break;
            case LifeCameraCue.Slide:
                break;
            case LifeCameraCue.Land:
                {
                    if (parameters.Length > 0)
                        currentLandForce = minLandMultiplyer + Math.Min(parameters[0], maxSecondsToMaxLandForce) / maxSecondsToMaxLandForce * maxLandMultiplyer;

                    Vector3 currentEuler = rotationTarget.eulerAngles;
                    rotationTarget = Quaternion.Euler(currentEuler.x + landAngle * currentLandForce, currentEuler.y, currentEuler.z);
                    currentLandAngle = landAngle * currentLandForce;
                    angleLandVelocity = 0f;

                    initialOffset = offsetTarget;
                    offsetTarget = new Vector3(offsetTarget.x, offsetTarget.y - landOffset * currentLandForce, offsetTarget.z);
                    currentLandYOffset = -landOffset * currentLandForce;
                    offsetLandVelocity = 0f;
                    StartCoroutine(NullStateDelayed(cue, landTime));
                    savedState = cue.ToString();
                    break;
                }
            case LifeCameraCue.HangUp:
                StartCoroutine(NullStateDelayed(cue, parameters[0]));
                savedState = cue.ToString();
                break;
            case LifeCameraCue.Fall:
                fallMultiplyer = Math.Min(parameters[0] - fallToMinTime, fallToMaxTime - fallToMinTime) / (fallToMaxTime - fallToMinTime);
                break;
            case LifeCameraCue.FallCliff:
                {
                    Vector3 currentEuler = rotationTarget.eulerAngles;
                    rotationTarget = Quaternion.Euler(currentEuler.x - fallCliffAngle, currentEuler.y, currentEuler.z);
                    currentfallCliffAngle = -fallCliffAngle;
                    anglefallCliffVelocity = 0f;
                    StartCoroutine(NullStateDelayed(cue, fallCliffTime));
                    savedState = cue.ToString();
                    break;
                }
            case LifeCameraCue.LegHit:
                {
                    Vector3 currentEuler = rotationTarget.eulerAngles;
                    rotationTarget = Quaternion.Euler(currentEuler.x - leghitAngle, currentEuler.y, currentEuler.z);
                    currentleghitAngle = -leghitAngle;
                    angleleghitVelocity = 0f;
                    StartCoroutine(NullStateDelayed(cue, leghitTime));
                    savedState = cue.ToString();
                    break;
                }
        }
    }

    private void HangUpDelayStart()
    {
        currentLifeCameraState = LifeCameraCue.HangUp;
        savedState = LifeCameraCue.HangUp.ToString();
        offsetTarget = Vector3.zero;
        rotationTarget = Quaternion.identity;

        var num = Math.Sign(UnityEngine.Random.value - 0.5f);
        hangupDirection = num == 0 ? 1 : num;
        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentEuler.x, currentEuler.y, currentEuler.z + hangupAngle * hangupDirection * hangupPercent);
        currentHangUpAngle = hangupAngle * hangupDirection * hangupPercent;
        currentHangUpSpeed = 0f;
    }

    /// <summary>Replaces the old string-returning GetCurrentState().</summary>
    public LifeCameraCue GetCurrentCue() => currentLifeCameraState;

    private void LifeCamera()
    {
        switch (currentLifeCameraState)
        {
            case LifeCameraCue.None: if (featureFlags.enableBreath) Breath(); break;
            case LifeCameraCue.Movement: if (featureFlags.enableMovement) Movement(); break;
            case LifeCameraCue.Jump: if (featureFlags.enableJump) OnJump(); break;
            case LifeCameraCue.Dash: if (featureFlags.enableDash) OnDash(); break;
            case LifeCameraCue.Land: if (featureFlags.enableLand) OnLand(); break;
            case LifeCameraCue.Slide: if (featureFlags.enableSlide) OnSlide(); break;
            case LifeCameraCue.Wallrun: if (featureFlags.enableWallrun) OnWallrun(); break;
            case LifeCameraCue.HangUp: if (featureFlags.enableHangup) OnHangUp(); break;
            case LifeCameraCue.Fall: if (featureFlags.enableFall) OnFall(); break;
            case LifeCameraCue.FallCliff: if (featureFlags.enableFallcliff) OnFallCliff(); break;
            case LifeCameraCue.LegHit: if (featureFlags.enableLeghit) OnLegHit(); break;
        }
    }

    private IEnumerator NullStateDelayed(LifeCameraCue cue, float time)
    {
        yield return new WaitForSeconds(time);
        NullState(cue);
    }

    private void NullState(LifeCameraCue cue)
    {
        if (cue == currentLifeCameraState)
        {
            currentLifeCameraState = LifeCameraCue.None;
            offsetTarget = Vector3.zero;
            rotationTarget = Quaternion.identity;
        }
    }

    private void Breath()
    {
        float speed = Mathf.Lerp(breathSpeed, maxBreathSpeed, breathMultiplyer);
        float shake = Mathf.Lerp(breathYShake, maxBreathYShake, breathMultiplyer);

        breathPhase += Time.deltaTime * speed;
        float breath = Mathf.Sin(breathPhase + BreathPhaseOffset) * shake;

        offsetTarget = Vector3.up * breath;

        breathMultiplyer = Mathf.Max(0, breathMultiplyer - Time.deltaTime / relaxTime);
    }

    private void Movement()
    {
        float frequency = Mathf.Lerp(startMoveSpeed, endMoveSpeed, currentSpeed);
        float amplitudeX = Mathf.Lerp(walkXShake, runXShake, currentSpeed);
        float amplitudeY = Mathf.Lerp(walkYShake, runYShake, currentSpeed);

        movementPhase += Time.deltaTime * frequency;
        float xShake = Mathf.Sin(movementPhase) * amplitudeX;
        float yShake = Mathf.Abs(Mathf.Cos(movementPhase)) * amplitudeY - amplitudeY;

        offsetTarget = Vector3.zero;
        offsetTarget += cameraObj.transform.up * yShake + cameraObj.transform.right * xShake;
    }

    private void OnRotate(float delta)
    {
        if (Math.Abs(minRotationDelta) < minRotationDelta) return;
        if (maxRotationDelta == 0) return;
        var currentDevariation = (Math.Abs(delta) - minRotationDelta) / (maxRotationDelta - minRotationDelta);
        var targetRot = currentDevariation > 1 ? 1 : currentDevariation;
        var signOfDelta = Math.Sign(-delta);
        var targetRollZ = Mathf.Lerp(minAngle * signOfDelta, maxAngle * signOfDelta, targetRot);
        Vector3 currentEuler = rotationTarget.eulerAngles;
        float z = Mathf.LerpAngle(currentEuler.z, targetRollZ, Time.deltaTime * smoothSpeed);
        rotationTarget = Quaternion.Euler(currentEuler.x, currentEuler.y, z);
    }

    private void OnJump()
    {
        currentJumpAngle = Mathf.SmoothDamp(currentJumpAngle, 0f, ref angleJumpVelocity, jumpTime);
        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentJumpAngle, currentEuler.y, currentEuler.z);
    }

    private void OnDash()
    {
        currentDashAngle = Mathf.SmoothDamp(currentDashAngle, 0f, ref angleDashVelocity, dashTime);
        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentDashAngle, currentEuler.y, currentEuler.z);
    }

    private void OnLand()
    {
        currentLandAngle = Mathf.SmoothDamp(currentLandAngle, 0f, ref angleLandVelocity, landTime);
        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentLandAngle, currentEuler.y, currentEuler.z);

        currentLandYOffset = Mathf.SmoothDamp(currentLandYOffset, 0f, ref offsetLandVelocity, landTime);
        offsetTarget = initialOffset + new Vector3(0, currentLandYOffset, 0);
    }

    private void OnSlide()
    {
        float randomTilt = Mathf.PerlinNoise(Time.time * 5f * slideSpeed, 0f) * 2f - 1f;
        float zRotation = randomTilt * slideZAngle;

        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentEuler.x, currentEuler.y, zRotation);
    }

    private void OnWallrun()
    {
        switch (wallrunState)
        {
            case 0: OnWallRunHorizontal(); break;
            case 1: OnWallClimb(); break;
            case 2: break;
        }

        if (isWallrunRight)
        {
            var curAngle = Mathf.Lerp(0, wallrunAngle, wallrunRightDot);
            rotationTarget = Quaternion.Euler(rotationTarget.x, rotationTarget.y, curAngle);
        }
        else
        {
            var curAngle = Mathf.Lerp(0, -wallrunAngle, -wallrunRightDot);
            rotationTarget = Quaternion.Euler(rotationTarget.x, rotationTarget.y, curAngle);
        }
    }

    private void OnWallRunHorizontal()
    {
        wallrunPhase += Time.deltaTime * wallrunSpeed;
        float xShake = Mathf.Sin(wallrunPhase) * wallrunXShake;
        float yShake = Mathf.Abs(Mathf.Cos(wallrunPhase)) * wallrunYShake;

        offsetTarget = Vector3.zero;
        offsetTarget += cameraObj.transform.up * yShake + cameraObj.transform.right * xShake;
    }

    private void OnWallClimb()
    {
        wallrunPhase += Time.deltaTime * wallclimbSpeed;
        float xShake = Mathf.Sin(wallrunPhase) * wallclimbXShake;
        float yShake = Mathf.Abs(Mathf.Cos(wallrunPhase)) * wallclimbYShake - wallclimbYShake;

        offsetTarget = Vector3.zero;
        offsetTarget += cameraObj.transform.up * yShake + cameraObj.transform.right * xShake;
    }

    private void OnHangUp()
    {
        currentHangUpAngle = Mathf.SmoothDamp(currentHangUpAngle, 0f, ref currentHangUpSpeed, hangUpTime);
        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentEuler.x, currentEuler.y, currentHangUpAngle);
    }

    private void OnFall()
    {
        if (fallMultiplyer <= 0)
        {
            offsetTarget = Vector3.zero;
            return;
        }
        float speed = Mathf.Lerp(minFallSpeed, fallSpeed, fallMultiplyer);
        float angle = Mathf.Lerp(minFallZRotation, fallZRotation, fallMultiplyer);
        float x = Mathf.Lerp(minFallXShake, fallXShake, fallMultiplyer);
        float y = Mathf.Lerp(minFallYShake, fallYShake, fallMultiplyer);

        Vector2 shakeOffset = UnityEngine.Random.insideUnitCircle * speed * Time.deltaTime;
        offsetTarget = Vector3.zero;
        offsetTarget += cameraObj.transform.up * shakeOffset.y * y + cameraObj.transform.right * shakeOffset.x * x;

        float randomTilt = Mathf.PerlinNoise(Time.time * 5f * speed, 0f) * 2f - 1f;
        float zRotation = randomTilt * angle;

        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentEuler.x, currentEuler.y, zRotation);
    }

    private void OnFallCliff()
    {
        currentfallCliffAngle = Mathf.SmoothDamp(currentfallCliffAngle, 0f, ref anglefallCliffVelocity, fallCliffTime);
        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentfallCliffAngle, currentEuler.y, currentEuler.z);
    }

    private void OnLegHit()
    {
        currentleghitAngle = Mathf.SmoothDamp(currentleghitAngle, 0f, ref angleleghitVelocity, leghitTime);
        Vector3 currentEuler = rotationTarget.eulerAngles;
        rotationTarget = Quaternion.Euler(currentleghitAngle, currentEuler.y, currentEuler.z);
    }

    private void ApplyOffset()
    {
        offsetCurrent = Vector3.Lerp(offsetCurrent, offsetTarget, Time.deltaTime * smoothSpeed);
        rotationCurrent = Quaternion.Slerp(rotationCurrent, rotationTarget, Time.deltaTime * smoothRotationSpeed);

        TargetTransform.position = offsetCurrent + cameraObj.transform.position;
        TargetTransform.localRotation = rotationCurrent;
    }
}
