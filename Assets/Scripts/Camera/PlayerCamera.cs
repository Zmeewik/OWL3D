using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerCamera : MonoBehaviour, IRotatable
{

    [Header("References")]
    [SerializeField]
    GameObject cameraObj;


    [Header("Rotation")]
    [SerializeField] float speedRotation;
    [SerializeField] float sensitivity;
    public float Sensitivity => sensitivity;


    //Rotation handle
    Vector2 rotationVector;
    



    public void FixedUpdate()
    {
        if(rotationVector != Vector2.zero)
            FirstPerson();
    }


    public void FirstPerson()
    {
        Vector3 deltaRotation = new Vector3(rotationVector.y * speedRotation * sensitivity, 0f, 0f);
        transform.Rotate(deltaRotation);
    }


    //Change camera rotation
    public void DeltaRotation(Vector2 delta)
    {
        rotationVector = new Vector2 (delta.x, -delta.y);
    }

}
