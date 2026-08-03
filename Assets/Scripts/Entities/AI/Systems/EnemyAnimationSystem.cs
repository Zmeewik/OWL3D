using UnityEngine;

/// <summary>
/// Drives the entity's Animator from what the AI is actually doing: locomotion follows the movement
/// system, one-shots (attack, hit reactions) interrupt it and hand control back when the clip ends.
///
/// States are played by name with a HasState guard rather than through Animator parameters, because
/// the rigs here (Runner's Body controller) are flat lists of states with no parameters or
/// transitions wired. The guard matters -- Animator.Play on a missing state logs
/// "Animator.GotoState: State could not be found" and does nothing visible, which is easy to miss.
/// </summary>
public class EnemyAnimationSystem : EnemySystem
{
    [Header("References")]
    [SerializeField] private Animator animator;

    [Header("State names")]
    [SerializeField] private string idleState = "E_Runner_Char_Unweponized_Idle";
    [SerializeField] private string walkState = "E_Runner_Char_Unweponized_Walk";
    [SerializeField] private string attackState = "E_Runner_Char_Unweponized_Attack1";
    [SerializeField] private string hitFrontState = "E_Runner_Char_Unweponized_HitFront";
    [SerializeField] private string hitBackState = "E_Runner_Char_Unweponized_HitBack";

    [Header("Blending")]
    [SerializeField] private float crossFade = 0.12f;

    private EnemyMovementSystem movement;
    private EntityHealth health;
    private string currentState;
    private float oneShotRemaining;

    public override void Initialize(Enemy owner)
    {
        base.Initialize(owner);

        if (animator == null)
            animator = owner.GetComponentInChildren<Animator>();

        movement = owner.Movement;
        health = owner.Health;

        if (health != null)
            health.OnAnimateCommand += HandleHealthAnimation;

        PlayState(idleState, force: true);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnAnimateCommand -= HandleHealthAnimation;
    }

    public override void TickSystem(float deltaTime)
    {
        if (animator == null) return;

        // A one-shot owns the animator until it finishes; locomotion resumes after.
        if (oneShotRemaining > 0f)
        {
            oneShotRemaining -= deltaTime;
            return;
        }

        bool moving = movement != null && movement.IsMoving;
        PlayState(moving ? walkState : idleState);
    }

    public void PlayAttack() => PlayOneShot(attackState);

    /// <summary>
    /// EntityHealth broadcasts hit reactions over the same string channel the player's animation
    /// controllers listen on, so the enemy just picks the two it can play.
    /// </summary>
    private void HandleHealthAnimation(string command, bool loop, float speed)
    {
        if (command == AnimateCommand.HitFront)
            PlayOneShot(hitFrontState);
        else if (command == AnimateCommand.HitBack)
            PlayOneShot(hitBackState);
    }

    private void PlayOneShot(string state)
    {
        if (!PlayState(state, force: true))
            return;

        oneShotRemaining = GetStateLength(state);
    }

    private bool PlayState(string state, bool force = false)
    {
        if (animator == null || string.IsNullOrEmpty(state))
            return false;

        if (!force && currentState == state)
            return true;

        int hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash))
        {
            Debug.LogWarning($"[{name}] Animator has no state '{state}'.", this);
            return false;
        }

        if (force)
            animator.Play(hash, 0, 0f);
        else
            animator.CrossFade(hash, crossFade, 0);

        currentState = state;
        return true;
    }

    private float GetStateLength(string state)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0.5f;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == state)
                return clip.length;

        return 0.5f;
    }
}
