using System;
using System.Collections.Generic;
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

    // === Константы ===
    private const string TopFrontPartName = "Properell2";
    private const string TopRearPartName = "Properell4";
    private const string SideLeftPartName = "Properell1";
    private const string SideRightPartName = "Properell3";
    private static readonly string[] MotorNameTokens = { "properell", "propeller", "thruster", "motor" };

    private bool missingSetupLogged = false;
    private bool missingPartsLogged = false;

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
            if (!missingSetupLogged)
            {
                Debug.LogWarning("AUVAnimation: AUV or AUVAnimationSettings is missing. Animation is disabled.", this);
                missingSetupLogged = true;
            }

            return;
        }

        missingSetupLogged = false;

        if (topFrontMotorPart == null || topRearMotorPart == null || sideLeftMotorPart == null || sideRightMotorPart == null)
        {
            AutoAssignPartsIfNeeded();
            if (!missingPartsLogged && (topFrontMotorPart == null || topRearMotorPart == null || sideLeftMotorPart == null || sideRightMotorPart == null))
            {
                Debug.LogWarning("AUVAnimation: One or more motor parts are not assigned. Run ObjPartsLogger and bind transforms in Inspector.", this);
                missingPartsLogged = true;
            }
        }
        else
        {
            missingPartsLogged = false;
        }

        AnimateMotor(topFrontMotorPart, animationSettings.TopFrontMotorId, animationSettings.TopFrontRotationVector);
        AnimateMotor(topRearMotorPart, animationSettings.TopRearMotorId, animationSettings.TopRearRotationVector);
        AnimateMotor(sideLeftMotorPart, animationSettings.SideLeftMotorId, animationSettings.SideLeftRotationVector);
        AnimateMotor(sideRightMotorPart, animationSettings.SideRightMotorId, animationSettings.SideRightRotationVector);
    }

    // === Вспомогательные функции ===

    public Transform[] GetAnimatedParts()
    {
        return new[]
        {
            topFrontMotorPart,
            topRearMotorPart,
            sideLeftMotorPart,
            sideRightMotorPart
        };
    }

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

        if (animationSettings == null)
        {
            animationSettings = gameObject.AddComponent<AUVAnimationSettings>();
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

        if (topFrontMotorPart == null || topRearMotorPart == null || sideLeftMotorPart == null || sideRightMotorPart == null)
        {
            List<Transform> candidates = new List<Transform>();
            CollectMotorCandidates(transform, candidates);
            candidates.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

            int index = 0;
            if (topFrontMotorPart == null && index < candidates.Count) topFrontMotorPart = candidates[index++];
            if (topRearMotorPart == null && index < candidates.Count) topRearMotorPart = candidates[index++];
            if (sideLeftMotorPart == null && index < candidates.Count) sideLeftMotorPart = candidates[index++];
            if (sideRightMotorPart == null && index < candidates.Count) sideRightMotorPart = candidates[index++];
        }
    }

    // Вращает визуальный мотор согласно текущей команде силы
    private void AnimateMotor(Transform motorPart, int motorId, Vector3 rotationVector)
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

        Vector3 rotationAxis = rotationVector.sqrMagnitude > 0.0001f ? rotationVector.normalized : Vector3.right;
        float rotationStep = maxDegreesPerSecond * (commandPercent / 100f) * Time.deltaTime;
        Vector3 worldAxis = motorPart.TransformDirection(rotationAxis).normalized;

        if (TryGetGeometricCenterWorld(motorPart, out Vector3 centerWorld))
        {
            // Some imported propeller meshes have their pivot stuck at the model origin.
            // Rotate around the visible geometry center instead of the transform pivot.
            motorPart.RotateAround(centerWorld, worldAxis, rotationStep);
        }
        else
        {
            motorPart.Rotate(rotationAxis, rotationStep, Space.Self);
        }
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

    private static void CollectMotorCandidates(Transform root, List<Transform> output)
    {
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            string childName = child.name.ToLowerInvariant();

            for (int tokenIndex = 0; tokenIndex < MotorNameTokens.Length; tokenIndex++)
            {
                if (childName.Contains(MotorNameTokens[tokenIndex]))
                {
                    output.Add(child);
                    break;
                }
            }

            CollectMotorCandidates(child, output);
        }
    }

    private static bool TryGetGeometricCenterWorld(Transform motorPart, out Vector3 centerWorld)
    {
        Renderer[] renderers = motorPart.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            centerWorld = motorPart.position;
            return false;
        }

        bool hasBounds = false;
        Bounds combinedBounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            centerWorld = motorPart.position;
            return false;
        }

        centerWorld = combinedBounds.center;
        return true;
    }
}
