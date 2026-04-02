using System;
using UnityEngine;
using System.Threading;

[RequireComponent(typeof(Camera))]
public class AUVCamera : MonoBehaviour
{
    private const float SnapshotDemandKeepAliveSeconds = 2f;

    public struct CameraInfo
    {
        public int width;
        public int height;
        public float aspect;
        public bool orthographic;
        public float orthographicSize;
        public float fieldOfView;
        public float nearClipPlane;
        public float farClipPlane;
        public Vector3 worldPosition;
        public Quaternion worldRotation;
        public float capturedAtTime;
        public long sequence;

        public bool IsValid => width > 0 && height > 0;
    }

    public struct SnapshotData
    {
        public CameraInfo info;
        public ReadOnlyMemory<byte> rgba32;

        public bool IsValid => info.IsValid && !rgba32.IsEmpty;
    }

    [SerializeField] private Camera auvcamera;

    protected RenderTexture texture;
    protected Texture2D cpuTexture;

    private AUVSettings settings;
    private byte[] latestSnapshotBytes = Array.Empty<byte>();
    private CameraInfo latestCameraInfo;
    private readonly object snapshotLock = new object();
    private float snapshotInterval = 0f;
    private float snapshotTimer = 0f;
    private long snapshotDemandTicks = 0;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        Init();
        snapshotTimer = snapshotInterval;
    }

    private void LateUpdate()
    {
        Init();

        if (!ShouldCaptureSnapshots())
        {
            return;
        }

        if (snapshotInterval <= 0f)
        {
            RenderSnapshot();
            return;
        }

        snapshotTimer += Time.deltaTime;
        if (snapshotTimer >= snapshotInterval)
        {
            snapshotTimer -= snapshotInterval;
            RenderSnapshot();
        }
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private void Init()
    {
        if (auvcamera == null)
        {
            auvcamera = GetComponent<Camera>();
        }

        if (auvcamera == null)
        {
            return;
        }

        if (settings == null)
        {
            settings = AUVSettings.GetOrFind();
        }

        ApplySettings();
        EnsureTextures();

        auvcamera.targetTexture = texture;
        auvcamera.enabled = false;
    }

    private void ApplySettings()
    {
        if (auvcamera == null)
        {
            return;
        }

        AUVSettings.SensorCameraSettings cameraSettings = settings != null
            ? settings.GetSensorCameraSettings()
            : new AUVSettings.SensorCameraSettings(512, 512, 24, RenderTextureFormat.ARGB32, 1f, true, 2f, 60f, 0.3f, 100f);

        auvcamera.aspect = cameraSettings.aspect;
        auvcamera.orthographic = cameraSettings.orthographic;
        auvcamera.orthographicSize = cameraSettings.orthographicSize;
        auvcamera.fieldOfView = cameraSettings.fieldOfView;
        auvcamera.nearClipPlane = cameraSettings.nearClipPlane;
        auvcamera.farClipPlane = cameraSettings.farClipPlane;
        snapshotInterval = CalculateSnapshotInterval(settings != null ? settings.CameraSnapshotRateHz : 35f);
    }

    private void EnsureTextures()
    {
        AUVSettings.SensorCameraSettings cameraSettings = settings != null
            ? settings.GetSensorCameraSettings()
            : new AUVSettings.SensorCameraSettings(512, 512, 24, RenderTextureFormat.ARGB32, 1f, true, 2f, 60f, 0.3f, 100f);

        int width = cameraSettings.width;
        int height = cameraSettings.height;

        bool renderTextureMatches = texture != null && texture.width == width && texture.height == height;
        bool cpuTextureMatches = cpuTexture != null && cpuTexture.width == width && cpuTexture.height == height;
        if (renderTextureMatches && cpuTextureMatches)
        {
            return;
        }

        ReleaseResources();

        if (settings != null)
        {
            texture = settings.CreateSensorRenderTexture($"{name}_SensorRT");
        }
        else
        {
            texture = new RenderTexture(cameraSettings.width, cameraSettings.height, cameraSettings.depthBufferBits, cameraSettings.renderTextureFormat)
            {
                name = $"{name}_SensorRT"
            };
            texture.Create();
        }

        cpuTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = $"{name}_SensorSnapshot"
        };
    }

    private void ReleaseResources()
    {
        if (auvcamera != null && auvcamera.targetTexture == texture)
        {
            auvcamera.targetTexture = null;
        }

        if (texture != null)
        {
            texture.Release();
            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
            texture = null;
        }

        if (cpuTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(cpuTexture);
            }
            else
            {
                DestroyImmediate(cpuTexture);
            }
            cpuTexture = null;
        }

        lock (snapshotLock)
        {
            latestSnapshotBytes = Array.Empty<byte>();
            latestCameraInfo = default;
        }
        snapshotTimer = 0f;
    }

    private void RenderSnapshot()
    {
        if (auvcamera == null || texture == null || cpuTexture == null)
        {
            return;
        }

        RenderTexture previousActive = RenderTexture.active;

        auvcamera.targetTexture = texture;
        auvcamera.Render();

        RenderTexture.active = texture;
        cpuTexture.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0, false);
        cpuTexture.Apply(false, false);

        var rawData = cpuTexture.GetRawTextureData<byte>();
        lock (snapshotLock)
        {
            if (latestSnapshotBytes.Length != rawData.Length)
            {
                latestSnapshotBytes = new byte[rawData.Length];
            }

            rawData.CopyTo(latestSnapshotBytes);
            latestCameraInfo = new CameraInfo
            {
                width = texture.width,
                height = texture.height,
                aspect = auvcamera.aspect,
                orthographic = auvcamera.orthographic,
                orthographicSize = auvcamera.orthographicSize,
                fieldOfView = auvcamera.fieldOfView,
                nearClipPlane = auvcamera.nearClipPlane,
                farClipPlane = auvcamera.farClipPlane,
                worldPosition = transform.position,
                worldRotation = transform.rotation,
                capturedAtTime = Time.time,
                sequence = latestCameraInfo.sequence + 1
            };
        }

        RenderTexture.active = previousActive;
    }

    public Texture GetPreviewTexture()
    {
        return texture != null ? texture : cpuTexture;
    }

    public Texture2D GetSnapshot()
    {
        return cpuTexture;
    }

    public bool TryGetCameraInfo(out CameraInfo info)
    {
        lock (snapshotLock)
        {
            if (!latestCameraInfo.IsValid)
            {
                info = default;
                return false;
            }

            info = latestCameraInfo;
        }

        return true;
    }

    public bool TryGetSnapshotData(out SnapshotData snapshot)
    {
        MarkSnapshotDemand();

        lock (snapshotLock)
        {
            if (latestSnapshotBytes.Length == 0 || !latestCameraInfo.IsValid)
            {
                snapshot = default;
                return false;
            }

            snapshot = new SnapshotData
            {
                info = latestCameraInfo,
                rgba32 = new ReadOnlyMemory<byte>(latestSnapshotBytes)
            };
        }

        return true;
    }

    public void RequestSnapshot()
    {
        MarkSnapshotDemand();
    }

    private void MarkSnapshotDemand()
    {
        Interlocked.Exchange(ref snapshotDemandTicks, DateTime.UtcNow.Ticks);
    }

    private bool ShouldCaptureSnapshots()
    {
        long observedDemandTicks = Interlocked.Read(ref snapshotDemandTicks);
        if (observedDemandTicks <= 0)
        {
            return false;
        }

        long ageTicks = DateTime.UtcNow.Ticks - observedDemandTicks;
        return ageTicks <= TimeSpan.FromSeconds(SnapshotDemandKeepAliveSeconds).Ticks;
    }

    private static float CalculateSnapshotInterval(float snapshotRateHz)
    {
        float safeRate = Mathf.Max(0.1f, snapshotRateHz);
        if (safeRate >= 1000f)
        {
            return 0f;
        }

        return 1f / safeRate;
    }
}
