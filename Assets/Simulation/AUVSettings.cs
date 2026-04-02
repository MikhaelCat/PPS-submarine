using System;
using UnityEngine;

// Общие настройки для всех AUV в сцене
public class AUVSettings : MonoBehaviour
{
    private const float DefaultMaxPower = 10000f;
    private const int DefaultMBESPointsCount = 1024;
    private const int DefaultMBESDistance = 80;
    private const float DefaultMBESMaxRange = 200f;
    private const float DefaultMBESPublishRateHz = 35f;
    private const float LegacyDefaultSensorRateHz = 60f;
    private static readonly Vector3 DefaultMBESLookDirection = Vector3.down;
    private static readonly Vector3 DefaultMBESSpanDirection = Vector3.right;
    private const float DefaultSideSonarMaxRange = 200f;
    private const int DefaultSideSonarPointsPerSide = 512;
    private const float DefaultSideSonarSwathPerSide = 100f;
    private const float DefaultSideSonarDownAngleDegrees = 45f;
    private const float DefaultSideSonarDistanceAttenuation = 0.05f;
    private const float DefaultSideSonarPublishRateHz = 35f;
    private const int DefaultCameraWidth = 128;
    private const int DefaultCameraHeight = 128;
    private const float DefaultCameraAspect = 1f;
    private const bool DefaultCameraOrthographic = true;
    private const float DefaultCameraOrthographicSize = 2f;
    private const float DefaultCameraFieldOfView = 60f;
    private const float DefaultCameraNearClipPlane = 0.3f;
    private const float DefaultCameraFarClipPlane = 100f;
    private const float DefaultCameraSnapshotRateHz = 35f;

    // === Структуры данных ===
    [Serializable]
    public struct ForcePoint
    {
        [SerializeField] public int id;
        [SerializeField] public Vector3 localPoint;
        [SerializeField] public Vector3 localDirection;
    }

    [Serializable]
    public struct SensorCameraSettings
    {
        public int width;
        public int height;
        public int depthBufferBits;
        public RenderTextureFormat renderTextureFormat;
        public float aspect;
        public bool orthographic;
        public float orthographicSize;
        public float fieldOfView;
        public float nearClipPlane;
        public float farClipPlane;

        public SensorCameraSettings(
            int width,
            int height,
            int depthBufferBits,
            RenderTextureFormat renderTextureFormat,
            float aspect,
            bool orthographic,
            float orthographicSize,
            float fieldOfView,
            float nearClipPlane,
            float farClipPlane)
        {
            this.width = width;
            this.height = height;
            this.depthBufferBits = depthBufferBits;
            this.renderTextureFormat = renderTextureFormat;
            this.aspect = aspect;
            this.orthographic = orthographic;
            this.orthographicSize = orthographicSize;
            this.fieldOfView = fieldOfView;
            this.nearClipPlane = nearClipPlane;
            this.farClipPlane = farClipPlane;
        }
    }
    
    // === Unity параметры ===
    [Header("Motors")]
    [SerializeField] private float maxPower = DefaultMaxPower;
    [SerializeField] private ForcePoint[] forcePoints = CreateDefaultForcePoints();

    [Header("MBES")]
    [SerializeField] public int MBESPointsCount = DefaultMBESPointsCount; // количество точек
    [SerializeField] public int MBESDistance = DefaultMBESDistance; // ширина в метрах
    [SerializeField] public float MBESMaxRange = 200f; // максимальная длина луча
    [SerializeField] public float MBESPublishRateHz = DefaultMBESPublishRateHz; // частота публикации MBES
    [SerializeField] public Vector3 MBESLookDirection = new Vector3(0f, -1f, 0f); // куда смотрит центральный луч в локальных координатах MBESPoint
    [SerializeField] public Vector3 MBESSpanDirection = new Vector3(1f, 0f, 0f); // вдоль какой оси строится полоса в локальных координатах MBESPoint
    
    [Header("Camera")]
    [SerializeField] private int cameraWidth = DefaultCameraWidth;
    [SerializeField] private int cameraHeight = DefaultCameraHeight;
    [SerializeField] private int cameraDepthBufferBits = 24;
    [SerializeField] private RenderTextureFormat cameraRenderTextureFormat = RenderTextureFormat.ARGB32;
    [SerializeField] private float cameraAspect = DefaultCameraAspect;
    [SerializeField] private bool cameraOrthographic = DefaultCameraOrthographic;
    [SerializeField] private float cameraOrthographicSize = DefaultCameraOrthographicSize;
    [SerializeField] private float cameraFieldOfView = DefaultCameraFieldOfView;
    [SerializeField] private float cameraNearClipPlane = DefaultCameraNearClipPlane;
    [SerializeField] private float cameraFarClipPlane = DefaultCameraFarClipPlane;
    [SerializeField] private float cameraSnapshotRateHz = DefaultCameraSnapshotRateHz;
    
    [Header("Side Sonars")]
    [SerializeField] public float SideSonarMaxRange = DefaultSideSonarMaxRange;
    [SerializeField] public int SideSonarPointsPerSide = DefaultSideSonarPointsPerSide;
    [SerializeField] public float SideSonarSwathPerSide = DefaultSideSonarSwathPerSide;
    [SerializeField] public float SideSonarDownAngleDegrees = DefaultSideSonarDownAngleDegrees;
    [SerializeField] public float SideSonarDistanceAttenuation = DefaultSideSonarDistanceAttenuation;
    [SerializeField] public float SideSonarPublishRateHz = DefaultSideSonarPublishRateHz;

    // === Переменные класса ===
    private static AUVSettings shared;


    // Публичные свойства для чтения параметров
    public float MaxPower => maxPower;
    public ForcePoint[] ForcePoints => forcePoints ?? Array.Empty<ForcePoint>();
    public int CameraWidth => cameraWidth;
    public int CameraHeight => cameraHeight;
    public int CameraDepthBufferBits => cameraDepthBufferBits;
    public RenderTextureFormat CameraRenderTextureFormat => cameraRenderTextureFormat;
    public float CameraAspect => cameraAspect;
    public bool CameraOrthographic => cameraOrthographic;
    public float CameraOrthographicSize => cameraOrthographicSize;
    public float CameraFieldOfView => cameraFieldOfView;
    public float CameraNearClipPlane => cameraNearClipPlane;
    public float CameraFarClipPlane => cameraFarClipPlane;
    public float CameraSnapshotRateHz => cameraSnapshotRateHz;

    public SensorCameraSettings GetSensorCameraSettings()
    {
        return new SensorCameraSettings(
            cameraWidth,
            cameraHeight,
            cameraDepthBufferBits,
            cameraRenderTextureFormat,
            cameraAspect,
            cameraOrthographic,
            cameraOrthographicSize,
            cameraFieldOfView,
            cameraNearClipPlane,
            cameraFarClipPlane);
    }

    public RenderTexture CreateSensorRenderTexture(string textureName = null)
    {
        SensorCameraSettings cameraSettings = GetSensorCameraSettings();
        RenderTexture renderTexture = new RenderTexture(
            cameraSettings.width,
            cameraSettings.height,
            cameraSettings.depthBufferBits,
            cameraSettings.renderTextureFormat)
        {
            name = string.IsNullOrWhiteSpace(textureName) ? "AUVSensorRT" : textureName
        };
        renderTexture.Create();
        return renderTexture;
    }

    private static ForcePoint[] CreateDefaultForcePoints()
    {
        return new[]
        {
            new ForcePoint
            {
                id = 1,
                localPoint = new Vector3(-2.5943f, -0.2098f, 3.5286f),
                localDirection = Vector3.right,
            },
            new ForcePoint
            {
                id = 2,
                localPoint = new Vector3(-2.5943f, -0.2109f, -3.5237f),
                localDirection = Vector3.right,
            },
            new ForcePoint
            {
                id = 3,
                localPoint = new Vector3(-1.5834f, 1.1273f, -0.0439f),
                localDirection = Vector3.up,
            },
            new ForcePoint
            {
                id = 4,
                localPoint = new Vector3(-11.7923f, 1.1273f, -0.0439f),
                localDirection = Vector3.up,
            },
        };
    }

    private void EnsureDefaultsIfNeeded()
    {
        if (maxPower <= 0f)
        {
            maxPower = DefaultMaxPower;
        }

        if (forcePoints == null || forcePoints.Length == 0)
        {
            forcePoints = CreateDefaultForcePoints();
        }

        if (MBESPointsCount < 2)
        {
            MBESPointsCount = DefaultMBESPointsCount;
        }

        MBESDistance = Mathf.Clamp(MBESDistance, 1, 100);

        if (MBESMaxRange <= 0f)
        {
            MBESMaxRange = DefaultMBESMaxRange;
        }
        if (Mathf.Approximately(MBESPublishRateHz, LegacyDefaultSensorRateHz))
        {
            MBESPublishRateHz = DefaultMBESPublishRateHz;
        }
        MBESPublishRateHz = Mathf.Max(0.1f, MBESPublishRateHz);

        if (MBESLookDirection.sqrMagnitude < 0.0001f)
        {
            MBESLookDirection = DefaultMBESLookDirection;
        }

        if (MBESSpanDirection.sqrMagnitude < 0.0001f)
        {
            MBESSpanDirection = DefaultMBESSpanDirection;
        }

        if (SideSonarMaxRange <= 0f)
        {
            SideSonarMaxRange = DefaultSideSonarMaxRange;
        }

        SideSonarPointsPerSide = Mathf.Max(8, SideSonarPointsPerSide);
        SideSonarSwathPerSide = Mathf.Max(1f, SideSonarSwathPerSide);
        SideSonarDownAngleDegrees = Mathf.Clamp(SideSonarDownAngleDegrees, 1f, 89f);
        SideSonarDistanceAttenuation = Mathf.Max(0f, SideSonarDistanceAttenuation);
        if (Mathf.Approximately(SideSonarPublishRateHz, LegacyDefaultSensorRateHz))
        {
            SideSonarPublishRateHz = DefaultSideSonarPublishRateHz;
        }
        SideSonarPublishRateHz = Mathf.Max(0.1f, SideSonarPublishRateHz);

        cameraWidth = Mathf.Max(16, cameraWidth);
        cameraHeight = Mathf.Max(16, cameraHeight);
        cameraDepthBufferBits = Mathf.Max(0, cameraDepthBufferBits);

        if (cameraAspect <= 0f)
        {
            cameraAspect = DefaultCameraAspect;
        }

        if (cameraOrthographicSize <= 0f)
        {
            cameraOrthographicSize = DefaultCameraOrthographicSize;
        }

        cameraFieldOfView = Mathf.Clamp(cameraFieldOfView, 1f, 179f);

        if (cameraNearClipPlane <= 0f)
        {
            cameraNearClipPlane = DefaultCameraNearClipPlane;
        }

        if (cameraFarClipPlane <= cameraNearClipPlane)
        {
            cameraFarClipPlane = Mathf.Max(DefaultCameraFarClipPlane, cameraNearClipPlane + 0.1f);
        }

        if (Mathf.Approximately(cameraSnapshotRateHz, LegacyDefaultSensorRateHz))
        {
            cameraSnapshotRateHz = DefaultCameraSnapshotRateHz;
        }
        cameraSnapshotRateHz = Mathf.Max(0.1f, cameraSnapshotRateHz);
    }

    private void Reset()
    {
        maxPower = DefaultMaxPower;
        forcePoints = CreateDefaultForcePoints();
        MBESPointsCount = DefaultMBESPointsCount;
        MBESDistance = DefaultMBESDistance;
        MBESMaxRange = DefaultMBESMaxRange;
        MBESPublishRateHz = DefaultMBESPublishRateHz;
        MBESLookDirection = DefaultMBESLookDirection;
        MBESSpanDirection = DefaultMBESSpanDirection;
        SideSonarMaxRange = DefaultSideSonarMaxRange;
        SideSonarPointsPerSide = DefaultSideSonarPointsPerSide;
        SideSonarSwathPerSide = DefaultSideSonarSwathPerSide;
        SideSonarDownAngleDegrees = DefaultSideSonarDownAngleDegrees;
        SideSonarDistanceAttenuation = DefaultSideSonarDistanceAttenuation;
        SideSonarPublishRateHz = DefaultSideSonarPublishRateHz;
        cameraWidth = DefaultCameraWidth;
        cameraHeight = DefaultCameraHeight;
        cameraDepthBufferBits = 24;
        cameraRenderTextureFormat = RenderTextureFormat.ARGB32;
        cameraAspect = DefaultCameraAspect;
        cameraOrthographic = DefaultCameraOrthographic;
        cameraOrthographicSize = DefaultCameraOrthographicSize;
        cameraFieldOfView = DefaultCameraFieldOfView;
        cameraNearClipPlane = DefaultCameraNearClipPlane;
        cameraFarClipPlane = DefaultCameraFarClipPlane;
        cameraSnapshotRateHz = DefaultCameraSnapshotRateHz;
    }

    // Регистрирует общий экземпляр настроек
    private void Awake()
    {
        EnsureDefaultsIfNeeded();

        if (shared != null && shared != this)
        {
            Debug.LogWarning("More than one AUVSettings exists in the scene. The first instance will be used.");
            return;
        }

        shared = this;
    }

    // Возвращает общий экземпляр настроек
    public static AUVSettings GetOrFind()
    {
        if (shared != null)
        {
            return shared;
        }

        shared = UnityEngine.Object.FindAnyObjectByType<AUVSettings>();
        return shared;
    }

    private void OnValidate()
    {
        EnsureDefaultsIfNeeded();
    }
}
