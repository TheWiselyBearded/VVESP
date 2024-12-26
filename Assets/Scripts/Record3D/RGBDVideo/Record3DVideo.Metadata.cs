using System.Collections.Generic;
using UnityEngine;

// A separate file for cleanliness; used by Record3DVideo
[System.Serializable]
public struct Record3DMetadata
{
    public int w;
    public int h;
    public List<float> K;
    public int fps;
}

