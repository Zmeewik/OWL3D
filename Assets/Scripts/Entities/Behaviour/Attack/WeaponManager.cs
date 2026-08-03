using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public interface IDefender
{
    bool IsBlocking { get; }
}

public class WeaponManager : MonoBehaviour, IAttackable, IDefender, IButtonClick
{
    [Header("Settings")] [SerializeField] private float timeToChange;
    [Header("Weapons")]
    public WeaponBase[] weapons;
    public WeaponBase leg;
    private int currentWeaponIndex = 0;
    public WeaponBase CurrentWeapon => weapons.Length > 0 ? weapons[currentWeaponIndex] : null;

    //References
    [Header("References")]
    [SerializeField] private Transform[] commandSendersObjects;
    [SerializeField] private WeaponWheelUI weaponWheelUI;
    private IWeaponCommand[] commandSenders;
    
    public bool IsBlocking { get; private set; }


    void Awake()
    {
        commandSenders = new IWeaponCommand[commandSendersObjects.Length];
        int r = 0;
        foreach(var obj in commandSendersObjects)
        {
            var scr = obj.GetComponent<IWeaponCommand>();
            commandSenders[r] = scr;
        }
        foreach(var ws in commandSenders)
            if(ws != null)
                ws.OnWeaponCommand += HandleCommand;
        weaponWheelUI.OnWeaponChange += SwitchWeapon;
        weaponWheelUI.lastWeapon = CurrentWeapon.name;
        PlayerMovement.OnPlayAnimationLArm += OnMovementAnimation;
    }

    void OnDisable()
    {
        foreach(var ws in commandSenders)
            if(ws != null)
                ws.OnWeaponCommand -= HandleCommand;
        weaponWheelUI.OnWeaponChange -= SwitchWeapon;
    }

    // Switch weapons
    private void SwitchWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length)
            return;
        CurrentWeapon.PutAway();
        currentWeaponIndex = index;
        StartCoroutine(PickUpWeaponAtTime(CurrentWeapon));
    }

    IEnumerator PickUpWeaponAtTime(WeaponBase lastWeapon)
    {
        yield return new WaitForSeconds(timeToChange);
        CurrentWeapon.PickUp();
    }

    private void SwitchWeapon(string weaponName)
    {
        if (weaponName == "")
            return;
        
        var id = 0;
        foreach (var weapon in weapons)
        {
            if (weapon.name == weaponName)
            {
                print(weapon.name + " is selected!");
                SwitchWeapon(id);
                break;
            }

            id++;
        }
    }

    // Input forwarding
    public void OnAttackPressed(int attackIndex)
    {
        CurrentWeapon?.HandleInput(attackIndex, AttackInputType.Pressed);
    }

    public void OnAttackHeld(int attackIndex)
    {
        if(IsBlocking)
            OnBlockCancelled();
        CurrentWeapon?.HandleInput(attackIndex, AttackInputType.Held);
    }

    public void OnAttackReleased(int attackIndex)
        => CurrentWeapon?.HandleInput(attackIndex, AttackInputType.Released);
    public void OnAttackCancelled()
        => CurrentWeapon?.OnAttackCancelled();
    
    public void PressButton(string buttonName, bool buttonState)
    {
        //print(buttonName + " pressed");
        CurrentWeapon?.PressButton(buttonName, buttonState);
    }

    // Block actions
    public void OnBlockPressed()
    {
        IsBlocking = true;
        OnAttackCancelled();
        CurrentWeapon?.OnBlockPressed();
    }

    public void OnBlockReleased()
    {
        if(!IsBlocking) return;
        IsBlocking = false;
        CurrentWeapon?.OnBlockReleased();
    }

    public void OnBlockCancelled()
    {
        if(!IsBlocking) return;
        IsBlocking = false;
    }

    public void OnBlockBreak()
    {
        if(!IsBlocking) return;
        IsBlocking = false;
        CurrentWeapon?.OnBlockBreak();
    }

    public void OnBlockAction() 
        => CurrentWeapon?.OnBlockAction();

    //Animations only
    public void OnLegHit() => leg?.HandleInput(0, AttackInputType.Pressed);
    public void OnPutAway() => CurrentWeapon?.PutAway();
    public void HideWeapon() => CurrentWeapon?.HideWeapon();
    public void OnIdle() => CurrentWeapon?.Idle();
    public void OnShowOff() => CurrentWeapon?.ShowOff();



    //Handle action of major script
    public void HandleCommand(WeaponCommand command)
    {
        switch(command)
        {
            case WeaponCommand.LeftAttack:
                OnAttackPressed(0);
            break;
            case WeaponCommand.RightAttack:
                OnAttackPressed(1);
            break;

            case WeaponCommand.LeftAttackStart:
                OnAttackHeld(0);
            break;
            case WeaponCommand.RightAttackStart:
                OnAttackHeld(1);
            break;
            case WeaponCommand.LeftAttackContinue:
            break;
            case WeaponCommand.RightAttackContinue:
            break;
            case WeaponCommand.LeftAttackEnd:
                OnAttackReleased(0);
            break;
            case WeaponCommand.RightAttackEnd:
                OnAttackReleased(1);
            break;
            case WeaponCommand.LeftAttackCancel:
                OnAttackCancelled();
            break;
            case WeaponCommand.RightAttackCancel:
                OnAttackCancelled();
            break;

            case WeaponCommand.BlockStart:
                OnBlockPressed();
            break;
            case WeaponCommand.BlockContinue:
            break;
            case WeaponCommand.BlockAction:
                OnBlockAction();
            break;
            case WeaponCommand.BlockBreak:
                OnBlockBreak();
            break;
            case WeaponCommand.BlockEnd:
                OnBlockReleased();
            break;

            case WeaponCommand.MeleeHit:
                OnLegHit();
            break;
            case WeaponCommand.Idle:
                OnIdle();
            break;
            case WeaponCommand.PutAway:
                OnPutAway();
            break;
            case WeaponCommand.ShowOff:
                OnShowOff();
            break;
        }
    }
    
    // Moves at players animations
    void OnMovementAnimation(string animationName, bool loop)
    {
        if (animationName == "PutAwayAnimation")
            CurrentWeapon?.HideWeapon();
        if (animationName == "H_Arms_GetUp")
            CurrentWeapon?.HideWeapon();
    }
}