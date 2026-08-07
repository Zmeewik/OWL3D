using UnityEngine;

/// <summary>
/// What an entity does when it has nothing to do -- which should not be "stand perfectly still
/// forever". Rolls between strolling a short way, stopping to look around, and turning to chat with
/// a nearby ally, then rolls again, indefinitely.
///
/// Everything stays tethered to <see cref="EnemyStrategy.HomePosition"/> rather than to wherever the
/// entity currently is, so milling about can never accumulate into wandering away from its post.
///
/// This never ends on its own: it's the fallback the queue falls into, and it's replaced the moment
/// anything real happens (vision spotting something clears the queue). Interruption is the exit.
/// </summary>
public class AmbientIdleAction : EnemyAction
{
    private enum Beat
    {
        Standing,
        Strolling,
        Chatting,
    }

    private readonly float wanderRadius;
    private readonly float walkSpeed;
    private readonly float chatRange;

    private Beat beat;
    private Beat previousBeat;
    private float beatRemaining;
    private Vector3 home;

    /// <summary>Ally this entity is currently turned toward, so a conversation keeps facing the right way.</summary>
    private Transform chatPartner;

    public AmbientIdleAction(float wanderRadius = 2.5f, float walkSpeed = 0.4f, float chatRange = 6f)
    {
        this.wanderRadius = wanderRadius;
        this.walkSpeed = walkSpeed;
        this.chatRange = chatRange;
    }

    public override void Begin(Enemy owner)
    {
        base.Begin(owner);

        home = owner.Strategy != null ? owner.Strategy.HomePosition : owner.transform.position;

        if (enemy.Movement != null)
            enemy.Movement.SpeedMultiplier = walkSpeed;

        PickBeat();
    }

    public override ActionStatus Tick(float deltaTime)
    {
        beatRemaining -= deltaTime;

        // Strolling ends on arrival as well as on the clock, so the entity doesn't stand frozen at
        // its destination waiting out a timer.
        if (beat == Beat.Strolling && enemy.Movement != null && enemy.Movement.ReachedDestination)
            beatRemaining = Mathf.Min(beatRemaining, 0f);

        if (beat == Beat.Chatting)
            KeepFacingPartner();

        if (beatRemaining <= 0f)
            PickBeat();

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
        chatPartner = null;
    }

    /// <summary>
    /// Called by a neighbour that has started talking to this entity, so a conversation is two-sided
    /// instead of one guard monologuing at another's back.
    /// </summary>
    public void RespondToChat(Transform speaker, float duration)
    {
        if (speaker == null)
            return;

        // Refuse to be dragged into another conversation while already in one. Without this, a
        // couple of neighbours taking turns to talk at the same entity kept resetting its beat and
        // pinned it in place indefinitely -- one guard of three never took a single step.
        if (beat == Beat.Chatting)
            return;

        chatPartner = speaker;
        previousBeat = beat;
        beat = Beat.Chatting;
        beatRemaining = duration;

        enemy.Movement?.Stop();
        enemy.Rotation?.AimAt(speaker);
        enemy.Animation?.PlayOneShot(EnemyMotion.Talk);
    }

    private void PickBeat()
    {
        chatPartner = null;
        previousBeat = beat;

        // Chatting only comes up when there's actually someone to chat to, so a lone guard paces and
        // looks around instead of miming a conversation with nobody. Never twice running, either:
        // back-to-back conversations are what let a group talk itself into standing still forever.
        //
        // Weighted heavily toward simply standing about: a guard on duty is mostly still, glancing
        // around, with the occasional few paces. Chatting and strolling both used to come up often
        // enough that a group read as restless rather than posted.
        var ally = previousBeat == Beat.Chatting ? null : FindNearbyAlly();
        float roll = Random.value;

        if (ally != null && roll < 0.12f)
            BeginChat(ally);
        else if (roll < 0.42f)
            BeginStroll();
        else
            BeginStand();
    }

    private void BeginStand()
    {
        beat = Beat.Standing;
        beatRemaining = Random.Range(5f, 10f);

        enemy.Movement?.Stop();

        // Glance somewhere new rather than staring straight ahead the whole time.
        float yaw = Random.Range(-120f, 120f);
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * enemy.transform.forward;
        enemy.Rotation?.AimAt(enemy.transform.position + direction * 6f);
    }

    private void BeginStroll()
    {
        beat = Beat.Strolling;
        beatRemaining = Random.Range(3f, 5f);

        // Short paces around the post, not laps of it.
        Vector2 offset = Random.insideUnitCircle * wanderRadius;
        Vector3 spot = home + new Vector3(offset.x, 0f, offset.y);

        enemy.Rotation?.ClearAim();
        enemy.Movement?.SetDestination(spot);
    }

    private void BeginChat(Enemy ally)
    {
        beat = Beat.Chatting;
        beatRemaining = Random.Range(3.5f, 6.5f);
        chatPartner = ally.transform;

        enemy.Movement?.Stop();
        enemy.Rotation?.AimAt(chatPartner);
        enemy.Animation?.PlayOneShot(EnemyMotion.Talk);

        // Ask them to turn and talk back, so it reads as a conversation from the outside.
        if (ally.Strategy != null && ally.Strategy.CurrentAction is AmbientIdleAction theirIdle)
            theirIdle.RespondToChat(enemy.transform, beatRemaining);
    }

    private void KeepFacingPartner()
    {
        if (chatPartner == null)
            return;

        enemy.Rotation?.AimAt(chatPartner);
    }

    /// <summary>Nearest living ally close enough to talk to.</summary>
    private Enemy FindNearbyAlly()
    {
        Enemy best = null;
        float bestDistance = chatRange;

        var active = EnemyManager.Active;
        for (int i = 0; i < active.Count; i++)
        {
            var other = active[i];
            if (other == null || other == enemy || !other.IsAlive)
                continue;

            if (Teams.Relation(enemy.Team, other.Team) != TeamRelation.Friendly)
                continue;

            float distance = Vector3.Distance(enemy.transform.position, other.transform.position);
            if (distance >= bestDistance)
                continue;

            best = other;
            bestDistance = distance;
        }

        return best;
    }
}
