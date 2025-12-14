using UnityEngine;

public class EntityAnimationManager : IAnimation
{
    public static EntityAnimationManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Plays an animation on given entity.
    /// </summary>
    public override void Play(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false)
    {
        if (entity == null)
        {
            Debug.LogWarning($"[{nameof(EntityAnimationManager)}] Tried to play animation '{animName}' on a null entity.");
            return;
        }

        entity.Play(animName, bodyPart, loop);
    }

    public override void Enqueue(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false)
    {
        if (entity == null) return;
        entity.Enqueue(animName, bodyPart, loop);
    }

    public override void StopAllAnimations(EntityAnimator entity, string fallbackState = null, string bodyPart = null)
    {
        if(entity == null) return;
        entity.StopAnimation(fallbackState, bodyPart);
    }
}