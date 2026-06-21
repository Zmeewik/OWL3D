using UnityEngine;

public class Player_Movement
{
    private readonly PlayerController controller;

    Rigidbody rb => controller.rb;

    public Player_Movement(PlayerController controller)
    {
        this.controller = controller;
    }

    public void Tick()
    {
        Move();

        Rotate();

        Drag();

        CounterMovement();
    }

    private void Move(float multiplier = 1f)
    {
        if (controller.MoveInput == Vector2.zero)
            return;

        Vector3 surfaceForward =
            Vector3.ProjectOnPlane(
                controller.transform.forward,
                controller.GroundNormal
            ).normalized;

        Vector3 surfaceRight =
            Vector3.ProjectOnPlane(
                controller.transform.right,
                controller.GroundNormal
            ).normalized;

        rb.AddForce(
            surfaceRight *
            controller.MoveInput.x *
            controller.acceleration *
            multiplier,
            ForceMode.Acceleration);

        rb.AddForce(
            surfaceForward *
            controller.MoveInput.y *
            controller.acceleration *
            multiplier,
            ForceMode.Acceleration);
    }

    private void Drag()
    {
        if (controller.MoveInput != Vector2.zero)
            return;

        Vector3 horizontalVel =
            new Vector3(rb.velocity.x, 0, rb.velocity.z);

        horizontalVel =
            Vector3.ProjectOnPlane(
                horizontalVel,
                controller.GroundNormal);

        if (horizontalVel.magnitude > 0.5f)
        {
            Vector3 drag =
                -horizontalVel.normalized *
                controller.decceleration;

            rb.AddForce(
                drag,
                ForceMode.Acceleration);
        }
        else
        {
            rb.velocity =
                new Vector3(
                    0,
                    rb.velocity.y,
                    0);
        }
    }

    private void CounterMovement()
    {
        Vector3 horizontalVel =
            new Vector3(
                rb.velocity.x,
                0,
                rb.velocity.z);

        horizontalVel =
            Vector3.ProjectOnPlane(
                horizontalVel,
                controller.GroundNormal);

        if (horizontalVel.magnitude <= controller.maxSpeed)
            return;

        Vector3 moveDir =
            horizontalVel.normalized;

        Vector3 counterForce =
            -moveDir *
            controller.acceleration *
            1.1f;

        rb.AddForce(
            counterForce,
            ForceMode.Acceleration);
    }

    private void Rotate()
    {
        Vector3 forward =
            controller.cameraFront.forward;

        forward.y = 0;
        forward.Normalize();

        Quaternion targetRotation =
            Quaternion.LookRotation(forward);

        rb.MoveRotation(targetRotation);
    }
}