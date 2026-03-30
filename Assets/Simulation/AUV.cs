using System.Collections.Generic;
using UnityEngine;


// Класс управления
[RequireComponent(typeof(Collider), typeof(Rigidbody), typeof(WaterObject))] // Требуется Colider и Rigbody и WaterObject
public class AUV : MonoBehaviour
{   
    // ID
    protected static int nextid = 0;
    public int id;

    // Переменные класса
    protected struct Motor
    {
        public AUVSettings.ForcePoint inf;
        public Vector3 force;
    }

    protected Motor[] Motors = System.Array.Empty<Motor>();
    protected float ForceRatio = 1;

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
                    force = new Vector3(0, 0, 0)
                }
                );
        }
        Motors = tmp_motorForcePoints.ToArray();

        // Расчет отношения для диапозона
        ForceRatio = settings.MaxPower / 100f;
    }
    private void Awake()
    {   
        SetID();
        Init();
    }

    private void FixedUpdate()
    {
        ApllyMotorForce();
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

    // Устанавли вает скорость мотору по id | 1 ошибка нет id, 2 сила не в диапозоне -100 - 100, 0 - все ок
    public int SetMotorForce(int id, float force)
    {
        if (force > 100 || force < -100) return 2;

        int MotorIndex = GetMotorIndexById(id);
        if (MotorIndex < 0) return 1;
        
        Motors[MotorIndex].force = Motors[MotorIndex].inf.localDirection * (ForceRatio * force);

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

    public void ApllyMotorForce()
    {
        for (int i = 0; i < Motors.Length; i++)
        {
            rb.AddRelativeForce(Motors[i].force);
        }
    }

    public void ManualControl()
    {
    }


}
