using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine; // for JsonUtility

/// <summary>
/// A local-file-based IVolumetricVideoSource that loads a .zip from disk via ZipUtility
/// and parses volumetric frames/metadata.
/// </summary>
public class LocalFileVolumetricVideoSource : IVolumetricVideoSource {
    private readonly string _zipFileName;   // e.g. "myRecording.zip"
    private readonly string _captureTitle;  // optional, if you store metadata under "captureTitle/metadata"

    private ZipArchive _zipArchive;         // once loaded from disk

    // Internal fields for metadata
    private int _frameCount;
    private int _fps;
    private int _width;
    private int _height;
    private float _fx, _fy, _tx, _ty;

    /// <summary>
    /// Creates a data source that loads a .zip from local disk (StreamingAssets).
    /// </summary>
    /// <param name="zipFileName">The name of the .zip file to load from disk.</param>
    /// <param name="captureTitle">Optional subfolder or 'captureTitle' inside the .zip for metadata.</param>
    public LocalFileVolumetricVideoSource(string zipFileName, string captureTitle = "") {
        _zipFileName = zipFileName;
        _captureTitle = captureTitle;
    }

    #region IVolumetricVideoSource Properties
    public int FrameCount => _frameCount;
    public int FPS => _fps;
    public int Width => _width;
    public int Height => _height;

    public float Fx => _fx;
    public float Fy => _fy;
    public float Tx => _tx;
    public float Ty => _ty;
    #endregion

    /// <summary>
    /// Asynchronously initializes this data source by loading the .zip from local disk
    /// and extracting metadata (fps, width, height, intrinsics).
    /// </summary>
    public async Task InitializeSourceAsync() {
        // 1. Load the .zip archive from disk (StreamingAssets or other location).
        _zipArchive = await ZipUtility.LoadZipArchiveAsync(_zipFileName);
        if (_zipArchive == null) {
            throw new FileNotFoundException($"Failed to load zip archive: {_zipFileName}");
        }

        // 2. Parse the metadata
        await Task.Run(() => {
            // If captureTitle is specified, we look for "[captureTitle]/metadata"
            // Otherwise, fallback to "metadata"
            string metadataPath = string.IsNullOrEmpty(_captureTitle)
                ? "metadata"
                : $"{_captureTitle}/metadata";

            var metadataEntry = _zipArchive.GetEntry(metadataPath);
            if (metadataEntry == null) {
                throw new FileNotFoundException($"Metadata not found at: {metadataPath}");
            }

            using (var sr = new StreamReader(metadataEntry.Open())) {
                string json = sr.ReadToEnd();
                Record3DMetadata meta = JsonUtility.FromJson<Record3DMetadata>(json);

                _fps = meta.fps;
                _width = meta.w;
                _height = meta.h;

                // The camera intrinsics are in meta.K
                // e.g. K[0] = fx, K[4] = fy, K[6] = tx, K[7] = ty
                _fx = meta.K[0];
                _fy = meta.K[4];
                _tx = meta.K[6];
                _ty = meta.K[7];
            }

            // 3. Count frames
            string depthSearchPrefix = string.IsNullOrEmpty(_captureTitle)
                ? "rgbd/"
                : $"{_captureTitle}/rgbd/";

            // Count .depth
            _frameCount = _zipArchive.Entries.Count(e =>
                e.FullName.StartsWith(depthSearchPrefix) && e.FullName.EndsWith(".depth"));

            // fallback to .bytes
            if (_frameCount == 0) {
                _frameCount = _zipArchive.Entries.Count(e =>
                    e.FullName.StartsWith(depthSearchPrefix) && e.FullName.EndsWith(".bytes"));
            }
        });
    }

    /// <summary>
    /// Asynchronously retrieves the depth buffer bytes for the given frame.
    /// </summary>
    public async Task<byte[]> GetDepthBufferAsync(int frameIndex) {
        return await Task.Run(() => {
            // e.g. "rgbd/{frameIndex}.depth" or "[captureTitle]/rgbd/{frameIndex}.depth"
            string path = string.IsNullOrEmpty(_captureTitle)
                ? $"rgbd/{frameIndex}.depth"
                : $"{_captureTitle}/rgbd/{frameIndex}.depth";

            var entry = _zipArchive.GetEntry(path);
            if (entry == null) {
                throw new FileNotFoundException($"Depth file not found: {path}");
            }

            using (var ms = new MemoryStream())
            using (var es = entry.Open()) {
                es.CopyTo(ms);
                return ms.ToArray();
            }
        });
    }

    /// <summary>
    /// Asynchronously retrieves the color buffer bytes for the given frame.
    /// </summary>
    public async Task<byte[]> GetColorBufferAsync(int frameIndex) {
        return await Task.Run(() => {
            // e.g. "rgbd/{frameIndex}.jpg" or "[captureTitle]/rgbd/{frameIndex}.jpg"
            string path = string.IsNullOrEmpty(_captureTitle)
                ? $"rgbd/{frameIndex}.jpg"
                : $"{_captureTitle}/rgbd/{frameIndex}.jpg";

            var entry = _zipArchive.GetEntry(path);
            if (entry == null) {
                throw new FileNotFoundException($"Color file not found: {path}");
            }

            using (var ms = new MemoryStream())
            using (var es = entry.Open()) {
                es.CopyTo(ms);
                return ms.ToArray();
            }
        });
    }

    /// <summary>
    /// Closes this data source, disposing of the loaded ZipArchive.
    /// </summary>
    public void CloseSource() {
        _zipArchive?.Dispose();
        _zipArchive = null;
    }
}
