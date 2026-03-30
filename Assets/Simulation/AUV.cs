using System.Collections.Generic;
using UnityEngine;


// Класс управления
[RequireComponent(typeof(Collider), typeof(Rigidbody), typeof(WaterObject))] // Требуется Colider и Rigbody и WaterObject
public class AUV : MonoBehaviour
{
    private static readonly Vector3 DefaultInertiaTensor = new Vector3(3333.3333f, 1666.6666f, 3333.3333f);
    private const float DefaultMaxYawControlTorque = 705.23f;

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

    protected Motor[] Motors = System.Array.Empty<Motor>();
    protected float ForceRatio = 1;
    protected float YawControlTorque = 0f;

    Rigidbody rb;
    
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

        AUVSettings settings = AUVSettings.GetOrFind();
        if (settings == null)
        {
            Debug.LogError("AUVSettings was not found in scene. Add one AUVSettings component to any GameObject.");
            enabled = false;
            return;
        }

        AUVSettings.ForcePoint[] forcePoints = settings.ForcePoints;

        // Создания моторов в массиве
        List<Motor> tmp_motorForcePoints = new List<Motor>(forcePoints.Length);
        for (int i = 0; i < forcePoints.Length; i++)
        {
            tmp_motorForcePoints.Add(
                new Motor
                {
                    inf = forcePoints[i],
                    force = new Vector3(0, 0, 0),
                    commandPercent = 0f
                }
                );
        }
        Motors = tmp_motorForcePoints.ToArray();

        // Расчет отношения для диапозона
        ForceRatio = settings.MaxPower / 100f;

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

    // === MBES ===
    void MBESInit()
    {
        
    }



    // === UI ===
    void MBESDisplay()
    {
        
    }
    void MotorInfDisplay()
    {
        
    }
    void PositionDisplay()
    {
        
    }
    void UIInit()
    {
        if (!UIOn) return;

    }
    void UIDisplay()
    {
        if (!UIOn) return;
    }
    void Update()
    {
        UIDisplay();
    }
}
