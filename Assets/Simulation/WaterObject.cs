using System.Collections.Generic;
using UnityEngine;


// Инициализирует класс, который взаимодействует с водой (Сопротивление + Архимедова сила) | Требует Colider и Rigbody
[RequireComponent(typeof(Collider), typeof(Rigidbody))] // Требуется Colider и Rigbody
public class WaterObject :  MonoBehaviour
{   

    // Константы
    static float waterLevelY = 0f; 

    // === Unity параметры ===

    // Сила архимеда
    [Header ("Buoyant force")]
    [SerializeField] bool autoBuoyancyForce = false; // Автоматически балансирует плавучесть с гравитацией;
    [SerializeField] Vector3 buoyancyForce = new Vector3(0, 1, 0); // Плавучесть | Сила, с которой выталкивается объект | Положительные значеие
    [SerializeField] bool autoCenterOffBuoyancy = true; // True: Автоматический центр | False: использует CenterOffBuoyancy
    [SerializeField] Vector3 centerOffBuoyancy = new Vector3(0, 0, 0); // Центр всплывания - только при выключенном AutoCenterOffBuoyancy
    [SerializeField] float GenerationStepCOB = 0.25f; // Шаг, с которым генерируются точки - только при включенном AutoCenterOffBuoyancy
    
    // Сопротивление
    [Header ("Resistance")]
    [SerializeField] float forwardDrag; // сопротивление движения по x
    [SerializeField] float lateralDrag; // сопротивление движения по y
    [SerializeField] float verticalDrag; // сопротивление движения по z

    [SerializeField] float rollDrag; // сопротитивление движения по крену
    [SerializeField] float pitchDrag; // сопротивления движения по тангажу
    [SerializeField] float yawDrag; // сопротивление по рысканию
    
    // === Переменные класса ===
    private Vector3[] COBSamplePoints = null; // сетка точек, входящих в объект, гарантируется, что между ними растояние одинаково (если нет разрыва) | Координаты локальные

    // Компоненты
    private Collider col;
    private Rigidbody rb;
    
    // === Инициализация | Стартовая генерация ===

    // Инициализация параметров и получение компонентов
    protected void Init()
    {
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        if (GenerationStepCOB <= 0) GenerationStepCOB = 0.25f;
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
            for (float y = min_y; y <= max_y; y += step)
                for (float z = min_z; z <= max_z; z += step)
                {
                    Vector3 point = new Vector3(x, y, z);
                    if (IsLocalPointInCollider(point)){
                        tmp_points.Add(point);
                    }
                }
        COBSamplePoints = tmp_points.ToArray();
    }

    // Автоматически создает bouncy, который может сопротевлятся гравитации в полной мере
    protected void AutoBouncy()
    {
        if (autoBuoyancyForce) buoyancyForce = -(rb.mass * Physics.gravity);
    }

    // Установить нулевое сопротивление, что бы не работала стандартная физика Unity
    protected void SetZeroDrag()
    {
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }
    
    // Стартовая настройка
    public void Start()
    {   
        Init();
        GenerateGrid(GenerationStepCOB);
        if (autoBuoyancyForce) AutoBouncy();
        SetZeroDrag();
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
    };

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
                xsum += point[0]; ysum += point[1]; zsum += point[2];
                points_in_water_count += 1;
            }
            
        }
        bool inWater = true;
        float ratio;
        float x = 0;
        float y = 0;
        float z = 0;
        if (points_in_water_count == 0){ // Если мы не в воде, то силы ен будет
            ratio = 0;
            inWater = false;
            }
        else {
            ratio = (float)points_in_water_count / (float)COBSamplePoints.Length;
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
        if (information.inWater){
            Vector3 COB = new Vector3(0, 0, 0);
            if (autoCenterOffBuoyancy) COB = information.LocalCOB;
            else COB = centerOffBuoyancy;
            COB = transform.TransformPoint(COB);
            rb.AddForceAtPosition(buoyancyForce * information.ratio, COB, ForceMode.Force);
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

        float drag_x = -lateralDrag * velocity_x * Mathf.Abs(velocity_x); 
        float drag_y = -verticalDrag * velocity_y * Mathf.Abs(velocity_y); 
        float drag_z = -forwardDrag * velocity_z * Mathf.Abs(velocity_z); 

        return new Vector3(drag_x, drag_y, drag_z);
    }

    // Расчет сопротивления поворота | Использовать с Relative (т.к. локальная сила)
    public Vector3 LocalAngularDrag()
    {
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        float velocity_x = localAngularVelocity[0];
        float velocity_y = localAngularVelocity[1];
        float velocity_z = localAngularVelocity[2];

        float drag_x = -pitchDrag * velocity_x * Mathf.Abs(velocity_x);
        float drag_y = -yawDrag * velocity_y * Mathf.Abs(velocity_y);
        float drag_z = -rollDrag * velocity_z * Mathf.Abs(velocity_z);

        return new Vector3(drag_x, drag_y, drag_z);
    }

    // Приминение сопротивления
    protected void ForceDrag(WaterInformation information)
    {   
        if (information.inWater){
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

}