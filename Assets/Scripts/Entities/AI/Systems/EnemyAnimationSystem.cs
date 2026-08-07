using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives every animator an enemy is made of -- body, weapon, magazine -- from a single semantic
/// motion, and tracks whether the enemy is currently armed.
///
/// Clip names are derived, not listed. The rig follows one strict pattern:
///
///     body      E_Runner_Char_Idle_Weponized
///     weapon    E_Runner_Uzi_Idle_Weponized_Bake
///     magazine  E_Runner_UziMag_Idle_Weponized_Bake
///
/// so a part only needs to know its own token ("Char" / "Uzi" / "UziMag") and whether its clips
/// carry the "_Bake" suffix. That mirrors how the player's weapons already work in
/// WeaponAnimationController.ChangeAnimationName, which substitutes the weapon token into the
/// arms' clip name -- the enemy naming just adds the armed-state suffix on top.
///
/// States are played by name with a HasState guard rather than through Animator parameters, because
/// these controllers are flat lists of states with no parameters or transitions wired. The guard
/// matters: Animator.Play on a missing state logs "Animator.GotoState: State could not be found"
/// and does nothing visible.
/// </summary>
public class EnemyAnimationSystem : EnemySystem
{
    /// <summary>One animator taking part in the same motion, and how its clip names are spelled.</summary>
    [Serializable]
    public class AnimatedPart
    {
        [Tooltip("Token this part occupies in the clip name: Char, Uzi, UziMag ...")]
        public string partToken = "Char";

        [Tooltip("Leave empty to auto-resolve from the controller name below.")]
        public Animator animator;

        [Tooltip("Animator controller asset name used to find this part when the reference is empty.")]
        public string controllerName = "Body";

        [Tooltip("Baked parts (weapon, magazine) carry a _Bake suffix; the body doesn't.")]
        public bool appendBake = false;
    }

    [Header("Naming")]
    [Tooltip("Common prefix of every clip on this enemy, e.g. 'E_Runner'.")]
    [SerializeField] private string clipPrefix = "E_Runner";

    [Header("Parts")]
    [SerializeField]
    private List<AnimatedPart> parts = new()
    {
        new AnimatedPart { partToken = "Char",   controllerName = "Body",   appendBake = false },
        new AnimatedPart { partToken = "Uzi",    controllerName = "Uzi",    appendBake = true  },
        new AnimatedPart { partToken = "UziMag", controllerName = "UziMag", appendBake = true  },
    };

    [Header("Legacy reference")]
    [Tooltip("Body animator. Kept so existing scene wiring survives; feeds the first part when set.")]
    [SerializeField] private Animator animator;

    [Header("State")]
    [SerializeField] private EnemyArmedState armedState = EnemyArmedState.Unweponized;

    [Header("Blending")]
    [SerializeField] private float crossFade = 0.12f;

    private EnemyMovementSystem movement;
    private EntityHealth health;

    private EnemyMotion currentMotion;
    private bool hasCurrentMotion;
    private float oneShotRemaining;

    private bool changingWeapon;
    private EnemyArmedState pendingArmedState;

    public bool IsArmed => armedState == EnemyArmedState.Weponized;

    /// <summary>True while the draw/holster animation is still playing. Attacks wait this out.</summary>
    public bool IsChangingWeapon => changingWeapon;

    /// <summary>True while a one-shot animation is still running.</summary>
    public bool IsBusy => oneShotRemaining > 0f;

    /// <summary>Which one-shot is running, meaningful only while <see cref="IsBusy"/>.</summary>
    public EnemyMotion BusyMotion => currentMotion;

    /// <summary>
    /// True while the running animation is one the body is committed to and can't walk out of.
    /// Movement reads this so an entity can't stroll away mid-shot, mid-posture or mid-sentence --
    /// actions overlapping like that is what made everything look like it was sliding around.
    ///
    /// A dodge is deliberately absent: the dodge *is* a movement, and locking it would cancel the
    /// very displacement it exists to produce.
    /// </summary>
    public bool MovementLocked => IsBusy && LocksMovement(currentMotion);

    private static bool LocksMovement(EnemyMotion motion)
    {
        switch (motion)
        {
            case EnemyMotion.AttackRanged1:
            case EnemyMotion.AttackRanged2:
            case EnemyMotion.LegHit:
            case EnemyMotion.ShowOff:
            case EnemyMotion.Talk:
            case EnemyMotion.Interaction:
            case EnemyMotion.ChangeWeapon:
            case EnemyMotion.Reload:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// How strongly a motion claims the body. A request never interrupts something more important
    /// than itself, which is what keeps a cosmetic flourish from cutting an attack in half -- the
    /// breather's ShowOff used to start 0.42s into a 0.83s attack animation and replace it, which is
    /// why attacks looked like they changed to something else partway through.
    /// </summary>
    private static int PriorityOf(EnemyMotion motion)
    {
        switch (motion)
        {
            case EnemyMotion.HitFront:
            case EnemyMotion.HitBack:
            case EnemyMotion.Stagger:
                return 4;

            case EnemyMotion.DodgeLeft:
            case EnemyMotion.DodgeRight:
                return 3;

            case EnemyMotion.AttackRanged1:
            case EnemyMotion.AttackRanged2:
            case EnemyMotion.LegHit:
            case EnemyMotion.ChangeWeapon:
            case EnemyMotion.Reload:
                return 2;

            default:
                return 1;   // cosmetic: ShowOff, Talk, Interaction
        }
    }

    public override void Initialize(Enemy owner)
    {
        base.Initialize(owner);

        movement = owner.Movement;
        health = owner.Health;

        ResolveParts(owner);

        if (health != null)
            health.OnAnimateCommand += HandleHealthAnimation;

        PlayMotion(EnemyMotion.Idle, force: true);
    }

    private void OnDestroy()
    {
        if (health != null)
            health.OnAnimateCommand -= HandleHealthAnimation;
    }

    /// <summary>
    /// Fills in any part whose animator wasn't wired by hand. Matching on the controller asset name
    /// rather than the GameObject name because the controllers are already named exactly after the
    /// parts (Body / Uzi / UziMag), while the GameObjects repeat the rig's name at several levels.
    /// </summary>
    private void ResolveParts(Enemy owner)
    {
        if (parts.Count > 0 && parts[0].animator == null && animator != null)
            parts[0].animator = animator;

        var found = owner.GetComponentsInChildren<Animator>(true);

        foreach (var part in parts)
        {
            if (part.animator != null)
                continue;

            foreach (var candidate in found)
            {
                var controller = candidate.runtimeAnimatorController;
                if (controller != null && controller.name == part.controllerName)
                {
                    part.animator = candidate;
                    break;
                }
            }

            if (part.animator == null)
                Debug.LogWarning($"[{name}] No animator found for part '{part.partToken}' " +
                                 $"(looked for controller '{part.controllerName}').", this);
        }
    }

    public override void TickSystem(float deltaTime)
    {
        if (oneShotRemaining > 0f)
        {
            oneShotRemaining -= deltaTime;
            if (oneShotRemaining > 0f)
                return;

            // A finished weapon change is what actually commits the new armed state, so the body
            // isn't holding a gun it hasn't drawn yet.
            if (changingWeapon)
            {
                changingWeapon = false;
                armedState = pendingArmedState;
                hasCurrentMotion = false;
            }
        }

        // Jumping overrides walk/idle -- IsMoving alone can't tell a jump apart from ordinary
        // ground movement (MoveDirection stays nonzero for the whole arc), which used to leave the
        // walk cycle looping while the rigidbody flew a parabola.
        if (movement != null && movement.IsJumping)
            PlayMotion(EnemyMotion.InAir);
        else
            PlayMotion(movement != null && movement.IsMoving ? EnemyMotion.Walk : EnemyMotion.Idle);

        RestartFinishedLoop();
    }

    /// <summary>
    /// Motions that are a continuous state rather than a one-off event, and so must keep running
    /// for as long as the enemy stays in them.
    /// </summary>
    private static bool IsLoopingMotion(EnemyMotion motion)
    {
        return motion == EnemyMotion.Idle
            || motion == EnemyMotion.Walk
            || motion == EnemyMotion.InAir
            || motion == EnemyMotion.BlockContinue
            || motion == EnemyMotion.Talk;
    }

    /// <summary>
    /// Re-issues a continuous motion once its clip has run out.
    ///
    /// Every clip on this rig is imported with Loop Time off, so a state like idle or walk played
    /// once and then froze on its final frame -- the animator kept advancing normalizedTime past 1
    /// while the pose never changed again, which reads as "the animations fire once and then stop
    /// working". PlayMotion alone can't recover from that: it skips re-issuing a motion that's
    /// already current, so nothing ever restarted the clip.
    ///
    /// Done here rather than by flipping Loop Time on the importers so the behaviour holds no
    /// matter how the FBXs are re-exported; a state the importer *does* loop is left alone.
    /// </summary>
    private void RestartFinishedLoop()
    {
        if (!hasCurrentMotion || oneShotRemaining > 0f || !IsLoopingMotion(currentMotion))
            return;

        foreach (var part in parts)
        {
            if (part.animator == null)
                continue;

            var state = part.animator.GetCurrentAnimatorStateInfo(0);
            if (state.loop || state.normalizedTime < 1f)
                continue;

            PlayMotion(currentMotion, force: true);
            return;
        }
    }

    // ---- Public API ---------------------------------------------------------

    /// <summary>
    /// Plays the attack that matches how the enemy actually struck: a ranged shot gets one of the
    /// firing animations, a melee swing gets the kick. Without this the body played a single
    /// generic attack regardless of which weapon went off.
    /// </summary>
    public void PlayAttack(bool ranged)
    {
        if (ranged)
            PlayOneShot(UnityEngine.Random.value < 0.5f ? EnemyMotion.AttackRanged1 : EnemyMotion.AttackRanged2);
        else
            PlayOneShot(EnemyMotion.LegHit);
    }

    /// <summary>Draws or stows the weapon, playing the transition and flipping state when it ends.</summary>
    public void SetArmed(bool armed, bool instant = false)
    {
        var wanted = armed ? EnemyArmedState.Weponized : EnemyArmedState.Unweponized;

        if (changingWeapon ? pendingArmedState == wanted : armedState == wanted)
            return;

        if (instant)
        {
            changingWeapon = false;
            armedState = wanted;
            hasCurrentMotion = false;
            return;
        }

        // ChangeWeapon is authored in the state being left: the Unweponized variant is the draw,
        // the Weponized one is the holster. So it plays before the state flips, not after.
        pendingArmedState = wanted;
        changingWeapon = true;
        PlayOneShot(EnemyMotion.ChangeWeapon);
    }

    /// <summary>
    /// Plays a motion that owns the body until it finishes. Refused while something at least as
    /// important is still playing, so actions queue up behind each other instead of overlapping.
    /// </summary>
    /// <returns>Whether the motion actually started.</returns>
    public bool PlayOneShot(EnemyMotion motion)
    {
        if (IsBusy && PriorityOf(motion) <= PriorityOf(currentMotion))
            return false;

        if (!PlayMotion(motion, force: true))
            return false;

        oneShotRemaining = GetMotionLength(motion);
        return true;
    }

    // ---- Playback -----------------------------------------------------------

    private bool PlayMotion(EnemyMotion motion, bool force = false)
    {
        if (!force && hasCurrentMotion && currentMotion == motion)
            return true;

        bool anyPlayed = false;

        foreach (var part in parts)
        {
            if (part.animator == null)
                continue;

            if (!TryResolveState(part, motion, out int hash))
                continue;

            if (force)
                part.animator.Play(hash, 0, 0f);
            else
                part.animator.CrossFade(hash, crossFade, 0);

            anyPlayed = true;
        }

        if (anyPlayed)
        {
            currentMotion = motion;
            hasCurrentMotion = true;
        }
        else
        {
            Debug.LogWarning($"[{name}] No part could play motion '{motion}' in state '{armedState}'.", this);
        }

        return anyPlayed;
    }

    /// <summary>
    /// Finds the state for a motion, preferring the current armed state and falling back to the
    /// other one. The fallback is what lets weaponized-only motions (the attacks, jump, land) be
    /// requested without every caller having to know which states a clip was authored for.
    /// </summary>
    private bool TryResolveState(AnimatedPart part, EnemyMotion motion, out int hash)
    {
        var other = armedState == EnemyArmedState.Weponized
            ? EnemyArmedState.Unweponized
            : EnemyArmedState.Weponized;

        if (TryState(part, motion, armedState, out hash))
            return true;

        return TryState(part, motion, other, out hash);
    }

    private bool TryState(AnimatedPart part, EnemyMotion motion, EnemyArmedState state, out int hash)
    {
        hash = Animator.StringToHash(BuildClipName(part, motion, state));
        return part.animator.HasState(0, hash);
    }

    private string BuildClipName(AnimatedPart part, EnemyMotion motion, EnemyArmedState state)
    {
        string name = $"{clipPrefix}_{part.partToken}_{motion}_{state}";
        return part.appendBake ? name + "_Bake" : name;
    }

    private float GetMotionLength(EnemyMotion motion)
    {
        foreach (var part in parts)
        {
            if (part.animator == null || part.animator.runtimeAnimatorController == null)
                continue;

            foreach (var state in new[] { armedState, Other(armedState) })
            {
                string wanted = BuildClipName(part, motion, state);
                foreach (var clip in part.animator.runtimeAnimatorController.animationClips)
                    if (clip.name == wanted)
                        return clip.length;
            }
        }

        return 0.5f;
    }

    private static EnemyArmedState Other(EnemyArmedState s) =>
        s == EnemyArmedState.Weponized ? EnemyArmedState.Unweponized : EnemyArmedState.Weponized;

    /// <summary>
    /// EntityHealth broadcasts hit reactions over the same string channel the player's animation
    /// controllers listen on, so the enemy just picks the two it can play.
    /// </summary>
    private void HandleHealthAnimation(string command, bool loop, float speed)
    {
        if (command == AnimateCommand.HitFront)
            PlayOneShot(EnemyMotion.HitFront);
        else if (command == AnimateCommand.HitBack)
            PlayOneShot(EnemyMotion.HitBack);
    }
}
