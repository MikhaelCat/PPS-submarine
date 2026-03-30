using System.Collections.Generic;
using UnityEngine;


// Инициализирует класс, который взаимодействует с водой (Сопротивление + Архимедова сила) | Требует Colider и Rigbody
[RequireComponent(typeof(Collider), typeof(Rigidbody))] // Требуется Colider и Rigbody
public class WaterObject : MonoBehaviour
{
    private const float DefaultBuoyancyOffsetKg = 1.5f;
    private const float DefaultMetacentricHeight = 0.013f;
    private const float DefaultGenerationStepCOB = 0.25f;
    private static readonly Vector3 DefaultCenterOfBuoyancy = new Vector3(-6.68785f, 0, -0.0439f);
    private const float DefaultForwardDrag = 200f;
    private const float DefaultLateralDrag = 330f;
    private const float DefaultVerticalDrag = 407f;
    private const float DefaultRollDrag = 10000f;
    private const float DefaultPitchDrag = 10000f;
    private const float DefaultYawDrag = 10000f;

    // Константы
    static float waterLevelY = 0f;

    // === Unity параметры ===

    // Сила архимеда
    [Header("Buoyant force")]
    [SerializeField] bool autoBuoyancyForce = true; // Автоматически балансирует плавучесть с гравитацией
    [SerializeField] float buoyancyOffsetKg = DefaultBuoyancyOffsetKg; // Дополнительная плавучесть в кг сверх массы объекта
    [SerializeField] Vector3 buoyancyForce = Vector3.up; // Плавучесть | Сила, с которой выталкивается объект | Положительные значеие
    [SerializeField] bool autoCenterOffBuoyancy = false; // True: Автоматический центр | False: использует CenterOffBuoyancy
    [SerializeField] Vector3 centerOffBuoyancy = new Vector3(-6.68785f, 0, -0.0439f); // Центр всплывания - только при выключенном AutoCenterOffBuoyancy
    [SerializeField] float metacentricHeight = DefaultMetacentricHeight; // Добавочный сдвиг центра плавучести по продольной оси для остойчивости
    [SerializeField] float GenerationStepCOB = DefaultGenerationStepCOB; // Шаг, с которым генерируются точки - только при включенном AutoCenterOffBuoyancy
    [SerializeField] bool useMeshForBuoyancySampling = true; // Использовать форму mesh для COB вместо collider
    [SerializeField] Vector3 meshSamplingRayDirection = new Vector3(1f, 0.37f, 0.13f); // Направление луча для теста точки внутри mesh
    [SerializeField] int maxCandidateGridPoints = 60000; // Ограничение общего числа точек сетки
    [SerializeField] int maxMeshTrianglesForSampling = 25000; // Выше этого лимита для стабильности переключаемся на collider sampling
    [SerializeField] int maxMeshSamplingRayTests = 25000000; // Ограничение общего числа ray/triangle тестов при mesh sampling

    // Сопротивление
    [Header("Resistance")]
    [SerializeField] float forwardDrag = DefaultForwardDrag; // сопротивление движения по локальной оси X (продольной)
    [SerializeField] float lateralDrag = DefaultLateralDrag; // сопротивление движения по локальной оси Z (боковой)
    [SerializeField] float verticalDrag = DefaultVerticalDrag; // сопротивление движения по локальной оси Y (вертикальной)

    [SerializeField] float rollDrag = DefaultRollDrag; // сопротитивление вращению вокруг локальной оси X
    [SerializeField] float pitchDrag = DefaultPitchDrag; // сопротивление вращению вокруг локальной оси Z
    [SerializeField] float yawDrag = DefaultYawDrag; // сопротивление вращению вокруг локальной оси Y

    // === Переменные класса ===
    private const float MeshSamplingRayEpsilon = 0.0001f;
    private static readonly Vector3 MeshSamplingFallbackDirectionA = new Vector3(-0.57f, 1f, 0.21f).normalized;
    private static readonly Vector3 MeshSamplingFallbackDirectionB = new Vector3(0.19f, -0.41f, 1f).normalized;
    private Vector3[] COBSamplePoints = System.Array.Empty<Vector3>(); // сетка точек, входящих в объект, гарантируется, что между ними растояние одинаково (если нет разрыва) | Координаты локальные
    private Mesh buoyancyMesh;
    private Vector3[] buoyancyMeshVertices = System.Array.Empty<Vector3>();
    private int[] buoyancyMeshTriangles = System.Array.Empty<int>();

    // Компоненты
    private Collider col;
    private Rigidbody rb;

    // === Инициализация | Стартовая генерация ===

    // Инициализация параметров и получение компонентов
    protected virtual void Init()
    {
        col = GetComponent<MeshCollider>();
        if (col == null)
        {
            col = GetComponent<Collider>();
        }
        if (col == null)
        {
            col = GetComponentInChildren<Collider>();
        }
        if (col == null || !col.enabled)
        {
            col = FindAnyEnabledCollider();
        }

        rb = GetComponent<Rigidbody>();
        CacheBuoyancyMesh();
        ApplyLegacyDefaultsIfNeeded();
        SetZeroDrag();
        if (autoBuoyancyForce)
        {
            AutoBouncy();
        }
    }

    private void ApplyLegacyDefaultsIfNeeded()
    {
        if (GenerationStepCOB <= 0f)
        {
            GenerationStepCOB = DefaultGenerationStepCOB;
        }

        if (Mathf.Approximately(buoyancyOffsetKg, 0f))
        {
            buoyancyOffsetKg = DefaultBuoyancyOffsetKg;
        }

        if (Mathf.Approximately(metacentricHeight, 0f))
        {
            metacentricHeight = DefaultMetacentricHeight;
        }

        if (centerOffBuoyancy == Vector3.zero)
        {
            centerOffBuoyancy = DefaultCenterOfBuoyancy;
        }

        if (HasLegacySceneHydrodynamics())
        {
            ApplyLegacyHydrodynamicsDefaults();
            return;
        }

        if (AreAllDragCoefficientsZero())
        {
            ApplyLegacyDragDefaults();
        }
    }

    private bool HasLegacySceneHydrodynamics()
    {
        return !autoBuoyancyForce
            && buoyancyForce == Vector3.up
            && autoCenterOffBuoyancy
            && centerOffBuoyancy == DefaultCenterOfBuoyancy
            && Mathf.Approximately(GenerationStepCOB, DefaultGenerationStepCOB)
            && AreAllDragCoefficientsZero();
    }

    private bool AreAllDragCoefficientsZero()
    {
        return Mathf.Approximately(forwardDrag, 0f)
            && Mathf.Approximately(lateralDrag, 0f)
            && Mathf.Approximately(verticalDrag, 0f)
            && Mathf.Approximately(rollDrag, 0f)
            && Mathf.Approximately(pitchDrag, 0f)
            && Mathf.Approximately(yawDrag, 0f);
    }

    private void ApplyLegacyHydrodynamicsDefaults()
    {
        autoBuoyancyForce = true;
        buoyancyOffsetKg = DefaultBuoyancyOffsetKg;
        autoCenterOffBuoyancy = false;
        centerOffBuoyancy = DefaultCenterOfBuoyancy;
        metacentricHeight = DefaultMetacentricHeight;
        ApplyLegacyDragDefaults();
    }

    private void ApplyLegacyDragDefaults()
    {
        forwardDrag = DefaultForwardDrag;
        lateralDrag = DefaultLateralDrag;
        verticalDrag = DefaultVerticalDrag;
        rollDrag = DefaultRollDrag;
        pitchDrag = DefaultPitchDrag;
        yawDrag = DefaultYawDrag;
    }

    private void Reset()
    {
        autoBuoyancyForce = true;
        buoyancyOffsetKg = DefaultBuoyancyOffsetKg;
        buoyancyForce = Vector3.up;
        autoCenterOffBuoyancy = false;
        centerOffBuoyancy = DefaultCenterOfBuoyancy;
        metacentricHeight = DefaultMetacentricHeight;
        GenerationStepCOB = DefaultGenerationStepCOB;
        useMeshForBuoyancySampling = true;
        meshSamplingRayDirection = new Vector3(1f, 0.37f, 0.13f);
        maxCandidateGridPoints = 60000;
        maxMeshTrianglesForSampling = 25000;
        maxMeshSamplingRayTests = 25000000;
        ApplyLegacyDragDefaults();
    }

    // Проверяет, что локальная точка в колайдере
    public bool IsLocalPointInCollider(Vector3 localPoint)
    {
        if (useMeshForBuoyancySampling && HasValidBuoyancyMesh())
        {
            return IsLocalPointInsideMesh(localPoint);
        }

        if (col == null || !col.enabled)
        {
            col = FindAnyEnabledCollider();
        }

        if (col == null)
        {
            return false;
        }

        Vector3 worldPoint = transform.TransformPoint(localPoint);
        return (col.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude < 0.0001f;
    }

    // Автоматически создает bouncy, который может сопротевлятся гравитации в полной мере
    protected void AutoBouncy()
    {
        if (autoBuoyancyForce)
        {
            buoyancyForce = -((rb.mass + buoyancyOffsetKg) * Physics.gravity);
        }
    }

    // Установить нулевое сопротивление, что бы не работала стандартная физика Unity
    protected void SetZeroDrag()
    {
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }

    // Стартовая настройка
    public void Awake()
    {
        Init();
    }

    public void Start()
    {
        CacheBuoyancyMesh();
        GenerateGrid(GenerationStepCOB);
    }

    // === Физика ===

    // ### Архимедова Сила ###

    // проверяет, что локальная точка в воде
    public bool LocalPointInWater(Vector3 localPoint)
    {
        Vector3 worldPoint = transform.TransformPoint(localPoint);
        return worldPoint[1] <= waterLevelY;
    }

    // проверяет, что мировая точка в воде
    public bool WorldPointInWater(Vector3 worldPoint)
    {
        return worldPoint[1] <= waterLevelY;
    }

    // Выдает информацию о плавучести
    public struct WaterInformation
    {
        public Vector3 buoyancy;
        public Vector3 LocalCOB;
        public bool inWater;
        public float ratio;
    }

    protected WaterInformation GetWaterInformation()
    {
        uint points_in_water_count = 0;

        double xsum = 0;
        double ysum = 0;
        double zsum = 0;

        for (uint i = 0; i < COBSamplePoints.Length; i++)
        {
            Vector3 point = COBSamplePoints[i];
            if (LocalPointInWater(point))
            {
                xsum += point[0];
                ysum += point[1];
                zsum += point[2];
                points_in_water_count += 1;
            }
        }

        bool inWater = true;
        float ratio;
        float x = 0;
        float y = 0;
        float z = 0;
        if (points_in_water_count == 0)
        {
            ratio = 0;
            inWater = false;
        }
        else
        {
            ratio = (float)points_in_water_count / COBSamplePoints.Length;
            x = (float)(xsum / points_in_water_count);
            y = (float)(ysum / points_in_water_count);
            z = (float)(zsum / points_in_water_count);
        }

        WaterInformation information = new WaterInformation
        {
            ratio = ratio,
            LocalCOB = new Vector3(x, y, z),
            inWater = inWater
        };
        return information;
    }

    // Применение силы архимеда
    protected void BuoyantForce(WaterInformation information)
    {
        if (information.inWater)
        {
            Vector3 COB;
            if (autoCenterOffBuoyancy)
            {
                COB = information.LocalCOB;
            }
            else
            {
                COB = centerOffBuoyancy;
            }

            COB += new Vector3(metacentricHeight, 0f, 0f);
            Vector3 worldCOB = transform.TransformPoint(COB);
            rb.AddForceAtPosition(buoyancyForce * information.ratio, worldCOB, ForceMode.Force);
        }
    }

    // ### Сопротивление ###

    // Расчет линейного сопротивления | Использовать с Relative (т.к. локальная сила)
    public Vector3 LocalLinearDrag()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float velocity_x = localVelocity[0];
        float velocity_y = localVelocity[1];
        float velocity_z = localVelocity[2];

        float drag_x = -forwardDrag * velocity_x * Mathf.Abs(velocity_x);
        float drag_y = -verticalDrag * velocity_y * Mathf.Abs(velocity_y);
        float drag_z = -lateralDrag * velocity_z * Mathf.Abs(velocity_z);

        return new Vector3(drag_x, drag_y, drag_z);
    }

    // Расчет сопротивления поворота | Использовать с Relative (т.к. локальная сила)
    public Vector3 LocalAngularDrag()
    {
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        float velocity_x = localAngularVelocity[0];
        float velocity_y = localAngularVelocity[1];
        float velocity_z = localAngularVelocity[2];

        float drag_x = -rollDrag * velocity_x * Mathf.Abs(velocity_x);
        float drag_y = -yawDrag * velocity_y * Mathf.Abs(velocity_y);
        float drag_z = -pitchDrag * velocity_z * Mathf.Abs(velocity_z);

        return new Vector3(drag_x, drag_y, drag_z);
    }

    // Приминение сопротивления
    protected void ForceDrag(WaterInformation information)
    {
        if (information.inWater)
        {
            rb.AddRelativeForce(LocalLinearDrag() * information.ratio, ForceMode.Force);
            rb.AddRelativeTorque(LocalAngularDrag() * information.ratio, ForceMode.Force);
        }
    }

    protected virtual void FixedUpdate()
    {
        WaterInformation inf = GetWaterInformation();
        BuoyantForce(inf); // Сначала применяем силу архимеда
        ForceDrag(inf); // Потом применяем сопротивление в этом же момента
    }

    // === Sampling mesh ===

    private void CacheBuoyancyMesh()
    {
        buoyancyMesh = null;
        buoyancyMeshVertices = System.Array.Empty<Vector3>();
        buoyancyMeshTriangles = System.Array.Empty<int>();

        if (!useMeshForBuoyancySampling)
        {
            return;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return;
        }

        Mesh mesh = meshFilter.sharedMesh;
        int[] triangles = mesh.triangles;
        if (triangles == null || triangles.Length < 3)
        {
            return;
        }

        buoyancyMesh = mesh;
        buoyancyMeshVertices = mesh.vertices;
        buoyancyMeshTriangles = triangles;

        int triangleCount = buoyancyMeshTriangles.Length / 3;
        if (triangleCount > maxMeshTrianglesForSampling)
        {
            useMeshForBuoyancySampling = false;
            Debug.LogWarning(
                $"WaterObject on {name}: mesh sampling disabled because triangle count is too high ({triangleCount}). " +
                "Using collider sampling to prevent editor freeze.",
                this);
        }
    }

    private bool HasValidBuoyancyMesh()
    {
        return buoyancyMesh != null
            && buoyancyMeshVertices != null
            && buoyancyMeshTriangles != null
            && buoyancyMeshVertices.Length > 0
            && buoyancyMeshTriangles.Length >= 3;
    }

    private bool IsLocalPointInsideMesh(Vector3 localPoint)
    {
        if (!buoyancyMesh.bounds.Contains(localPoint))
        {
            return false;
        }

        Vector3 primaryDirection = meshSamplingRayDirection.sqrMagnitude < 0.0001f
            ? new Vector3(1f, 0.37f, 0.13f).normalized
            : meshSamplingRayDirection.normalized;

        int insideVotes = 0;
        if (IsLocalPointInsideMeshByDirection(localPoint, primaryDirection))
        {
            insideVotes += 1;
        }
        if (IsLocalPointInsideMeshByDirection(localPoint, MeshSamplingFallbackDirectionA))
        {
            insideVotes += 1;
        }
        if (IsLocalPointInsideMeshByDirection(localPoint, MeshSamplingFallbackDirectionB))
        {
            insideVotes += 1;
        }

        return insideVotes >= 2;
    }

    private bool IsLocalPointInsideMeshByDirection(Vector3 localPoint, Vector3 direction)
    {
        Vector3 origin = localPoint + direction * MeshSamplingRayEpsilon;
        int hitCount = 0;

        for (int i = 0; i < buoyancyMeshTriangles.Length; i += 3)
        {
            Vector3 v0 = buoyancyMeshVertices[buoyancyMeshTriangles[i]];
            Vector3 v1 = buoyancyMeshVertices[buoyancyMeshTriangles[i + 1]];
            Vector3 v2 = buoyancyMeshVertices[buoyancyMeshTriangles[i + 2]];

            if (RayIntersectsTriangle(origin, direction, v0, v1, v2, out float distance)
                && distance > MeshSamplingRayEpsilon)
            {
                hitCount += 1;
            }
        }

        return (hitCount & 1) == 1;
    }

    private int GetSampleCount(float min, float max, float step)
    {
        float range = Mathf.Max(0f, max - min);
        return Mathf.Max(1, Mathf.CeilToInt(range / step) + 1);
    }

    protected void GenerateGrid(float step)
    {
        GenerateGridInternal(step);
    }

    private void GenerateGridInternal(float step)
    {
        if (step <= 0f)
        {
            step = DefaultGenerationStepCOB;
        }

        Vector3 min;
        Vector3 max;
        if (useMeshForBuoyancySampling && HasValidBuoyancyMesh())
        {
            min = buoyancyMesh.bounds.min;
            max = buoyancyMesh.bounds.max;
        }
        else
        {
            if (col == null || !col.enabled)
            {
                col = FindAnyEnabledCollider();
            }

            if (col == null)
            {
                COBSamplePoints = System.Array.Empty<Vector3>();
                return;
            }

            Vector3 worldMax = col.bounds.max;
            Vector3 worldMin = col.bounds.min;
            max = transform.InverseTransformPoint(worldMax);
            min = transform.InverseTransformPoint(worldMin);
        }

        float min_x = min.x;
        float min_y = min.y;
        float min_z = min.z;
        float max_x = max.x;
        float max_y = max.y;
        float max_z = max.z;

        int xCount = GetSampleCount(min_x, max_x, step);
        int yCount = GetSampleCount(min_y, max_y, step);
        int zCount = GetSampleCount(min_z, max_z, step);

        long candidateCount = (long)xCount * yCount * zCount;
        if (candidateCount > maxCandidateGridPoints)
        {
            float scale = Mathf.Pow((float)candidateCount / Mathf.Max(1, maxCandidateGridPoints), 1f / 3f);
            step *= Mathf.Max(1f, scale);

            xCount = GetSampleCount(min_x, max_x, step);
            yCount = GetSampleCount(min_y, max_y, step);
            zCount = GetSampleCount(min_z, max_z, step);
            candidateCount = (long)xCount * yCount * zCount;
        }

        if (useMeshForBuoyancySampling && HasValidBuoyancyMesh())
        {
            int triangleCount = buoyancyMeshTriangles.Length / 3;
            long estimatedRayTests = candidateCount * triangleCount * 3L;
            if (estimatedRayTests > maxMeshSamplingRayTests)
            {
                float scale = Mathf.Pow((float)estimatedRayTests / Mathf.Max(1, maxMeshSamplingRayTests), 1f / 3f);
                step *= Mathf.Max(1f, scale);

                xCount = GetSampleCount(min_x, max_x, step);
                yCount = GetSampleCount(min_y, max_y, step);
                zCount = GetSampleCount(min_z, max_z, step);
                candidateCount = (long)xCount * yCount * zCount;
            }
        }

        List<Vector3> tmp_points = new List<Vector3>((int)Mathf.Min(candidateCount, 500000));

        for (int xi = 0; xi < xCount; xi++)
        {
            float x = Mathf.Min(max_x, min_x + (xi * step));
            for (int yi = 0; yi < yCount; yi++)
            {
                float y = Mathf.Min(max_y, min_y + (yi * step));
                for (int zi = 0; zi < zCount; zi++)
                {
                    float z = Mathf.Min(max_z, min_z + (zi * step));
                    Vector3 point = new Vector3(x, y, z);
                    if (IsLocalPointInCollider(point))
                    {
                        tmp_points.Add(point);
                    }
                }
            }
        }

        COBSamplePoints = tmp_points.ToArray();
    }

    private bool RayIntersectsTriangle(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        out float distance)
    {
        distance = 0f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 pvec = Vector3.Cross(rayDirection, edge2);
        float det = Vector3.Dot(edge1, pvec);

        if (Mathf.Abs(det) < MeshSamplingRayEpsilon)
        {
            return false;
        }

        float invDet = 1f / det;
        Vector3 tvec = rayOrigin - v0;
        float u = Vector3.Dot(tvec, pvec) * invDet;
        if (u < 0f || u > 1f)
        {
            return false;
        }

        Vector3 qvec = Vector3.Cross(tvec, edge1);
        float v = Vector3.Dot(rayDirection, qvec) * invDet;
        if (v < 0f || u + v > 1f)
        {
            return false;
        }

        float t = Vector3.Dot(edge2, qvec) * invDet;
        if (t <= MeshSamplingRayEpsilon)
        {
            return false;
        }

        distance = t;
        return true;
    }

    private Collider FindAnyEnabledCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled)
            {
                return colliders[i];
            }
        }

        if (colliders.Length > 0)
        {
            return colliders[0];
        }

        return null;
    }
}
