using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityAttackAnimation : MonoBehaviour
{
    [Serializable]
    class AnimationNamed
    {
        public string name;
        public string[] animations;
        public int[] bodyPartsIntended;
        public int[] weaponIntended;
        public AnimationNamed(string name) {this.name = name;}
    }

    [Serializable]
    class BodyPartToAnimate
    {
        public string name;
        public IAnimation animator;
        public EntityAnimator entityAnimator;
    }

    [Header("All animations")]
    [SerializeField] GameObject[] animationSenders;
    [SerializeField] BodyPartToAnimate[] bodyPartToAnimates;
    [SerializeField] BodyPartToAnimate[] weaponsToAnimate;

    [SerializeField] 
    private AnimationNamed[] animationList = new AnimationNamed[]{
        new AnimationNamed("left_attack"),
        new AnimationNamed("left_attack_start"),
        new AnimationNamed("left_attack_continue"),
        new AnimationNamed("left_attack_end"),
        new AnimationNamed("left_attack_false"),
        
        new AnimationNamed("right_attack"),
        new AnimationNamed("right_attack_start"),
        new AnimationNamed("right_attack_continue"),
        new AnimationNamed("right_attack_end"),
        
        new AnimationNamed("middle_attack"),
        new AnimationNamed("middle_attack_start"),
        new AnimationNamed("middle_attack_continue"),
        new AnimationNamed("middle_attack_end"),

        new AnimationNamed("block_start"),
        new AnimationNamed("block_continue"),
        new AnimationNamed("block_action"),
        new AnimationNamed("block_end"),
        new AnimationNamed("block_break"),

        new AnimationNamed("melee_hit"),
        new AnimationNamed("show_off"),
        new AnimationNamed("idle"),
        new AnimationNamed("put_away"),
        new AnimationNamed("pick_up"),
    };

    //Save value for synchronize multipart animations in continuous
    int animIndex = -1;
    private Dictionary<string, int> animationIndexes = new Dictionary<string, int>();
    string lastAnimState = "";
    //Arms in different arms
    int armSynchronize = -1;

    private void Start()
    {
        //Subscribe to events
        foreach (var objectSender in animationSenders)
        {
            var animSender = objectSender.GetComponent<IAnimationSender>();
            animSender.OnAnimateCommand += HandleAnimations;
        }
    }

    private void OnDisable()
    {
        foreach (var objectSender in animationSenders)
        {
            var animSender = objectSender.GetComponent<IAnimationSender>();
            animSender.OnAnimateCommand -= HandleAnimations;
        }
    }

    // Main animation handle
    // Main animation handle
    private void HandleAnimations(string animName, bool loop = false, float time = 0)
    {
        
        // Check for animation availability
        if (!FindAnimation(animName, out var found))
            return;

        int randomIndex = -1;

        // One random variant for all simple animations
        if (!animName.EndsWith("_start") &&
            !animName.EndsWith("_continue") &&
            !animName.EndsWith("_end"))
        {
            randomIndex = UnityEngine.Random.Range(0, found.animations.Length);
        }

        // One random variant for start/continue/end
        if (animName.EndsWith("_start"))
        {
            if (!animationIndexes.ContainsKey(found.name))
                animationIndexes[found.name] = UnityEngine.Random.Range(0, found.animations.Length);
        }



        // BODY PARTS
        if (found.bodyPartsIntended != null)
        {
            foreach (int index in found.bodyPartsIntended)
            {
                if (index < 0 || index >= bodyPartToAnimates.Length)
                    continue;

                var part = bodyPartToAnimates[index];

                if (part.animator == null)
                    continue;

                if (animName.EndsWith("_start"))
                {
                    int idx = animationIndexes[found.name];

                    part.animator.Play(part.entityAnimator, found.animations[idx], part.name);

                    var continueAnimation = animName.Replace("_start", "_continue");
                    if (!FindAnimation(continueAnimation, out var foundContinue))
                        return;

                    part.animator.Enqueue(
                        part.entityAnimator,
                        foundContinue.animations[idx],
                        part.name,
                        loop: true);
                }
                else if (animName.EndsWith("_end"))
                {
                    int idx;
                    if (!animationIndexes.TryGetValue(found.name.Replace("_end", "_start"), out idx))
                        idx = 0;

                    part.animator.Play(part.entityAnimator, found.animations[idx], part.name);
                    
                    // Animate idle after animation
                    /*if (!FindAnimation("idle", out var foundIdle))
                        return;
                    part.animator.Enqueue(part.entityAnimator, foundIdle.animations[0], part.name, loop:true);*/
                }
                else
                {
                    part.animator.Play(
                        part.entityAnimator,
                        found.animations[randomIndex],
                        part.name);
                    
                    // Animate idle after animation
                    /*if (!animName.Contains("put_away"))
                    {
                        // Animate idle after animation
                        if (!FindAnimation("idle", out var foundIdle))
                            return;
                        part.animator.Enqueue(part.entityAnimator, foundIdle.animations[0], part.name, loop:true);
                    }*/
                }
            }
        }

        // WEAPONS
        if (found.weaponIntended.Length != 0)
        {
            foreach (int index in found.weaponIntended)
            {
                if (index < 0 || index >= weaponsToAnimate.Length)
                    continue;

                var weap = weaponsToAnimate[index];

                if (weap.animator == null)
                    continue;

                if (animName.EndsWith("_start"))
                {
                    int idx = animationIndexes[found.name];

                    weap.animator.Play(weap.entityAnimator, found.animations[idx]);

                    var continueAnimation = animName.Replace("_start", "_continue");
                    if (!FindAnimation(continueAnimation, out var foundContinue))
                        return;

                    weap.animator.Enqueue(
                        weap.entityAnimator,
                        foundContinue.animations[idx],
                        loop: true);
                }
                else if (animName.EndsWith("_end"))
                {
                    int idx;
                    if (!animationIndexes.TryGetValue(found.name.Replace("_end", "_start"), out idx))
                        idx = 0;

                    weap.animator.Play(weap.entityAnimator, found.animations[idx]);

                    if (index == found.weaponIntended.Length - 1)
                        animationIndexes.Remove(found.name.Replace("_end", "_start"));
                    
                    // Animate idle after animation
                    /*if (!FindAnimation("idle", out var foundIdle))
                        return;
                    weap.animator.Enqueue(weap.entityAnimator, foundIdle.animations[0], weap.name, loop:true);*/
                }
                else
                {
                    weap.animator.Play(
                        weap.entityAnimator,
                        found.animations[randomIndex]);
                    
                    // Animate idle after animation
                    /*if (!animName.Contains("put_away"))
                    {
                        // Animate idle after animation
                        if (!FindAnimation("idle", out var foundIdle))
                            return;
                        weap.animator.Enqueue(weap.entityAnimator, foundIdle.animations[0], weap.name, loop:true);
                    }*/
                }
            }
        }
    }


    private bool FindAnimation(string animName, out AnimationNamed found)
    {
        // Find animation by name
        found = Array.Find(animationList, a => a.name == animName);
        Debug.Log($"{gameObject.name}: {animName} -> {found?.name}");
        if (found == null)
        {
            Debug.LogWarning($"[EntityAttackAnimation] Animation '{animName}' not found.");
            return false;
        }

        // Check does animation has clips
        if (found.animations == null || found.animations.Length == 0)
        {
            Debug.LogWarning($"[EntityAttackAnimation] Animation '{animName}' has no clips.");
            return false;
        }

        return true;
    }

}
