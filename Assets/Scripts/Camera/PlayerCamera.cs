using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerCamera : MonoBehaviour, IRotatable
{

    [Header("References")]
    [SerializeField]
    GameObject cameraObj;
    [SerializeField] Transform cameraOffsetObject;
    [SerializeField] Transform cameraPosition;


    [Header("Rotation")]
    [SerializeField] float speedRotation;
    [SerializeField] float sensitivity;
    public float Sensitivity => sensitivity;

    enum IsCameraOn { On, Off }
    [Header("Life camera")]
    [SerializeField] IsCameraOn isCameraOn = IsCameraOn.On;
    [System.Serializable]
    private class FeatureFlags
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
    }
    [SerializeField] FeatureFlags featureFlags;
    [SerializeField] float smoothSpeed = 5f;
    [SerializeField] float smoothRotationSpeed = 5f;
    Vector3 cameraOffsetTarget = Vector3.zero;
    Vector3 cameraOffsetCurrent = Vector3.zero;

    Quaternion cameraRotationTarget = Quaternion.identity;
    Quaternion cameraRotationCurrent = Quaternion.identity;


    [Header("Breath")]
    [SerializeField] float breathYShake;
    [SerializeField] float maxBreathYShake;
    [SerializeField] float breathSpeed = 3;
    [SerializeField] float maxBreathSpeed;
    [SerializeField] float relaxTime;
    float savedBreathMultiplyer = 0;
    float breathMultiplyer = 0;
    [Header("Movement")]
    [SerializeField] float walkXShake;
    [SerializeField] float walkYShake;
    [SerializeField] float runXShake;
    [SerializeField] float runYShake;
    [SerializeField] float startMoveSpeed;
    [SerializeField] float endMoveSpeed;
    float currentSpeed;
    float movementPhase = 0;
    [Header("Rotate")]
    [SerializeField] float minAngle;
    [SerializeField] float maxAngle;
    [SerializeField] float rotateAnimSensivity;
    [SerializeField] float maxRotationDelta;
    [SerializeField] float minRotationDelta;
    [Header("Jump")]
    [SerializeField] float jumpYShake;
    [SerializeField] float jumpAngle;
    [SerializeField] float jumpTime;
    //For smoothDamp
    float angleJumpVelocity;
    float currentJumpAngle = 0f;
    [Header("Dash")]
    [SerializeField] float dashAngle;
    [SerializeField] float dashTime;
    //For smoothDamp
    float angleDashVelocity;
    float currentDashAngle = 0f;
    [Header("Slide")]
    [SerializeField] float slideSpeed;
    [SerializeField] float slideYAngle;
    [SerializeField] float slideZAngle;
    [Header("Wallrun")]
    [SerializeField] float wallrunAngle;
    [SerializeField] float wallrunXShake;
    [SerializeField] float wallrunYShake;
    [SerializeField] float wallrunSpeed;
    float wallrunPhase = 0f;
    bool isWallrunRight = false;
    float wallrunRightDot = 0f;
    int wallrunState = 0;
    [Header("Climb")]
    [SerializeField] float wallclimbXShake;
    [SerializeField] float wallclimbYShake;
    [SerializeField] float wallclimbSpeed;

    [Header("Hungup")]
    [SerializeField] float hangupAngle;
    [SerializeField] float hangUpTime;
    float currentHangUpAngle = 0f;
    float currentHangUpSpeed = 0f;
    int hangupDirection = 1;
    [Header("Fall")]
    [SerializeField] float minFallSpeed;
    [SerializeField] float fallSpeed;
    [SerializeField] float minFallXShake;
    [SerializeField] float fallXShake;
    [SerializeField] float minFallYShake;
    [SerializeField] float fallYShake;
    [SerializeField] float minFallZRotation;
    [SerializeField] float fallZRotation;
    [SerializeField] float fallToMinTime;
    [SerializeField] float fallToMaxTime;
    float fallMultiplyer = 0;
    [Header("Land")]
    [SerializeField] float landOffset;
    [SerializeField] float landAngle;
    [SerializeField] float landTime;
    [SerializeField] float maxLandMultiplyer;
    [SerializeField] float minLandMultiplyer;
    [SerializeField] float maxSecondsToMaxLandForce;
    float currentLandForce = 0;
    //For smoothDamp
    float angleLandVelocity;
    float currentLandAngle = 0f;
    float currentLandYOffset = 0f;
    float offsetLandVelocity = 0f;
    Vector3 initialCameraOffset = Vector3.zero;


    //life camera states
    private enum LifeCameraState { None, Movement, Jump, Wallrun, Slide, Hangup, Land, Fall, Dash };
    List<string> stateNames = new List<string> { "none", "movement", "jump", "wallrun", "slide", "hangup", "land", "fall", "dash" };
    LifeCameraState currentLifeCameraState = LifeCameraState.None;
    string currentState = "none";

    string savedState = "";
    Coroutine endCoroutine;


    //Rotation handle
    Vector2 rotationVector;
    float xRotation;

    //Update camera position and rotation at late update
    public void LateUpdate()
    {
        if (rotationVector != Vector2.zero)
            FirstPerson();
        AttachCamera();
        if (isCameraOn == IsCameraOn.On)
        {
            ApplyCameraOffset();
            LifeCamera();
            if (featureFlags.enableRotate)
            {
                OnRotate(rotationVector.x);
            }
        }
    }

    //Attach camera to an object
    public void AttachCamera()
    {
        transform.position = cameraPosition.position;
    }

    //Rotate camera with mouse
    public void FirstPerson()
    {
        //Find current look rotation
        Vector3 rot = cameraObj.transform.rotation.eulerAngles;
        var desiredX = rot.y + rotationVector.x * speedRotation * sensitivity;

        //Rotate and limit y axis
        xRotation -= rotationVector.y * speedRotation * sensitivity;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);

        //Perform the rotations
        cameraObj.transform.rotation = Quaternion.Euler(xRotation, desiredX, 0);
    }


    //Change camera rotation
    public void DeltaRotation(Vector2 delta)
    {
        rotationVector = new Vector2(delta.x, delta.y);
    }



    //Life camera
    public void ChangeLifeCameraState(string state, float[] parameters)
    {
        //Check for existing name
        if (!stateNames.Contains(state))
            return;

        //print(state);

        //Return if state disabled
        int id = stateNames.IndexOf(state);
        if (!IsFeatureEnabled((LifeCameraState)id))
            return;

        //print(state);

        //Change life camera state
        if (id != (int)currentLifeCameraState && state != "movement" || (state == "movement" && currentLifeCameraState == LifeCameraState.None))
        {
            if (state == "hangup")
            {
                //Delayed functions
                Invoke("HangUpDelayStart", Mathf.Max(0, parameters[0] - hangUpTime));
            }
            else
            {
                //Immediate functions
                currentLifeCameraState = (LifeCameraState)id;
                print(state);
                cameraOffsetTarget = Vector3.zero;
                cameraRotationTarget = Quaternion.identity;
                if (state == "none")
                    if (breathMultiplyer < parameters[0])
                        breathMultiplyer = parameters[0];
                if (state == "wallrun")
                    wallrunPhase = 0;
            }
        }


        //Action on state change
        switch (state)
        {
            case "none":

                break;
            case "dash":
                Vector3 currentEulerDash = cameraRotationTarget.eulerAngles;
                float xD = currentEulerDash.x - jumpAngle;
                float yD = currentEulerDash.y;
                float zD = currentEulerDash.z;
                cameraRotationTarget = Quaternion.Euler(xD, yD, zD);
                currentDashAngle = -dashAngle;
                angleDashVelocity = 0f;
                StartCoroutine(NullStateDelayed(id, dashTime));
                savedState = state;
                break;
            case "movement":
                currentSpeed = parameters[0];
                if (currentSpeed != 0)
                    savedBreathMultiplyer = currentSpeed;
                break;
            case "jump":
                Vector3 currentEulerJump = cameraRotationTarget.eulerAngles;
                float xJ = currentEulerJump.x - jumpAngle;
                float yJ = currentEulerJump.y;
                float zJ = currentEulerJump.z;
                cameraRotationTarget = Quaternion.Euler(xJ, yJ, zJ);
                currentJumpAngle = -jumpAngle;
                angleJumpVelocity = 0f;
                StartCoroutine(NullStateDelayed(id, jumpTime));
                savedState = state;
                break;
            case "wallrun":
                isWallrunRight = parameters[0] == 0 ? false : true;
                wallrunState = (int)parameters[1];
                wallrunRightDot = parameters[2];
                break;
            case "slide":

                break;
            case "land":
                //Transfer parameter
                if (parameters.Length > 0)
                    currentLandForce = minLandMultiplyer + Math.Min(parameters[0], maxSecondsToMaxLandForce) / maxSecondsToMaxLandForce * maxLandMultiplyer;

                // //Start rotation
                Vector3 currentEulerLand = cameraRotationTarget.eulerAngles;
                float xL = currentEulerLand.x + landAngle * currentLandForce;
                float yL = currentEulerLand.y;
                float zL = currentEulerLand.z;
                cameraRotationTarget = Quaternion.Euler(xL, yL, zL);
                currentLandAngle = landAngle * currentLandForce;
                angleLandVelocity = 0f;

                //Start change position
                initialCameraOffset = cameraOffsetTarget;
                cameraOffsetTarget = new Vector3(cameraOffsetTarget.x, cameraOffsetTarget.y - landOffset * currentLandForce, cameraOffsetTarget.z);
                currentLandYOffset = -landOffset * currentLandForce;
                offsetLandVelocity = 0f;
                StartCoroutine(NullStateDelayed(id, landTime));
                savedState = state;
                break;
            case "hangup":
                StartCoroutine(NullStateDelayed(id, parameters[0]));
                savedState = state;
                break;
            case "fall":
                fallMultiplyer = Math.Min(parameters[0] - fallToMinTime, fallToMaxTime - fallToMinTime) / (fallToMaxTime - fallToMinTime);
                break;
        }
    }

    void HangUpDelayStart()
    {
        //Start state
        print("hangup");
        currentLifeCameraState = LifeCameraState.Hangup;
        savedState = "hangup";
        cameraOffsetTarget = Vector3.zero;
        cameraRotationTarget = Quaternion.identity;

        //Start Rotation
        var num = Math.Sign(UnityEngine.Random.value-0.5f);
        hangupDirection = num == 0 ? 1 : num;
        Vector3 currentEulerHang = cameraRotationTarget.eulerAngles;
        float xH = currentEulerHang.x;
        float yH = currentEulerHang.y;
        float zH = currentEulerHang.z + hangupAngle * hangupDirection;
        cameraRotationTarget = Quaternion.Euler(xH, yH, zH);
        currentHangUpAngle = hangupAngle * hangupDirection;
        currentHangUpSpeed = 0f;
        
    }

    public string GetCurrentState()
    {
        return stateNames[(int)currentLifeCameraState];
    }

    private bool IsFeatureEnabled(LifeCameraState state)
    {
        string fieldName = state.ToString().ToLower(); //"Wallrun" → "wallrun"
        var field = typeof(FeatureFlags).GetField($"enable{char.ToUpper(fieldName[0]) + fieldName.Substring(1)}");
        if (fieldName == "none")
            field = typeof(FeatureFlags).GetField($"enableBreath");
        if (field != null && field.FieldType == typeof(bool))
        {
            return (bool)field.GetValue(featureFlags);
        }

        return false; //false if flag does not exist
    }

    void LifeCamera()
    {
        switch (currentLifeCameraState)
        {
            case LifeCameraState.None:
                if (featureFlags.enableBreath)
                {
                    Breath();
                }
                break;
            case LifeCameraState.Movement:
                if (featureFlags.enableMovement)
                {
                    Movement();
                }
                break;
            case LifeCameraState.Jump:
                if (featureFlags.enableJump)
                {
                    OnJump();
                }
                break;
            case LifeCameraState.Dash:
                if (featureFlags.enableDash)
                {
                    OnDash();
                }
                break;
            case LifeCameraState.Land:
                if (featureFlags.enableLand)
                {
                    OnLand();
                }
                break;
            case LifeCameraState.Slide:
                if (featureFlags.enableSlide)
                {
                    OnSlide();
                }
                break;
            case LifeCameraState.Wallrun:
                if (featureFlags.enableWallrun)
                {
                    OnWallrun();
                }
                break;
            case LifeCameraState.Hangup:
                if (featureFlags.enableHangup)
                {
                    OnHangUp();
                }
                break;
            case LifeCameraState.Fall:
                if (featureFlags.enableFall)
                {
                    OnFall();
                }
                break;
        }
    }

    IEnumerator NullStateDelayed(int state, float time)
    {
        yield return new WaitForSeconds(time);
        NullState(state);
    }

    void NullState(int state)
    {
        if (state == (int)currentLifeCameraState)
        {
            print("none");
            currentLifeCameraState = LifeCameraState.None;
            cameraOffsetTarget = Vector3.zero;
            cameraRotationTarget = Quaternion.identity;
        }
    }

    //Breath
    private float breathPhase;
    void Breath()
    {
        float speed = Mathf.Lerp(breathSpeed, maxBreathSpeed, breathMultiplyer);
        float shake = Mathf.Lerp(breathYShake, maxBreathYShake, breathMultiplyer);

        breathPhase += Time.deltaTime * speed;
        float breath = Mathf.Sin(breathPhase) * shake;

        cameraOffsetTarget = Vector3.up * breath;

        breathMultiplyer = Mathf.Max(0, breathMultiplyer - Time.deltaTime / relaxTime);
    }

    //Walk/Run
    void Movement()
    {
        //steps in second
        float frequency = Mathf.Lerp(startMoveSpeed, endMoveSpeed, currentSpeed);
        float amplitudeX = Mathf.Lerp(walkXShake, runXShake, currentSpeed);
        float amplitudeY = Mathf.Lerp(walkYShake, runYShake, currentSpeed);

        movementPhase += Time.deltaTime * frequency;
        float xShake = Mathf.Sin(movementPhase) * amplitudeX;
        float yShake = Mathf.Abs(Mathf.Cos(movementPhase)) * amplitudeY;

        cameraOffsetTarget = new Vector3(0, 0, 0);
        cameraOffsetTarget += cameraObj.transform.up * yShake + cameraObj.transform.right * xShake;
    }

    //Rotate
    void OnRotate(float delta)
    {
        if (Math.Abs(minRotationDelta) < minRotationDelta) return;
        if (maxRotationDelta == 0) return;
        var currentDevariation = (Math.Abs(delta) - minRotationDelta) / (maxRotationDelta - minRotationDelta);
        var targetRot = currentDevariation > 1 ? 1 : currentDevariation;
        var signOfDelta = Math.Sign(-delta);
        var targetRollZ = Mathf.Lerp(minAngle * signOfDelta, maxAngle * signOfDelta, targetRot);
        //Changing z rotation axis
        Vector3 currentEuler = cameraRotationTarget.eulerAngles;
        float x = currentEuler.x;
        float y = currentEuler.y;
        float z = Mathf.LerpAngle(currentEuler.z, targetRollZ, Time.deltaTime * smoothSpeed);
        cameraRotationTarget = Quaternion.Euler(x, y, z);
    }

    //Jump
    void OnJump()
    {
        //Change x rotation over time
        currentJumpAngle = Mathf.SmoothDamp(currentJumpAngle, 0f, ref angleJumpVelocity, jumpTime);
        Vector3 currentEuler = cameraRotationTarget.eulerAngles;
        float y = currentEuler.y;
        float z = currentEuler.z;
        cameraRotationTarget = Quaternion.Euler(currentJumpAngle, y, z);
    }

    //Dash
    void OnDash()
    {
        //Change x rotation over time
        currentDashAngle = Mathf.SmoothDamp(currentDashAngle, 0f, ref angleDashVelocity, dashTime);
        Vector3 currentEuler = cameraRotationTarget.eulerAngles;
        float y = currentEuler.y;
        float z = currentEuler.z;
        cameraRotationTarget = Quaternion.Euler(currentDashAngle, y, z);
    }

    //Land
    void OnLand()
    {
        //Change x rotation over time
        currentLandAngle = Mathf.SmoothDamp(currentLandAngle, 0f, ref angleLandVelocity, landTime);
        Vector3 currentEuler = cameraRotationTarget.eulerAngles;
        float y = currentEuler.y;
        float z = currentEuler.z;
        cameraRotationTarget = Quaternion.Euler(currentLandAngle, y, z);

        // Change y position of camera smooth
        currentLandYOffset = Mathf.SmoothDamp(currentLandYOffset, 0f, ref offsetLandVelocity, landTime);
        cameraOffsetTarget = initialCameraOffset + new Vector3(0, currentLandYOffset, 0);
    }

    //Slide
    void OnSlide()
    {
        // Random small tilt (rotation around Z)
        float randomTilt = Mathf.PerlinNoise(Time.time * 5f * slideSpeed, 0f) * 2f - 1f; // From -1 to 1
        float zRotation = randomTilt * slideZAngle;

        Vector3 currentEuler = cameraRotationTarget.eulerAngles;
        cameraRotationTarget = Quaternion.Euler(currentEuler.x, currentEuler.y, zRotation);
    }

    //Wallrun
    void OnWallrun()
    {
        switch (wallrunState)
        {
            //horizontal wallrun
            case 0:
                OnWallRunHorizontal();
                break;
            //vertical wallrun
            case 1:
                OnWallClimb();
                break;
            //sliding
            case 2:

                break;
        }

        //Angle devariation at wall if more directed to wall right more devariation from z rotation
        if (isWallrunRight)
        {
            var curAngle = Mathf.Lerp(0, wallrunAngle, wallrunRightDot);
            cameraRotationTarget = Quaternion.Euler(cameraRotationTarget.x, cameraRotationTarget.y, curAngle);
        }
        else
        {
            var curAngle = Mathf.Lerp(0, -wallrunAngle, -wallrunRightDot);
            cameraRotationTarget = Quaternion.Euler(cameraRotationTarget.x, cameraRotationTarget.y, curAngle);
        }
    }

    //Wallrun horizontal
    void OnWallRunHorizontal()
    {
        //Walk shake
        print("wallrun");
        wallrunPhase += Time.deltaTime * wallrunSpeed;
        float xShake = Mathf.Sin(wallrunPhase) * wallrunXShake;
        float yShake = Mathf.Abs(Mathf.Cos(wallrunPhase)) * wallrunYShake;

        cameraOffsetTarget = new Vector3(0, 0, 0);
        cameraOffsetTarget += cameraObj.transform.up * yShake + cameraObj.transform.right * xShake;
    }

    //Wall climb
    void OnWallClimb()
    {
        //Walk shake
        print("climb");
        wallrunPhase += Time.deltaTime * wallclimbSpeed;
        float xShake = Mathf.Sin(wallrunPhase) * wallclimbXShake;
        float yShake = Mathf.Abs(Mathf.Cos(wallrunPhase)) * wallclimbYShake;

        cameraOffsetTarget = new Vector3(0, 0, 0);
        cameraOffsetTarget += cameraObj.transform.up * yShake + cameraObj.transform.right * xShake;
    }

    //Hang up
    void OnHangUp()
    {
        //Change x rotation over time
        currentHangUpAngle = Mathf.SmoothDamp(currentHangUpAngle, 0f, ref currentHangUpSpeed, hangUpTime);
        Vector3 currentEuler = cameraRotationTarget.eulerAngles;
        float x = currentEuler.x;
        float y = currentEuler.y;
        cameraRotationTarget = Quaternion.Euler(x, y, currentHangUpAngle);
    }

    //Fall
    void OnFall()
    {
        if (fallMultiplyer <= 0)
        {
            cameraOffsetTarget = new Vector3(0, 0, 0);
            return;
        }
        float speed = Mathf.Lerp(minFallSpeed, fallSpeed, fallMultiplyer);
        float angle = Mathf.Lerp(minFallZRotation, fallZRotation, fallMultiplyer);
        float x = Mathf.Lerp(minFallXShake, fallXShake, fallMultiplyer);
        float y = Mathf.Lerp(minFallYShake, fallYShake, fallMultiplyer);

        // Random offset within a small radius (shake)
        Vector2 shakeOffset = UnityEngine.Random.insideUnitCircle * speed * Time.deltaTime;
        cameraOffsetTarget = new Vector3(0, 0, 0);
        cameraOffsetTarget += cameraObj.transform.up * shakeOffset.y * y + cameraObj.transform.right * shakeOffset.x * x;

        // Random small tilt (rotation around Z)
        float randomTilt = Mathf.PerlinNoise(Time.time * 5f * speed, 0f) * 2f - 1f; // From -1 to 1
        float zRotation = randomTilt * angle;

        Vector3 currentEuler = cameraRotationTarget.eulerAngles;
        cameraRotationTarget = Quaternion.Euler(currentEuler.x, currentEuler.y, zRotation);
    }

    //Aim rotation and move
    void ApplyCameraOffset()
    {
        cameraOffsetCurrent = Vector3.Lerp(cameraOffsetCurrent, cameraOffsetTarget, Time.deltaTime * smoothSpeed);
        cameraRotationCurrent = Quaternion.Slerp(cameraRotationCurrent, cameraRotationTarget, Time.deltaTime * smoothRotationSpeed);

        cameraOffsetObject.transform.position = cameraOffsetCurrent + cameraObj.transform.position;
        cameraOffsetObject.transform.localRotation = cameraRotationCurrent;
    }


}
