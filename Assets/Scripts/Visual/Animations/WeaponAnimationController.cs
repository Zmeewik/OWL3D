using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

public class WeaponAnimationController : IAnimation
{
    [Header("References")] [SerializeField]
    private string weaponName;
    [SerializeField] List<MeshRenderer> meshRenderers; 
    [SerializeField] Animator weapon;
    [SerializeField] private WeaponBase weaponBase;
    [SerializeField] string weaponState;

    //Animation states control
    Coroutine OnAnimationLoop;


    //[Header("AnimationSettings")]
    private string currentAnimState;
    

    private struct QueuedAnim
    {
        public string name;
        public bool loop;

        public QueuedAnim(string anim, bool lp)
        {
            name = anim;
            loop = lp;
        }
    }
    
    private class PartQueueData
    {
        public bool isPlaying = false;
        public Coroutine coroutine = null;
    }

    private Queue<QueuedAnim> animationQueue = new Queue<QueuedAnim>(); 
    
    private bool[] isPartPlaying = new bool[3];
    
    private PartQueueData queueData = new PartQueueData();

    /*private Dictionary<string, string> weaponAnimationNames = new()
    {
        ["left_attack"] = "",
        ["left_attack_start"] = "",
        ["left_attack_continue"] = "",
        ["left_attack_end"] = "",
        ["left_attack_false"] = "",
        
        ["right_attack"] = "",
        ["right_attack_start"] = "",
        ["right_attack_continue"] = "",
        ["right_attack_end"] = "",
        
        ["middle_attack"] = "",
        ["middle_attack_start"] = "",
        ["middle_attack_continue"] = "",
        ["middle_attack_end"] = "",

        ["block_start"] = "",
        ["block_continue"] = "",
        ["block_action"] = "",
        ["block_end"] = "",
        ["block_break"] = "",

        ["melee_hit"] = "",
        ["show_off"] = "",
        ["idle"] = "",
        ["put_away"] = "",
        ["pick_up"] = ""
    };*/


    void Start()
    {
        ChangeVisibility(false);
        ChangeAnimation(weaponState);
        //Subscriptions to animation actions in scripts
        //weaponBase.OnAnimation += (animName)  => ChangeAnimation(animName);
    }

    void OnDisable()
    {
        //Subscriptions to animation actions in scripts
        //weaponBase.OnAnimation -= (animName)  => ChangeAnimation(animName);
    }

    private Vector2 rotationVector;

    //Change animation clip of bodypart
    private void ChangeAnimation(string animationClip)
    {
        PlayAnimation(animationClip);
        currentAnimState = animationClip;
    }

    IEnumerator ChangeVisibilityAtTime(float time)
    {
        yield return new WaitForSeconds(time);
        print("remove visibility " + weaponName);
        ChangeVisibility(false);
    }

    public void ChangeAnimation(string animationName, bool loop)
    {
        PlayAnimation(animationName, loop);
        currentAnimState = animationName;
    }


    //Play animation by player part
    private void PlayAnimation(string animationClip)
    {
        if (weapon != null)
        {
            PlayAnimationBasic(animationClip);
        }
    }

    //Play animation with loop
    private void PlayAnimation(string animationClip, bool loop)
    {
        //If animation looped activate loop with animation
        if (loop)
        {
            //Disable if same animation cycle activated for same bodypart
            if (OnAnimationLoop != null && currentAnimState == animationClip) return;
            //Start loop of clip
            var animationClips = weapon.runtimeAnimatorController.animationClips;
            float animationTime = 0f;
            foreach (var clip in animationClips)
            {
                if (clip.name == animationClip)
                {
                    animationTime = clip.averageDuration;
                    print($"Average {clip.name} clip time: {animationTime}");
                }
            }
            OnAnimationLoop = StartCoroutine(AnimationLoop(animationClip, animationTime));
        }
        //If animation not looped deactivate current loop
        else
        {
            if (OnAnimationLoop != null)
            {
                StopCoroutine(OnAnimationLoop);
                OnAnimationLoop = null;
            }
            OnAnimationLoop = null;
            PlayAnimation(animationClip);
        }
    }

    private void PlayAnimationBasic(string animationClip)
    {
        if (animationClip.Contains("PickUp"))
        {
            print("add visibility " + weaponName);
            ChangeVisibility(true);
        }
        else if (animationClip.Contains("PutAway"))
        {
            var time = GetClipLength(animationClip);
            StartCoroutine(ChangeVisibilityAtTime(time));
        }
        print($"weapon animation {animationClip} started!");
        weapon.Play(animationClip, 0, 0f);
    }


    // Handle animation loop automaticlly
    IEnumerator AnimationLoop(string animationClip, float waitTime = 1f)
    {
        while (true)
        {
            //print($"Starting animation {animationClip}");
            PlayAnimation(animationClip);
            yield return new WaitForSeconds(waitTime);
        }
    }


    //Bodyparts visualisation control
    public void ChangeVisibility(bool state)
    {
        foreach (var meshRenderer in meshRenderers)
            meshRenderer.enabled = state;
    }

    private string ChangeAnimationName(string animName)
    {
        //Change animation name
        animName = animName.Replace("_Arms", "");
        var weaponBaseString = animName.Split('_')[1];
        animName += "_Bake";
        //print("BASE: " + weaponBaseString + " PART: " + weaponName);
        if (weaponBaseString != weaponName)
            animName = animName.Replace(weaponBaseString, weaponName);
        return animName;
    }

    //Interface realization for weapon controller
    public override void Play(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false)
    {
        Debug.LogWarning("Start animation: " + animName + " " + this.name);
        
        animName = ChangeAnimationName(animName);
        animationQueue.Clear();
        animationQueue.Enqueue(new QueuedAnim(animName, loop));
        PlayNextInQueue();
    }
    public override void Enqueue(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false)
    {
        animName = ChangeAnimationName(animName);
        animationQueue.Enqueue(new QueuedAnim(animName, loop));
    }
    
    private void PlayNextInQueue()
    {

        if (animationQueue.Count == 0)
        {
            queueData.isPlaying = false;
            return;
        }

        var anim = animationQueue.Dequeue();

        // если loop — отдаем управление твоей системе loop-а
        if (anim.loop)
        {
            ChangeAnimation(anim.name, true);
            queueData.isPlaying = false; // queue закончил работу — loop работает сам по себе
            return;
        }

        // non-loop
        ChangeAnimation(anim.name, false);

        float clipTime = GetClipLength(anim.name);
        queueData.isPlaying = true;

        queueData.coroutine = StartCoroutine(WaitThenNext(clipTime));
    }
    
    private IEnumerator WaitThenNext(float wait)
    {
        yield return new WaitForSeconds(wait);
        PlayNextInQueue();
    }
    
    private float GetClipLength(string clipName)
    {
        Animator anim = weapon;

        foreach (var clip in anim.runtimeAnimatorController.animationClips)
            if (clip.name == clipName)
                return clip.length;

        return 0.1f; // на случай отсутствия
    }

    


    //Stop all animations in the animatorn and queues
    public override void StopAllAnimations(EntityAnimator entity, string fallbackState = null, string bodyPart = null)
    {

        // стоп coroutine очереди
        if (queueData.coroutine != null)
        {
            StopCoroutine(queueData.coroutine);
            queueData.coroutine = null;
        }

        queueData.isPlaying = false;

        // очистка очереди
        animationQueue.Clear();

        // стоп loop, если был
        if (OnAnimationLoop != null)
        {
            StopCoroutine(OnAnimationLoop);
            OnAnimationLoop = null;
        }

        // fallback
        if (fallbackState != null)
            ChangeAnimation(fallbackState);
        
    }
}
