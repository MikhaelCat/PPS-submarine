using System;
using UnityEngine;

// Общие настройки для всех AUV в сцене
public class AUVSettings : MonoBehaviour
{
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
    [SerializeField] private float maxPower = 100f;
    [SerializeField] private ForcePoint[] forcePoints = Array.Empty<ForcePoint>();

    // === Переменные класса ===
    private static AUVSettings shared;

    // Публичные свойства для чтения параметров
    public float MaxPower => maxPower;
    public ForcePoint[] ForcePoints => forcePoints ?? Array.Empty<ForcePoint>();

    // Регистрирует общий экземпляр настроек
    private void Awake()
    {
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
}
