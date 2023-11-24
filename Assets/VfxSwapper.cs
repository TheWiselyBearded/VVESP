using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class VfxSwapper : MonoBehaviour
{
    public VisualEffect streamEffects;
    public VisualEffectAsset[] effects;
    public int effectIndex;


    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            streamEffects.visualEffectAsset = effects[effectIndex];
            effectIndex++;
            effectIndex %= effects.Length;
        }
    }
}
