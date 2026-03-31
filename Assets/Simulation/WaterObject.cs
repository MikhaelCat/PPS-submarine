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
    private const float DefaultForwardDrag = 700f;
    private const float DefaultLateralDrag = 1000f;
    private const float DefaultVerticalDrag = 300f;
    private const float DefaultRollDrag = 100_000f;
    private const float DefaultPitchDrag = 100_000f;
    private const float DefaultYawDrag = 100_000f;

    // Константы
    static float waterLevelY = 0f;

    // === Unity параметры ===

    // Сила архимеда
    [Header("Buoyant force")]
    [SerializeField] bool autoBuoyancyForce = true; // Автоматически балансирует плавучесть с гравитацией
    [SerializeField] float buoyancyOffsetKg = DefaultBuoyancyOffsetKg; // Дополнительная плавучесть в кг сверх массы объекта
    [SerializeField] Vector3 buoyancyForce = Vector3.up; // Плавучесть | Сила, с которой выталкивается объект | Положительные значеие
    [SerializeField] bool autoCenterOffBuoyancy = false; // True: Автоматический центр | False: использует CenterOffBuoyancy
    [SerializeField] Vector3 centerOffBuoyancy = new Vector3(-6.68785f, 1.1273f, -0.0439f); // Центр всплывания - только при выключенном AutoCenterOffBuoyancy
    [SerializeField] float metacentricHeight = DefaultMetacentricHeight; // Добавочный сдвиг центра плавучести по продольной оси для остойчивости
    [SerializeField] float GenerationStepCOB = DefaultGenerationStepCOB; // Шаг, с которым генерируются точки - только при включенном AutoCenterOffBuoyancy

    // Сопротивление
    [Header("Resistance")]
    [SerializeField] float forwardDrag = DefaultForwardDrag; // сопротивление движения по локальной оси X (продольной)
    [SerializeField] float lateralDrag = DefaultLateralDrag; // сопротивление движения по локальной оси Z (боковой)
    [SerializeField] float verticalDrag = DefaultVerticalDrag; // сопротивление движения по локальной оси Y (вертикальной)

    [SerializeField] float rollDrag = DefaultRollDrag; // сопротитивление вращению вокруг локальной оси X
    [SerializeField] float pitchDrag = DefaultPitchDrag; // сопротивление вращению вокруг локальной оси Z
    [SerializeField] float yawDrag = DefaultYawDrag; // сопротивление вращению вокруг локальной оси Y

    // === Переменные класса ===
    private Vector3[] COBSamplePoints = null; // сетка точек, входящих в объект, гарантируется, что между ними растояние одинаково (если нет разрыва) | Координаты локальные

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

        rb = GetComponent<Rigidbody>();
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
        ApplyLegacyDragDefaults();
    }

    // Проверяет, что локальная точка в колайдере
    public bool IsLocalPointInCollider(Vector3 localPoint)
    {
        Vector3 worldPoint = transform.TransformPoint(localPoint);
        return (col.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude < 0.0001f;
    }

    // Генерирует сетку точек, которые входят в объект | Принимает step
    protected void GenerateGrid(float step)
    {
        Vector3 max = transform.InverseTransformPoint(col.bounds.max);
        Vector3 min = transform.InverseTransformPoint(col.bounds.min);

        // Максимальные координаты
        float max_x = max[0];
        float max_y = max[1];
        float max_z = max[2];

        // Минимальные координаты
        float min_x = min[0];
        float min_y = min[1];
        float min_z = min[2];

        List<Vector3> tmp_points = new List<Vector3>();

        for (float x = min_x; x <= max_x; x += step)
        {
            for (float y = min_y; y <= max_y; y += step)
            {
                for (float z = min_z; z <= max_z; z += step)
                {
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
        if (COBSamplePoints == null || COBSamplePoints.Length == 0)
        {
            return;
        }

        WaterInformation inf = GetWaterInformation();
        BuoyantForce(inf); // Сначала применяем силу архимеда
        ForceDrag(inf); // Потом применяем сопротивление в этом же момента
    }
}
