using System.Threading.Tasks;

public interface IVolumetricVideoSource {
    // Basic metadata
    int FrameCount { get; }
    int FPS { get; }
    int Width { get; }
    int Height { get; }

    // Camera intrinsics or other relevant metadata
    float Fx { get; }
    float Fy { get; }
    float Tx { get; }
    float Ty { get; }

    // Async methods to retrieve color/depth frames
    Task<byte[]> GetDepthBufferAsync(int frameIndex);
    Task<byte[]> GetColorBufferAsync(int frameIndex);

    // Methods to initialize or close if needed
    Task InitializeSourceAsync();
    void CloseSource();
}
