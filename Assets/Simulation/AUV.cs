using System.Collections.Generic;
using System.Text;
using UnityEngine;


// Класс управления
[RequireComponent(typeof(Collider), typeof(Rigidbody), typeof(WaterObject))] // Требуется Colider и Rigbody и WaterObject
public class AUV : MonoBehaviour
{
    private static readonly Vector3 DefaultInertiaTensor = new Vector3(3333.3333f, 1666.6666f, 3333.3333f);
    private const float MBESOriginOffset = 0.05f;
    private const int MBESTextureHeight = 24;
    private const int SideSonarTextureHeight = 24;
    private const float MBESMinVisibleContrastRange = 2f;
    private static readonly Color MBESNearColor = new Color(0.98f, 0.98f, 0.96f, 1f);
    private static readonly Color MBESFarColor = new Color(0.24f, 0.32f, 0.4f, 1f);
    private static readonly Color MBESMissColor = new Color(0.1f, 0.13f, 0.17f, 1f);
    private static readonly string[] MBESPointSearchNames = { "MBESPoint", "MBES", "MBES Point" };
    private static readonly string[] SideSonarLeftSearchNames = { "SideSonarLeftPoint", "SideSonarLeft", "LeftSideSonar", "LeftSonar", "SonarLeft" };
    private static readonly string[] SideSonarRightSearchNames = { "SideSonarRightPoint", "SideSonarRight", "RightSideSonar", "RightSonar", "SonarRight" };

    // ID
    protected static int nextid = 0;
    [System.NonSerialized]
    public int id;

    [Header("Dynamics")]
    [SerializeField] bool applyLegacyInertiaTensor = true;
    [SerializeField] Vector3 inertiaTensor = new Vector3(3333.3333f, 1666.6666f, 3333.3333f);
    [SerializeField] bool useMotorForcePoints = true;
    [SerializeField] bool disableMotorForceOutOfWater = true;
    [SerializeField] float motorWaterBlendSpeed = 12f;


    [Header("UI")]
    [SerializeField] bool UIOn = false;

    [Header("Sensors")]
    [SerializeField] private Transform localMBESPoint;
    [SerializeField] private Transform localSideSonarLeftPoint;
    [SerializeField] private Transform localSideSonarRightPoint;

    // Переменные класса
    protected struct Motor
    {
        public AUVSettings.ForcePoint inf;
        public Vector3 force;
        public float commandPercent;
    }

    public struct MBESHit
    {
        public bool hasHit;
        public float range;
        public Vector3 pointWorld;
        public Vector3 pointLocal;
        public Vector3 normalWorld;
    }

    public enum SideSonarSide
    {
        Left = 0,
        Right = 1
    }

    public struct SideSonarHit
    {
        public bool hasHit;
        public float range;
        public float horizontalRange;
        public float intensity;
        public Vector3 pointWorld;
        public Vector3 pointLocal;
        public Vector3 normalWorld;
        public Vector3 rayDirectionWorld;
    }

    protected Motor[] Motors = System.Array.Empty<Motor>();
    protected float ForceRatio = 1;
    protected float YawControlTorque = 0f;
    private float[] motorWaterFactors = System.Array.Empty<float>();

    private Rigidbody rb;
    private WaterObject waterObject;
    private AUVSettings auvSettings;
    private AUVCamera sensorCamera;

    // === MBES ===
    private MBESHit[] mbesHits = System.Array.Empty<MBESHit>();
    private Transform mbesPoint;
    private int mbesPointsCount = 0;
    private float mbesDistance = 0f;
    private float mbesMaxRange = 0f;
    private Vector3 mbesLookDirectionLocal = Vector3.down;
    private Vector3 mbesSpanDirectionLocal = Vector3.right;
    private int mbesHitCount = 0;
    private float mbesNearestHitRange = 0f;
    private float mbesFarthestHitRange = 0f;

    // === Side sonars ===
    private Transform sideSonarLeftPoint;
    private Transform sideSonarRightPoint;
    private float sideSonarMaxRange = 0f;
    private int sideSonarPointsPerSide = 0;
    private float sideSonarSwathPerSide = 100f;
    private float sideSonarProbeDepth = 1f;
    private float sideSonarDownAngleDegrees = 45f;
    private float sideSonarDistanceAttenuation = 0.05f;
    private SideSonarHit[] sideSonarLeftLine = System.Array.Empty<SideSonarHit>();
    private SideSonarHit[] sideSonarRightLine = System.Array.Empty<SideSonarHit>();
    private int sideSonarLeftHitCount = 0;
    private int sideSonarRightHitCount = 0;
    private SideSonarHit sideSonarLeftHit;
    private SideSonarHit sideSonarRightHit;

    // === UI ===
    private GUIStyle uiTitleStyle;
    private GUIStyle uiTextStyle;
    private GUIStyle uiCenteredTitleStyle;
    private GUIStyle uiCenteredTextStyle;
    private Texture2D uiFillTexture;
    private Texture2D mbesTexture;
    private Color[] mbesTexturePixels = System.Array.Empty<Color>();
    private Texture2D sideSonarLeftTexture;
    private Texture2D sideSonarRightTexture;
    private Color[] sideSonarLeftTexturePixels = System.Array.Empty<Color>();
    private Color[] sideSonarRightTexturePixels = System.Array.Empty<Color>();

    protected void SetID()
    {
        id = nextid;
        nextid += 1;
    }

    // Инициализация
    protected virtual void Init()
    {
        rb = GetComponent<Rigidbody>();
        waterObject = GetComponent<WaterObject>();
        ApplyLegacyDefaults();

        auvSettings = AUVSettings.GetOrFind();
        if (auvSettings == null)
        {
            Debug.LogError("AUVSettings was not found in scene. Add one AUVSettings component to any GameObject.");
            enabled = false;
            return;
        }

        AUVSettings.ForcePoint[] forcePoints = auvSettings.ForcePoints;

        // Создания моторов в массиве
        List<Motor> tmp_motorForcePoints = new List<Motor>(forcePoints.Length);
        for (int i = 0; i < forcePoints.Length; i++)
        {
            tmp_motorForcePoints.Add(
                new Motor
                {
                    inf = forcePoints[i],
                    force = Vector3.zero,
                    commandPercent = 0f
                }
            );
        }
        Motors = tmp_motorForcePoints.ToArray();
        motorWaterFactors = new float[Motors.Length];
        for (int i = 0; i < motorWaterFactors.Length; i++)
        {
            motorWaterFactors[i] = 1f;
        }
        BalanceMotorForcePoints();

        // Расчет отношения для диапозона
        ForceRatio = auvSettings.MaxPower / 100f;

        // MBES
        MBESInit();

        // Side sonars
        SideSonarInit();

        // Sensor Camera
        CameraInit();

        // UI
        UIInit();
    }

    private void ApplyLegacyDefaults()
    {
        if (applyLegacyInertiaTensor)
        {
            rb.inertiaTensorRotation = Quaternion.identity;
            rb.inertiaTensor = inertiaTensor;
        }
    }

    private void Reset()
    {
        applyLegacyInertiaTensor = true;
        inertiaTensor = DefaultInertiaTensor;
        useMotorForcePoints = true;
        AssignLocalSensorPointsIfMissing();
    }

    private void Awake()
    {
        SetID();
        Init();
    }

    private void OnValidate()
    {
        motorWaterBlendSpeed = Mathf.Max(0.01f, motorWaterBlendSpeed);
        AssignLocalSensorPointsIfMissing();
    }

    private void FixedUpdate()
    {
        ApllyMotorForce();
        MBESUpdate();
        SideSonarUpdate();
    }

    private void OnGUI()
    {
        UIDisplay();
    }

    private void OnDestroy()
    {
        if (mbesTexture != null)
        {
            if (Application.isPlaying) Destroy(mbesTexture);
            else DestroyImmediate(mbesTexture);
        }

        if (sideSonarLeftTexture != null)
        {
            if (Application.isPlaying) Destroy(sideSonarLeftTexture);
            else DestroyImmediate(sideSonarLeftTexture);
        }

        if (sideSonarRightTexture != null)
        {
            if (Application.isPlaying) Destroy(sideSonarRightTexture);
            else DestroyImmediate(sideSonarRightTexture);
        }

        if (uiFillTexture != null)
        {
            if (Application.isPlaying) Destroy(uiFillTexture);
            else DestroyImmediate(uiFillTexture);
        }
    }

    // Получить мотор по id, если не найден то значение меньше 0 (-1)
    public int GetMotorIndexById(int id)
    {
        for (int i = 0; i < Motors.Length; i++)
        {
            if (Motors[i].inf.id == id)
            {
                return i;
            }
        }
        return -1;
    }

    // === МОТОРЫ ===

    // Устанавли вает скорость мотору по id | 1 ошибка нет id, 2 сила не в диапозоне -100 - 100, 0 - все ок
    public int SetMotorForce(int id, float force)
    {
        if (force > 100 || force < -100) return 2;

        int MotorIndex = GetMotorIndexById(id);
        if (MotorIndex < 0) return 1;

        Motors[MotorIndex].force = Motors[MotorIndex].inf.localDirection * (ForceRatio * force);
        Motors[MotorIndex].commandPercent = force;

        return 0;
    }

    public void SetAllMotorForces(float force)
    {
        float clampedForce = Mathf.Clamp(force, -100f, 100f);
        for (int i = 0; i < Motors.Length; i++)
        {
            SetMotorForce(Motors[i].inf.id, clampedForce);
        }
    }

    // Возвращает последнее заданное значение мотора в диапазоне -100..100
    public bool TryGetMotorCommandPercent(int motorId, out float percent)
    {
        int motorIndex = GetMotorIndexById(motorId);
        if (motorIndex < 0)
        {
            percent = 0f;
            return false;
        }

        percent = Motors[motorIndex].commandPercent;
        return true;
    }


    public void ApllyMotorForce()
    {
        for (int i = 0; i < Motors.Length; i++)
        {
            if (useMotorForcePoints)
            {
                Vector3 worldPoint = transform.TransformPoint(Motors[i].inf.localPoint);
                float targetFactor = GetMotorWaterTargetFactor(worldPoint);
                float waterFactor = SmoothMotorWaterFactor(i, targetFactor);
                Vector3 worldForce = transform.TransformDirection(Motors[i].force) * waterFactor;
                rb.AddForceAtPosition(worldForce, worldPoint, ForceMode.Force);
            }
            else
            {
                float targetFactor = GetMotorWaterTargetFactor(transform.position);
                float waterFactor = SmoothMotorWaterFactor(i, targetFactor);
                rb.AddRelativeForce(Motors[i].force * waterFactor, ForceMode.Force);
            }
        }
    }

    private float GetMotorWaterTargetFactor(Vector3 worldPoint)
    {
        if (!disableMotorForceOutOfWater)
        {
            return 1f;
        }

        bool inWater = waterObject != null
            ? waterObject.WorldPointInWater(worldPoint)
            : worldPoint.y <= 0f;
        return inWater ? 1f : 0f;
    }

    private float SmoothMotorWaterFactor(int motorIndex, float targetFactor)
    {
        if (motorIndex < 0 || motorIndex >= motorWaterFactors.Length)
        {
            return Mathf.Clamp01(targetFactor);
        }

        float blendSpeed = Mathf.Max(0.01f, motorWaterBlendSpeed);
        float blend = 1f - Mathf.Exp(-blendSpeed * Time.fixedDeltaTime);
        motorWaterFactors[motorIndex] = Mathf.Lerp(motorWaterFactors[motorIndex], Mathf.Clamp01(targetFactor), blend);
        return motorWaterFactors[motorIndex];
    }


    private void BalanceMotorForcePoints()
    {
        if (rb == null || Motors.Length == 0)
        {
            return;
        }

        bool hasForwardGroup = false;
        bool hasVerticalGroup = false;

        float forwardAverageY = 0f;
        float forwardAverageZ = 0f;
        int forwardCount = 0;

        float verticalAverageX = 0f;
        float verticalAverageZ = 0f;
        int verticalCount = 0;

        for (int i = 0; i < Motors.Length; i++)
        {
            Vector3 direction = NormalizeDirection(Motors[i].inf.localDirection, Vector3.forward);
            int dominantAxis = GetDominantAxis(direction);
            Vector3 point = Motors[i].inf.localPoint;

            if (dominantAxis == 0)
            {
                hasForwardGroup = true;
                forwardAverageY += point.y;
                forwardAverageZ += point.z;
                forwardCount++;
            }
            else if (dominantAxis == 1)
            {
                hasVerticalGroup = true;
                verticalAverageX += point.x;
                verticalAverageZ += point.z;
                verticalCount++;
            }
        }

        if (forwardCount > 0)
        {
            forwardAverageY /= forwardCount;
            forwardAverageZ /= forwardCount;
        }

        if (verticalCount > 0)
        {
            verticalAverageX /= verticalCount;
            verticalAverageZ /= verticalCount;
        }

        Vector3 balancedCenterOfMass = rb.centerOfMass;
        if (hasVerticalGroup)
        {
            balancedCenterOfMass.x = verticalAverageX;
        }

        if (hasForwardGroup)
        {
            balancedCenterOfMass.y = forwardAverageY;
        }

        float balancedZ = 0f;
        int balancedZContributors = 0;
        if (hasForwardGroup)
        {
            balancedZ += forwardAverageZ;
            balancedZContributors++;
        }

        if (hasVerticalGroup)
        {
            balancedZ += verticalAverageZ;
            balancedZContributors++;
        }

        if (balancedZContributors > 0)
        {
            balancedCenterOfMass.z = balancedZ / balancedZContributors;
        }

        rb.centerOfMass = balancedCenterOfMass;

        for (int i = 0; i < Motors.Length; i++)
        {
            Vector3 direction = NormalizeDirection(Motors[i].inf.localDirection, Vector3.forward);
            int dominantAxis = GetDominantAxis(direction);
            AUVSettings.ForcePoint forcePoint = Motors[i].inf;
            Vector3 point = forcePoint.localPoint;

            if (dominantAxis == 0 && forwardCount > 0)
            {
                point.y = balancedCenterOfMass.y;
                point.z = balancedCenterOfMass.z + (point.z - forwardAverageZ);
            }
            else if (dominantAxis == 1 && verticalCount > 0)
            {
                point.x = balancedCenterOfMass.x + (point.x - verticalAverageX);
                point.z = balancedCenterOfMass.z + (point.z - verticalAverageZ);
            }

            forcePoint.localPoint = point;
            Motors[i].inf = forcePoint;
        }
    }

    private static int GetDominantAxis(Vector3 direction)
    {
        Vector3 absoluteDirection = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
        if (absoluteDirection.x >= absoluteDirection.y && absoluteDirection.x >= absoluteDirection.z)
        {
            return 0;
        }

        if (absoluteDirection.y >= absoluteDirection.z)
        {
            return 1;
        }

        return 2;
    }

    // === MBES ===
    private void MBESInit()
    {
        mbesPointsCount = Mathf.Max(2, auvSettings.MBESPointsCount);
        mbesDistance = Mathf.Clamp(auvSettings.MBESDistance, 1f, 100f);
        mbesMaxRange = Mathf.Max(0.1f, auvSettings.MBESMaxRange);
        mbesPoint = ResolveSensorTransform(localMBESPoint, MBESPointSearchNames);
        mbesLookDirectionLocal = NormalizeDirection(auvSettings.MBESLookDirection, Vector3.down);
        mbesSpanDirectionLocal = NormalizeDirection(auvSettings.MBESSpanDirection, Vector3.right);

        Vector3 projectedSpan = Vector3.ProjectOnPlane(mbesSpanDirectionLocal, mbesLookDirectionLocal);
        if (projectedSpan.sqrMagnitude < 0.0001f)
        {
            projectedSpan = Vector3.ProjectOnPlane(Vector3.right, mbesLookDirectionLocal);
            if (projectedSpan.sqrMagnitude < 0.0001f)
            {
                projectedSpan = Vector3.ProjectOnPlane(Vector3.forward, mbesLookDirectionLocal);
            }
        }
        mbesSpanDirectionLocal = projectedSpan.normalized;

        mbesHits = new MBESHit[mbesPointsCount];
        mbesNearestHitRange = 0f;
        mbesFarthestHitRange = 0f;
        mbesHitCount = 0;

        EnsureMBESTexture();
    }

    private void MBESUpdate()
    {
        if (mbesHits.Length == 0)
        {
            return;
        }

        Transform mbesTransform = mbesPoint != null ? mbesPoint : transform;
        Vector3 lookDirectionWorld = mbesTransform.TransformDirection(mbesLookDirectionLocal).normalized;
        Vector3 spanDirectionWorld = mbesTransform.TransformDirection(mbesSpanDirectionLocal).normalized;
        spanDirectionWorld = Vector3.ProjectOnPlane(spanDirectionWorld, lookDirectionWorld).normalized;

        if (spanDirectionWorld.sqrMagnitude < 0.0001f)
        {
            spanDirectionWorld = Vector3.ProjectOnPlane(mbesTransform.right, lookDirectionWorld).normalized;
        }

        Vector3 origin = mbesTransform.position + lookDirectionWorld * MBESOriginOffset;
        Vector3 farCenter = origin + lookDirectionWorld * mbesMaxRange;
        float halfWidth = mbesDistance * 0.5f;

        mbesHitCount = 0;
        mbesNearestHitRange = mbesMaxRange;
        mbesFarthestHitRange = 0f;

        for (int i = 0; i < mbesHits.Length; i++)
        {
            float t = mbesHits.Length == 1 ? 0.5f : (float)i / (mbesHits.Length - 1);
            float lateralOffset = Mathf.Lerp(-halfWidth, halfWidth, t);
            Vector3 targetPoint = farCenter + spanDirectionWorld * lateralOffset;
            Vector3 rayDirection = (targetPoint - origin).normalized;

            if (Physics.Raycast(origin, rayDirection, out RaycastHit hit, mbesMaxRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                mbesHits[i] = new MBESHit
                {
                    hasHit = true,
                    range = hit.distance,
                    pointWorld = hit.point,
                    pointLocal = transform.InverseTransformPoint(hit.point),
                    normalWorld = hit.normal
                };

                mbesHitCount++;
                if (hit.distance < mbesNearestHitRange)
                {
                    mbesNearestHitRange = hit.distance;
                }

                if (hit.distance > mbesFarthestHitRange)
                {
                    mbesFarthestHitRange = hit.distance;
                }
            }
            else
            {
                Vector3 missPoint = origin + rayDirection * mbesMaxRange;
                mbesHits[i] = new MBESHit
                {
                    hasHit = false,
                    range = mbesMaxRange,
                    pointWorld = missPoint,
                    pointLocal = transform.InverseTransformPoint(missPoint),
                    normalWorld = -lookDirectionWorld
                };
            }
        }

        if (mbesHitCount == 0)
        {
            mbesNearestHitRange = 0f;
            mbesFarthestHitRange = 0f;
        }

        UpdateMBESTexture();
    }

    public int GetMBESPointCount()
    {
        return mbesHits.Length;
    }

    public bool TryGetMBESHit(int pointIndex, out MBESHit hit)
    {
        if (pointIndex < 0 || pointIndex >= mbesHits.Length)
        {
            hit = default;
            return false;
        }

        hit = mbesHits[pointIndex];
        return true;
    }

    public bool TryGetMBESPointWorld(int pointIndex, out Vector3 pointWorld)
    {
        if (TryGetMBESHit(pointIndex, out MBESHit hit))
        {
            pointWorld = hit.pointWorld;
            return true;
        }

        pointWorld = Vector3.zero;
        return false;
    }

    public bool TryGetMBESPointLocal(int pointIndex, out Vector3 pointLocal)
    {
        if (TryGetMBESHit(pointIndex, out MBESHit hit))
        {
            pointLocal = hit.pointLocal;
            return true;
        }

        pointLocal = Vector3.zero;
        return false;
    }

    public bool TryGetMBESRange(int pointIndex, out float range)
    {
        if (TryGetMBESHit(pointIndex, out MBESHit hit))
        {
            range = hit.range;
            return true;
        }

        range = 0f;
        return false;
    }

    private static Vector3 NormalizeDirection(Vector3 direction, Vector3 fallback)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return fallback.normalized;
        }

        return direction.normalized;
    }

    private void EnsureMBESTexture()
    {
        if (mbesPointsCount <= 0)
        {
            return;
        }

        if (mbesTexture != null && mbesTexture.width == mbesPointsCount)
        {
            return;
        }

        if (mbesTexture != null)
        {
            if (Application.isPlaying) Destroy(mbesTexture);
            else DestroyImmediate(mbesTexture);
        }

        mbesTexture = new Texture2D(mbesPointsCount, MBESTextureHeight, TextureFormat.RGBA32, false);
        mbesTexture.wrapMode = TextureWrapMode.Clamp;
        mbesTexture.filterMode = FilterMode.Point;
        mbesTexturePixels = new Color[mbesTexture.width * mbesTexture.height];
        UpdateMBESTexture();
    }

    private void UpdateMBESTexture()
    {
        if (mbesTexture == null || mbesHits.Length == 0)
        {
            return;
        }

        int textureWidth = mbesTexture.width;
        int textureHeight = mbesTexture.height;
        if (mbesTexturePixels.Length != textureWidth * textureHeight)
        {
            mbesTexturePixels = new Color[textureWidth * textureHeight];
        }

        float visibleMinRange = mbesNearestHitRange;
        float visibleMaxRange = mbesFarthestHitRange;
        if (mbesHitCount > 0)
        {
            float visibleRange = visibleMaxRange - visibleMinRange;
            if (visibleRange < MBESMinVisibleContrastRange)
            {
                float centerRange = (visibleMinRange + visibleMaxRange) * 0.5f;
                visibleMinRange = Mathf.Max(0f, centerRange - (MBESMinVisibleContrastRange * 0.5f));
                visibleMaxRange = visibleMinRange + MBESMinVisibleContrastRange;
            }
        }

        for (int x = 0; x < textureWidth; x++)
        {
            Color beamColor = MBESMissColor;
            if (mbesHits[x].hasHit)
            {
                float distanceRatio = mbesHitCount > 0
                    ? Mathf.InverseLerp(visibleMinRange, visibleMaxRange, mbesHits[x].range)
                    : Mathf.InverseLerp(0f, mbesMaxRange, mbesHits[x].range);
                distanceRatio = Mathf.Pow(Mathf.Clamp01(distanceRatio), 0.8f);
                beamColor = Color.Lerp(MBESNearColor, MBESFarColor, distanceRatio);
            }

            for (int y = 0; y < textureHeight; y++)
            {
                mbesTexturePixels[(y * textureWidth) + x] = beamColor;
            }
        }

        mbesTexture.SetPixels(mbesTexturePixels);
        mbesTexture.Apply(false, false);
    }

    // === Side Sonars ===

    private void SideSonarInit()
    {
        sideSonarLeftPoint = ResolveSensorTransform(localSideSonarLeftPoint, SideSonarLeftSearchNames);
        sideSonarRightPoint = ResolveSensorTransform(localSideSonarRightPoint, SideSonarRightSearchNames);
        sideSonarMaxRange = Mathf.Max(0.1f, auvSettings.SideSonarMaxRange);
        sideSonarPointsPerSide = Mathf.Max(8, auvSettings.SideSonarPointsPerSide);
        sideSonarSwathPerSide = Mathf.Max(1f, auvSettings.SideSonarSwathPerSide);
        sideSonarDownAngleDegrees = Mathf.Clamp(auvSettings.SideSonarDownAngleDegrees, 1f, 89f);
        sideSonarDistanceAttenuation = Mathf.Max(0f, auvSettings.SideSonarDistanceAttenuation);
        sideSonarProbeDepth = Mathf.Max(1f, sideSonarSwathPerSide * Mathf.Tan(sideSonarDownAngleDegrees * Mathf.Deg2Rad));

        sideSonarLeftLine = new SideSonarHit[sideSonarPointsPerSide];
        sideSonarRightLine = new SideSonarHit[sideSonarPointsPerSide];

        FillSideSonarLineWithMisses(sideSonarLeftLine, sideSonarLeftPoint, -1);
        FillSideSonarLineWithMisses(sideSonarRightLine, sideSonarRightPoint, 1);

        sideSonarLeftHit = GetRepresentativeSideSonarHit(sideSonarLeftLine);
        sideSonarRightHit = GetRepresentativeSideSonarHit(sideSonarRightLine);

        EnsureSideSonarTextures();
        UpdateSideSonarTextures();
    }

    private void SideSonarUpdate()
    {
        if (sideSonarLeftLine.Length == 0 || sideSonarRightLine.Length == 0)
        {
            return;
        }

        sideSonarLeftHitCount = SampleSideSonarLine(sideSonarLeftPoint, -1, sideSonarLeftLine);
        sideSonarRightHitCount = SampleSideSonarLine(sideSonarRightPoint, 1, sideSonarRightLine);

        sideSonarLeftHit = GetRepresentativeSideSonarHit(sideSonarLeftLine);
        sideSonarRightHit = GetRepresentativeSideSonarHit(sideSonarRightLine);

        UpdateSideSonarTextures();
    }

    private int SampleSideSonarLine(Transform sonarTransform, int sideSign, SideSonarHit[] targetLine)
    {
        if (targetLine == null || targetLine.Length == 0)
        {
            return 0;
        }

        int hitCount = 0;
        int sampleCount = targetLine.Length;
        for (int i = 0; i < sampleCount; i++)
        {
            float horizontalDistance = sideSonarSwathPerSide * ((i + 0.5f) / sampleCount);
            SideSonarHit sample = SampleSideSonarBin(sonarTransform, sideSign, horizontalDistance);
            targetLine[i] = sample;
            if (sample.hasHit)
            {
                hitCount++;
            }
        }

        return hitCount;
    }

    private SideSonarHit SampleSideSonarBin(Transform sonarTransform, int sideSign, float horizontalDistance)
    {
        Transform originTransform = sonarTransform != null ? sonarTransform : transform;
        Vector3 sideAxis = (sideSign < 0 ? -originTransform.right : originTransform.right).normalized;
        Vector3 localTarget = new Vector3(sideSign * horizontalDistance, -sideSonarProbeDepth, 0f);
        Vector3 rayDirection = originTransform.TransformDirection(localTarget.normalized);
        Vector3 origin = originTransform.position + rayDirection * MBESOriginOffset;

        if (Physics.Raycast(origin, rayDirection, out RaycastHit hit, sideSonarMaxRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            float horizontalRange = Mathf.Max(0f, Vector3.Dot(hit.point - originTransform.position, sideAxis));
            if (horizontalRange > sideSonarSwathPerSide)
            {
                return CreateSideSonarMiss(originTransform, sideSign, horizontalDistance);
            }

            float angleFactor = Mathf.Clamp01(Vector3.Dot(hit.normal, -rayDirection));
            float attenuation = 1f / (1f + hit.distance * sideSonarDistanceAttenuation);
            float intensity = angleFactor * attenuation;

            return new SideSonarHit
            {
                hasHit = true,
                range = hit.distance,
                horizontalRange = horizontalRange,
                intensity = intensity,
                pointWorld = hit.point,
                pointLocal = transform.InverseTransformPoint(hit.point),
                normalWorld = hit.normal,
                rayDirectionWorld = rayDirection
            };
        }

        return CreateSideSonarMiss(originTransform, sideSign, horizontalDistance);
    }

    private void FillSideSonarLineWithMisses(SideSonarHit[] targetLine, Transform sonarTransform, int sideSign)
    {
        if (targetLine == null || targetLine.Length == 0)
        {
            return;
        }

        int sampleCount = targetLine.Length;
        for (int i = 0; i < sampleCount; i++)
        {
            float horizontalDistance = sideSonarSwathPerSide * ((i + 0.5f) / sampleCount);
            targetLine[i] = CreateSideSonarMiss(sonarTransform, sideSign, horizontalDistance);
        }
    }

    private SideSonarHit CreateSideSonarMiss(Transform sonarTransform, int sideSign, float horizontalDistance)
    {
        Transform originTransform = sonarTransform != null ? sonarTransform : transform;
        Vector3 localTarget = new Vector3(sideSign * horizontalDistance, -sideSonarProbeDepth, 0f);
        Vector3 rayDirection = originTransform.TransformDirection(localTarget.normalized);
        Vector3 missPoint = originTransform.position + rayDirection * sideSonarMaxRange;

        return new SideSonarHit
        {
            hasHit = false,
            range = sideSonarMaxRange,
            horizontalRange = horizontalDistance,
            intensity = 0f,
            pointWorld = missPoint,
            pointLocal = transform.InverseTransformPoint(missPoint),
            normalWorld = -rayDirection,
            rayDirectionWorld = rayDirection
        };
    }

    private static SideSonarHit GetRepresentativeSideSonarHit(SideSonarHit[] line)
    {
        if (line == null || line.Length == 0)
        {
            return default;
        }

        int bestIndex = -1;
        float bestIntensity = -1f;
        for (int i = 0; i < line.Length; i++)
        {
            if (!line[i].hasHit)
            {
                continue;
            }

            if (line[i].intensity > bestIntensity)
            {
                bestIntensity = line[i].intensity;
                bestIndex = i;
            }
        }

        if (bestIndex >= 0)
        {
            return line[bestIndex];
        }

        return line[line.Length / 2];
    }

    public bool TryGetSideSonarHit(SideSonarSide side, out SideSonarHit hit)
    {
        hit = side == SideSonarSide.Left ? sideSonarLeftHit : sideSonarRightHit;
        return true;
    }

    public bool TryGetSideSonarHit(SideSonarSide side, int pointIndex, out SideSonarHit hit)
    {
        SideSonarHit[] line = side == SideSonarSide.Left ? sideSonarLeftLine : sideSonarRightLine;
        if (line == null || pointIndex < 0 || pointIndex >= line.Length)
        {
            hit = default;
            return false;
        }

        hit = line[pointIndex];
        return true;
    }

    public int GetSideSonarPointCount()
    {
        return sideSonarPointsPerSide;
    }

    public float GetSideSonarSwathPerSide()
    {
        return sideSonarSwathPerSide;
    }

    public float GetSideSonarMaxRange()
    {
        return sideSonarMaxRange;
    }

    public int GetSideSonarHitCount(SideSonarSide side)
    {
        return side == SideSonarSide.Left ? sideSonarLeftHitCount : sideSonarRightHitCount;
    }

    public Texture GetSideSonarTexture(SideSonarSide side)
    {
        return side == SideSonarSide.Left ? sideSonarLeftTexture : sideSonarRightTexture;
    }

    private void EnsureSideSonarTextures()
    {
        if (sideSonarPointsPerSide <= 0)
        {
            return;
        }

        sideSonarLeftTexture = EnsureSideSonarTexture(sideSonarLeftTexture, sideSonarPointsPerSide);
        sideSonarRightTexture = EnsureSideSonarTexture(sideSonarRightTexture, sideSonarPointsPerSide);

        if (sideSonarLeftTexturePixels.Length != sideSonarLeftTexture.width * sideSonarLeftTexture.height)
        {
            sideSonarLeftTexturePixels = new Color[sideSonarLeftTexture.width * sideSonarLeftTexture.height];
        }

        if (sideSonarRightTexturePixels.Length != sideSonarRightTexture.width * sideSonarRightTexture.height)
        {
            sideSonarRightTexturePixels = new Color[sideSonarRightTexture.width * sideSonarRightTexture.height];
        }
    }

    private Texture2D EnsureSideSonarTexture(Texture2D texture, int width)
    {
        if (texture != null && texture.width == width)
        {
            return texture;
        }

        if (texture != null)
        {
            if (Application.isPlaying) Destroy(texture);
            else DestroyImmediate(texture);
        }

        Texture2D createdTexture = new Texture2D(width, SideSonarTextureHeight, TextureFormat.RGBA32, false);
        createdTexture.wrapMode = TextureWrapMode.Clamp;
        createdTexture.filterMode = FilterMode.Point;
        return createdTexture;
    }

    private void UpdateSideSonarTextures()
    {
        UpdateSideSonarTexture(sideSonarLeftLine, sideSonarLeftTexture, sideSonarLeftTexturePixels);
        UpdateSideSonarTexture(sideSonarRightLine, sideSonarRightTexture, sideSonarRightTexturePixels);
    }

    private static void UpdateSideSonarTexture(SideSonarHit[] line, Texture2D texture, Color[] pixels)
    {
        if (line == null || line.Length == 0 || texture == null || pixels == null || pixels.Length != texture.width * texture.height)
        {
            return;
        }

        int width = texture.width;
        int height = texture.height;
        for (int x = 0; x < width; x++)
        {
            float intensity = Mathf.Clamp01(line[x].intensity);
            Color beamColor = Color.Lerp(new Color(0.04f, 0.06f, 0.08f, 1f), new Color(0.82f, 0.95f, 1f, 1f), Mathf.Pow(intensity, 0.7f));
            if (!line[x].hasHit)
            {
                beamColor = new Color(0.03f, 0.04f, 0.05f, 1f);
            }

            for (int y = 0; y < height; y++)
            {
                pixels[(y * width) + x] = beamColor;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);
    }

    public bool TryGetLeftSideSonarHit(out SideSonarHit hit)
    {
        return TryGetSideSonarHit(SideSonarSide.Left, out hit);
    }

    public bool TryGetRightSideSonarHit(out SideSonarHit hit)
    {
        return TryGetSideSonarHit(SideSonarSide.Right, out hit);
    }

    // === Sensor Camera ===
    
    private void CameraInit()
    {
        sensorCamera = GetComponentInChildren<AUVCamera>(true);
        if (sensorCamera != null)
        {
            return;
        }

        Camera[] childCameras = GetComponentsInChildren<Camera>(true);
        Camera fallbackCamera = null;
        if (childCameras.Length > 0)
        {
            for (int i = 0; i < childCameras.Length; i++)
            {
                if (childCameras[i] == null)
                {
                    continue;
                }

                fallbackCamera = childCameras[i];
                if (string.Equals(childCameras[i].name, "AUVCamera", System.StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        if (fallbackCamera == null)
        {
            return;
        }

        sensorCamera = fallbackCamera.GetComponent<AUVCamera>();
        if (sensorCamera == null)
        {
            sensorCamera = fallbackCamera.gameObject.AddComponent<AUVCamera>();
        }
    }

    public bool TryGetCameraInfo(out AUVCamera.CameraInfo info)
    {
        if (sensorCamera == null)
        {
            CameraInit();
        }

        if (sensorCamera != null && sensorCamera.TryGetCameraInfo(out info))
        {
            return true;
        }

        info = default;
        return false;
    }

    public bool TryGetCameraSnapshot(out AUVCamera.SnapshotData snapshot)
    {
        if (sensorCamera == null)
        {
            CameraInit();
        }

        if (sensorCamera != null && sensorCamera.TryGetSnapshotData(out snapshot))
        {
            return true;
        }

        snapshot = default;
        return false;
    }

    public Texture GetCameraPreviewTexture()
    {
        if (sensorCamera == null)
        {
            CameraInit();
        }

        return sensorCamera != null ? sensorCamera.GetPreviewTexture() : null;
    }

    private void AssignLocalSensorPointsIfMissing()
    {
        if (localMBESPoint == null)
        {
            localMBESPoint = FindChildByNames(MBESPointSearchNames);
        }

        if (localSideSonarLeftPoint == null)
        {
            localSideSonarLeftPoint = FindChildByNames(SideSonarLeftSearchNames);
        }

        if (localSideSonarRightPoint == null)
        {
            localSideSonarRightPoint = FindChildByNames(SideSonarRightSearchNames);
        }
    }

    private Transform ResolveSensorTransform(Transform explicitTransform, string[] searchNames)
    {
        if (explicitTransform != null && explicitTransform.IsChildOf(transform))
        {
            return explicitTransform;
        }

        Transform localFoundTransform = FindChildByNames(searchNames);
        return localFoundTransform != null ? localFoundTransform : transform;
    }

    private Transform FindChildByNames(string[] searchNames)
    {
        if (searchNames == null)
        {
            return null;
        }

        for (int i = 0; i < searchNames.Length; i++)
        {
            Transform foundTransform = FindChildRecursive(transform, searchNames[i]);
            if (foundTransform != null)
            {
                return foundTransform;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nestedChild = FindChildRecursive(child, childName);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }

    // === UI ===
    private string MBESDisplay()
    {
        if (mbesHits.Length == 0)
        {
            return "MBES: no data";
        }

        return $"MBES: {mbesHitCount}/{mbesHits.Length} hits | swath <= {mbesDistance:F1} m | range <= {mbesMaxRange:F1} m | nearest {mbesNearestHitRange:F1} m | farthest {mbesFarthestHitRange:F1} m";
    }

    private string MotorInfDisplay()
    {
        if (Motors.Length == 0)
        {
            return "Motors: no data";
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("Motors:");
        for (int i = 0; i < Motors.Length; i++)
        {
            builder.Append("\n");
            builder.Append("M");
            builder.Append(Motors[i].inf.id);
            builder.Append(": ");
            builder.Append(Motors[i].commandPercent.ToString("F1"));
            builder.Append("%");
        }
        builder.Append("\nYaw: ");
        builder.Append(YawControlTorque.ToString("F1"));

        return builder.ToString();
    }

    private string PositionDisplay()
    {
        Vector3 euler = transform.eulerAngles;
        Vector3 velocity = rb != null ? rb.linearVelocity : Vector3.zero;

        return $"Position: {transform.position.x:F2}, {transform.position.y:F2}, {transform.position.z:F2}\nRotation: {euler.x:F1}, {euler.y:F1}, {euler.z:F1}\nVelocity: {velocity.x:F2}, {velocity.y:F2}, {velocity.z:F2}";
    }

    private string CameraDisplay()
    {
        if (!TryGetCameraInfo(out AUVCamera.CameraInfo info))
        {
            return "Camera: no data";
        }

        string projectionInfo = info.orthographic
            ? $"ortho size {info.orthographicSize:F2}"
            : $"fov {info.fieldOfView:F1}";

        return $"Camera: {info.width}x{info.height} | aspect {info.aspect:F2} | {projectionInfo} | near {info.nearClipPlane:F2} | far {info.farClipPlane:F1}";
    }

    private string SideSonarStateText(SideSonarHit hit, int hitCount)
    {
        string state = hitCount > 0 ? "ACTIVE" : "MISS";
        return $"State: {state}\nHits: {hitCount}/{sideSonarPointsPerSide}\nBest slant: {hit.range:F1} m\nBest cross-track: {hit.horizontalRange:F1} m\nBest intensity: {hit.intensity:F2}";
    }

    private void DrawSideSonarPanel(Rect panelRect, string title, SideSonarHit hit, int hitCount, Texture stripTexture, Color accentColor)
    {
        DrawFilledRect(panelRect, new Color(0.06f, 0.1f, 0.14f, 0.82f));

        GUI.Label(new Rect(panelRect.x + 8f, panelRect.y + 10f, panelRect.width - 16f, 22f), title, uiCenteredTitleStyle);

        Rect stripRect = new Rect(panelRect.x + 10f, panelRect.y + 40f, panelRect.width - 20f, 58f);
        DrawFilledRect(stripRect, new Color(0.05f, 0.07f, 0.1f, 0.95f));
        if (stripTexture != null)
        {
            GUI.DrawTexture(stripRect, stripTexture, ScaleMode.StretchToFill, false);
        }
        GUI.Label(new Rect(stripRect.x, stripRect.yMax + 4f, stripRect.width, 16f), "Shadow line", uiCenteredTextStyle);

        GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 120f, panelRect.width - 20f, 88f), SideSonarStateText(hit, hitCount), uiTextStyle);

        float intensity = Mathf.Clamp01(hit.intensity);
        Rect intensityTrackRect = new Rect(panelRect.x + 10f, panelRect.y + 214f, panelRect.width - 20f, 12f);
        DrawFilledRect(intensityTrackRect, new Color(0.16f, 0.2f, 0.25f, 0.95f));
        if (intensity > 0f)
        {
            Rect intensityFillRect = new Rect(intensityTrackRect.x, intensityTrackRect.y, intensityTrackRect.width * intensity, intensityTrackRect.height);
            DrawFilledRect(intensityFillRect, accentColor);
        }
        GUI.Label(new Rect(intensityTrackRect.x, intensityTrackRect.yMax + 4f, intensityTrackRect.width, 20f), "Echo level", uiCenteredTextStyle);

        float rangeRatio = Mathf.Clamp01(hit.horizontalRange / Mathf.Max(0.1f, sideSonarSwathPerSide));
        Rect rangeTrackRect = new Rect(panelRect.x + (panelRect.width * 0.5f) - 14f, panelRect.y + 258f, 28f, panelRect.height - 318f);
        DrawFilledRect(rangeTrackRect, new Color(0.14f, 0.18f, 0.22f, 0.95f));

        if (hitCount > 0)
        {
            float markerY = Mathf.Lerp(rangeTrackRect.y + 2f, rangeTrackRect.yMax - 6f, rangeRatio);
            Rect markerRect = new Rect(rangeTrackRect.x - 6f, markerY, rangeTrackRect.width + 12f, 4f);
            DrawFilledRect(markerRect, accentColor);
        }

        GUI.Label(new Rect(panelRect.x + 8f, rangeTrackRect.y - 18f, panelRect.width - 16f, 16f), "0 m", uiCenteredTextStyle);
        GUI.Label(new Rect(panelRect.x + 8f, rangeTrackRect.yMax + 2f, panelRect.width - 16f, 16f), $"{sideSonarSwathPerSide:F0} m", uiCenteredTextStyle);
    }

    private void UIInit()
    {
        if (!UIOn) return;

        if (uiFillTexture == null)
        {
            uiFillTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            uiFillTexture.SetPixel(0, 0, Color.white);
            uiFillTexture.Apply(false, false);
        }

        EnsureMBESTexture();
        EnsureSideSonarTextures();
    }

    private void EnsureUIResources()
    {
        if (uiFillTexture == null)
        {
            uiFillTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            uiFillTexture.SetPixel(0, 0, Color.white);
            uiFillTexture.Apply(false, false);
        }

        if (uiTitleStyle == null)
        {
            uiTitleStyle = new GUIStyle();
            uiTitleStyle.fontSize = 14;
            uiTitleStyle.fontStyle = FontStyle.Bold;
            uiTitleStyle.normal.textColor = Color.white;
        }

        if (uiTextStyle == null)
        {
            uiTextStyle = new GUIStyle();
            uiTextStyle.fontSize = 12;
            uiTextStyle.alignment = TextAnchor.UpperLeft;
            uiTextStyle.wordWrap = true;
            uiTextStyle.normal.textColor = new Color(0.9f, 0.95f, 1f, 1f);
        }

        if (uiCenteredTitleStyle == null)
        {
            uiCenteredTitleStyle = new GUIStyle(uiTitleStyle);
            uiCenteredTitleStyle.alignment = TextAnchor.UpperCenter;
        }

        if (uiCenteredTextStyle == null)
        {
            uiCenteredTextStyle = new GUIStyle(uiTextStyle);
            uiCenteredTextStyle.alignment = TextAnchor.UpperCenter;
        }

        EnsureMBESTexture();
        EnsureSideSonarTextures();
    }

    private void UIDisplay()
    {
        if (!UIOn) return;

        EnsureUIResources();

        string mbesText = MBESDisplay();
        string motorText = MotorInfDisplay();
        string positionText = PositionDisplay();
        string cameraText = CameraDisplay();
        Texture cameraPreview = GetCameraPreviewTexture();

        float margin = 16f;
        float gap = 12f;
        float availableWidth = Screen.width - (margin * 2f);
        if (availableWidth < 260f)
        {
            return;
        }

        float minCenterWidth = 220f;
        float maxSideWidth = Mathf.Max(80f, (availableWidth - minCenterWidth - (gap * 2f)) * 0.5f);
        float sidePanelWidth = Mathf.Clamp(availableWidth * 0.18f, 80f, Mathf.Min(220f, maxSideWidth));
        float centerPanelWidth = Mathf.Max(minCenterWidth, availableWidth - ((sidePanelWidth * 2f) + (gap * 2f)));
        centerPanelWidth = Mathf.Min(centerPanelWidth, 760f);
        sidePanelWidth = Mathf.Max(80f, (availableWidth - centerPanelWidth - (gap * 2f)) * 0.5f);
        float panelHeight = 516f;

        Rect leftSonarRect = new Rect(margin, margin, sidePanelWidth, panelHeight);
        Rect centerRect = new Rect(leftSonarRect.xMax + gap, margin, centerPanelWidth, panelHeight);
        Rect rightSonarRect = new Rect(centerRect.xMax + gap, margin, sidePanelWidth, panelHeight);

        DrawSideSonarPanel(leftSonarRect, "Left Sonar", sideSonarLeftHit, sideSonarLeftHitCount, sideSonarLeftTexture, new Color(0.27f, 0.71f, 0.95f, 0.95f));
        DrawSideSonarPanel(rightSonarRect, "Right Sonar", sideSonarRightHit, sideSonarRightHitCount, sideSonarRightTexture, new Color(0.42f, 0.86f, 0.74f, 0.95f));

        DrawFilledRect(centerRect, new Color(0.05f, 0.08f, 0.12f, 0.82f));

        GUI.Label(new Rect(centerRect.x + 12f, centerRect.y + 10f, centerRect.width - 24f, 22f), $"AUV #{id}", uiTitleStyle);

        Rect mbesRect = new Rect(centerRect.x + 12f, centerRect.y + 36f, centerRect.width - 24f, 72f);
        DrawFilledRect(mbesRect, new Color(0.1f, 0.14f, 0.18f, 0.95f));
        if (mbesTexture != null)
        {
            GUI.DrawTexture(mbesRect, mbesTexture, ScaleMode.StretchToFill, false);
        }

        GUI.Label(new Rect(mbesRect.x, mbesRect.yMax + 6f, centerRect.width - 24f, 36f), mbesText, uiTextStyle);
        GUI.Label(new Rect(centerRect.x + 12f, centerRect.y + 150f, (centerRect.width * 0.5f) - 18f, 96f), motorText, uiTextStyle);
        GUI.Label(new Rect(centerRect.x + (centerRect.width * 0.5f), centerRect.y + 150f, (centerRect.width * 0.5f) - 12f, 96f), positionText, uiTextStyle);

        Rect cameraRect = new Rect(centerRect.x + 12f, centerRect.y + 258f, centerRect.width - 24f, 208f);
        DrawFilledRect(cameraRect, new Color(0.07f, 0.1f, 0.14f, 0.95f));
        if (cameraPreview != null)
        {
            GUI.DrawTexture(cameraRect, cameraPreview, ScaleMode.ScaleToFit, false);
        }

        GUI.Label(new Rect(cameraRect.x, cameraRect.yMax + 8f, centerRect.width - 24f, 28f), cameraText, uiTextStyle);
    }

    private void DrawFilledRect(Rect rect, Color color)
    {
        if (uiFillTexture == null)
        {
            return;
        }

        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, uiFillTexture, ScaleMode.StretchToFill, true);
        GUI.color = previousColor;
    }
}
