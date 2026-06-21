using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    public Transform cameraFront;

    [Header("Movement")]
    public float acceleration;
    public float decceleration;
    public float maxSpeed;
    public float airControlMultiplier;

    [Header("Jump")]
    public float jumpForce;

    public Vector2 MoveInput { get; private set; }

    public Vector3 GroundNormal { get; set; } = Vector3.up;

    public bool IsGrounded { get; set; }

    public PlayerMovement Movement { get; private set; }
    public PlayerJump Jump { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        Movement = new PlayerMovement(this);
        Jump = new PlayerJump(this);
    }

    private void FixedUpdate()
    {
        Movement.Tick();
    }

    public void OnMove(Vector2 input)
    {
        MoveInput = input;
    }

    public void OnJumpInput()
    {
        Jump.Jump();
    }
}