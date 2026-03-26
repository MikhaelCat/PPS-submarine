using System.Collections.Generic;
using UnityEngine;


// Инициализирует класс, который взаимодействует с водой (Сопротивление + Архимедова сила)
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class WaterObject :  MonoBehaviour
{   
    // Unity параметры

    // Сила архимеда
    [Header ("Buoyant force")]
    [SerializeField] bool autoBouncy = false; // Автоматически балансирует bouncy с гравитацией;
    [SerializeField] Vector3 bouncy = new Vector3(0, 1, 0); // Плавучесть | Сила, с которой выталкивается объект | Положительные значеие
    [SerializeField] bool autoCenterOffBouncy = true; // True: Автоматический центр | False: использует CenterOffBouncy
    [SerializeField] Vector3 centerOffBouncy = new Vector3(0, 0, 0); // Центр всплывания - только при выключенном AutoCenterOffBouncy
    [SerializeField] float GenerationStepCOB = 0.25f; // Шаг, с которым генерируются точки - только при включенном AutoCenterOffBouncy
    
    // Сопротивление
    [Header ("Resistance")]
    [SerializeField] float forwardDrag; // сопротивление движения по x
    [SerializeField] float lateralDrag; // сопротивление движения по y
    [SerializeField] float verticalDrag; // сопротивление движения по z

    [SerializeField] float rollDrag; // сопротитивление движения по крену
    [SerializeField] float pitchDrag; // сопротивления движения по тангажу
    [SerializeField] float yawDrag; // сопротивление по рысканию

    // Переменные класса
    private Vector3[] COBSamplePoints = null; // сетка точек, входящих в объект, гарантируется, что между ними растояние одинаково (если нет разрыва) | Координаты локальные

    // Компоненты
    private Collider col;
    private Rigidbody rb;
    
    // Базовая инициализация
    protected void Init()
    {
        col = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
        if (GenerationStepCOB <= 0) GenerationStepCOB = 0.25f;
    }

    public bool IsPointInCollider(Vector3 point)
    {
        return (col.ClosestPoint(point) - point).sqrMagnitude < 0.0001f;
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
                    if (IsPointInCollider(point)){
                        tmp_points.Add(point);
                    }
                }
    }

    // Автоматически создает bouncy, который может сопротевлятся гравитации в полной мере
    protected void AutoBouncy()
    {
        if (autoBouncy) bouncy = -(rb.mass * Physics.gravity);
    }

    // Установить нулевое сопротивление, что бы не работала стандартная физика Unity
    protected void SetZeroDrag()
    {
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
    }
    
    public Vector3 linearDragForce()
    {
        float velocity_x = rb.linearVelocity[0];
        float velocity_y = rb.linearVelocity[1];
        float velocity_z = rb.linearVelocity[2];

        float drag_x = -forwardDrag * velocity_x * Mathf.Abs(velocity_x);
        float drag_y = -lateralDrag * velocity_y * Mathf.Abs(velocity_y);
        float drag_z = -verticalDrag * velocity_z * Mathf.Abs(velocity_z);

        return new Vector3(drag_x, drag_y, drag_z);
    }

    public void Start()
    {   
        Init();
        GenerateGrid(GenerationStepCOB);
        if (autoBouncy) AutoBouncy();
        SetZeroDrag();
    }


}