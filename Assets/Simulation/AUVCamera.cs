using System;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AUVCamera : MonoBehaviour
{
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
        public byte[] rgba32;

        public bool IsValid => info.IsValid && rgba32 != null && rgba32.Length > 0;
    }

    [SerializeField] private Camera auvcamera;

    protected RenderTexture texture;
    protected Texture2D cpuTexture;

    private AUVSettings settings;
    private byte[] latestSnapshotBytes = Array.Empty<byte>();
    private float lastCapturedAtTime;
    private long snapshotSequence;

    private void Awake()
    {
        Init();
    }

    private void OnEnable()
    {
        Init();
    }

    private void LateUpdate()
    {
        Init();
        RenderSnapshot();
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

        latestSnapshotBytes = Array.Empty<byte>();
        lastCapturedAtTime = 0f;
        snapshotSequence = 0;
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
        if (latestSnapshotBytes.Length != rawData.Length)
        {
            latestSnapshotBytes = new byte[rawData.Length];
        }

        rawData.CopyTo(latestSnapshotBytes);
        snapshotSequence += 1;
        lastCapturedAtTime = Time.time;

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
        if (auvcamera == null || texture == null || cpuTexture == null)
        {
            info = default;
            return false;
        }

        info = new CameraInfo
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
            capturedAtTime = lastCapturedAtTime,
            sequence = snapshotSequence
        };
        return true;
    }

    public bool TryGetSnapshotData(out SnapshotData snapshot)
    {
        if (!TryGetCameraInfo(out CameraInfo info) || latestSnapshotBytes.Length == 0)
        {
            snapshot = default;
            return false;
        }

        snapshot = new SnapshotData
        {
            info = info,
            rgba32 = (byte[])latestSnapshotBytes.Clone()
        };
        return true;
    }
}
