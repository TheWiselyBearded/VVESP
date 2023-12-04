using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Temp : MonoBehaviour
{
    public GameObject one;
    public GameObject two;
    

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) {
            one.SetActive(true);
            two.SetActive(false);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow)) {
            one.SetActive(false);
            two.SetActive(true);
        }
        if (Input.GetKeyDown(KeyCode.A)) {
            one.SetActive(true);
            two.SetActive(true);
        }

    }
}
