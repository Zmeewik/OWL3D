using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEditor;
using UnityEngine;

public class Particles : MonoBehaviour
{
    public static Particles Instance { get; private set; }

    [Serializable]
    struct ParticleEffect
    {
        public string name;
        public typesOfEffects type;
        public GameObject objectEffect;
        [HideInInspector] public int ID;

    }
    public enum typesOfEffects { InFront, InPlace };
    [SerializeField] List<ParticleEffect> particleEffects;
    [SerializeField] Transform face;
    List<GameObject> headEffects = new List<GameObject>();
    List<GameObject> allEffects = new List<GameObject>();


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        for (var pID = 0; pID < particleEffects.Count; pID++)
        {
            var pE = particleEffects[pID];
            if (pE.type == typesOfEffects.InFront)
            {
                headEffects.Add(Instantiate(pE.objectEffect, face));
                pE.ID = headEffects.Count - 1;
            }
        }
    }

    //Methonds to start effects
    public void StartEffect(string name)
    {
        StartEffectObject(name, -1, new Color[0], Vector3.zero);
    }
    public void StartEffect(string name, float duration)
    {
        StartEffectObject(name, duration, new Color[0], Vector3.zero);
    }
    public void StartEffect(string name, Vector3 pos)
    {
        StartEffectObject(name, -1, new Color[0], pos);
    }
    
    public void StartEffect(string name, Vector3 pos, Quaternion rotation)
    {
        StartEffectObject(name, -1, new Color[0], pos);
    }

    public void StartEffect(string name, float duration, Vector3 pos)
    {
        StartEffectObject(name, duration, new Color[0], pos);
    }
    public void StartEffect(string name, float duration, Color[] colors, Vector3 pos)
    {
        StartEffectObject(name, duration, colors, pos);
    }

    public void ChangeColor(string name, Color[] colors)
    {
        //Find effect
        ParticleEffect effect = new ParticleEffect();
        for (int n = 0; n < particleEffects.Count; n++)
        {
            if (name == particleEffects[n].name)
            {
                effect = particleEffects[n];
            }
        }

        //Handle not found effect situation
        if (effect.name == null)
        {
            print($"Particle effect {name} not found!");
            return;
        }

        //Get object
        GameObject SystemObject = null;
        if (effect.type == typesOfEffects.InFront)
            SystemObject = headEffects[effect.ID];
        if (SystemObject == null)
            return;

        //Change Colors
        if (colors.Length != 0)
        {
            for (int r = 0; r < colors.Length; r++)
            {
                var obj = SystemObject.gameObject.transform.GetChild(r).GetComponent<ParticleSystem>();
                if (obj != null)
                    obj.startColor = colors[r];
            }
        }
    }

    //Create and start effect object
    private void StartEffectObject(string name, float duration, Color[] colors, Vector3 pos, Quaternion rot = new Quaternion())
    {
        //Find effect
        ParticleEffect effect = new ParticleEffect();
        for (int n = 0; n < particleEffects.Count; n++)
        {
            if (name == particleEffects[n].name)
            {
                effect = particleEffects[n];
            }
        }

        //Handle not found effect situation
        if (effect.name == null)
        {
            print($"Particle effect {name} not found!");
            return;
        }

        //Create or enable effect object
        GameObject SystemObject = null;
        if (effect.type == typesOfEffects.InFront)
        {
            headEffects[effect.ID].SetActive(true);
            SystemObject = headEffects[effect.ID];
        }
        else if (effect.type == typesOfEffects.InPlace)
        {
            SystemObject = Instantiate(effect.objectEffect, pos, rot);
        }

        //Start all effects attached to System
        for (int c = 0; c < SystemObject.transform.childCount; c++)
        {
            var PS = SystemObject.transform.GetChild(c).GetComponent<ParticleSystem>();
            if (PS != null)
                PS.Play();
            else
                print($"Attached particle system not found (item={c}, name={name})!");
        }

        //Change Colors
        if (colors.Length != 0)
        {
            for (int r = 0; r < colors.Length; r++)
            {
                var obj = SystemObject.gameObject.transform.GetChild(r).GetComponent<ParticleSystem>();
                if (obj != null)
                    obj.startColor = colors[r];
            }
        }

        //Start deactivation
        if (duration == -1 && effect.type == typesOfEffects.InFront)
            return;
        if (duration != -1)
        {
            var delete = effect.type == typesOfEffects.InPlace ? true : false;
            StartCoroutine(DeactivateEffect(duration, SystemObject, delete));
        }
        else
        {
            var delete = effect.type == typesOfEffects.InPlace ? true : false;
            for (int r = 0; r < SystemObject.transform.childCount; r++)
            {
                var obj = SystemObject.gameObject.transform.GetChild(r).GetComponent<ParticleSystem>();
                if (obj != null)
                {
                    if (duration < obj.duration)
                        duration = obj.duration;
                }
            }
            StartCoroutine(DeactivateEffect(duration, SystemObject, delete));
        }

    }

    public void StopAllEffects()
    {
        for (var t = 0; t < allEffects.Count; t++)
        {
            StartCoroutine(DeactivateEffect(0f, allEffects[t], true));
        }
        for (var t = 0; t < headEffects.Count; t++)
        {
            StartCoroutine(DeactivateEffect(0f, headEffects[t], false));
        }
    }

    //Stop effect depending on its type
    public void StopEffect(string name)
    {
        //Find effect
        ParticleEffect effect = new ParticleEffect();
        for (int n = 0; n < particleEffects.Count; n++)
        {
            if (name == particleEffects[n].name)
            {
                effect = particleEffects[n];
            }
        }

        //Handle not found effect situation
        if (effect.name == null)
        {
            print($"Particle effect {name} not found!");
            return;
        }

        if (effect.type == typesOfEffects.InFront)
            StartCoroutine(DeactivateEffect(0f, headEffects[effect.ID], false));
        // else
        //     StartCoroutine(DeactivateEffect(0f,  , true));

    }

    private IEnumerator DeactivateEffect(float duration, GameObject particles, bool delete)
    {
        yield return new WaitForSeconds(duration);

        if (particles == null)
            yield return 0;

        //Stop all effects
        for (int c = 0; c < particles.transform.childCount; c++)
        {
            var PS = particles.transform.GetChild(c).GetComponent<ParticleSystem>();
            if (PS != null)
                PS.Pause();
        }

        //Deactivate effect
        particles.gameObject.SetActive(false);

        //Delete effect object
        if (delete)
        {
            Destroy(particles.gameObject);
        }
    }

}
