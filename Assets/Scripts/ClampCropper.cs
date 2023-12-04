using UnityEngine;
using UnityEngine.VFX;

public class ClampCropper : MonoBehaviour {
    [System.Serializable]
    public enum Axis {
        x,
        y, 
        z
    }


    [SerializeField] public Axis axis;
    public VisualEffect vfx;
    public float min = 0.8f; // Minimum z-axis value
    public float max = 3.6f; // Maximum z-axis value

    public float minValue = 0f; // Minimum mapped value
    public float maxValue = 1f; // Maximum mapped value

    private Vector3 initialPosition;

    void Start() {
        // Store the initial position of the game object
        initialPosition = transform.position;
    }

    void Update() {
        // Get the current position of the game object
        Vector3 currentPosition = transform.position;
        float mappedValue = 0f;
        switch (axis) {
            case Axis.x:
                currentPosition.x = Mathf.Clamp(currentPosition.x, min, max);
                mappedValue = Mathf.InverseLerp(min, max, currentPosition.x);
                break;
            case Axis.y:
                currentPosition.y = Mathf.Clamp(currentPosition.y, min, max);
                mappedValue = Mathf.InverseLerp(min, max, currentPosition.y);
                break;
            case Axis.z:
                currentPosition.z = Mathf.Clamp(currentPosition.z, min, max);
                mappedValue = Mathf.InverseLerp(min, max, currentPosition.z);
                break;
        }

        // Map the value to the specified value range
        float mappedResult = Mathf.Lerp(minValue, maxValue, mappedValue);
        switch (axis) {
            case Axis.x:
                vfx.SetFloat("xCrop", mappedResult);
                break;
            case Axis.y:
                vfx.SetFloat("yCrop", mappedResult);
                break;
            case Axis.z:
                vfx.SetFloat("zCrop", mappedResult);
                break;
        }

        // Use the mappedResult for something else (you can replace this with your desired functionality)
        // For now, we'll just print it to the console
        //Debug.Log("Mapped Result: " + mappedResult);

        // Update the game object's position with the clamped z-axis value
        transform.position = currentPosition;
    }
}
