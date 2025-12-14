using UnityEngine;
using System;
using System.Collections.Generic;

public interface IDefender
{
    bool IsBlocking { get; }
}

public class WeaponManager : MonoBehaviour, IAttackable, IDefender
{
    public WeaponBase[] weapons;
    public WeaponBase leg;
    private int currentWeaponIndex = 0;
    public WeaponBase CurrentWeapon => weapons.Length > 0 ? weapons[currentWeaponIndex] : null;

    //References
    [SerializeField] private Transform[] commandSendersObjects;
    private IWeaponCommand[] commandSenders;


    //Animation handling
    public Action<string, bool> OnAnimateEvent;

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
    }

    void OnDisable()
    {
        foreach(var ws in commandSenders)
            if(ws != null)
                ws.OnWeaponCommand -= HandleCommand;
    }

    // Switch weapons
    public void SwitchWeapon(int index)
    {
        if (index < 0 || index >= weapons.Length)
            return;

        currentWeaponIndex = index;
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
    public void OnIdle() => CurrentWeapon?.Idle();
    public void OnShowOff() => CurrentWeapon?.ShowOff();



    //Handle action of major script
    public void HandleCommand(string command)
    {
        switch(command)
        {
            case "left_attack":
                OnAttackPressed(0);
            break;
            case "right_attack":
                OnAttackPressed(1);
            break;

            case "left_attack_start":
                OnAttackHeld(0);
            break;
            case "right_attack_start":
                OnAttackHeld(1);
            break;
            case "left_attack_continue":
            break;
            case "right_attack_continue":
            break;
            case "left_attack_end":
                OnAttackReleased(0);
            break;
            case "right_attack_end":
                OnAttackReleased(1);
            break;
            case "left_attack_cancel":
                OnAttackCancelled();
            break;
            case "right_attack_cancel":
                OnAttackCancelled();
            break;

            case "block_start":
                OnBlockPressed();
            break;
            case "block_continue":
            break;
            case "block_action":
                OnBlockAction();
            break;
            case "block_break":
                OnBlockBreak();
            break;
            case "block_end":
                OnBlockReleased();
            break;

            case "melee_hit":
                OnLegHit();
            break;
            case "idle":
                OnIdle();
            break;
            case "put_away":
                OnPutAway();
            break;
            case "show_off":
                OnShowOff();
            break;
        }
    }

}