using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EntityAnimator : MonoBehaviour
{
    [System.Serializable]
    public class BodyPart
    {
        public string name;
        public Animator animator;
    }

    [Header("General Animator (for full-body animations)")]
    [SerializeField] private Animator sharedAnimator;

    [Header("Body Parts (for partial animations)")]
    public List<BodyPart> bodyParts = new();
    private readonly Dictionary<string, Animator> _partAnimators = new();

    //Loops handle
    private readonly Dictionary<string, Coroutine> _loops = new();

    //Queue handle
    private readonly Queue<(string anim, string part, bool loop, float speed)> _queue = new();
    private Coroutine _queueRoutine;


    [SerializeField] EntityHealth health;
    

    private void Awake()
    {
        _partAnimators.Clear();
        Play("E_Robot_Boxer_Dance", true);

        health.OnAnimateCommand += GetHitAndReturn;
        
        foreach (var part in bodyParts)
        {
            if (part.animator != null && !_partAnimators.ContainsKey(part.name))
                _partAnimators.Add(part.name, part.animator);
        }
    }

    private bool stopThis = false;
    
    //Delete this
    private void GetHitAndReturn(string anim, string part, bool loop, float speed = 1)
    {
        if (stopThis) return;
        stopThis = true;
        StopAllLoops();
        StartCoroutine(PlayThenRoutine(anim, "E_Robot_Boxer_Dance", "Body", true, speed));
    }
    
    //Delegte this too
    private IEnumerator PlayThenRoutine(string firstAnim, string nextAnim, string part, bool nextLoop, float speed)
    {
        Animator anim;

        if (part == null)
            anim = sharedAnimator;
        else
            anim = _partAnimators[part];

        // play first
        anim.speed = speed;
        anim.Play(firstAnim, 0, 0);

        // wait for first animation length
        float len = GetClipLength(anim, firstAnim);
        yield return new WaitForSeconds(len / speed);

        // play second
        stopThis = false;
        
        anim.Play(nextAnim, 0, 0);
        
        if (nextLoop)
            _loops[nextAnim] = StartCoroutine(Loop(anim, nextAnim));
    }

    //Animation calls
    /// <summary>
    /// Play animation at all body
    /// </summary>
    public void Play(string animationName, bool loop = false, float speed = 1)
    {
        // Clear queue to avoid overlap
        _queue.Clear();
        if (_queueRoutine != null)
        {
            StopCoroutine(_queueRoutine);
            _queueRoutine = null;
        }

        if (sharedAnimator == null)
        {
            Debug.LogWarning($"[{name}] No shared animator for full-body animation {animationName}");
            return;
        }

        if (!HasAnimation(sharedAnimator, animationName))
        {
            Debug.LogWarning($"[{name}] Animation '{animationName}' not found in shared animator.");
            return;
        }

        StopAllLoops();
        
        sharedAnimator.speed = speed;
        sharedAnimator.Play(animationName, 0, 0);
        if (loop)
            _loops["__shared"] = StartCoroutine(Loop(sharedAnimator, animationName, speed));
    }

    /// <summary>
    /// Play animation at one bodypart
    /// </summary>
    public void Play(string animationName, string bodyPart, bool loop = false, float speed = 1)
    {
        if (!_partAnimators.TryGetValue(bodyPart, out var animator))
        {
            Debug.LogWarning($"[{name}] Body part '{bodyPart}' not found.");
            return;
        }

        if (!HasAnimation(animator, animationName))
        {
            Debug.LogWarning($"[{name}] Animation '{animationName}' not found in '{bodyPart}'.");
            return;
        }

        StopLoop(bodyPart);
        
        animator.speed = speed;
        animator.Play(animationName, 0, 0);
        if (loop)
            _loops[bodyPart] = StartCoroutine(Loop(animator, animationName, speed));
    }

    /// <summary>
    /// Play animation at several bodyparts
    /// </summary>
    public void Play(string animationName, string[] bodyParts, bool loop = false, float speed = 1)
    {
        foreach (var part in bodyParts)
            Play(animationName, part, loop, speed);
    }


    // Check up for animation existance
    private bool HasAnimation(Animator animator, string clipName)
    {
        if (animator.runtimeAnimatorController == null)
            return false;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == clipName)
                return true;

        return false;
    }

    // Loop Handle
    private IEnumerator Loop(Animator animator, string animationName, float speed = 1)
    {
        var clip = GetClipLength(animator, animationName);
        while (true)
        {
            animator.speed = speed;
            animator.Play(animationName, 0, 0);
            yield return new WaitForSeconds(clip / speed);
        }
    }

    private float GetClipLength(Animator animator, string clipName)
    {
        if (animator.runtimeAnimatorController == null)
            return 1f;

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == clipName)
                return clip.length;

        return 1f;
    }

    private void StopLoop(string key)
    {
        if (_loops.TryGetValue(key, out var coroutine) && coroutine != null)
        {
            StopCoroutine(coroutine);
            _loops[key] = null;
        }
    }

    private void StopAllLoops()
    {
        foreach (var key in new List<string>(_loops.Keys))
            StopLoop(key);
    }


    //Queue handle
    public void Enqueue(string animationName, string bodyPart = null, bool loop = false, float speed = 1)
    {
        _queue.Enqueue((animationName, bodyPart, loop, speed));

        if (_queueRoutine == null)
            _queueRoutine = StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        while (_queue.Count > 0)
        {
            var (anim, part, loop, speed) = _queue.Dequeue();

            // Start with current bodypart
            if (part == null)
                Play(anim, loop, speed);
            else
                Play(anim, part, loop, speed);

            float length = part == null 
                ? GetClipLength(sharedAnimator, anim)
                : GetClipLength(_partAnimators[part], anim);

            if (!loop)
            {
                yield return new WaitForSeconds(length / speed);
            }
            else
            {
                // Loop until queue is finished
                while (_queue.Count == 0)
                {
                    if (part == null)
                    {
                        sharedAnimator.speed = speed;
                        sharedAnimator.Play(anim, 0, 0);
                    }
                    else
                    {
                        sharedAnimator.speed = speed;
                        _partAnimators[part].Play(anim, 0, 0);
                    }

                    yield return new WaitForSeconds(length / speed);
                }
            }
        }

        _queueRoutine = null;
    }



    //Stop anuy animation
    public void StopAnimation(string fallbackState = null, string bodyPart = null)
    {
        //Stop queue
        _queue.Clear();

        if (_queueRoutine != null)
        {
            StopCoroutine(_queueRoutine);
            _queueRoutine = null;
        }

        //Clear cycles
        if (bodyPart == null)
            StopAllLoops();
        else
            StopLoop(bodyPart);

        //Reset animation
        if (bodyPart == null)
        {
            if (sharedAnimator != null)
            {
                if (!string.IsNullOrEmpty(fallbackState))
                    sharedAnimator.Play(fallbackState, 0, 0);
                else
                    sharedAnimator.Play("Empty", 0, 0);
            }
        }
        else
        {
            if (_partAnimators.TryGetValue(bodyPart, out var animator))
            {
                if (!string.IsNullOrEmpty(fallbackState))
                    animator.Play(fallbackState, 0, 0);
                else
                    animator.Play("Empty", 0, 0);
            }
        }
    }


    //Service
    public bool HasBodyPart(string name) => _partAnimators.ContainsKey(name);
    public IEnumerable<string> GetBodyParts() => _partAnimators.Keys;
}