using UnityEngine;
using System.Collections.Generic;

public class PlayerSurface: MonoBehaviour
{
    [SerializeField] PlayerMovement playerMovement;
    
    Vector3 lastWallNormal = Vector3.zero;
    [HideInInspector] public Vector3 groundNormal = Vector3.up;
    [HideInInspector] public Vector3 wallNormal;
    Vector3 closestWallContact = Vector3.zero;
    
    private Vector3 savedSlideNormal;
    private int counterNormal;
    
    //Handle collisions with surfaces
    public void OnSurfaceCollide(ContactPoint[] contacts)
    {
        if (playerMovement.CurrentState == PlayerMovement.BodyState.Dashing)
            return;

        if (playerMovement.playerWallRun.currentWallState == PlayerWallRun.WallState.HangUp)
        {
            playerMovement.CurrentState = PlayerMovement.BodyState.WallRunning;
            return;
        }

        //If there is ono contact object is flying
        if (contacts.Length == 0)
        {
            playerMovement.lastState = playerMovement.CurrentState;
            playerMovement.OnFly();
            return;
        }

        //Handling and counting multiple contacts
        int[] contactSurfaces = { 0, 0, 0, 0 };
        List<int> indexesGround = new List<int>();
        List<int> indexesSlope = new List<int>();
        List<int> indexesCeiling = new List<int>();
        List<int> indexesWall = new List<int>();
        for (int i = 0; i < contacts.Length; i++)
        {
            var surfaceType = playerMovement.surfaceHandler.GetSurfaceType(contacts[i].normal, playerMovement.playerCrouch.IsPlayerCrouching());
            switch (surfaceType)
            {
                case SurfaceHandler.SurfaceType.Ground:
                    contactSurfaces[0]++;
                    indexesGround.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Slope:
                    contactSurfaces[1]++;
                    indexesSlope.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Ceiling:
                    contactSurfaces[2]++;
                    indexesCeiling.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Wall:
                    contactSurfaces[3]++;
                    indexesWall.Add(i);
                    break;
            }
        }

        //Ground contact main
        if (contactSurfaces[0] > 0)
        {
            //Find main surface: closest to vector.up
            var curnormal = FindClosestToVector(indexesGround, contacts, Vector3.up);

            //Handle main logic
            HandleGround(curnormal);
            playerMovement.OnLand();
        }
        //Slope contact main
        else if (contactSurfaces[1] > 0 && playerMovement.features.enableSlide)
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestTo90(indexesSlope, contacts, out var index);

            //Handle main logic
            HandleSlope(curnormal, contacts[index].otherCollider.tag);
            playerMovement.OnLand();
        }
        //Wall contact main
        else if (contactSurfaces[3] > 0 && (playerMovement.features.enableWallRun || playerMovement.features.enableWallClimb || playerMovement.features.enableWallSlide))
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestTo90(indexesWall, contacts, out var index);
            closestWallContact = FindClosestToObjectContatct(indexesWall, contacts, transform.position);

            //Handle main logic
            HandleWall(curnormal, contacts[index]);
        }
        //Ceiling contact main
        else if (contactSurfaces[2] > 0)
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestToVector(indexesGround, contacts, Vector3.down);

            //Handle main logic
            HandleCeiling(curnormal);
        }

        //Clear all lists
        indexesGround.Clear();
        indexesSlope.Clear();
        indexesCeiling.Clear();
        indexesWall.Clear();
    }

    //Find main surface: closest to 90 degrees
    Vector3 FindClosestTo90(List<int> indexes, ContactPoint[] contacts, out int objIndex)
    {
        var closestNormal = Vector3.zero;
        var closestAngle = 0f;
        var num = 0;
        foreach (var i in indexes)
        {
            var currentNormal = contacts[i].normal;
            var angle = Vector3.Angle(currentNormal, Vector3.up);
            if (angle > closestAngle)
            {
                closestNormal = currentNormal;
                closestAngle = angle;
                num = i;
            }
        }
        objIndex = num;
        return closestNormal;
    }

    //Find main surface: closest to vector.up
    Vector3 FindClosestToVector(List<int> indexes, ContactPoint[] contacts, Vector3 vec)
    {
        var closestNormal = Vector3.zero;
        var closestDot = 0f;
        foreach (var i in indexes)
        {
            var currentNormal = contacts[i].normal;
            var dot = Vector3.Dot(currentNormal, vec);
            if (dot > closestDot)
            {
                closestNormal = currentNormal;
                closestDot = dot;
            }
        }
        return closestNormal;
    }

    Vector3 FindClosestToObjectContatct(List<int> indexes, ContactPoint[] contacts, Vector3 pos)
    {
        var closestPosition = Vector3.positiveInfinity;
        var closestDistance = Mathf.Infinity;
        foreach (var i in indexes)
        {
            var currentPosition = contacts[i].point;
            var dist = Vector3.Distance(currentPosition, pos);
            if (dist < closestDistance)
            {
                closestPosition = currentPosition;
                closestDistance = dist;
            }
        }
        return closestPosition;
    }




    public void HandleCollisionWithObjects(ContactPoint[] contacts)
    {

        if (playerMovement.playerWallRun.currentWallState == PlayerWallRun.WallState.HangUp)
        {
            playerMovement.CurrentState = PlayerMovement.BodyState.WallRunning;
            return;
        }

        //If there is ono contact object is flying
        if (contacts.Length == 0)
        {
            playerMovement.OnFly();
            return;
        }

        //Handling and counting multiple contacts
        int[] contactSurfaces = { 0, 0, 0, 0 };
        List<int> indexesGround = new List<int>();
        List<int> indexesSlope = new List<int>();
        List<int> indexesCeiling = new List<int>();
        List<int> indexesWall = new List<int>();
        for (int i = 0; i < contacts.Length; i++)
        {
            var surfaceType = playerMovement.surfaceHandler.GetSurfaceType(contacts[i].normal, playerMovement.playerCrouch.IsPlayerCrouching());
            switch (surfaceType)
            {
                case SurfaceHandler.SurfaceType.Ground:
                    contactSurfaces[0]++;
                    indexesGround.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Slope:
                    contactSurfaces[1]++;
                    indexesSlope.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Ceiling:
                    contactSurfaces[2]++;
                    indexesCeiling.Add(i);
                    break;

                case SurfaceHandler.SurfaceType.Wall:
                    contactSurfaces[3]++;
                    indexesWall.Add(i);
                    break;
            }
        }

        //Ground contact main
        if (contactSurfaces[0] > 0)
        {
            //Find main surface: closest to vector.up
            var curnormal = FindClosestToVector(indexesGround, contacts, Vector3.up);

            //Handle main logic
            HandleGround(curnormal);
            playerMovement.OnLand();
            playerMovement.rb.useGravity = true;
        }
        //Slope contact main
        else if (contactSurfaces[1] > 0 && playerMovement.features.enableSlide)
        {
            //Find main surface: closest to 90 degrees
            var curnormal = FindClosestTo90(indexesSlope, contacts, out var index);

            //Handle main logic
            HandleSlope(curnormal, contacts[index].otherCollider.tag);
            playerMovement.OnLand();
        }

        //Clear all lists
        indexesGround.Clear();
        indexesSlope.Clear();
        indexesCeiling.Clear();
        indexesWall.Clear();
    }


    void HandleGround(Vector3 normal)
    {

        groundNormal = normal;
        if (playerMovement.CurrentState != PlayerMovement.BodyState.Moving)
        {
            playerMovement.rb.useGravity = false;
            //Impulse when touching the ground after slope
            if (savedSlideNormal != Vector3.zero && groundNormal == Vector3.up && playerMovement.lastState == PlayerMovement.BodyState.Sliding)
            {
                var forceOfSlide = new Vector3(savedSlideNormal.x, 0, savedSlideNormal.z).normalized;
                playerMovement.rb.AddForce(forceOfSlide * playerMovement.currentMaxSpeed * 100, ForceMode.Impulse);
            }
            playerMovement.CurrentState = PlayerMovement.BodyState.Moving;
            savedSlideNormal = Vector3.zero;

        }
    }

    void HandleSlope(Vector3 normal, string tag)
    {

        groundNormal = normal;
        //Check for consistent slope
        if (savedSlideNormal != groundNormal && tag == "Slope")
            playerMovement.rb.velocity = Vector3.zero;

        if (savedSlideNormal == groundNormal)
            counterNormal++;
        else
            counterNormal = 0;

        if (counterNormal > 3)
        {
            playerMovement.BuildSpeed("slide");
            if (playerMovement.CurrentState != PlayerMovement.BodyState.Sliding)
            {
                counterNormal = 0;
                playerMovement.CurrentState = PlayerMovement.BodyState.Sliding;
                playerMovement.rb.useGravity = true;
            }
        }

        savedSlideNormal = groundNormal;
    }

    void HandleWall(Vector3 normal, ContactPoint contact)
    {

        if (playerMovement.isGrounded == PlayerMovement.IsGrounded.InAir)
        {
            //Check for consistent wall
            if (lastWallNormal == normal)
                playerMovement.playerWallRun.checkWallCounter++;
            else
                playerMovement.playerWallRun.checkWallCounter = 0;

            if (playerMovement.playerWallRun.checkWallCounter > 3)
            {
                playerMovement.CurrentState = PlayerMovement.BodyState.WallRunning;
                playerMovement.rb.useGravity = true;
                wallNormal = normal;

                //Check for the same wall for additional wall run possibility
                playerMovement.playerWallRun.wallReference = contact.otherCollider.transform;
                if (playerMovement.playerWallRun.wallReference != playerMovement.playerWallRun.wallReferenceSaved)
                {
                    playerMovement.playerWallRun.runnedAlready = false;
                    playerMovement.playerWallRun.stoppedByWall = false;
                }
                playerMovement.playerWallRun.wallReferenceSaved = playerMovement.playerWallRun.wallReference;
                playerMovement.OnCrouch(false);
            }

            lastWallNormal = normal;
        }
    }


    void HandleCeiling(Vector3 normal)
    {
        if (playerMovement.CurrentState == PlayerMovement.BodyState.Dashing)
            return;
        //rb.AddForce(normal * 100f, ForceMode.Impulse);
    }
}