using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Input : MonoBehaviour
{

    [Header("References")]
    [SerializeField]
    List<GameObject> move_goal;
    List<IMovable> movable_goal = new List<IMovable>();
    [SerializeField]
    List<GameObject> rotate_goal;
    List<IRotatable> rotatable_goal = new List<IRotatable>();

    //Chnage goal of moving
    void Awake()
    {
        foreach(var goal in move_goal)
        {
            var move = goal.GetComponent<IMovable>();
            if(move != null)
                AddMoveGoal(move);
        }
        foreach(var goal in rotate_goal)
        {
            var rot = goal.GetComponent<IRotatable>();
            if(rot != null)
                AddRotateGoal(rot);
        }
    }

    //Work with goals for interacting
    public void AddMoveGoal(IMovable movable_goal)
    {
        this.movable_goal.Add(movable_goal);
    }

    public void ClearMoveGoals()
    {
        move_goal.Clear();
    }

    public void AddRotateGoal(IRotatable rotatable_goal)
    {
        this.rotatable_goal.Add(rotatable_goal);
    }

    public void ClearRotateGoals()
    {
        rotatable_goal.Clear();
    }

    //Input in gaming process
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

}
