using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationHide : MonoBehaviour
{
    [SerializeField] MeshRenderer[] meshRenderers;

    public void EnableMesh()
    {
        foreach (var mesh in meshRenderers)
        {
            mesh.enabled = true;
        }
    }

    public void DisableMesh()
    {
        foreach (var mesh in meshRenderers)
        {
            mesh.enabled = false;
        }
    }
}
