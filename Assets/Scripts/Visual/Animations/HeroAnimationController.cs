using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

public class HeroAnimationController : IAnimation
{
    [Header("References")]
    [SerializeField] Animator leftHandAnimator;
    [SerializeField] Animator rightHandAnimator;
    [SerializeField] Animator leftLegAnimator;
    [SerializeField] Animator rightLegAnimator;
    [SerializeField] AnimationState startLeftHandState;
    [SerializeField] AnimationState startRightHandState;
    [SerializeField] AnimationState startLegState;

    //Animation states control
    Coroutine[] OnAnimationLoop = new Coroutine[3];

    [Header("OffsetAnimation")]
    

    [Header("RotationCamera")]
    [SerializeField] Transform handsOffsetObject;

    //[Header("AnimationSettings")]
    Dictionary<BodyPart, AnimationState> currentAnimStates = new Dictionary<BodyPart, AnimationState>();


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

    private Queue<QueuedAnim>[] animationQueues = 
    {
        new Queue<QueuedAnim>(), // left hand
        new Queue<QueuedAnim>(), // right hand
        new Queue<QueuedAnim>()  // right leg
    };
    
    private bool[] isPartPlaying = new bool[3];
    
    private PartQueueData[] queueData = new PartQueueData[3];
    
    //Singleton instance
    public static HeroAnimationController Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        for (int i = 0; i < 3; i++)
            queueData[i] = new PartQueueData();
    }

    void Start()
    {
        ChangeAnimation(startLeftHandState, BodyPart.left_hand);
        ChangeAnimation(startRightHandState, BodyPart.right_hand);
        ChangeAnimation(startLegState, BodyPart.right_leg);

        //Subscriptions to animation actions in scripts
        PlayerMovement.OnPlayAnimationLArm += (animName, loop)  => ChangeAnimation(animName, BodyPart.left_hand, loop);
        PlayerMovement.OnPlayAnimationRArm += (animName, loop) => ChangeAnimation(animName, BodyPart.right_hand, loop);
        PlayerMovement.OnPlayAnimationRLeg += (animName, loop) => ChangeAnimation(animName, BodyPart.right_leg, loop);
    }

    void OnDisable()
    {
        //Subscriptions to animation actions in scripts
        PlayerMovement.OnPlayAnimationLArm -= (animName, loop) => ChangeAnimation(animName, BodyPart.left_hand, loop);
        PlayerMovement.OnPlayAnimationRArm -= (animName, loop) => ChangeAnimation(animName, BodyPart.right_hand, loop);
        PlayerMovement.OnPlayAnimationRLeg -= (animName, loop) => ChangeAnimation(animName, BodyPart.right_leg, loop);
    }

    //Player move states
    public enum AnimationState { 
                                H_Arms_Boxing_HitChargeContinue1,
                                H_Arms_Boxing_HitChargeContinue1_1,
                                H_Arms_Boxing_HitChargeContinue2,
                                H_Arms_Boxing_HitChargeContinue2_1,
                                H_Arms_Boxing_HitChargeStart1,
                                H_Arms_Boxing_HitChargeStart1_1,
                                H_Arms_Boxing_HitChargeStart2,
                                H_Arms_Boxing_HitChargeStart2_1,
                                H_Arms_Boxing_HitChargeEnd1,
                                H_Arms_Boxing_HitChargeEnd1_1,
                                H_Arms_Boxing_HitChargeEnd2,
                                H_Arms_Boxing_HitChargeEnd2_1,
                                H_Arms_Boxing_HitWrong1,
                                
                                //Block boxing
                                H_Arms_Boxing_BlockAction1,
                                H_Arms_Boxing_BlockAction2,
                                H_Arms_Boxing_BlockActionStrong,
                                H_Arms_Boxing_BlockEnd,
                                H_Arms_Boxing_BlockIdle,
                                H_Arms_Boxing_BlockStart,

                                //Boxing
                                H_Arms_Boxing_Idle,
                                H_Arms_Boxing_PutAway,
                                H_Arms_Boxing_WeakHit1,
                                H_Arms_Boxing_WeakHit2,
                                H_Arms_Boxing_WeakHit3,
                                H_Arms_Boxing_LegHit1,
                                H_Arms_Boxing_LegHit2,
                                H_Arms_Hide,

                                //Parkour
                                H_Arms_Get_Up,
                                H_Arms_ClimbUp
    }
    static string[] animationNames = {
                                //Charge boxing hit
                                "H_Arms_Boxing_HitChargeContinue1",
                                "H_Arms_Boxing_HitChargeContinue1_1",
                                "H_Arms_Boxing_HitChargeContinue2",
                                "H_Arms_Boxing_HitChargeContinue2_1",
                                "H_Arms_Boxing_HitChargeStart1",
                                "H_Arms_Boxing_HitChargeStart1_1",
                                "H_Arms_Boxing_HitChargeStart2",
                                "H_Arms_Boxing_HitChargeStart2_1",
                                "H_Arms_Boxing_HitChargeEnd1",
                                "H_Arms_Boxing_HitChargeEnd1_1",
                                "H_Arms_Boxing_HitChargeEnd2",
                                "H_Arms_Boxing_HitChargeEnd2_1",
                                "H_Arms_Boxing_HitWrong1",
                                
                                //Block boxing
                                "H_Arms_Boxing_BlockAction1",
                                "H_Arms_Boxing_BlockAction2",
                                "H_Arms_Boxing_BlockActionStrong",
                                "H_Arms_Boxing_BlockEnd",
                                "H_Arms_Boxing_BlockIdle",
                                "H_Arms_Boxing_BlockStart",

                                //Boxing
                                "H_Arms_Boxing_Idle",
                                "H_Arms_Boxing_PutAway",
                                "H_Arms_Boxing_WeakHit1",
                                "H_Arms_Boxing_WeakHit2",
                                "H_Arms_Boxing_WeakHit3",
                                "H_Arms_Boxing_LegHit1",
                                "H_Arms_Boxing_LegHit2",
                                "H_Arms_Hide",

                                //Parkour
                                "H_Arms_GetUp",
                                "H_Arms_ClimbUp"
                                };
    public enum BodyPart { left_hand, right_hand, right_leg, left_leg }
    private Vector2 rotationVector;


    //Change animation clip of bodypart
    private void ChangeAnimation(AnimationState animationClip, BodyPart bodyPart)
    {
        int index = (int)animationClip;
        PlayAnimation(animationNames[index], bodyPart);
        currentAnimStates[bodyPart] = animationClip;
    }

    public void ChangeAnimation(string animationName, BodyPart bodyPart)
    {
        AnimationState state = (AnimationState)Enum.Parse(typeof(AnimationState), animationName);
        int index = (int)state;
        PlayAnimation(animationNames[index], bodyPart);
        currentAnimStates[bodyPart] = state;
    }

    public void ChangeAnimation(string animationName, BodyPart bodyPart, bool loop)
    {
        AnimationState state = (AnimationState)Enum.Parse(typeof(AnimationState), animationName);
        int index = (int)state;
        PlayAnimation(animationNames[index], bodyPart, loop);
        currentAnimStates[bodyPart] = state;
    }


    //Play animation by player part
    private void PlayAnimation(string animationClip, BodyPart bodyPart)
    {
        if (bodyPart == BodyPart.left_hand)
        {
            leftHandAnimator.Play(animationClip, 0, 0f);
            print($"left hand animation {animationClip} started!");
        }
        else if (bodyPart == BodyPart.right_hand)
        {
            rightHandAnimator.Play(animationClip, 0, 0f);
            print($"right hand animation {animationClip} started!");
        }
        else if (bodyPart == BodyPart.right_leg)
        {
            rightLegAnimator.Play(animationClip, 0, 0f);
            print($"right leg animation {animationClip} started!");
        }
        else if (bodyPart == BodyPart.left_leg)
        {
            leftLegAnimator.Play(animationClip, 0, 0f);
            print($"left leg animation {animationClip} started!");
        }
    }

    //Play animation with loop
    private void PlayAnimation(string animationClip, BodyPart bodyPart, bool loop)
    {
        //If animation looped activate loop with animation
        if (loop)
        {
            //Disable if same animation cycle activated for same bodypart
            AnimationState state = (AnimationState)Enum.Parse(typeof(AnimationState), animationClip);
            if (OnAnimationLoop[(int)bodyPart] != null && currentAnimStates[bodyPart] == state) return;
            //Start loop of clip
            var animationClips = leftHandAnimator.runtimeAnimatorController.animationClips;
            float animationTime = 0f;
            foreach (var clip in animationClips)
            {
                if (clip.name == animationClip)
                {
                    animationTime = clip.averageDuration;
                    print($"Average {clip.name} clip time: {animationTime}");
                }
            }
            OnAnimationLoop[(int)bodyPart] = StartCoroutine(AnimationLoop(animationClip, bodyPart, animationTime));
        }
        //If animation not looped deactivate current loop
        else
        {
            if (OnAnimationLoop[(int)bodyPart] != null)
            {
                StopCoroutine(OnAnimationLoop[(int)bodyPart]);
                OnAnimationLoop[(int)bodyPart] = null;
            }
            OnAnimationLoop[(int)bodyPart] = null;
            PlayAnimation(animationClip, bodyPart);
        }
    }


    // Handle animation loop automaticlly
    IEnumerator AnimationLoop(string animationClip, BodyPart bodyPart, float waitTime = 1f)
    {
        while (true)
        {
            print($"Starting animation {animationClip}");
            PlayAnimation(animationClip, bodyPart);
            yield return new WaitForSeconds(waitTime);
        }
    }


    //Bodyparts visualisation control
    public void ChangeVisibility(BodyPart bodyPart, bool state)
    {
        switch (bodyPart)
        {
            case BodyPart.left_hand:
                leftHandAnimator.gameObject.SetActive(state);
                break;
            case BodyPart.right_hand:
                rightHandAnimator.gameObject.SetActive(state);
                break;
            case BodyPart.right_leg:
                rightLegAnimator.gameObject.SetActive(state);
                break;
            case BodyPart.left_leg:
                leftLegAnimator.gameObject.SetActive(state);
                break;
        }
    }


    // public override void Play(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false)
    // {}
    // public override void Enqueue(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false)
    // {}
    // public override void StopAllAnimations(EntityAnimator entity, string animName, string bodyPart = null)
    // {}

    //Interface realization for weapon controller
    public override void Play(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false)
    {
        BodyPart part = BodyPart.left_hand;
        if (bodyPart == "right_hand") part = BodyPart.right_hand;
        if (bodyPart == "right_leg") part = BodyPart.right_leg;
        //ChangeAnimation(animName, part, loop);
        
        int index = (int)part;
        
        animationQueues[index].Clear();
        animationQueues[index].Enqueue(new QueuedAnim(animName, loop));
        PlayNextInQueue(part);
    }
    public override void Enqueue(EntityAnimator entity, string animName, string bodyPart = null, bool loop = false)
    {
        BodyPart part = BodyPart.left_hand;
        if (bodyPart == "right_hand") part = BodyPart.right_hand;
        if (bodyPart == "right_leg") part = BodyPart.right_leg;

        int i = (int)part;
        animationQueues[i].Enqueue(new QueuedAnim(animName, loop));
    }
    
    private void PlayNextInQueue(BodyPart part)
    {
        int i = (int)part;

        if (animationQueues[i].Count == 0)
        {
            queueData[i].isPlaying = false;
            return;
        }

        var anim = animationQueues[i].Dequeue();

        // если loop — отдаем управление твоей системе loop-а
        if (anim.loop)
        {
            ChangeAnimation(anim.name, part, true);
            queueData[i].isPlaying = false; // queue закончил работу — loop работает сам по себе
            return;
        }

        // non-loop
        ChangeAnimation(anim.name, part, false);

        float clipTime = GetClipLength(anim.name, part);
        queueData[i].isPlaying = true;

        queueData[i].coroutine = StartCoroutine(WaitThenNext(part, clipTime));
    }
    
    private IEnumerator WaitThenNext(BodyPart part, float wait)
    {
        yield return new WaitForSeconds(wait);
        PlayNextInQueue(part);
    }
    
    private float GetClipLength(string clipName, BodyPart part)
    {
        Animator anim = leftHandAnimator;

        if (part == BodyPart.right_hand)
            anim = rightHandAnimator;
        else if (part == BodyPart.right_leg)
            anim = rightLegAnimator;
        else if (part == BodyPart.left_leg)
            anim = leftLegAnimator;

        foreach (var clip in anim.runtimeAnimatorController.animationClips)
            if (clip.name == clipName)
                return clip.length;

        return 0.1f; // на случай отсутствия
    }

    


    //Stop all animations in the animatorn and queues
    public override void StopAllAnimations(EntityAnimator entity, string fallbackState = null, string bodyPart = null)
    {
        BodyPart[] parts;

        if (bodyPart == null)
            parts = new[] { BodyPart.left_hand, BodyPart.right_hand, BodyPart.right_leg };
        else
            parts = new[] { (BodyPart)Enum.Parse(typeof(BodyPart), bodyPart) };

        foreach (var p in parts)
        {
            int i = (int)p;

            // стоп coroutine очереди
            if (queueData[i].coroutine != null)
            {
                StopCoroutine(queueData[i].coroutine);
                queueData[i].coroutine = null;
            }

            queueData[i].isPlaying = false;

            // очистка очереди
            animationQueues[i].Clear();

            // стоп loop, если был
            if (OnAnimationLoop[i] != null)
            {
                StopCoroutine(OnAnimationLoop[i]);
                OnAnimationLoop[i] = null;
            }

            // fallback
            if (fallbackState != null)
                ChangeAnimation(fallbackState, p);
        }
    }
}
