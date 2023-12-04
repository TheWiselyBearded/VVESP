using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using UnityEngine;

public class DataLayer
{
    public BufferBlock<ValueTuple<byte[], byte[]>> encodedBuffer;

    public DataLayer()
    {
        DataflowBlockOptions dataflowBlockOptions = new DataflowBlockOptions();
        dataflowBlockOptions.BoundedCapacity = 30;
        encodedBuffer = new BufferBlock<ValueTuple<byte[], byte[]>>(dataflowBlockOptions);
    }


    public async Task ConsumerCaptureData()
    {
        while (await encodedBuffer.OutputAvailableAsync())
        {    // subscribe for buffer write 

            while (encodedBuffer.TryReceive(out ValueTuple<byte[], byte[]> frameDatablock))
            {
                Debug.Log($"Color Data Length {frameDatablock.Item1.Length}, Depth Data Length {frameDatablock.Item2.Length}");


            }
        }
    }
}
