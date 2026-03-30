using UnityEngine;

// Визуальная анимация пропеллеров AUV на основе текущих команд моторов
[RequireComponent(typeof(AUV))]
public class AUVAnimation : MonoBehaviour
{
    // === Unity параметры ===
    [Header("References")]
    [SerializeField] private AUV auv;
    [SerializeField] private AUVAnimationSettings animationSettings;

    [Header("Motor Parts")]
    [SerializeField] private Transform topFrontMotorPart;
    [SerializeField] private Transform topRearMotorPart;
    [SerializeField] private Transform sideLeftMotorPart;
    [SerializeField] private Transform sideRightMotorPart;

    [Header("Animation")]
    [SerializeField] private float maxDegreesPerSecond = 1440f;
    [SerializeField] private Vector3 topMotorsAxis = Vector3.right;
    [SerializeField] private Vector3 sideMotorsAxis = Vector3.right;
    [SerializeField] private bool invertTopMotors = false;
    [SerializeField] private bool invertSideMotors = false;

    // === Константы ===
    private const string TopFrontPartName = "Properell2";
    private const string TopRearPartName = "Properell4";
    private const string SideLeftPartName = "Properell1";
    private const string SideRightPartName = "Properell3";

    // === Unity жизненный цикл ===

    // Инициализирует ссылки и автопоиск частей модели
    private void Awake()
    {
        CacheComponents();
        AutoAssignPartsIfNeeded();
    }

    // Удобный автозаполнитель при добавлении компонента
    private void Reset()
    {
        CacheComponents();
        AutoAssignPartsIfNeeded();
    }

    // Обновляет вращение пропеллеров каждый кадр
    private void Update()
    {
        if (auv == null || animationSettings == null)
        {
            return;
        }

        AnimateMotor(topFrontMotorPart, animationSettings.TopFrontMotorId, topMotorsAxis, invertTopMotors);
        AnimateMotor(topRearMotorPart, animationSettings.TopRearMotorId, topMotorsAxis, invertTopMotors);
        AnimateMotor(sideLeftMotorPart, animationSettings.SideLeftMotorId, sideMotorsAxis, invertSideMotors);
        AnimateMotor(sideRightMotorPart, animationSettings.SideRightMotorId, sideMotorsAxis, invertSideMotors);
    }

    // === Вспомогательные функции ===

    // Кэширует обязательные/опциональные компоненты
    private void CacheComponents()
    {
        if (auv == null)
        {
            auv = GetComponent<AUV>();
        }

        if (animationSettings == null)
        {
            animationSettings = GetComponent<AUVAnimationSettings>();
        }
    }

    // Автоматически привязывает части пропеллеров по имени из модели
    private void AutoAssignPartsIfNeeded()
    {
        if (topFrontMotorPart == null)
        {
            topFrontMotorPart = FindChildRecursive(transform, TopFrontPartName);
        }

        if (topRearMotorPart == null)
        {
            topRearMotorPart = FindChildRecursive(transform, TopRearPartName);
        }

        if (sideLeftMotorPart == null)
        {
            sideLeftMotorPart = FindChildRecursive(transform, SideLeftPartName);
        }

        if (sideRightMotorPart == null)
        {
            sideRightMotorPart = FindChildRecursive(transform, SideRightPartName);
        }
    }

    // Вращает визуальный мотор согласно текущей команде силы
    private void AnimateMotor(Transform motorPart, int motorId, Vector3 axis, bool invert)
    {
        if (motorPart == null)
        {
            return;
        }

        if (!auv.TryGetMotorCommandPercent(motorId, out float commandPercent))
        {
            return;
        }

        if (Mathf.Approximately(commandPercent, 0f))
        {
            return;
        }

        float direction = invert ? -1f : 1f;
        float rotationStep = maxDegreesPerSecond * (commandPercent / 100f) * direction * Time.deltaTime;
        Vector3 rotationAxis = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.right;

        motorPart.Rotate(rotationAxis, rotationStep, Space.Self);
    }

    // Ищет дочерний трансформ рекурсивно по имени
    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
