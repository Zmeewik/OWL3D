using UnityEngine;

/// <summary>
/// Turns the body. Two sources, aim wins: while the entity has something to shoot at it faces the
/// target (otherwise it would fire wherever it happens to be walking, since the ranged resolvers
/// shoot along the weapon's forward axis), and the rest of the time it faces where it's going.
/// </summary>
public class EnemyRotationSystem : EnemySystem
{
    [Header("Speeds (degrees/second)")]
    [SerializeField] private float moveTurnSpeed = 480f;
    [SerializeField] private float aimTurnSpeed = 720f;

    [Tooltip("Body to rotate. Defaults to the entity root.")]
    [SerializeField] private Transform body;

    private Transform aimTarget;
    private Vector3 aimPoint;
    private bool hasAimPoint;

    private EnemyMovementSystem movement;

    public override void Initialize(Enemy owner)
    {
        base.Initialize(owner);
        if (body == null)
            body = owner.transform;
        movement = owner.Movement;
    }

    /// <summary>Face this transform until cleared. Overrides movement-facing.</summary>
    public void AimAt(Transform target)
    {
        aimTarget = target;
        hasAimPoint = false;
    }

    /// <summary>Face a fixed world point until cleared.</summary>
    public void AimAt(Vector3 worldPoint)
    {
        aimTarget = null;
        aimPoint = worldPoint;
        hasAimPoint = true;
    }

    public void ClearAim()
    {
        aimTarget = null;
        hasAimPoint = false;
    }

    /// <summary>True once the body is facing its aim within <paramref name="toleranceDegrees"/> -- attacks wait on this.</summary>
    public bool IsFacingAim(float toleranceDegrees = 12f)
    {
        if (!TryGetAimDirection(out var dir))
            return true;

        return Vector3.Angle(Flat(body.forward), dir) <= toleranceDegrees;
    }

    public override void TickSystem(float deltaTime)
    {
        if (body == null) return;

        Vector3 desired;
        float speed;

        if (TryGetAimDirection(out var aimDir))
        {
            desired = aimDir;
            speed = aimTurnSpeed;
        }
        else if (movement != null && movement.IsMoving)
        {
            desired = Flat(movement.MoveDirection);
            speed = moveTurnSpeed;
        }
        else
        {
            return;
        }

        if (desired.sqrMagnitude < 0.0001f)
            return;

        var targetRotation = Quaternion.LookRotation(desired, Vector3.up);
        body.rotation = Quaternion.RotateTowards(body.rotation, targetRotation, speed * deltaTime);
    }

    private bool TryGetAimDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (body == null)
            return false;

        Vector3 point;
        if (aimTarget != null)
            point = aimTarget.position;
        else if (hasAimPoint)
            point = aimPoint;
        else
            return false;

        direction = Flat(point - body.position);
        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }

    private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
}
