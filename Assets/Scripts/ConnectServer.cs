using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectServer : MonoBehaviour
{
    public bool localLoad = false;
    public TcpZipDataClient ReceiveZipFileFromServer;
    public LocalDataSourceDebug LocalDataSource;
    public Material changed;

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Player") || other.CompareTag("MainCamera")) {
            gameObject.GetComponent<Renderer>().material = changed;
            if (!localLoad) {
                //ReceiveZipFileFromServer.ConnectedToServer();
                ReceiveZipFileFromServer.StartServerThread();
            } else if (localLoad) {
                LocalDataSource.ProcessReceivedZipFile();
            }
        }        
    }

}
