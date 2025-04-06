using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerCamera : MonoBehaviour, IRotatable
{

    [Header("References")]
    [SerializeField]
    GameObject cameraObj;
    [SerializeField] Transform cameraPosition;


    [Header("Rotation")]
    [SerializeField] float speedRotation;
    [SerializeField] float sensitivity;
    public float Sensitivity => sensitivity;


    //Rotation handle
    Vector2 rotationVector;
    float xRotation;

    //Update camera position and rotation at late update
    public void LateUpdate()
    {
        if(rotationVector != Vector2.zero)
            FirstPerson();
        AttachCamera();
    }

    //Attach camera to an object
    public void AttachCamera()
    {
        transform.position = cameraPosition.position;
    }

    //Rotate camera with mouse
    public void FirstPerson()
    {
        //Find current look rotation
        Vector3 rot = cameraObj.transform.rotation.eulerAngles;
        var desiredX = rot.y + rotationVector.x * speedRotation * sensitivity;
        
        //Rotate and limit y axis
        xRotation -= rotationVector.y * speedRotation * sensitivity;
        xRotation = Mathf.Clamp(xRotation, -89f, 89f);

        //Perform the rotations
        cameraObj.transform.rotation = Quaternion.Euler(xRotation, desiredX, 0);
    }


    //Change camera rotation
    public void DeltaRotation(Vector2 delta)
    {
        rotationVector = new Vector2 (delta.x, delta.y);
    }

}
