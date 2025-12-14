using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Input : MonoBehaviour
{

    [Header("References")]
    [SerializeField] List<GameObject> move_goal;
    List<IMovable> movable_goal = new List<IMovable>();
    [SerializeField] List<GameObject> rotate_goal;
    List<IRotatable> rotatable_goal = new List<IRotatable>();
    [SerializeField] List<GameObject> attack_goal;
    private List<IAttackable> attackable_goal = new List<IAttackable>();

    //Chnage goal of moving
    void Awake()
    {
        foreach (var goal in move_goal)
            if (goal.TryGetComponent(out IMovable m)) movable_goal.Add(m);

        foreach (var goal in rotate_goal)
            if (goal.TryGetComponent(out IRotatable r)) rotatable_goal.Add(r);

        foreach (var goal in attack_goal)
            if (goal.TryGetComponent(out IAttackable a)) attackable_goal.Add(a);
    }

    //Work with goals for interacting
    public void ClearMoveGoals()
    {
        move_goal.Clear();
    }

    public void ClearRotateGoals()
    {
        rotatable_goal.Clear();
    }

    //Input in gaming process
    //Movement
    public void OnMove(InputAction.CallbackContext context)
    {
        foreach(var goal in movable_goal)
        {
            goal.OnMove(context.ReadValue<Vector2>());
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if(context.performed)
            foreach(var goal in movable_goal)
            {
                goal.OnJump();
            }
    }

    public void OnLook(InputAction.CallbackContext context)
    {
        if(context.performed || context.canceled)
        {
            foreach(var goal in rotatable_goal)
            {
                goal.DeltaRotation(context.ReadValue<Vector2>());
            }
        }
    }


    public void OnDash(InputAction.CallbackContext context)
    {
        if(context.performed)
            foreach(var goal in movable_goal)
            {
                goal.OnDash();
            }
    }

    public void OnCrouch(InputAction.CallbackContext context)
    {
                
        if(context.canceled)
        {
            foreach(var goal in movable_goal)
            {
                goal.OnCrouch(false);
            }
        }
        else if(context.started)
        {
            foreach(var goal in movable_goal)
            {
                goal.OnCrouch(true);
            }
        }
    }


    // Attack
    // M0
    public void OnAttackPrimary(InputAction.CallbackContext context)
    {
        if (context.performed)
            foreach (var target in attackable_goal)
                target.OnAttackPressed(0);
    }
    // M1
    public void OnAttackSecondary(InputAction.CallbackContext context)
    {
        if (context.performed)
            foreach (var target in attackable_goal)
                target.OnAttackPressed(1);


    }
    // M3
    public void OnWheelClick(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            foreach (var target in attackable_goal)
                target.OnAttackPressed(3);
        }

        if (context.canceled)
            foreach (var target in attackable_goal)
                target.OnAttackReleased(3);
    }


    // M0 Hold
    bool[] hold = new bool[3];
    public void OnAttackPrimaryHold(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            foreach (var target in attackable_goal)
            {
                target.OnAttackHeld(0);
                hold[0] = true;
            }
        }

        if (context.canceled)
        {
            foreach (var target in attackable_goal)
            {
                if (hold[0])
                {
                    target.OnAttackReleased(0);
                    hold[0] = false;
                }
            }
        }
    }
    // M1 Hold
    public void OnAttackSecondaryHold(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            foreach (var target in attackable_goal)
            {
                target.OnAttackHeld(1);
                hold[1] = true;
            }
        }

        if (context.canceled)
        {
            foreach (var target in attackable_goal)
            {
                if (hold[1])
                {
                    target.OnAttackReleased(1);
                    hold[1] = false;
                }
            }
        }
    }

    // Kick
    public void OnAttackKick(InputAction.CallbackContext context)
    {
        if (context.started)
            foreach (var target in attackable_goal)
                target.OnLegHit();
    }

    // Block
    public void OnAttackBlock(InputAction.CallbackContext context)
    {
        if (context.started)
            foreach (var target in attackable_goal)
                target.OnBlockPressed();
        
        if (context.canceled)
            foreach (var target in attackable_goal)
                target.OnBlockReleased();
    }

}
