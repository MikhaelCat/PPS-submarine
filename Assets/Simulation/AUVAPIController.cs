using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


// ДОЛГАЯ ПОДДЕРЖКА ФУНКЦИЙ ячозвфршыв-ршфрш-ыв-=гфо=вы
public class AUVAPIController : MonoBehaviour
{
    public struct MBESPointReading
    {
        public bool hasHit;
        public float range;
        public Vector3 pointWorld;
        public Vector3 pointLocal;
        public Vector3 normalWorld;
    }

    public struct MBESData
    {
        public int auvId;
        public int pointCount;
        public int hitCount;
        public float nearestHitRange;
        public float farthestHitRange;
        public float capturedAtTime;
        public long sequence;
        public MBESPointReading[] points;

        public bool IsValid => points != null;
    }

    public struct SideSonarPointReading
    {
        public bool hasHit;
        public float slantRange;
        public float horizontalRange;
        public float intensity;
        public Vector3 pointWorld;
        public Vector3 pointLocal;
        public Vector3 normalWorld;
    }

    public struct SideSonarData
    {
        public int auvId;
        public int pointsPerSide;
        public int leftHitCount;
        public int rightHitCount;
        public float swathPerSide;
        public float maxRange;
        public float capturedAtTime;
        public long sequence;
        public SideSonarPointReading[] leftLine;
        public SideSonarPointReading[] rightLine;

        public bool IsValid => leftLine != null && rightLine != null;
    }

    public struct AUVCameraData
    {
        public int auvId;
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
        public byte[] rgba32;

        public bool IsValid => rgba32 != null && rgba32.Length > 0 && width > 0 && height > 0;
    }

    // === Переменные класса ===
    private readonly Dictionary<int, AUV> auvById = new Dictionary<int, AUV>();
    private readonly object stateLock = new object();
    private Dictionary<int, AUV> auvByIdSnapshot = new Dictionary<int, AUV>();
    private Dictionary<int, MBESData> mbesByAuvId = new Dictionary<int, MBESData>();
    private Dictionary<int, SideSonarData> sideSonarByAuvId = new Dictionary<int, SideSonarData>();
    private Dictionary<int, AUVCameraData> cameraByAuvId = new Dictionary<int, AUVCameraData>();
    private long nextSnapshotSequence = 1;

    // === Unity жизненный цикл ===

    // Собирает кэш всех AUV при старте
    private void Awake()
    {
        RefreshAUVCache();
        RefreshMBESSnapshots();
        RefreshSideSonarSnapshots();
        RefreshCameraSnapshots();
    }

    private void LateUpdate()
    {
        RefreshAUVCache();
        RefreshMBESSnapshots();
        RefreshSideSonarSnapshots();
        RefreshCameraSnapshots();
    }

    // === Публичный API ===

    // Устанавливает силу конкретному мотору конкретного AUV
    // Возврат: -1 если AUV не найден; иначе коды из AUV.SetMotorForce (0/1/2)
    public int SetAUVMotorSpeed(int auv_id, int motorid, float force)
    {
        if (!TryGetAUV(auv_id, out AUV auv))
        {
            return -1;
        }

        return auv.SetMotorForce(motorid, force);
    }

    // Возвращает список id всех AUV в сцене
    public List<int> GetAUVs()
    {
        RefreshAUVCache();

        lock (stateLock)
        {
            return auvByIdSnapshot.Keys.OrderBy(id => id).ToList();
        }
    }

    // Возвращает список id моторов (общий для всех AUV из AUVSettings)
    public List<int> GetAUVMotorIds()
    {
        return GetMotorIdsFromSettings();
    }

    // Возвращает список id моторов конкретного AUV
    // Если AUV с таким id не найден, возвращает пустой список
    public List<int> GetAUVMotorIds(int auv_id)
    {
        if (!TryGetAUV(auv_id, out _))
        {
            return new List<int>();
        }

        return GetMotorIdsFromSettings();
    }

    // Возвращает последний потокобезопасный снимок MBES для конкретного AUV.
    // Этот метод можно вызывать не из Main Thread: он не обращается к Unity API,
    // а читает только ранее подготовленную копию данных.
    public bool TryGetAUVMBESData(int auv_id, out MBESData mbesData)
    {
        lock (stateLock)
        {
            if (!mbesByAuvId.TryGetValue(auv_id, out MBESData cachedData) || cachedData.points == null)
            {
                mbesData = default;
                return false;
            }

            mbesData = CloneMBESData(cachedData);
            return true;
        }
    }

    // Удобный вариант, который возвращает только массив лучей MBES.
    // Массив также является копией и безопасен для использования в другом потоке.
    public bool TryGetAUVMBESPoints(int auv_id, out MBESPointReading[] points)
    {
        if (TryGetAUVMBESData(auv_id, out MBESData data))
        {
            points = data.points;
            return true;
        }

        points = Array.Empty<MBESPointReading>();
        return false;
    }

    // Возвращает последний потокобезопасный снимок бокового гидролокатора для конкретного AUV.
    // leftLine/rightLine представляют строчки теневой картинки по левому и правому борту.
    public bool TryGetAUVSideSonarData(int auv_id, out SideSonarData sideSonarData)
    {
        lock (stateLock)
        {
            if (!sideSonarByAuvId.TryGetValue(auv_id, out SideSonarData cachedData) || !cachedData.IsValid)
            {
                sideSonarData = default;
                return false;
            }

            sideSonarData = CloneSideSonarData(cachedData);
            return true;
        }
    }

    public bool TryGetAUVSideSonarLines(int auv_id, out SideSonarPointReading[] leftLine, out SideSonarPointReading[] rightLine)
    {
        if (TryGetAUVSideSonarData(auv_id, out SideSonarData data))
        {
            leftLine = data.leftLine;
            rightLine = data.rightLine;
            return true;
        }

        leftLine = Array.Empty<SideSonarPointReading>();
        rightLine = Array.Empty<SideSonarPointReading>();
        return false;
    }

    // Возвращает последний потокобезопасный снимок камеры конкретного AUV.
    // В rgba32 лежит изображение в формате RGBA32, упакованное построчно.
    public bool TryGetAUVCameraData(int auv_id, out AUVCameraData cameraData)
    {
        lock (stateLock)
        {
            if (!cameraByAuvId.TryGetValue(auv_id, out AUVCameraData cachedData) || !cachedData.IsValid)
            {
                cameraData = default;
                return false;
            }

            cameraData = CloneCameraData(cachedData);
            return true;
        }
    }

    public bool TryGetAUVCameraImage(int auv_id, out byte[] rgba32, out int width, out int height)
    {
        if (TryGetAUVCameraData(auv_id, out AUVCameraData cameraData))
        {
            rgba32 = cameraData.rgba32;
            width = cameraData.width;
            height = cameraData.height;
            return true;
        }

        rgba32 = Array.Empty<byte>();
        width = 0;
        height = 0;
        return false;
    }

    // === Вспомогательные функции ===

    // Забирает id моторов из общих настроек AUV
    private static List<int> GetMotorIdsFromSettings()
    {
        AUVSettings settings = AUVSettings.GetOrFind();
        if (settings == null)
        {
            return new List<int>();
        }

        return settings.ForcePoints
            .Select(point => point.id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    // Пересобирает словарь id->AUV
    private void RefreshAUVCache()
    {
        auvById.Clear();

        AUV[] auvs = UnityEngine.Object.FindObjectsByType<AUV>(FindObjectsInactive.Exclude);
        for (int i = 0; i < auvs.Length; i++)
        {
            AUV auv = auvs[i];
            if (auv == null)
            {
                continue;
            }

            if (!auvById.ContainsKey(auv.id))
            {
                auvById.Add(auv.id, auv);
            }
            else
            {
                Debug.LogWarning($"Duplicate AUV id detected: {auv.id}. Only the first instance will be used by AUVAPIController.");
            }
        }

        lock (stateLock)
        {
            auvByIdSnapshot = new Dictionary<int, AUV>(auvById);
        }
    }

    private void RefreshMBESSnapshots()
    {
        Dictionary<int, MBESData> freshSnapshots = new Dictionary<int, MBESData>(auvById.Count);

        foreach (KeyValuePair<int, AUV> pair in auvById)
        {
            AUV auv = pair.Value;
            if (auv == null)
            {
                continue;
            }

            freshSnapshots[pair.Key] = BuildMBESSnapshot(auv);
        }

        lock (stateLock)
        {
            mbesByAuvId = freshSnapshots;
        }
    }

    private void RefreshSideSonarSnapshots()
    {
        Dictionary<int, SideSonarData> freshSnapshots = new Dictionary<int, SideSonarData>(auvById.Count);

        foreach (KeyValuePair<int, AUV> pair in auvById)
        {
            AUV auv = pair.Value;
            if (auv == null)
            {
                continue;
            }

            freshSnapshots[pair.Key] = BuildSideSonarSnapshot(auv);
        }

        lock (stateLock)
        {
            sideSonarByAuvId = freshSnapshots;
        }
    }

    private void RefreshCameraSnapshots()
    {
        Dictionary<int, AUVCameraData> freshSnapshots = new Dictionary<int, AUVCameraData>(auvById.Count);

        foreach (KeyValuePair<int, AUV> pair in auvById)
        {
            AUV auv = pair.Value;
            if (auv == null || !auv.TryGetCameraSnapshot(out AUVCamera.SnapshotData snapshot) || !snapshot.IsValid)
            {
                continue;
            }

            AUVCamera.CameraInfo info = snapshot.info;
            freshSnapshots[pair.Key] = new AUVCameraData
            {
                auvId = auv.id,
                width = info.width,
                height = info.height,
                aspect = info.aspect,
                orthographic = info.orthographic,
                orthographicSize = info.orthographicSize,
                fieldOfView = info.fieldOfView,
                nearClipPlane = info.nearClipPlane,
                farClipPlane = info.farClipPlane,
                worldPosition = info.worldPosition,
                worldRotation = info.worldRotation,
                capturedAtTime = info.capturedAtTime,
                sequence = info.sequence,
                rgba32 = snapshot.rgba32
            };
        }

        lock (stateLock)
        {
            cameraByAuvId = freshSnapshots;
        }
    }

    private MBESData BuildMBESSnapshot(AUV auv)
    {
        int pointCount = Mathf.Max(0, auv.GetMBESPointCount());
        MBESPointReading[] points = new MBESPointReading[pointCount];

        int hitCount = 0;
        float nearestHitRange = 0f;
        float farthestHitRange = 0f;
        bool hasAnyHit = false;

        for (int i = 0; i < pointCount; i++)
        {
            if (!auv.TryGetMBESHit(i, out AUV.MBESHit hit))
            {
                continue;
            }

            points[i] = new MBESPointReading
            {
                hasHit = hit.hasHit,
                range = hit.range,
                pointWorld = hit.pointWorld,
                pointLocal = hit.pointLocal,
                normalWorld = hit.normalWorld
            };

            if (!hit.hasHit)
            {
                continue;
            }

            hitCount++;
            if (!hasAnyHit)
            {
                nearestHitRange = hit.range;
                farthestHitRange = hit.range;
                hasAnyHit = true;
            }
            else
            {
                if (hit.range < nearestHitRange)
                {
                    nearestHitRange = hit.range;
                }

                if (hit.range > farthestHitRange)
                {
                    farthestHitRange = hit.range;
                }
            }
        }

        if (!hasAnyHit)
        {
            nearestHitRange = 0f;
            farthestHitRange = 0f;
        }

        return new MBESData
        {
            auvId = auv.id,
            pointCount = pointCount,
            hitCount = hitCount,
            nearestHitRange = nearestHitRange,
            farthestHitRange = farthestHitRange,
            capturedAtTime = Time.time,
            sequence = nextSnapshotSequence++,
            points = points
        };
    }

    private SideSonarData BuildSideSonarSnapshot(AUV auv)
    {
        int pointsPerSide = Mathf.Max(0, auv.GetSideSonarPointCount());
        SideSonarPointReading[] leftLine = new SideSonarPointReading[pointsPerSide];
        SideSonarPointReading[] rightLine = new SideSonarPointReading[pointsPerSide];

        int leftHitCount = 0;
        int rightHitCount = 0;

        for (int i = 0; i < pointsPerSide; i++)
        {
            if (auv.TryGetSideSonarHit(AUV.SideSonarSide.Left, i, out AUV.SideSonarHit leftHit))
            {
                leftLine[i] = new SideSonarPointReading
                {
                    hasHit = leftHit.hasHit,
                    slantRange = leftHit.range,
                    horizontalRange = leftHit.horizontalRange,
                    intensity = leftHit.intensity,
                    pointWorld = leftHit.pointWorld,
                    pointLocal = leftHit.pointLocal,
                    normalWorld = leftHit.normalWorld
                };

                if (leftHit.hasHit)
                {
                    leftHitCount++;
                }
            }

            if (auv.TryGetSideSonarHit(AUV.SideSonarSide.Right, i, out AUV.SideSonarHit rightHit))
            {
                rightLine[i] = new SideSonarPointReading
                {
                    hasHit = rightHit.hasHit,
                    slantRange = rightHit.range,
                    horizontalRange = rightHit.horizontalRange,
                    intensity = rightHit.intensity,
                    pointWorld = rightHit.pointWorld,
                    pointLocal = rightHit.pointLocal,
                    normalWorld = rightHit.normalWorld
                };

                if (rightHit.hasHit)
                {
                    rightHitCount++;
                }
            }
        }

        return new SideSonarData
        {
            auvId = auv.id,
            pointsPerSide = pointsPerSide,
            leftHitCount = leftHitCount,
            rightHitCount = rightHitCount,
            swathPerSide = auv.GetSideSonarSwathPerSide(),
            maxRange = auv.GetSideSonarMaxRange(),
            capturedAtTime = Time.time,
            sequence = nextSnapshotSequence++,
            leftLine = leftLine,
            rightLine = rightLine
        };
    }

    private static MBESData CloneMBESData(MBESData source)
    {
        return new MBESData
        {
            auvId = source.auvId,
            pointCount = source.pointCount,
            hitCount = source.hitCount,
            nearestHitRange = source.nearestHitRange,
            farthestHitRange = source.farthestHitRange,
            capturedAtTime = source.capturedAtTime,
            sequence = source.sequence,
            points = source.points != null ? (MBESPointReading[])source.points.Clone() : Array.Empty<MBESPointReading>()
        };
    }

    private static SideSonarData CloneSideSonarData(SideSonarData source)
    {
        return new SideSonarData
        {
            auvId = source.auvId,
            pointsPerSide = source.pointsPerSide,
            leftHitCount = source.leftHitCount,
            rightHitCount = source.rightHitCount,
            swathPerSide = source.swathPerSide,
            maxRange = source.maxRange,
            capturedAtTime = source.capturedAtTime,
            sequence = source.sequence,
            leftLine = source.leftLine != null ? (SideSonarPointReading[])source.leftLine.Clone() : Array.Empty<SideSonarPointReading>(),
            rightLine = source.rightLine != null ? (SideSonarPointReading[])source.rightLine.Clone() : Array.Empty<SideSonarPointReading>()
        };
    }

    private static AUVCameraData CloneCameraData(AUVCameraData source)
    {
        return new AUVCameraData
        {
            auvId = source.auvId,
            width = source.width,
            height = source.height,
            aspect = source.aspect,
            orthographic = source.orthographic,
            orthographicSize = source.orthographicSize,
            fieldOfView = source.fieldOfView,
            nearClipPlane = source.nearClipPlane,
            farClipPlane = source.farClipPlane,
            worldPosition = source.worldPosition,
            worldRotation = source.worldRotation,
            capturedAtTime = source.capturedAtTime,
            sequence = source.sequence,
            rgba32 = source.rgba32 != null ? (byte[])source.rgba32.Clone() : Array.Empty<byte>()
        };
    }

    // Находит AUV по id, при необходимости обновляет кэш
    private bool TryGetAUV(int auvId, out AUV auv)
    {
        lock (stateLock)
        {
            if (auvByIdSnapshot.TryGetValue(auvId, out auv) && auv != null)
            {
                return true;
            }
        }

        RefreshAUVCache();

        lock (stateLock)
        {
            return auvByIdSnapshot.TryGetValue(auvId, out auv) && auv != null;
        }
    }
}
