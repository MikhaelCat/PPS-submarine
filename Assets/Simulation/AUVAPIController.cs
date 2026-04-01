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

    // === Переменные класса ===
    private readonly Dictionary<int, AUV> auvById = new Dictionary<int, AUV>();
    private readonly object stateLock = new object();
    private Dictionary<int, AUV> auvByIdSnapshot = new Dictionary<int, AUV>();
    private Dictionary<int, MBESData> mbesByAuvId = new Dictionary<int, MBESData>();
    private long nextSnapshotSequence = 1;

    // === Unity жизненный цикл ===

    // Собирает кэш всех AUV при старте
    private void Awake()
    {
        RefreshAUVCache();
        RefreshMBESSnapshots();
    }

    private void LateUpdate()
    {
        RefreshAUVCache();
        RefreshMBESSnapshots();
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
