using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class PlayerMomentum: MonoBehaviour
{
    [SerializeField] PlayerMovement playerMovement;
    
    //Speed up system
    [HideInInspector] public float maxSpeedDifference;
    [HideInInspector] public float currentMaxSpeed;
    [HideInInspector] public float momentum = 0;
    [System.Serializable]
    public class DictionaryDummy
    {
        public string key;
        public float value;
    }
    [Header("Speed Point Values")]
    public List<DictionaryDummy> speedPointList = new List<DictionaryDummy>()
    {
        new DictionaryDummy(){key = "wallrun", value = 3f},
        new DictionaryDummy(){key = "jump", value = 1f},
        new DictionaryDummy(){key = "slide", value = 1f},
        new DictionaryDummy(){key = "not moving", value = -1f},
        new DictionaryDummy(){key = "none", value = -0.01f},
    };
    Dictionary<string, float> speedPoints;

    public void StartFunc()
    {
        maxSpeedDifference = playerMovement.playerMovementConfig.maxTopSpeed - playerMovement.playerMovementConfig.maxLowSpeed;
        currentMaxSpeed = playerMovement.playerMovementConfig.maxLowSpeed;
        //List to dictionary
        speedPoints = speedPointList.ToDictionary(entry => entry.key, entry => entry.value);
    }

    //Speed system
    public void BuildSpeed(string type)
    {
        if (!playerMovement.features.enableSpeedSystem) return;

        momentum += speedPoints[type];
        if (momentum > playerMovement.playerMovementConfig.maxMomentum) momentum = playerMovement.playerMovementConfig.maxMomentum;
        if (momentum < 0) momentum = 0;
        currentMaxSpeed = Math.Max(playerMovement.playerMovementConfig.maxLowSpeed, playerMovement.playerMovementConfig.maxLowSpeed + momentum / playerMovement.playerMovementConfig.maxMomentum * maxSpeedDifference);
    }
}