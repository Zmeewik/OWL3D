using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntityAttckAnimation : MonoBehaviour
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
    [SerializeField] WeaponBase weaponBase;
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

        new AnimationNamed("block_start"),
        new AnimationNamed("block_continue"),
        new AnimationNamed("block_action"),
        new AnimationNamed("block_end"),
        new AnimationNamed("block_break"),

        new AnimationNamed("melee_hit"),
        new AnimationNamed("show_off"),
        new AnimationNamed("idle"),
        new AnimationNamed("put_away"),
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
        if(weaponBase != null)
            weaponBase.OnAnimation += HandleAnimations;
    }

    // Main animation handle
    private void HandleAnimations(string animName)
    {
        //Check for animation avialability
        if(!FindAnimation(animName, out var found))
            return;
        
        print(found.name);

        armSynchronize = -1;
        // Start animation at all body parts
        if (found.bodyPartsIntended != null)
        {
            foreach (int index in found.bodyPartsIntended)
            {
                if (index < 0 || index >= bodyPartToAnimates.Length)
                    continue;

                var part = bodyPartToAnimates[index];

                if (part.animator != null)
                {
                    //Start folowing animation if holding
                    //Realize start and continue of long animation
                    if(animName.EndsWith("_start"))
                    {
                        if (!animationIndexes.ContainsKey(found.name))
                            animationIndexes[found.name] = UnityEngine.Random.Range(0, found.animations.Length);

                        int index_anim = animationIndexes[found.name];
                        
                        // start animation
                        var animationClip = found.animations[index_anim];
                        part.animator.Play(part.entityAnimator, animationClip, part.name);

                        // Continue animation
                        var continueAnimation = animName.Replace("_start", "_continue");
                        if (!FindAnimation(continueAnimation, out var new_found))
                            return;

                        var animationClipContinue = new_found.animations[index_anim];
                        part.animator.Enqueue(part.entityAnimator, animationClipContinue, part.name, loop: true);
                    }
                    //Realize fixate ending of long animation
                    else if(animName.EndsWith("_end"))
                    {
                        int index_anim;
                        if (!animationIndexes.TryGetValue(found.name.Replace("_end", "_start"), out index_anim))
                            index_anim = 0;

                        var animationClip = found.animations[index_anim];
                        part.animator.Play(part.entityAnimator, animationClip, part.name);

                        //Clearing index
                        animationIndexes.Remove(found.name.Replace("_end", "_start"));
                    }
                    //Simple play of the animation
                    else
                    {
                        var selectedIndex  = UnityEngine.Random.Range(0, found.animations.Length);
                        if(armSynchronize != -1)
                            selectedIndex  = armSynchronize;
                        else
                            armSynchronize = selectedIndex ;
                        var animationClip = found.animations[selectedIndex];
                        print(selectedIndex);
                        print(animationClip);
                        part.animator.Play(part.entityAnimator, animationClip, part.name);
                    }
                }

                
            }
        }

        // Start animation at all weapons
        if (found.weaponIntended != null)
        {
            foreach (int index in found.weaponIntended)
            {
                if (index < 0 || index >= weaponsToAnimate.Length)
                    continue;

                var weap = weaponsToAnimate[index];

                if (weap.animator != null)
                {
                    // Start folowing animation if holding
                    if(animName.EndsWith("_start"))
                    {
                        // get or assign index for this animation
                        if (!animationIndexes.ContainsKey(found.name))
                            animationIndexes[found.name] = UnityEngine.Random.Range(0, found.animations.Length);
                        int idx = animationIndexes[found.name];
                        var clipStart = found.animations[idx];

                        // play start
                        weap.animator.Play(weap.entityAnimator, clipStart);
                        // play continue
                        var continueAnim = animName.Replace("_start", "_continue");
                        if (!FindAnimation(continueAnim, out var foundContinue))
                            return;
                        var clipContinue = foundContinue.animations[idx];
                        weap.animator.Enqueue(weap.entityAnimator, clipContinue, loop: true);
                    }
                    else if(animName.EndsWith("_end"))
                    {
                        int idx;
                        if (!animationIndexes.TryGetValue(found.name.Replace("_end", "_start"), out idx))
                            idx = 0;
                        var clipEnd = found.animations[idx];
                        
                        weap.animator.Play(weap.entityAnimator, clipEnd);
                        if (index == found.weaponIntended.Length - 1)
                        {
                            animationIndexes.Remove(found.name.Replace("_end", "_start"));
                        }
                    }
                    else
                    {
                        var selectedIndex = UnityEngine.Random.Range(0, found.animations.Length);
                        
                        var animationClip = found.animations[selectedIndex];
                        weap.animator.Play(weap.entityAnimator, animationClip);
                    }
                }

                
            }
        }
    }


    private bool FindAnimation(string animName, out AnimationNamed found)
    {
        // Find animation by name
        found = Array.Find(animationList, a => a.name == animName);
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
