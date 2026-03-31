using System.Collections.Generic;
using System.Text;
using UnityEngine;


// Класс управления
[RequireComponent(typeof(Collider), typeof(Rigidbody), typeof(WaterObject))] // Требуется Colider и Rigbody и WaterObject
public class AUV : MonoBehaviour
{
    private static readonly Vector3 DefaultInertiaTensor = new Vector3(3333.3333f, 1666.6666f, 3333.3333f);
    private const float DefaultMaxYawControlTorque = 705.23f;
    private const float MBESOriginOffset = 0.05f;
    private const int MBESTextureHeight = 24;
    private const float MBESMinVisibleContrastRange = 2f;
    private static readonly Color MBESNearColor = new Color(0.98f, 0.98f, 0.96f, 1f);
    private static readonly Color MBESFarColor = new Color(0.24f, 0.32f, 0.4f, 1f);
    private static readonly Color MBESMissColor = new Color(0.1f, 0.13f, 0.17f, 1f);

    // ID
    protected static int nextid = 0;
    [System.NonSerialized]
    public int id;

    [Header("Dynamics")]
    [SerializeField] bool applyLegacyInertiaTensor = true;
    [SerializeField] Vector3 inertiaTensor = new Vector3(3333.3333f, 1666.6666f, 3333.3333f);
    [SerializeField] bool useMotorForcePoints = true;
    [SerializeField] float maxYawControlTorque = DefaultMaxYawControlTorque;

    [Header("UI")]
    [SerializeField] bool UIOn = false;

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

    protected Motor[] Motors = System.Array.Empty<Motor>();
    protected float ForceRatio = 1;
    protected float YawControlTorque = 0f;

    private Rigidbody rb;
    private AUVSettings auvSettings;

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

    // === UI ===
    private GUIStyle uiTitleStyle;
    private GUIStyle uiTextStyle;
    private Texture2D uiFillTexture;
    private Texture2D mbesTexture;
    private Color[] mbesTexturePixels = System.Array.Empty<Color>();

    protected void SetID()
    {
        id = nextid;
        nextid += 1;
    }

    // Инициализация
    protected virtual void Init()
    {
        rb = GetComponent<Rigidbody>();
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
        BalanceMotorForcePoints();

        // Расчет отношения для диапозона
        ForceRatio = auvSettings.MaxPower / 100f;

        // MBES
        MBESInit();

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
        maxYawControlTorque = DefaultMaxYawControlTorque;
    }

    private void Awake()
    {
        SetID();
        Init();
    }

    private void FixedUpdate()
    {
        ApllyMotorForce();
        ApplyControlTorque();
        MBESUpdate();
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

    public void SetYawControlPercent(float percent)
    {
        float clampedPercent = Mathf.Clamp(percent, -100f, 100f);
        YawControlTorque = maxYawControlTorque * (clampedPercent / 100f);
    }

    public void ApllyMotorForce()
    {
        for (int i = 0; i < Motors.Length; i++)
        {
            if (useMotorForcePoints)
            {
                Vector3 worldForce = transform.TransformDirection(Motors[i].force);
                Vector3 worldPoint = transform.TransformPoint(Motors[i].inf.localPoint);
                rb.AddForceAtPosition(worldForce, worldPoint, ForceMode.Force);
            }
            else
            {
                rb.AddRelativeForce(Motors[i].force, ForceMode.Force);
            }
        }
    }

    private void ApplyControlTorque()
    {
        if (!Mathf.Approximately(YawControlTorque, 0f))
        {
            rb.AddRelativeTorque(new Vector3(0f, YawControlTorque, 0f), ForceMode.Force);
        }
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
        mbesPoint = auvSettings.MBESPoint != null ? auvSettings.MBESPoint : transform;
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

        EnsureMBESTexture();
    }

    private void UIDisplay()
    {
        if (!UIOn) return;

        EnsureUIResources();

        string mbesText = MBESDisplay();
        string motorText = MotorInfDisplay();
        string positionText = PositionDisplay();

        float margin = 16f;
        float panelWidth = Mathf.Max(320f, Mathf.Min(Screen.width - (margin * 2f), 760f));
        Rect panelRect = new Rect(margin, margin, panelWidth, 260f);
        DrawFilledRect(panelRect, new Color(0.05f, 0.08f, 0.12f, 0.82f));

        GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, 22f), $"AUV #{id}", uiTitleStyle);

        Rect mbesRect = new Rect(panelRect.x + 12f, panelRect.y + 36f, panelRect.width - 24f, 72f);
        DrawFilledRect(mbesRect, new Color(0.1f, 0.14f, 0.18f, 0.95f));
        if (mbesTexture != null)
        {
            GUI.DrawTexture(mbesRect, mbesTexture, ScaleMode.StretchToFill, false);
        }

        GUI.Label(new Rect(mbesRect.x, mbesRect.yMax + 6f, panelRect.width - 24f, 36f), mbesText, uiTextStyle);
        GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 138f, (panelRect.width * 0.5f) - 18f, 110f), motorText, uiTextStyle);
        GUI.Label(new Rect(panelRect.x + (panelRect.width * 0.5f), panelRect.y + 138f, (panelRect.width * 0.5f) - 12f, 110f), positionText, uiTextStyle);
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
