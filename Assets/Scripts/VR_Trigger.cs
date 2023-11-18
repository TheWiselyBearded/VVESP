using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VR_Trigger : MonoBehaviour
{
    public bool UnityJPG = false;
    public ImageDecoder decoder;
    public string playerTag = "Player"; // The tag of the player object.

    private void OnTriggerEnter(Collider other) {
        // Check if the entering object has the specified player tag.
        if (other.CompareTag(playerTag)) {
            // Generate a random color.
            Color randomColor = new Color(Random.value, Random.value, Random.value);

            // Change the color of the game object to the randomly generated color.
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = randomColor;
            }
            if (UnityJPG) decoder.DecodeImgUnity();
            else decoder.DecodeImgTurbo();
        }
    }
}
