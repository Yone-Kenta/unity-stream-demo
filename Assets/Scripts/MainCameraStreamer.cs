using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Captures frames from the assigned camera and uploads them as JPEG images
/// to the configured endpoint so they can be displayed in a browser.
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

    [Tooltip("Identifier for this stream. When not 'main', it is appended to the upload URL.")]
    [SerializeField]
    private string cameraId = "main";

    [Tooltip("Abort the upload if it takes longer than this many seconds.")]
    [SerializeField]
    private int requestTimeoutSeconds = 5;

    [Header("Screenshot Capture")]
    [SerializeField]
    private bool enableScreenshotUpload = true;

    [SerializeField]
    private string screenshotUploadUrl = "http://localhost:3000/screenshots";

    [SerializeField]
    private KeyCode screenshotKey = KeyCode.S;

    [Tooltip("Minimum seconds between screenshot uploads.")]
    [SerializeField]
    private float screenshotCooldownSeconds = 1f;

    private RenderTexture captureTarget;
    private Texture2D readbackTexture;
    private Coroutine streamingRoutine;
    private string resolvedUploadUrl;
    private string resolvedCameraId = "main";
    private string resolvedScreenshotUrl;
    private float lastScreenshotTime;

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
        resolvedCameraId = NormalizeCameraId(cameraId);

        if (sourceCamera == null)
        {
            Debug.LogWarning($"[MainCameraStreamer] No source camera assigned; streaming disabled for camera '{resolvedCameraId}'.");
            return;
        }

        resolvedUploadUrl = ResolveUploadUrl(resolvedCameraId);
        resolvedScreenshotUrl = ResolveScreenshotUrl();
        if (string.IsNullOrWhiteSpace(resolvedUploadUrl))
        {
            Debug.LogWarning($"[MainCameraStreamer] Upload URL is empty; streaming disabled for camera '{resolvedCameraId}'.");
            return;
        }

        if (streamingRoutine == null)
        {
            streamingRoutine = StartCoroutine(StreamLoop());
        }
    }

    private void Update()
    {
        if (!enableScreenshotUpload || string.IsNullOrEmpty(resolvedScreenshotUrl) || sourceCamera == null)
        {
            return;
        }

        if (Input.GetKeyDown(screenshotKey))
        {
            var cooldown = Mathf.Max(screenshotCooldownSeconds, 0f);
            if (cooldown > 0f && Time.unscaledTime - lastScreenshotTime < cooldown)
            {
                return;
            }

            var frameData = CaptureFrame();
            if (frameData != null && frameData.Length > 0)
            {
                lastScreenshotTime = Time.unscaledTime;
                StartCoroutine(UploadScreenshot(frameData));
            }
        }
    }

    private void OnDisable()
    {
        if (streamingRoutine != null)
        {
            StopCoroutine(streamingRoutine);
            streamingRoutine = null;
        }

        resolvedUploadUrl = string.Empty;
        resolvedScreenshotUrl = string.Empty;
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

        resolvedUploadUrl = string.Empty;
        resolvedScreenshotUrl = string.Empty;
    }

    private IEnumerator StreamLoop()
    {
        var waitForEndOfFrame = new WaitForEndOfFrame();
        var waitBetweenUploads = uploadIntervalSeconds > 0f ? new WaitForSeconds(uploadIntervalSeconds) : null;

        while (enabled && sourceCamera != null)
        {
            if (string.IsNullOrEmpty(resolvedUploadUrl))
            {
                yield return null;
                continue;
            }

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
        if (payload == null || payload.Length == 0 || string.IsNullOrEmpty(resolvedUploadUrl))
        {
            yield break;
        }

        using var request = new UnityWebRequest(resolvedUploadUrl, UnityWebRequest.kHttpVerbPOST);
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
            Debug.LogWarning($"[MainCameraStreamer] Upload failed for camera '{resolvedCameraId}': {request.error}");
        }
    }

    private IEnumerator UploadScreenshot(byte[] payload)
    {
        if (payload == null || payload.Length == 0 || string.IsNullOrEmpty(resolvedScreenshotUrl))
        {
            yield break;
        }

        using var request = new UnityWebRequest(resolvedScreenshotUrl, UnityWebRequest.kHttpVerbPOST);
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
            Debug.LogWarning($"[MainCameraStreamer] Screenshot upload failed for camera '{resolvedCameraId}': {request.error}");
        }
        else
        {
            Debug.Log($"[MainCameraStreamer] Screenshot uploaded for camera '{resolvedCameraId}'.");
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

    private static string NormalizeCameraId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "main";
        }

        string trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);

        foreach (char ch in trimmed)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
            {
                builder.Append(ch);
                if (builder.Length >= 64)
                {
                    break;
                }
            }
        }

        return builder.Length > 0 ? builder.ToString() : "main";
    }

    private string ResolveUploadUrl(string normalizedCameraId)
    {
        string baseUrl = uploadUrl?.Trim();
        if (string.IsNullOrEmpty(baseUrl))
        {
            return string.Empty;
        }

        string sanitizedBase = baseUrl.TrimEnd('/');

        if (string.Equals(normalizedCameraId, "main", StringComparison.OrdinalIgnoreCase))
        {
            return sanitizedBase;
        }

        if (sanitizedBase.EndsWith("/" + normalizedCameraId, StringComparison.OrdinalIgnoreCase))
        {
            return sanitizedBase;
        }

        return $"{sanitizedBase}/{normalizedCameraId}";
    }

    private string ResolveScreenshotUrl()
    {
        if (!enableScreenshotUpload)
        {
            return string.Empty;
        }

        string baseUrl = screenshotUploadUrl?.Trim();
        if (string.IsNullOrEmpty(baseUrl))
        {
            return string.Empty;
        }

        return baseUrl.TrimEnd('/');
    }

    private static bool RequestSucceeded(UnityWebRequest request)
    {
#if UNITY_2020_2_OR_NEWER
        return request.result == UnityWebRequest.Result.Success;
#else
        return !(request.isNetworkError || request.isHttpError);
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        cameraId = NormalizeCameraId(cameraId);
    }
#endif
}
