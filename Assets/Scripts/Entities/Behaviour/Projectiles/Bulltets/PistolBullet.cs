using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PistolBullet : Projectile
{
        
    protected override void ActionOnStart()
    {
        
    }

    protected override void ActionOnBulletHit(RaycastHit hit)
    {
        // Destroy bullet
        if (hit.collider.name.Contains("PistolBullet"))
        {
            base.ActionOnBulletHit(hit);
        }
        else
        {
            base.ActionOnBulletHit(hit);
        }
    }

    protected override void ActionOnEnd()
    {
        base.ActionOnEnd();
        // Explode logic
    }
}
