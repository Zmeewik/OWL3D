using UnityEngine;

public interface IMovable
{
    void OnMove(Vector2 vector);
    void OnJump();
    void OnDash();
}