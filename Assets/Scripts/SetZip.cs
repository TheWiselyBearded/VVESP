using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetZip : MonoBehaviour
{
    public ReceiveZipFileFromServer ReceiveZipFileFromServer;
    public Material changed;
    

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player") || other.CompareTag("MainCamera")) {
            gameObject.GetComponent<Renderer>().material = changed;
            ReceiveZipFileFromServer.CreateZipArchive();
        }
    }
}
