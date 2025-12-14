using UnityEngine;

public abstract class IAnimation : MonoBehaviour
{
    public abstract void Play(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false);
    public abstract void Enqueue(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false);
    public abstract void StopAllAnimations(EntityAnimator entity, string fallbackState = null, string bodyPart = null);
}