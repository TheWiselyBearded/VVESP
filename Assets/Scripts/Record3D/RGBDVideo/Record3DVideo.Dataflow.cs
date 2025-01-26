using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

public partial class Record3DVideo {
    private (
        TransformBlock<int, byte[]> depthBlock,
        ActionBlock<byte[]> depthDecodeBlock,
        TransformBlock<int, byte[]> colorBlock,
        ActionBlock<byte[]> colorDecodeBlock,
        TransformBlock<int, byte[]> colorBGBlock,
        ActionBlock<byte[]> colorBGDecodeBlock
    ) ConfigureDataflowBlocks() {
        // Depth blocks
        var loadDepthBlock = new TransformBlock<int, byte[]>(async idx => {
            // OLD CODE referencing Zip:
            // using (var stream = underlyingZip_.GetEntry($"{captureTitle}/rgbd/{idx}.depth").Open())
            // ...

            // NEW CODE: call dataSource
            return await dataSource.GetDepthBufferAsync(idx);
        });

        var decodeDepthBlock = new ActionBlock<byte[]>(async depthBuffer => {
            // same decode logic
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
        var loadColorBlock = new TransformBlock<int, byte[]>(async idx => {
            // Instead of calling underlyingZip_ and colorChoice path, 
            // call dataSource. If you have different "colorChoice" logic, 
            // you might need a new method in your data source, e.g.:
            // return await dataSource.GetColorBufferAsync(idx, colorChoice);

            return await dataSource.GetColorBufferAsync(idx);
        });

        var decodeColorBlock = new ActionBlock<byte[]>(async colorBuffer => {
            unsafe {
                fixed (byte* ptr = rgbBuffer) {
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

        // Background color blocks (if you have background frames)
        var loadColorBGBlock = new TransformBlock<int, byte[]>(async idx => {
            // same logic: create a new method in your data source 
            // e.g. dataSource.GetBackgroundColorBufferAsync(idx),
            // or if your data source doesn't handle that, you might skip or unify logic
            if (dataSource is IBackgroundColorSource bgSource) {
                return await bgSource.GetBackgroundColorBufferAsync(idx);
            }
            return new byte[0]; // or throw
        });

        var decodeColorBGBlock = new ActionBlock<byte[]>(async colorBuffer => {
            unsafe {
                fixed (byte* ptr = rgbBufferBG) {
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

        // Link them together
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
