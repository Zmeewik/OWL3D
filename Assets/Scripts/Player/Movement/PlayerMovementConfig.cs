using UnityEngine;
[CreateAssetMenu(
    fileName = "PlayerConfig",
    menuName = "OWL/Player/Config")]
public class PlayerMovementConfig : ScriptableObject
{
    
    [Header("Movement")]
    [SerializeField] public float acceleration;
    [SerializeField] public float decceleration;
    [SerializeField] public float maxSpeed;
    
    [Header("Jump")]
    [SerializeField] public float jumpForce;
    
    [Header("Dash")]
    [SerializeField] public float dashDistance;
    [SerializeField] public float dashUpForce;
    [SerializeField] public float dashTime;
    
    [Header("Air")]
    [SerializeField, Range(0, 1)] public float airControlMultiplier;
    [SerializeField] public float minFallTime;
    [SerializeField] public float flyMaxParticleTime;
    
    [Header("Slide")]
    [SerializeField] public float maxSlideSpeed;
    [SerializeField] public float slideSpeed;
    
    [Header("Wall run")]
    [SerializeField] public float wallrunForceX;
    [SerializeField] public float wallRunForceY;
    [SerializeField] public float wallrunMaxForceX;
    [SerializeField] public float wallrunMaxForceY;
    [SerializeField] public float wallrunTime;
    [SerializeField] public int wallrunMaxJumps;
    
    [Header("Wall climb")]
    [SerializeField] public float wallClimbForce;
    [SerializeField] public float wallClimbTime;
    [SerializeField] public int wallClimbMaxJumps;
    
    [Header("Wall slide")]
    [SerializeField] public float wallSlideMaxSpeed;
    
    [Header("Hang Up")]
    [SerializeField] public float minHangUpTime;
    [SerializeField] public float maxHangUpTime;
    [SerializeField] public float hangUpHeight;
    [SerializeField] public float forwardOffset = 0.6f;
    
    [Header("Crouching")]
    [SerializeField] public float crouchHeadOffset;
    [SerializeField] public float crouchMaxMultiplyer;
    [SerializeField] public float crouchTime;
    
    
    [Header("Momentum")]
    [SerializeField] public float maxTopSpeed;
    [SerializeField] public float maxLowSpeed;
    [SerializeField] public float maxMomentum;
}