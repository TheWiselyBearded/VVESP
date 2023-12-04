using UnityEngine;

public class PositionDifferenceCalculator : MonoBehaviour {
    [System.Serializable]
    public enum Axis {
        x,
        y,
        z
    }

    [SerializeField] public Axis axis;


    public bool calculateDiff = false; // Control activation with this boolean
    public GameObject trackedObject; // The GameObject to track
    public float minValue = 0f; // Minimum value for normalization
    public float maxValue = 1f; // Maximum value for normalization
    public GameObject objectToMove; // The GameObject to move based on the normalized value
    public float newMinValue = 0f; // Minimum value for mapping
    public float newMaxValue = 1f; // Maximum value for mapping

    private Vector3 startingPosition;

    public void SetCalculateDiff(bool status) {
        calculateDiff = status;
    }

    void Update() {
        if (trackedObject == null) {
            Debug.LogError("Tracked Object is not assigned.");
            return;
        }

        // Check if the script is active
        if (calculateDiff) {
            // Check if starting position has not been set
            if (startingPosition == Vector3.zero) {
                // Set the starting position when the script is activated
                startingPosition = trackedObject.transform.position;
            } else {
                // Calculate the magnitude difference between the starting position and current position
                float magnitudeDifference = Vector3.Distance(startingPosition, trackedObject.transform.position);

                // Normalize the magnitude difference to a range between 0 and 1
                float normalizedValue = Mathf.InverseLerp(minValue, maxValue, magnitudeDifference);

                // Map the normalized value to the new additional min/max range
                float mappedValue = Mathf.Lerp(newMinValue, newMaxValue, normalizedValue);

                // Use the mappedValue to update the position of the objectToMove
                if (objectToMove != null) {
                    Vector3 newPosition = objectToMove.transform.position;
                    switch (axis) {
                        case Axis.x:
                            newPosition.x = mappedValue;
                            break;
                        case Axis.y:
                            newPosition.y = mappedValue; 
                            break;
                        case Axis.z:
                            newPosition.z = mappedValue;
                            break;
                    }
                    objectToMove.transform.position = newPosition;
                }

                // You can use both the magnitudeDifference, normalizedValue, and mappedValue for any desired functionality
                // For now, we'll just print them to the console
                Debug.Log("Magnitude Difference: " + magnitudeDifference);
                Debug.Log("Normalized Value: " + normalizedValue);
                Debug.Log("Mapped Value: " + mappedValue);
            }
        } else {
            // Reset the starting position when the script is deactivated
            startingPosition = Vector3.zero;
        }
    }
}
