using UnityEngine;
using System;


public abstract class WeaponBase : MonoBehaviour, IAnimationSender
{
    public AttackVariant[] attacks;
    public WeaponAnimationController[] weaponAnimationController;
    public Transform owner;
    
    // Weapon actions
    public Action<string, float[]> OnWeaponAction;

    //Animation handle
    public Action<string> OnAnimation;

    private bool isWeaponHided;
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
        if (isWeaponHided)
        {
            PickUp();
            return;
        }
        
        print(type + " attack");
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

        var animation = "";
        switch (index - 3)
        {
            case 0:
                animation = AnimateCommand.LeftAttackStart;
                break;
            case 1:
                animation = AnimateCommand.RightAttackStart;
                break;
            case 2:
                animation = AnimateCommand.MiddleAttackStart;
                break;
        }

        OnAnimationCall(animation);
    }

    private void OnReleased(int index, AttackVariant attack)
    {
        if (!held[index])
            return;

        held[index] = false;
        chargeTime[index] = Time.time - chargeTime[index];

        if (attack.chargeable)
        {
            var animation = "";
            switch (index - 3)
            {
                case 0:
                    animation = AnimateCommand.LeftAttackEnd;
                    break;
                case 1:
                    animation = AnimateCommand.RightAttackEnd;
                    break;
                case 2:
                    animation = AnimateCommand.MiddleAttackEnd;
                    break;
            }

            OnAnimationCall(animation);
            float finalCharge = Mathf.Clamp(chargeTime[index], 0, attack.maxChargeTime);
            //print(finalCharge);
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
        var animation = "";
        switch (index)
        {
            case 0:
                animation = AnimateCommand.LeftAttack;
                break;
            case 1:
                animation = AnimateCommand.RightAttack;
                break;
            case 2:
                animation = AnimateCommand.MiddleAttack;
                break;
        }
        OnAnimationCall(animation);
        ExecuteAttack(attack);
    }


    // Block handle
    public void OnBlockPressed()
    {
        if (isWeaponHided)
        {
            PickUp();
            return;
        }
        OnAnimationCall(AnimateCommand.BlockStart);
    }
    public void OnBlockReleased()
    {
        OnAnimationCall(AnimateCommand.BlockEnd);
    }
    public void OnBlockBreak()
    {
        OnAnimationCall(AnimateCommand.BlockBreak);
    }
    public void OnBlockAction()
    {
        OnAnimationCall(AnimateCommand.BlockAction);
    }

    public void ShowOff()
    {
        if (isWeaponHided)
        {
            PickUp();
            return;
        }
        OnAnimationCall(AnimateCommand.ShowOff);
    }

    public void Idle()
    {
        OnAnimationCall(AnimateCommand.Idle);
    }

    public void PutAway()
    {
        if (!isWeaponHided)
        {
            isWeaponHided = true;
            OnAnimationCall(AnimateCommand.PutAway);
        }
    }

    public void HideWeapon()
    {
        if (!isWeaponHided)
        {
            isWeaponHided = true;
            OnAnimationCall(AnimateCommand.PutAway);
            foreach (var weapon in weaponAnimationController)
            {
                weapon.ChangeVisibility(false);
            }

        }
    }


    public void PickUp()
    {
        isWeaponHided = false;
        foreach (var weapon in weaponAnimationController)
        {
            weapon.ChangeVisibility(true);
        }

        OnAnimationCall(AnimateCommand.PickUp);
    }


    void OnAnimationCall(string anim)
    {
        //OnAnimation?.Invoke(anim);
        OnWeaponAction?.Invoke(anim, new []{ 0f });
        OnAnimateCommand?.Invoke(anim, false, 1);
    }


    protected abstract void ExecuteAttack(AttackVariant attack, float charged = -1);
    public void PressButton(string buttonName, bool buttonState)
    {
        if(buttonName == "ShowOff" && buttonState)
            ShowOff();
        if(buttonName == "HideWeapon" && buttonState)
            PutAway();
    }

    public Action<string, bool, float> OnAnimateCommand { get; set; }
}