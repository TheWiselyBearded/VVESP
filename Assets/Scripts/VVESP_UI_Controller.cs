using Oculus.Interaction;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VVESP_UI_Controller : MonoBehaviour
{
    public GameObject rootUI;
    public GameObject PFB_ButtonPrefab;


    public void InitializeCaptureButtons(Capture[] captures) {
        int captureIndex = 1;
        Debug.Log($"initializing capture buttons {captures.Length}");
        foreach (Capture capture in captures) // Assuming 'captures' is your list of Capture instances
        {
            GameObject button = Instantiate(PFB_ButtonPrefab, rootUI.transform);
            button.transform.localPosition -= new Vector3(0, (captureIndex * 0.06f), 0); // Adjust Y position

            // Find the Text (TMP) GameObject and set its text
            TMP_Text textComponent = button.transform.GetChild(1).GetChild(0).GetChild(1).GetComponent<TMP_Text>();
            textComponent.text = capture.filename;

            button.GetComponent<CaptureInfoContainer>().SetCapture(capture);
            Debug.Log($"Init capture button {capture.filename}");
            captureIndex++;
        }
    }


}
