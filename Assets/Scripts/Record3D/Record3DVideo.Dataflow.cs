using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

public partial class Record3DVideo
{
    /// <summary>
    /// Configures the TPL Dataflow blocks for parallel processing.
    /// </summary>
    private (
        TransformBlock<int, byte[]> depthBlock,
        ActionBlock<byte[]> depthDecodeBlock,
        TransformBlock<int, byte[]> colorBlock,
        ActionBlock<byte[]> colorDecodeBlock,
        TransformBlock<int, byte[]> colorBGBlock,
        ActionBlock<byte[]> colorBGDecodeBlock
    ) ConfigureDataflowBlocks()
    {
        // Depth blocks
        var loadDepthBlock = new TransformBlock<int, byte[]>(async idx =>
        {
            using (var stream = underlyingZip_.GetEntry($"{captureTitle}/rgbd/{idx}.depth").Open())
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.GetBuffer();
            }
        });

        var decodeDepthBlock = new ActionBlock<byte[]>(async depthBuffer =>
        {
            IntPtr decodedDepthDataPtr = IntPtr.Zero;
            ulong totalDecompressDepth = Record3DNative.DecompressDepth(
                depthBuffer, (uint)depthBuffer.Length,
                out decodedDepthDataPtr, width_, height_
            );

            Record3DNative.PopulatePositionBuffer(
                decodedDepthDataPtr,
                1440, 1920,
                (uint)depthBuffer.Length,
                positionsBuffer,
                (uint)totalDecompressDepth,
                (uint)width_,
                (uint)height_,
                fx_, fy_, tx_, ty_
            );

            await Task.Yield();
        });

        // Color blocks
        var loadColorBlock = new TransformBlock<int, byte[]>(async idx =>
        {
            using (var stream = underlyingZip_.GetEntry(String.Format(colorChoice, idx)).Open())
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.GetBuffer();
            }
        });

        var decodeColorBlock = new ActionBlock<byte[]>(async colorBuffer =>
        {
            unsafe
            {
                fixed (byte* ptr = rgbBuffer)
                {
                    IntPtr jpgPtr = VVP_Utilities.ConvertByteArrayToIntPtr(colorBuffer);
                    Record3DNative.tjDecompress2(
                        turboJPEGHandle,
                        jpgPtr,
                        (uint)colorBuffer.Length,
                        (IntPtr)ptr,
                        loadedRGBWidth,
                        0,
                        loadedRGBHeight,
                        0,
                        0
                    );
                }
            }
            await Task.Yield();
        });

        // Background color blocks
        var loadColorBGBlock = new TransformBlock<int, byte[]>(async idx =>
        {
            using (var stream =
                   underlyingZip_.GetEntry($"{captureTitle}/rgbd/bg/bgColor{idx}.jpg").Open())
            using (var memoryStream = new MemoryStream())
            {
                await stream.CopyToAsync(memoryStream);
                return memoryStream.GetBuffer();
            }
        });

        var decodeColorBGBlock = new ActionBlock<byte[]>(async colorBuffer =>
        {
            unsafe
            {
                fixed (byte* ptr = rgbBufferBG)
                {
                    IntPtr jpgPtr = VVP_Utilities.ConvertByteArrayToIntPtr(colorBuffer);
                    Record3DNative.tjDecompress2(
                        turboJPEGHandle,
                        jpgPtr,
                        (uint)colorBuffer.Length,
                        (IntPtr)ptr,
                        loadedRGBWidth,
                        0,
                        loadedRGBHeight,
                        0,
                        0
                    );
                }
            }
            await Task.Yield();
        });

        loadDepthBlock.LinkTo(decodeDepthBlock, new DataflowLinkOptions { PropagateCompletion = true });
        loadColorBlock.LinkTo(decodeColorBlock, new DataflowLinkOptions { PropagateCompletion = true });
        loadColorBGBlock.LinkTo(decodeColorBGBlock, new DataflowLinkOptions { PropagateCompletion = true });

        return (
            loadDepthBlock, decodeDepthBlock,
            loadColorBlock, decodeColorBlock,
            loadColorBGBlock, decodeColorBGBlock
        );
    }
}

