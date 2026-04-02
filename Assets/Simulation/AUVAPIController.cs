using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;


// ДОЛГАЯ ПОДДЕРЖКА ФУНКЦИЙ ячозвфршыв-ршфрш-ыв-=гфо=вы
public class AUVAPIController : MonoBehaviour
{
    private const float FallbackSnapshotRefreshRateHz = 35f;
    private const float SensorDemandKeepAliveSeconds = 2f;

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
        public ReadOnlyMemory<byte> rgba32;

        public bool IsValid => !rgba32.IsEmpty && width > 0 && height > 0;
    }

    private struct TeleportRequest
    {
        public int auvId;
        public Vector3 worldPosition;
        public Vector3 worldEulerAngles;
    }

    // === Переменные класса ===
    private readonly Dictionary<int, AUV> auvById = new Dictionary<int, AUV>();
    private readonly object stateLock = new object();
    private readonly object commandQueueLock = new object();
    private Dictionary<int, AUV> auvByIdSnapshot = new Dictionary<int, AUV>();
    private Dictionary<int, MBESData> mbesByAuvId = new Dictionary<int, MBESData>();
    private Dictionary<int, SideSonarData> sideSonarByAuvId = new Dictionary<int, SideSonarData>();
    private readonly Queue<TeleportRequest> pendingTeleportRequests = new Queue<TeleportRequest>();
    [SerializeField] private AUVControllerManager controllerManager;
    [SerializeField] private Vector3 apiSpawnWorldPosition = new Vector3(0f, 50f, 0f);
    [SerializeField] private Vector3 apiSpawnWorldEulerAngles = Vector3.zero;
    private long nextSnapshotSequence = 1;
    private float snapshotRefreshInterval = 1f / FallbackSnapshotRefreshRateHz;
    private float snapshotRefreshTimer = 0f;
    private long mbesDemandTicks = 0;
    private long sideSonarDemandTicks = 0;

    // === Unity жизненный цикл ===

    // Собирает кэш всех AUV при старте
    private void Awake()
    {
        AUV.Registered += OnAUVRegistered;
        AUV.Unregistered += OnAUVUnregistered;
        ConfigureSnapshotRefreshInterval();
        RefreshAUVCache();
        RefreshMBESSnapshots();
        RefreshSideSonarSnapshots();
    }

    private void LateUpdate()
    {
        if (snapshotRefreshInterval <= 0f)
        {
            RefreshMBESSnapshots();
            RefreshSideSonarSnapshots();
            return;
        }

        snapshotRefreshTimer += Time.deltaTime;
        if (snapshotRefreshTimer < snapshotRefreshInterval)
        {
            return;
        }

        snapshotRefreshTimer -= snapshotRefreshInterval;
        if (IsDemandActive(ref mbesDemandTicks))
        {
            RefreshMBESSnapshots();
        }

        if (IsDemandActive(ref sideSonarDemandTicks))
        {
            RefreshSideSonarSnapshots();
        }
    }

    private void FixedUpdate()
    {
        ProcessQueuedCommands();
    }

    private void OnDestroy()
    {
        AUV.Registered -= OnAUVRegistered;
        AUV.Unregistered -= OnAUVUnregistered;
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
        MarkMBESDemand();

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
        MarkMBESDemand();

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
        MarkSideSonarDemand();

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
        MarkSideSonarDemand();

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

    // Возвращает последний снимок камеры конкретного AUV.
    // rgba32 - ReadOnlyMemory на внутренний буфер AUVCamera без копирования.
    public bool TryGetAUVCameraData(int auv_id, out AUVCameraData cameraData)
    {
        cameraData = default;
        if (!TryGetAUV(auv_id, out AUV auv) || auv == null)
        {
            return false;
        }

        if (!auv.TryGetCameraSnapshot(out AUVCamera.SnapshotData snapshot) || !snapshot.IsValid)
        {
            return false;
        }

        AUVCamera.CameraInfo info = snapshot.info;
        cameraData = new AUVCameraData
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

        return true;
    }

    public bool TryGetAUVCameraImage(int auv_id, out ReadOnlyMemory<byte> rgba32, out int width, out int height)
    {
        if (TryGetAUVCameraData(auv_id, out AUVCameraData cameraData))
        {
            rgba32 = cameraData.rgba32;
            width = cameraData.width;
            height = cameraData.height;
            return true;
        }

        rgba32 = ReadOnlyMemory<byte>.Empty;
        width = 0;
        height = 0;
        return false;
    }

    // Совместимость со старым API: возвращает массив байтов без дополнительного копирования,
    // если ReadOnlyMemory обернут вокруг обычного byte[].
    public bool TryGetAUVCameraImage(int auv_id, out byte[] rgba32, out int width, out int height)
    {
        if (TryGetAUVCameraImage(auv_id, out ReadOnlyMemory<byte> rgba32Memory, out width, out height))
        {
            if (MemoryMarshal.TryGetArray(rgba32Memory, out ArraySegment<byte> segment) && segment.Array != null && segment.Offset == 0 && segment.Count == segment.Array.Length)
            {
                rgba32 = segment.Array;
                return true;
            }

            rgba32 = rgba32Memory.ToArray();
            return true;
        }

        rgba32 = Array.Empty<byte>();
        width = 0;
        height = 0;
        return false;
    }

    // Удаляет AUV по id и сразу убирает его из API-кэшей.
    // Возвращает false, если AUV с таким id не найден.
    public bool TryRemoveAUV(int auv_id)
    {
        if (!TryGetAUV(auv_id, out AUV auv) || auv == null)
        {
            return false;
        }

        auv.SetAllMotorForces(0f);

        if (Application.isPlaying)
        {
            Destroy(auv.gameObject);
        }
        else
        {
            DestroyImmediate(auv.gameObject);
        }

        RemoveAUVFromCaches(auv_id);
        return true;
    }

    // Создает AUV через AUVControllerManager с его настройками спавна из Inspector.
    public bool TrySpawnAUV(out int spawnedAuvId)
    {
        return TrySpawnAUVAt(apiSpawnWorldPosition, apiSpawnWorldEulerAngles, out spawnedAuvId);
    }

    // Создает AUV в заданной мировой позиции/ориентации.
    public bool TrySpawnAUVAt(Vector3 worldPosition, Vector3 worldEulerAngles, out int spawnedAuvId)
    {
        spawnedAuvId = -1;
        if (!TryGetControllerManager(out AUVControllerManager manager))
        {
            return false;
        }

        Quaternion worldRotation = Quaternion.Euler(worldEulerAngles);
        if (!manager.TrySpawnAUVAt(worldPosition, worldRotation, out AUV spawnedAuv) || spawnedAuv == null)
        {
            return false;
        }

        spawnedAuvId = spawnedAuv.id;
        RefreshSnapshotsAfterSpawn();
        return true;
    }

    // Перегрузка для внешних API-клиентов, где удобнее передавать числа по отдельности.
    public bool TrySpawnAUVAt(float x, float y, float z, float pitch, float yaw, float roll, out int spawnedAuvId)
    {
        Vector3 worldPosition = new Vector3(x, y, z);
        Vector3 worldEulerAngles = new Vector3(pitch, yaw, roll);
        return TrySpawnAUVAt(worldPosition, worldEulerAngles, out spawnedAuvId);
    }

    // Телепортирует существующий AUV к API-стартовой точке появления.
    public bool TryTeleportAUVToSpawn(int auv_id)
    {
        return TryTeleportAUVTo(auv_id, apiSpawnWorldPosition, apiSpawnWorldEulerAngles);
    }

    // Потокобезопасно ставит телепорт существующего AUV в очередь.
    // Реальное перемещение выполняется в FixedUpdate на Main Thread.
    public bool TryTeleportAUVTo(int auv_id, Vector3 worldPosition, Vector3 worldEulerAngles)
    {
        if (auv_id < 0)
        {
            return false;
        }

        lock (stateLock)
        {
            if (!auvByIdSnapshot.ContainsKey(auv_id))
            {
                return false;
            }
        }

        EnqueueTeleportRequest(auv_id, worldPosition, worldEulerAngles);
        return true;
    }

    // Перегрузка для внешних API-клиентов, где удобнее передавать числа по отдельности.
    public bool TryTeleportAUVTo(int auv_id, float x, float y, float z, float pitch, float yaw, float roll)
    {
        Vector3 worldPosition = new Vector3(x, y, z);
        Vector3 worldEulerAngles = new Vector3(pitch, yaw, roll);
        return TryTeleportAUVTo(auv_id, worldPosition, worldEulerAngles);
    }

    // Обновляет точку/ориентацию спавна, используемые TrySpawnAUV(out int).
    public void SetAPISpawnTransform(Vector3 worldPosition, Vector3 worldEulerAngles)
    {
        apiSpawnWorldPosition = worldPosition;
        apiSpawnWorldEulerAngles = worldEulerAngles;
    }

    public void GetAPISpawnTransform(out Vector3 worldPosition, out Vector3 worldEulerAngles)
    {
        worldPosition = apiSpawnWorldPosition;
        worldEulerAngles = apiSpawnWorldEulerAngles;
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

    private bool TryGetControllerManager(out AUVControllerManager manager)
    {
        manager = controllerManager;
        if (manager == null)
        {
            manager = UnityEngine.Object.FindAnyObjectByType<AUVControllerManager>();
            controllerManager = manager;
        }

        if (manager == null)
        {
            Debug.LogWarning("AUVAPIController: AUVControllerManager not found in scene.");
            return false;
        }

        return true;
    }

    private void ConfigureSnapshotRefreshInterval()
    {
        AUVSettings settings = AUVSettings.GetOrFind();
        float mbesRate = settings != null ? settings.MBESPublishRateHz : FallbackSnapshotRefreshRateHz;
        float sideSonarRate = settings != null ? settings.SideSonarPublishRateHz : FallbackSnapshotRefreshRateHz;
        float refreshRate = Mathf.Max(0.1f, Mathf.Max(mbesRate, sideSonarRate));
        snapshotRefreshInterval = 1f / refreshRate;
        snapshotRefreshTimer = snapshotRefreshInterval;
    }

    private void MarkMBESDemand()
    {
        Interlocked.Exchange(ref mbesDemandTicks, DateTime.UtcNow.Ticks);
    }

    private void MarkSideSonarDemand()
    {
        Interlocked.Exchange(ref sideSonarDemandTicks, DateTime.UtcNow.Ticks);
    }

    private static bool IsDemandActive(ref long demandTicks)
    {
        long observedTicks = Interlocked.Read(ref demandTicks);
        if (observedTicks <= 0)
        {
            return false;
        }

        long ageTicks = DateTime.UtcNow.Ticks - observedTicks;
        return ageTicks <= TimeSpan.FromSeconds(SensorDemandKeepAliveSeconds).Ticks;
    }

    private void OnAUVRegistered(AUV auv)
    {
        if (auv == null)
        {
            return;
        }

        if (!auvById.ContainsKey(auv.id))
        {
            auvById.Add(auv.id, auv);
        }
        else
        {
            auvById[auv.id] = auv;
        }

        lock (stateLock)
        {
            auvByIdSnapshot = new Dictionary<int, AUV>(auvById);
        }

        snapshotRefreshTimer = snapshotRefreshInterval;
    }

    private void OnAUVUnregistered(AUV auv)
    {
        if (auv == null)
        {
            return;
        }

        RemoveAUVFromCaches(auv.id);
    }

    private void EnqueueTeleportRequest(int auvId, Vector3 worldPosition, Vector3 worldEulerAngles)
    {
        lock (commandQueueLock)
        {
            pendingTeleportRequests.Enqueue(new TeleportRequest
            {
                auvId = auvId,
                worldPosition = worldPosition,
                worldEulerAngles = worldEulerAngles
            });
        }
    }

    private void ProcessQueuedCommands()
    {
        while (TryDequeueTeleportRequest(out TeleportRequest request))
        {
            ExecuteTeleportAUVTo(request.auvId, request.worldPosition, request.worldEulerAngles);
        }
    }

    private bool TryDequeueTeleportRequest(out TeleportRequest request)
    {
        lock (commandQueueLock)
        {
            if (pendingTeleportRequests.Count == 0)
            {
                request = default;
                return false;
            }

            request = pendingTeleportRequests.Dequeue();
            return true;
        }
    }

    private bool ExecuteTeleportAUVTo(int auvId, Vector3 worldPosition, Vector3 worldEulerAngles)
    {
        if (!TryGetAUV(auvId, out AUV auv) || auv == null)
        {
            return false;
        }

        auv.SetAllMotorForces(0f);

        Quaternion worldRotation = Quaternion.Euler(worldEulerAngles);
        Transform auvTransform = auv.transform;
        Rigidbody rigidbody = auv.GetComponent<Rigidbody>();

        if (rigidbody != null)
        {
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.position = worldPosition;
            rigidbody.rotation = worldRotation;
            rigidbody.Sleep();
        }
        else
        {
            auvTransform.SetPositionAndRotation(worldPosition, worldRotation);
        }

        RefreshSnapshotsAfterSpawn();
        return true;
    }

    private void RefreshSnapshotsAfterSpawn()
    {
        RefreshMBESSnapshots();
        RefreshSideSonarSnapshots();
    }

    private void RemoveAUVFromCaches(int auvId)
    {
        auvById.Remove(auvId);

        lock (stateLock)
        {
            auvByIdSnapshot.Remove(auvId);
            mbesByAuvId.Remove(auvId);
            sideSonarByAuvId.Remove(auvId);
        }
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

        snapshotRefreshTimer = snapshotRefreshInterval;
    }

    private void RefreshMBESSnapshots()
    {
        if (auvById.Count == 0)
        {
            lock (stateLock)
            {
                mbesByAuvId = new Dictionary<int, MBESData>();
            }
            return;
        }

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
        if (auvById.Count == 0)
        {
            lock (stateLock)
            {
                sideSonarByAuvId = new Dictionary<int, SideSonarData>();
            }
            return;
        }

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
