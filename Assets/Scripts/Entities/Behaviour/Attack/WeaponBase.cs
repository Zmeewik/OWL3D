using UnityEngine;
using System;


public abstract class WeaponBase : MonoBehaviour
{
    public AttackVariant[] attacks;


    //Animation handle
    public Action<string> OnAnimation;

    private bool[] held;
    private float[] chargeTime;

    protected float lastAttackTime;

    void Awake()
    {
        held = new bool[attacks.Length];
        chargeTime = new float[attacks.Length];
    }


    // Attack handle
    public bool CanAttack(AttackVariant attack)
        => Time.time >= lastAttackTime + attack.cooldown;

    public void HandleInput(int index, AttackInputType type)
    {
        print(type);
        var attack = attacks[index];
        if (attack == null) return;
        
        switch(type)
        {
            case AttackInputType.Pressed:
                OnPressed(index, attack);
                break;

            case AttackInputType.Held:
                attack = attacks[index+3];
                OnHeld(index + 3, attack);
                break;

            case AttackInputType.Released:
                attack = attacks[index+3];
                OnReleased(index + 3, attack);
                break;
        }
    }

    private void OnPressed(int index, AttackVariant attack)
    {
        held[index] = true;
        chargeTime[index] = 0f;

        if (!attack.chargeable)
        {
            TryDoAttack(attack, index);
        }
    }

    private void OnHeld(int index, AttackVariant attack)
    {
        if (held[index] || !attack.chargeable)
            return;
        
        held[index] = true;
        chargeTime[index] = Time.time;
    
        OnAnimationCall(index - 3 == 0 ? "left_attack_start" : "right_attack_start");
    }

    private void OnReleased(int index, AttackVariant attack)
    {
        if (!held[index])
            return;

        held[index] = false;
        chargeTime[index] = Time.time - chargeTime[index];

        if (attack.chargeable)
        {
            OnAnimationCall(index - 3 == 0 ? "left_attack_end" : "right_attack_end");
            float finalCharge = Mathf.Clamp(chargeTime[index], 0, attack.maxChargeTime);
            print(finalCharge);
            ExecuteAttack(attack, finalCharge);
        }
    }

    public void OnAttackCancelled()
    {
        for(int y = 0; y < held.Length; y++)
            held[y] = false;
    }

    private void TryDoAttack(AttackVariant attack, int index)
    {
        if (!CanAttack(attack))
            return;

        lastAttackTime = Time.time;
        OnAnimationCall(index == 0 ? "left_attack" : "right_attack");
        ExecuteAttack(attack);
    }


    // Block handle
    public void OnBlockPressed()
    {
        OnAnimationCall("block_start");
    }
    public void OnBlockReleased()
    {
        OnAnimationCall("block_end");
    }
    public void OnBlockBreak()
    {
        OnAnimationCall("block_break");
    }
    public void OnBlockAction()
    {
        OnAnimationCall("block_action");
    }

    public void ShowOff()
    {
        OnAnimationCall("show_off");
    }

    public void Idle()
    {
        OnAnimationCall("idle");
    }

    public void PutAway()
    {
        OnAnimationCall("put_away");
    }


    void OnAnimationCall(string anim)
    {
        print("Start animation: " + anim);
        OnAnimation?.Invoke(anim);
    }


    protected abstract void ExecuteAttack(AttackVariant attack, float charged = -1);
}