using UnityEngine;
using System;
using Unity.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

public partial class Record3DPlayback
{
    #region Texture Initialization
    private void ReinitializeTextures(int width, int height)
    {
        CleanupTextures();
        CreateTextures(width, height);
        InitializeVisualEffects(width, height);
        StartConsumerThread();
    }

    private void CleanupTextures()
    {
        if (positionTex) DestroyImmediate(positionTex);
        if (colorTex) DestroyImmediate(colorTex);
        if (colorTexBG) DestroyImmediate(colorTexBG);

        positionTex = colorTex = colorTexBG = null;
        Resources.UnloadUnusedAssets();
    }

    private void CreateTextures(int width, int height)
    {
        positionTex = new Texture2D(width, height, TextureFormat.RGBAFloat, false)
        {
            filterMode = FilterMode.Point
        };
        colorTex = new Texture2D(width, height, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Point
        };
        colorTexBG = new Texture2D(width, height, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Point
        };
        numParticles = width * height;
    }

    private void InitializeVisualEffects(int width, int height)
    {
        foreach (var effect in streamEffects)
        {
            effect.SetInt("Number of Particles", numParticles);
            effect.SetTexture("Particle Position Texture", positionTex);
            effect.SetTexture("Particle Color Texture",
                effect == streamEffects[0] ? colorTex : colorTexBG);
        }
    }

    private void StartConsumerThread()
    {
        consumerThread = new Thread(ConsumerCaptureBufferTaskStart)
        {
            IsBackground = true
        };
        consumerThread.Start();
    }
    #endregion

    #region Frame Loading
    public void LoadFrame(int frameNumber)
    {
        if (!isPlaying_) return;
        _ = currentVideo_.LoadFrameDataAsync(frameNumber);

        currentFrame_ = frameNumber;
        UpdateTexturesFromCurrentVideo();
    }

    public async void LoadFrameAsync(int frameNumber)
    {
        if (!isPlaying_) return;

        // Produce frame data on background thread
        await Task.Run(() => currentVideo_.FrameDataProduce(frameNumber));

        currentFrame_ = frameNumber;
        LoadFrameDataMainThread();
    }

    private void LoadFrameDataMainThread()
    {
        // Depth
        positionTex.SetPixelData(currentVideo_.DataLayer.positionsBuffer, 0);
        positionTex.Apply(false, false);

        // Color
        colorTex.SetPixelData(currentVideo_.DataLayer.rgbBuffer, 0);
        colorTex.Apply(false, false);

        // BG color
        if (currentVideo_.rgbBufferBG != null)
        {
            colorTexBG.SetPixelData(currentVideo_.rgbBufferBG, 0);
            colorTexBG.Apply(false, false);
        }
    }

    private void UpdateTexturesFromCurrentVideo()
    {
        if (currentVideo_ == null) return;

        // If positionsBuffer was populated (e.g. after decompression),
        // copy into positionTex
        if (currentVideo_.positionsBuffer != null && currentVideo_.positionsBuffer.Length > 0) {
            positionTex.SetPixelData(currentVideo_.positionsBuffer, 0);
            positionTex.Apply(false, false);
        }

        // If rgbBuffer was populated, copy to colorTex
        if (currentVideo_.rgbBuffer != null && currentVideo_.rgbBuffer.Length > 0) {
            colorTex.SetPixelData(currentVideo_.rgbBuffer, 0);
            colorTex.Apply(false, false);
        }

        // If you use a separate background buffer
        if (currentVideo_.rgbBufferBG != null && currentVideo_.rgbBufferBG.Length > 0) {
            colorTexBG.SetPixelData(currentVideo_.rgbBufferBG, 0);
            colorTexBG.Apply(false, false);
        }
    }
    #endregion

    #region Timer & Events
    public void OnTimerTick(object sender, ElapsedEventArgs e)
    {
        shouldRefresh_ = true;
    }

    public void OnLoadDepthEvent(float[] positions)
    {
        positionTex.SetPixelData(positions, 0);
        positionTex.Apply(false, false);
    }

    public void OnLoadColorEvent(byte[] colors)
    {
        colorTex.SetPixelData(colors, 0);
        colorTex.Apply(false, false);
    }
    #endregion

    #region Threading
    protected void ConsumerCaptureBufferTaskStart()
    {
        var consumerCaptureTask = Task.Run(() =>
            currentVideo_.DataLayer.ConsumerCaptureData().Wait());
    }

    private void StopServerThread()
    {
        if (consumerThread?.IsAlive == true)
        {
            consumerThread.Abort();
            consumerThread = null;
        }
        if (currentVideo_?.DataLayer != null)
        {
            currentVideo_.DataLayer.encodedBuffer = null;
        }
    }

    private void OnDestroy()
    {
        StopServerThread();
        if (currentVideo_ != null)
            currentVideo_.CloseVideo();
    }
    #endregion

    #region Image Processing Methods (Optional)
    public static void RotateImage(Texture2D tex, float angleDegrees)
    {
        // Same body as original
        // ...
    }
    #endregion
}

