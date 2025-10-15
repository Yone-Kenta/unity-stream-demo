using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Captures frames from the assigned camera and uploads them as JPEG images
/// to the configured HTTP endpoint so they can be displayed on a website.
/// </summary>
public sealed class MainCameraStreamer : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField]
    private Camera sourceCamera;

    [Tooltip("Width of the captured frame in pixels.")]
    [SerializeField]
    private int captureWidth = 1280;

    [Tooltip("Height of the captured frame in pixels.")]
    [SerializeField]
    private int captureHeight = 720;

    [Range(1, 100)]
    [Tooltip("JPEG quality for uploaded frames.")]
    [SerializeField]
    private int jpegQuality = 60;

    [Tooltip("Seconds to wait between uploads. Set to 0 for every frame.")]
    [SerializeField]
    private float uploadIntervalSeconds = 0.5f;

    [Header("Networking")]
    [SerializeField]
    private string uploadUrl = "http://localhost:3000/frame";

    [Tooltip("Abort the upload if it takes longer than this many seconds.")]
    [SerializeField]
    private int requestTimeoutSeconds = 5;

    private RenderTexture captureTarget;
    private Texture2D readbackTexture;
    private Coroutine streamingRoutine;

    private void Awake()
    {
        if (sourceCamera == null)
        {
            sourceCamera = Camera.main;
        }

        EnsureBuffers();
    }

    private void OnEnable()
    {
        if (sourceCamera == null)
        {
            Debug.LogWarning("[MainCameraStreamer] No source camera assigned; streaming disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(uploadUrl))
        {
            Debug.LogWarning("[MainCameraStreamer] Upload URL is empty; streaming disabled.");
            return;
        }

        if (streamingRoutine == null)
        {
            streamingRoutine = StartCoroutine(StreamLoop());
        }
    }

    private void OnDisable()
    {
        if (streamingRoutine != null)
        {
            StopCoroutine(streamingRoutine);
            streamingRoutine = null;
        }
    }

    private void OnDestroy()
    {
        if (captureTarget != null)
        {
            captureTarget.Release();
            captureTarget = null;
        }

        if (readbackTexture != null)
        {
            Destroy(readbackTexture);
            readbackTexture = null;
        }
    }

    private IEnumerator StreamLoop()
    {
        var waitForEndOfFrame = new WaitForEndOfFrame();
        var waitBetweenUploads = uploadIntervalSeconds > 0f ? new WaitForSeconds(uploadIntervalSeconds) : null;

        while (enabled && sourceCamera != null)
        {
            yield return waitForEndOfFrame;

            var frameData = CaptureFrame();
            if (frameData != null && frameData.Length > 0)
            {
                yield return UploadFrame(frameData);
            }

            if (waitBetweenUploads != null)
            {
                yield return waitBetweenUploads;
            }
            else
            {
                yield return null;
            }
        }
    }

    private byte[] CaptureFrame()
    {
        EnsureBuffers();

        if (captureTarget == null || readbackTexture == null)
        {
            return null;
        }

        var previousTarget = sourceCamera.targetTexture;
        var previousActive = RenderTexture.active;

        sourceCamera.targetTexture = captureTarget;
        sourceCamera.Render();

        RenderTexture.active = captureTarget;
        readbackTexture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        readbackTexture.Apply();

        RenderTexture.active = previousActive;
        sourceCamera.targetTexture = previousTarget;

        return readbackTexture.EncodeToJPG(jpegQuality);
    }

    private IEnumerator UploadFrame(byte[] payload)
    {
        using var request = new UnityWebRequest(uploadUrl, UnityWebRequest.kHttpVerbPOST);
        request.uploadHandler = new UploadHandlerRaw(payload)
        {
            contentType = "image/jpeg"
        };
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = Mathf.Max(requestTimeoutSeconds, 1);

#if UNITY_2020_1_OR_NEWER
        yield return request.SendWebRequest();
#else
        yield return request.Send();
#endif

        if (!RequestSucceeded(request))
        {
            Debug.LogWarning($"[MainCameraStreamer] Upload failed: {request.error}");
        }
    }

    private void EnsureBuffers()
    {
        captureWidth = Mathf.Clamp(captureWidth, 16, 4096);
        captureHeight = Mathf.Clamp(captureHeight, 16, 4096);
        jpegQuality = Mathf.Clamp(jpegQuality, 1, 100);

        if (captureTarget == null || captureTarget.width != captureWidth || captureTarget.height != captureHeight)
        {
            if (captureTarget != null)
            {
                captureTarget.Release();
            }

            captureTarget = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
            captureTarget.Create();
        }

        if (readbackTexture == null || readbackTexture.width != captureWidth || readbackTexture.height != captureHeight)
        {
            if (readbackTexture != null)
            {
                Destroy(readbackTexture);
            }

            readbackTexture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        }
    }

    private static bool RequestSucceeded(UnityWebRequest request)
    {
#if UNITY_2020_2_OR_NEWER
        return request.result == UnityWebRequest.Result.Success;
#else
        return !(request.isNetworkError || request.isHttpError);
#endif
    }
}
