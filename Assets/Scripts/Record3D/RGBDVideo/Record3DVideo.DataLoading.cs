using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

public partial class Record3DVideo
{
    /*/// <summary>
    /// Loads depth data for a specific frame from archive.
    /// </summary>
    private void LoadDepthData(int frameIdx)
    {
        using (var lzfseDepthStream =
               underlyingZip_.GetEntry($"{captureTitle}/rgbd/{frameIdx}.depth").Open())
        using (var memoryStream = new MemoryStream())
        {
            lzfseDepthStream.CopyTo(memoryStream);
            lzfseDepthBuffer = memoryStream.GetBuffer();
        }
    }

    /// <summary>
    /// Loads color data for a specific frame from archive.
    /// </summary>
    private void LoadColorData(int frameIdx)
    {
        using (var jpgStream =
               underlyingZip_.GetEntry($"{captureTitle}/rgbd/{frameIdx}.jpg").Open())
        using (var memoryStream = new MemoryStream())
        {
            jpgStream.CopyTo(memoryStream);
            jpgBuffer = memoryStream.GetBuffer();
        }
    }*/

    private async Task LoadDepthData(int frameIdx) {
        lzfseDepthBuffer = await dataSource.GetDepthBufferAsync(frameIdx);
    }

    private async Task LoadColorData(int frameIdx) {
        jpgBuffer = await dataSource.GetColorBufferAsync(frameIdx);
    }

}

