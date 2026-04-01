using System;
using UnityEngine;

// Общие настройки для всех AUV в сцене
public class AUVSettings : MonoBehaviour
{
    private const float DefaultMaxPower = 10000f;
    private const int DefaultMBESPointsCount = 1024;
    private const int DefaultMBESDistance = 80;
    private const float DefaultMBESMaxRange = 200f;
    private static readonly Vector3 DefaultMBESLookDirection = Vector3.down;
    private static readonly Vector3 DefaultMBESSpanDirection = Vector3.right;

    // === Структуры данных ===
    [Serializable]
    public struct ForcePoint
    {
        [SerializeField] public int id;
        [SerializeField] public Vector3 localPoint;
        [SerializeField] public Vector3 localDirection;
    }
    
    // === Unity параметры ===
    [Header("Motors")]
    [SerializeField] private float maxPower = DefaultMaxPower;
    [SerializeField] private ForcePoint[] forcePoints = CreateDefaultForcePoints();

    [Header("MBES")]
    [SerializeField] public int MBESPointsCount = DefaultMBESPointsCount; // количество точек
    [SerializeField] public int MBESDistance = DefaultMBESDistance; // ширина в метрах
    [SerializeField] public float MBESMaxRange = 200f; // максимальная длина луча
    [SerializeField] public Transform MBESPoint; // точка для MBED
    [SerializeField] public Vector3 MBESLookDirection = new Vector3(0f, -1f, 0f); // куда смотрит центральный луч в локальных координатах MBESPoint
    [SerializeField] public Vector3 MBESSpanDirection = new Vector3(1f, 0f, 0f); // вдоль какой оси строится полоса в локальных координатах MBESPoint
    

    // === Переменные класса ===
    private static AUVSettings shared;


    // Публичные свойства для чтения параметров
    public float MaxPower => maxPower;
    public ForcePoint[] ForcePoints => forcePoints ?? Array.Empty<ForcePoint>();

    private static ForcePoint[] CreateDefaultForcePoints()
    {
        return new[]
        {
            new ForcePoint
            {
                id = 1,
                localPoint = new Vector3(-2.5943f, -0.2098f, 3.5286f),
                localDirection = Vector3.right,
            },
            new ForcePoint
            {
                id = 2,
                localPoint = new Vector3(-2.5943f, -0.2109f, -3.5237f),
                localDirection = Vector3.right,
            },
            new ForcePoint
            {
                id = 3,
                localPoint = new Vector3(-1.5834f, 1.1273f, -0.0439f),
                localDirection = Vector3.up,
            },
            new ForcePoint
            {
                id = 4,
                localPoint = new Vector3(-11.7923f, 1.1273f, -0.0439f),
                localDirection = Vector3.up,
            },
        };
    }

    private void EnsureDefaultsIfNeeded()
    {
        if (maxPower <= 0f)
        {
            maxPower = DefaultMaxPower;
        }

        if (forcePoints == null || forcePoints.Length == 0)
        {
            forcePoints = CreateDefaultForcePoints();
        }

        if (MBESPointsCount < 2)
        {
            MBESPointsCount = DefaultMBESPointsCount;
        }

        MBESDistance = Mathf.Clamp(MBESDistance, 1, 100);

        if (MBESMaxRange <= 0f)
        {
            MBESMaxRange = DefaultMBESMaxRange;
        }

        if (MBESLookDirection.sqrMagnitude < 0.0001f)
        {
            MBESLookDirection = DefaultMBESLookDirection;
        }

        if (MBESSpanDirection.sqrMagnitude < 0.0001f)
        {
            MBESSpanDirection = DefaultMBESSpanDirection;
        }
    }

    private void Reset()
    {
        maxPower = DefaultMaxPower;
        forcePoints = CreateDefaultForcePoints();
        MBESPointsCount = DefaultMBESPointsCount;
        MBESDistance = DefaultMBESDistance;
        MBESMaxRange = DefaultMBESMaxRange;
        MBESLookDirection = DefaultMBESLookDirection;
        MBESSpanDirection = DefaultMBESSpanDirection;
    }

    // Регистрирует общий экземпляр настроек
    private void Awake()
    {
        EnsureDefaultsIfNeeded();

        if (shared != null && shared != this)
        {
            Debug.LogWarning("More than one AUVSettings exists in the scene. The first instance will be used.");
            return;
        }

        shared = this;
    }

    // Возвращает общий экземпляр настроек
    public static AUVSettings GetOrFind()
    {
        if (shared != null)
        {
            return shared;
        }

        shared = UnityEngine.Object.FindAnyObjectByType<AUVSettings>();
        return shared;
    }

    private void OnValidate()
    {
        EnsureDefaultsIfNeeded();
    }
}
