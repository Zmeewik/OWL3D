using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class PistolCharged : Projectile
{
    [SerializeField] private float startSize;
    [SerializeField] private float endSize;
    [SerializeField] private float bulletIncrease;
        
    protected override void ActionOnStart()
    {
        base.ActionOnStart();
        var scale = Mathf.Lerp(startSize, endSize, chargeFactor);
        transform.localScale = new Vector3(scale, scale, scale);
    }

    protected override void ActionOnBulletHit(RaycastHit hit)
    {
        print("Bullet hit");
        if (hit.collider.name.Contains("PistolBullet"))
        {
            // Increase charge and size
            print("Start: " + chargeFactor);
            chargeFactor += bulletIncrease;
            print("Now: " + chargeFactor);
            chargeFactor = Mathf.Min(chargeFactor, 1);
            var scale = Mathf.Lerp(startSize, endSize, chargeFactor);
            transform.localScale = new Vector3(scale, scale, scale);
        }
    }

    protected override void ActionOnEnd()
    {
        base.ActionOnEnd();
        // Explode logic
    }
}
