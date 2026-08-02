using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationHide : MonoBehaviour
{
    [SerializeField] MeshRenderer[] meshRenderers;

    private Coroutine hideCoroutine;

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

    /// <summary>
    /// Hides the mesh immediately and re-enables it after `duration` seconds via a coroutine.
    /// Replaces the old two-Animation-Event setup (a DisableMesh event followed by a separate
    /// EnableMesh event later in the same clip, e.g. the Kunai throw animations) with a single
    /// Animation Event call -- point the event's functionName at HideMeshFor and set its
    /// floatParameter to the desired duration in seconds.
    /// </summary>
    public void HideMeshFor(float duration)
    {
        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        DisableMesh();
        hideCoroutine = StartCoroutine(EnableMeshAfter(duration));
    }

    private IEnumerator EnableMeshAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        EnableMesh();
        hideCoroutine = null;
    }
}
