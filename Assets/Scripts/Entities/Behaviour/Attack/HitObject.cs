using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HitObject : MonoBehaviour
{
    [SerializeField] float disappearTime;
    
    public void Disappear()
    {
        StartCoroutine(DisappearCoroutine());
    }

    IEnumerator DisappearCoroutine()
    {
        yield return new WaitForSeconds(disappearTime);
        Destroy(gameObject);
    }
}
