using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class StartSettings : MonoBehaviour
{

    [Header("Window settings")]
    [SerializeField] int frameRate;

    //Game start settings
    void Awake(){
        Cursor.visible = false;
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 100000;
    }

    //Debug info    
    private float refreshRate = 0.2f;
    private float timer;
    private int frameCount;
    private float fps;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        frameCount++;
        timer += Time.unscaledDeltaTime;

        if (timer >= refreshRate)
        {
            fps = frameCount / timer;
            var text = "FPS: " + Mathf.RoundToInt(fps);
            DebugOutput.Instance.Output(text, 0);
            timer = 0;
            frameCount = 0;
        }
    }
}
