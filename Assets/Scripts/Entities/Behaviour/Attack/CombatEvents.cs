using UnityEngine;

/// <summary>
/// Combat happenings that things other than the target need to know about, raised the moment they
/// occur rather than when their consequences land.
///
/// This exists for dodging. Reacting to a bullet when it hits is too late to be a dodge at all --
/// the shot has already been resolved, so moving aside changes nothing and only the animation
/// suggests otherwise. Announcing the shot as it leaves the barrel gives anyone in its path the
/// travel time to actually get out of the way, and whether they succeed is then decided by ordinary
/// collision: the projectile either still reaches them or it doesn't.
/// </summary>
public static class CombatEvents
{
    /// <summary>
    /// Raised as a shot is fired, before the projectile has travelled anywhere.
    /// Arguments: where it started, the direction it is travelling, and who fired it.
    /// </summary>
    public static event System.Action<Vector3, Vector3, Transform> ShotFired;

    public static void RaiseShotFired(Vector3 origin, Vector3 direction, Transform shooter)
    {
        ShotFired?.Invoke(origin, direction, shooter);
    }
}
