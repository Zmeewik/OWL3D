using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;

public class HeroAnimationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Animator leftHandAnimator;
    [SerializeField] Animator rightHandAnimator;
    [SerializeField] Animator LegAnimator;
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
                                H_Arms_Boxing_HitChargeContinue2,
                                H_Arms_Boxing_HitChargeStart1,
                                H_Arms_Boxing_HitChargeStart2,
                                H_Arms_Boxing_HitChargeEnd1,
                                H_Arms_Boxing_HitChargeEnd2,
                                
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
                                H_Arms_Hide,

                                //Parkour
                                H_Arms_Get_Up
    }
    static string[] animationNames = {
                                //Charge boxing hit
                                "H_Arms_Boxing_HitChargeContinue1",
                                "H_Arms_Boxing_HitChargeContinue2",
                                "H_Arms_Boxing_HitChargeStart1",
                                "H_Arms_Boxing_HitChargeStart2",
                                "H_Arms_Boxing_HitChargeEnd1",
                                "H_Arms_Boxing_HitChargeEnd2",
                                
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
                                "H_Arms_Hide",

                                //Parkour
                                "H_Arms_GetUp",
                                };
    public enum BodyPart { left_hand, right_hand, right_leg }
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

    private void PlayAnimation(string animationClip, BodyPart bodyPart)
    {
        if (bodyPart == BodyPart.left_hand)
        {
            leftHandAnimator.Play(animationClip);
            print($"left hand animation {animationClip} started!");
        }
        else if (bodyPart == BodyPart.right_hand)
        {
            rightHandAnimator.Play(animationClip);
            print($"right hand animation {animationClip} started!");
        }
        else if (bodyPart == BodyPart.right_leg)
        {
            LegAnimator.Play(animationClip);
            print($"right leg animation {animationClip} started!");
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
            StopCoroutine(OnAnimationLoop[(int)bodyPart]);
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
                LegAnimator.gameObject.SetActive(state);
                break;
        }
    }
}
