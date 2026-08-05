using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The thinking half of an enemy: turns manager commands and its own senses into an ordered list of
/// <see cref="EnemyAction"/>s, then runs them one at a time.
///
/// A list rather than a state machine because the requirement is explicitly that plans can be
/// appended to, cut down and cleared at runtime -- "surround that point, then hold" is a queue, and
/// "you've lost the target, stop chasing" is a targeted removal from the middle of one.
/// </summary>
public abstract class EnemyStrategy : MonoBehaviour
{
    protected Enemy enemy;

    private readonly List<EnemyAction> actions = new();
    private bool frontBegun;

    public IReadOnlyList<EnemyAction> Actions => actions;
    public EnemyAction CurrentAction => actions.Count > 0 ? actions[0] : null;

    /// <summary>The side this strategy is acting for. Read from the entity so there's one source of truth.</summary>
    public Team Team => enemy != null ? enemy.Team : Team.Neutral;

    public virtual void Initialize(Enemy owner)
    {
        enemy = owner;

        if (owner.Vision != null)
        {
            owner.Vision.OnTargetSpotted += HandleTargetSpotted;
            owner.Vision.OnTargetLost += HandleTargetLost;
        }

        if (owner.Health != null)
            owner.Health.OnDamaged += HandleDamaged;

        OnInitialized();
    }

    protected virtual void OnInitialized() { }

    private void OnDestroy()
    {
        if (enemy == null) return;

        if (enemy.Vision != null)
        {
            enemy.Vision.OnTargetSpotted -= HandleTargetSpotted;
            enemy.Vision.OnTargetLost -= HandleTargetLost;
        }

        if (enemy.Health != null)
            enemy.Health.OnDamaged -= HandleDamaged;
    }

    /// <summary>Runs the action at the head of the queue, advancing when it finishes.</summary>
    public void Tick(float deltaTime)
    {
        if (actions.Count == 0)
        {
            OnQueueEmpty();
            if (actions.Count == 0)
                return;
        }

        var action = actions[0];

        if (!frontBegun)
        {
            action.Begin(enemy);
            frontBegun = true;
        }

        var status = action.Tick(deltaTime);
        if (status == ActionStatus.Running)
            return;

        action.End();
        actions.RemoveAt(0);
        frontBegun = false;
    }

    /// <summary>Called when nothing is queued. Default keeps the entity idling rather than frozen mid-action.</summary>
    protected virtual void OnQueueEmpty()
    {
        Enqueue(new IdleAction());
    }

    // ---- Queue manipulation -------------------------------------------------

    public void Enqueue(EnemyAction action)
    {
        if (action != null)
            actions.Add(action);
    }

    /// <summary>Puts an action at the front, suspending whatever was running. Used for reactions that can't wait.</summary>
    public void Push(EnemyAction action)
    {
        if (action == null) return;

        EndFrontIfBegun();
        actions.Insert(0, action);
    }

    /// <summary>Drops the whole plan, ending the running action cleanly so no system is left mid-command.</summary>
    public void ClearActions()
    {
        EndFrontIfBegun();
        actions.Clear();
    }

    public void RemoveAction(EnemyAction action)
    {
        int index = actions.IndexOf(action);
        if (index < 0) return;

        if (index == 0)
            EndFrontIfBegun();

        actions.RemoveAt(index);
    }

    /// <summary>Removes every queued action of a kind -- e.g. dropping all combat when a target is lost.</summary>
    public void RemoveActions<T>() where T : EnemyAction
    {
        for (int i = actions.Count - 1; i >= 0; i--)
        {
            if (actions[i] is not T) continue;

            if (i == 0)
                EndFrontIfBegun();

            actions.RemoveAt(i);
        }
    }

    public bool HasAction<T>() where T : EnemyAction
    {
        for (int i = 0; i < actions.Count; i++)
            if (actions[i] is T)
                return true;
        return false;
    }

    private void EndFrontIfBegun()
    {
        if (!frontBegun || actions.Count == 0)
        {
            frontBegun = false;
            return;
        }

        actions[0].End();
        frontBegun = false;
    }

    // ---- Reactions ----------------------------------------------------------

    /// <summary>Parses one broadcast from <see cref="EnemyManager"/> into plan changes.</summary>
    public abstract void HandleCommand(EnemyCommand command);

    private void HandleTargetSpotted(TeamMember target) => OnTargetSpotted(target);
    private void HandleTargetLost(TeamMember target) => OnTargetLost(target);
    private void HandleDamaged(DamagePacket packet) => OnDamaged(packet);

    protected virtual void OnTargetSpotted(TeamMember target) { }
    protected virtual void OnTargetLost(TeamMember target) { }

    /// <summary>
    /// Reacts to taking a hit by turning to look where it came from -- not locking onto the
    /// attacker, just facing the point. Vision keeps scanning while the turn runs, so a real target
    /// caught in view during it gets engaged the normal way (OnTargetSpotted); if the turn finishes
    /// and nothing's there, the queue falls back to idling on its own. Override for a strategy that
    /// should react differently (or not at all).
    /// </summary>
    protected virtual void OnDamaged(DamagePacket packet)
    {
        if (enemy == null)
            return;

        // forceApplied points from attacker into victim (EntityHealth.ApplyDamage relies on the
        // same fact for its hit-reaction animation), so its reverse points back toward the source.
        Vector3 towardSource = -packet.forceApplied;
        if (towardSource.sqrMagnitude < 0.0001f)
            return;

        Push(new FaceDirectionAction(enemy.transform.position + towardSource.normalized * 5f));
    }
}
