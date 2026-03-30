using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    // === Unity параметры ===
    public Transform target;
    public float distance = 7.0f;
    public float minDistance = 2.0f;
    public float maxDistance = 15.0f;
    public float xSpeed = 20.0f;
    public float ySpeed = 20.0f;
    public float zoomSpeed = 0.01f;
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;
    public bool autoFrameTargetOnStart = true;
    public float framingPadding = 1.3f;

    // === Переменные класса ===
    private float x = 0.0f;
    private float y = 0.0f;
    private Vector3 targetFocusOffset = Vector3.zero;
    private Transform lastTarget;
    private Camera cachedCamera;

    // Инициализирует стартовые углы камеры
    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
        cachedCamera = GetComponent<Camera>();

        if (target != null && autoFrameTargetOnStart)
        {
            FrameTarget();
        }

        lastTarget = target;
    }

    // Обновляет орбитальную камеру после движения цели
    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (target != lastTarget)
        {
            lastTarget = target;
            if (autoFrameTargetOnStart)
            {
                FrameTarget();
            }
        }

        HandleRotationInput();
        HandleZoomInput();
        UpdateCameraTransform();
    }

    // Обрабатывает поворот камеры правой кнопкой мыши
    private void HandleRotationInput()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.isPressed)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        x += mouseDelta.x * xSpeed * Time.deltaTime;
        y -= mouseDelta.y * ySpeed * Time.deltaTime;
        y = ClampAngle(y, yMinLimit, yMaxLimit);
    }

    // Обрабатывает зум колесиком мыши
    private void HandleZoomInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll != 0)
        {
            distance = Mathf.Clamp(distance - (scroll * zoomSpeed), minDistance, maxDistance);
        }
    }

    // Выставляет финальные позицию и поворот камеры
    private void UpdateCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 focusPoint = target.position + targetFocusOffset;
        Vector3 position = rotation * negDistance + focusPoint;

        transform.rotation = rotation;
        transform.position = position;
    }

    [ContextMenu("Frame Target")]
    public void FrameTarget()
    {
        if (target == null)
        {
            return;
        }

        if (TryGetTargetBounds(out Bounds bounds))
        {
            targetFocusOffset = bounds.center - target.position;

            float radius = bounds.extents.magnitude;
            if (radius > 0.001f)
            {
                Camera referenceCamera = cachedCamera != null ? cachedCamera : Camera.main;
                float halfFovRad = referenceCamera != null
                    ? referenceCamera.fieldOfView * 0.5f * Mathf.Deg2Rad
                    : 30f * Mathf.Deg2Rad;

                float fittedDistance = radius / Mathf.Tan(halfFovRad);
                distance = Mathf.Clamp(fittedDistance * framingPadding, minDistance, maxDistance);
            }
        }
        else
        {
            targetFocusOffset = Vector3.zero;
        }
    }

    public void SetTarget(Transform newTarget, bool frameImmediately = true)
    {
        target = newTarget;
        lastTarget = newTarget;

        if (target != null && frameImmediately)
        {
            FrameTarget();
            UpdateCameraTransform();
        }
    }

    private bool TryGetTargetBounds(out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }

    // Ограничивает угол в заданном диапазоне
    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}
