using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine; // for JsonUtility if desired


/// <summary>
/// An IVolumetricVideoSource that reads frames/metadata from a .zip archive.
/// </summary>
public class ZipVolumetricVideoSource : IVolumetricVideoSource {
    private readonly ZipArchive _zipArchive;
    private readonly string _captureTitle;

    // Internal metadata
    private int _frameCount;
    private int _fps;
    private int _width;
    private int _height;
    private float _fx, _fy, _tx, _ty;

    public ZipVolumetricVideoSource(ZipArchive zipArchive, string captureTitle = "") {
        _zipArchive = zipArchive;

        // Check if the captureTitle ends with ".zip" and remove it
        _captureTitle = captureTitle.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? captureTitle.Substring(0, captureTitle.Length - 4)
            : captureTitle;
    }

    // Interface properties
    public int FrameCount => _frameCount;
    public int FPS => _fps;
    public int Width => _width;
    public int Height => _height;
    public float Fx => _fx;
    public float Fy => _fy;
    public float Tx => _tx;
    public float Ty => _ty;

    /// <summary>
    /// Reads metadata (JSON) and sets up internal fields.
    /// </summary>
    public async Task InitializeSourceAsync() {
        // For demonstration, do a synchronous read (JSON is tiny).
        // If truly large, you'd do an asynchronous pattern, but let's keep it simple.
        await Task.Run(() => {
            // Try something like: "capture/metadata" or "[captureTitle]/metadata"
            // If no captureTitle was given, fallback to "metadata"
            string metadataPath = string.IsNullOrEmpty(_captureTitle)
                ? "metadata"
                : $"{_captureTitle}/metadata";
            Debug.Log($"Found metadata {metadataPath}");
            var metadataEntry = _zipArchive.GetEntry(metadataPath);
            if (metadataEntry == null)
                throw new FileNotFoundException($"Metadata not found at: {metadataPath}");

            using (var sr = new StreamReader(metadataEntry.Open())) {
                string json = sr.ReadToEnd();
                Record3DMetadata meta = JsonUtility.FromJson<Record3DMetadata>(json);

                _fps = meta.fps;
                _width = meta.w;
                _height = meta.h;

                // The camera intrinsics are in meta.K, e.g. K[0] = fx, K[4] = fy, K[6] = tx, K[7] = ty
                _fx = meta.K[0];
                _fy = meta.K[4];
                _tx = meta.K[6];
                _ty = meta.K[7];
            }

            // Attempt to infer number of frames by counting .depth or .bytes
            // If captureTitle is non-empty, we look in [captureTitle]/rgbd/
            // Otherwise, we look in "rgbd/"
            string depthSearchPrefix =
                string.IsNullOrEmpty(_captureTitle) ? "rgbd/" : $"{_captureTitle}/rgbd/";

            _frameCount = _zipArchive.Entries.Count(
                e => e.FullName.StartsWith(depthSearchPrefix) && e.FullName.EndsWith(".depth")
            );

            if (_frameCount == 0) {
                // fallback to counting .bytes if no .depth found
                _frameCount = _zipArchive.Entries.Count(
                    e => e.FullName.StartsWith(depthSearchPrefix) && e.FullName.EndsWith(".bytes")
                );
            }
        });
    }

    /// <summary>
    /// Loads depth data for a specific frame from the .zip archive, asynchronously.
    /// </summary>
    public async Task<byte[]> GetDepthBufferAsync(int frameIndex) {
        return await Task.Run(() => {
            // Example path: "[captureTitle]/rgbd/{frameIndex}.depth"
            // or if no title: "rgbd/{frameIndex}.depth"
            string path =
                string.IsNullOrEmpty(_captureTitle)
                ? $"rgbd/{frameIndex}.depth"
                : $"{_captureTitle}/rgbd/{frameIndex}.depth";

            var entry = _zipArchive.GetEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"Depth file not found: {path}");

            using (var ms = new MemoryStream())
            using (var es = entry.Open()) {
                es.CopyTo(ms);
                return ms.ToArray();
            }
        });
    }

    /// <summary>
    /// Loads color data for a specific frame from the .zip archive, asynchronously.
    /// </summary>
    public async Task<byte[]> GetColorBufferAsync(int frameIndex) {
        return await Task.Run(() => {
            // For color, might be "[captureTitle]/rgbd/{frameIndex}.jpg"
            // or "rgbd/{frameIndex}.jpg"
            string path =
                string.IsNullOrEmpty(_captureTitle)
                ? $"rgbd/{frameIndex}.jpg"
                : $"{_captureTitle}/rgbd/{frameIndex}.jpg";

            var entry = _zipArchive.GetEntry(path);
            if (entry == null)
                throw new FileNotFoundException($"Color file not found: {path}");

            using (var ms = new MemoryStream())
            using (var es = entry.Open()) {
                es.CopyTo(ms);
                return ms.ToArray();
            }
        });
    }

    public void CloseSource() {
        _zipArchive?.Dispose();
    }
}
