using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class HandTrigger : MonoBehaviour
{
    [SerializeField] public UnityEvent OnTriggered;
    public string playerTag = "Player"; // The tag of the player object.

    private void OnTriggerEnter(Collider other) {
        // Check for specific conditions, if needed
        if (other.CompareTag(playerTag)) {
            // Generate a random color.
            Color randomColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);

            // Change the color of the game object to the randomly generated color.
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null) {
                renderer.material.color = randomColor;
            }
            // Invoke the UnityEvent when the trigger condition is met
            OnTriggered?.Invoke();
        }
    }
}
