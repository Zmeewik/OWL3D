using UnityEngine;

/// <summary>
/// Goes and looks for something that isn't there any more: walks to the spot where contact was last
/// made -- the point a target was last seen, or roughly where a shot came from -- and then hunts
/// around it, pacing side to side and stopping to look about, before giving up.
///
/// Deliberately a walk rather than a run (see <see cref="EnemyMovementSystem.SpeedMultiplier"/>):
/// this is searching, not chasing, and sprinting around a corpse-empty room reads as the entity
/// still being in combat.
///
/// Nothing here detects anything. Vision keeps sweeping every frame regardless of which action is
/// running, so a target that turns up mid-search is picked up the usual way -- OnTargetSpotted fires
/// and the strategy clears this action to engage. All this has to do is put the entity in promising
/// places with its eyes pointed at plausible directions, and eventually give up.
/// </summary>
public class SearchAction : EnemyAction
{
    /// <summary>How close counts as having got to a point. Wider than the movement system's own
    /// stopping distance, because a search waypoint is a suggestion rather than a target.</summary>
    private const float ArriveDistance = 1.5f;

    private readonly float searchDuration;
    private readonly float speedMultiplier;
    private readonly float sweepRadius;
    private readonly float travelTimeout;
    private readonly float lookHoldTime;

    /// <summary>Yaw offsets, in degrees, that the sweep waypoints and look directions cycle through.
    /// Alternating sign is what makes the entity cross back and forth over the area instead of
    /// drifting steadily off in one direction.</summary>
    private static readonly float[] SweepAngles = { 65f, -65f, 135f, -135f, 25f, -25f, 180f };

    private Vector3 focus;
    private float searchElapsed;
    private float travelElapsed;
    private float holdRemaining;
    private int sweepStep;
    private bool arrived;
    private bool standingStill;

    /// <param name="focusPoint">Where contact was last made -- the centre of the area to search.</param>
    /// <param name="searchDuration">Seconds spent searching once the focus point is reached, before giving up.</param>
    public SearchAction(Vector3 focusPoint, float searchDuration = 20f, float speedMultiplier = 0.45f,
                        float sweepRadius = 4f, float travelTimeout = 25f, float lookHoldTime = 1.4f)
    {
        focus = focusPoint;
        this.searchDuration = searchDuration;
        this.speedMultiplier = speedMultiplier;
        this.sweepRadius = sweepRadius;
        this.travelTimeout = travelTimeout;
        this.lookHoldTime = lookHoldTime;
    }

    /// <summary>Where this search is centred, so a fresh contact can redirect a search already under way.</summary>
    public Vector3 Focus => focus;

    /// <summary>
    /// Points an in-progress search somewhere new and gives it its full time again -- what should
    /// happen when the entity is shot at again while already searching, rather than starting a whole
    /// new action and losing the fact that it was already hunting.
    /// </summary>
    public void Refocus(Vector3 focusPoint)
    {
        focus = focusPoint;
        searchElapsed = 0f;
        travelElapsed = 0f;
        arrived = false;
        standingStill = false;
        sweepStep = 0;

        if (enemy != null && enemy.Movement != null)
        {
            enemy.Rotation?.ClearAim();
            enemy.Movement.SetDestination(focus);
        }
    }

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);

        searchElapsed = 0f;
        travelElapsed = 0f;
        sweepStep = 0;
        arrived = false;
        standingStill = false;

        if (enemy.Movement != null)
            enemy.Movement.SpeedMultiplier = speedMultiplier;

        // Face where it's walking rather than staying locked on whatever it was aiming at.
        enemy.Rotation?.ClearAim();
        enemy.Movement?.SetDestination(focus);
    }

    public override ActionStatus Tick(float deltaTime)
    {
        var movement = enemy.Movement;
        if (movement == null)
            return ActionStatus.Failed;

        // Leg one: get to the place contact happened. The clock on the search itself doesn't start
        // until arrival, so a long walk doesn't eat the time meant for actually looking around.
        if (!arrived)
        {
            travelElapsed += deltaTime;

            if (AtDestination(movement) || travelElapsed >= travelTimeout)
            {
                arrived = true;
                BeginLook();
            }

            return ActionStatus.Running;
        }

        searchElapsed += deltaTime;
        if (searchElapsed >= searchDuration)
            return ActionStatus.Complete;

        if (standingStill)
        {
            holdRemaining -= deltaTime;
            if (holdRemaining <= 0f)
                BeginSweep();

            return ActionStatus.Running;
        }

        if (AtDestination(movement))
            BeginLook();

        return ActionStatus.Running;
    }

    public override void End()
    {
        if (enemy.Movement != null)
        {
            enemy.Movement.SpeedMultiplier = 1f;
            enemy.Movement.Stop();
        }

        enemy.Rotation?.ClearAim();
    }

    private static bool AtDestination(EnemyMovementSystem movement) =>
        movement.ReachedDestination || movement.DistanceToDestination <= ArriveDistance;

    /// <summary>Stand and turn to look somewhere the target could plausibly be.</summary>
    private void BeginLook()
    {
        standingStill = true;
        holdRemaining = lookHoldTime;

        enemy.Movement?.Stop();

        float yaw = SweepAngles[sweepStep % SweepAngles.Length];
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * enemy.transform.forward;
        enemy.Rotation?.AimAt(enemy.transform.position + direction * 6f);
    }

    /// <summary>Pace off to another spot around the focus point and keep looking from there.</summary>
    private void BeginSweep()
    {
        standingStill = false;
        sweepStep++;

        float yaw = SweepAngles[sweepStep % SweepAngles.Length];
        Vector3 offset = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * sweepRadius;

        enemy.Rotation?.ClearAim();
        enemy.Movement?.SetDestination(focus + offset);
    }
}
