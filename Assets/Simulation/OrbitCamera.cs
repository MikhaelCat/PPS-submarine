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
    public float yMinLimit = -20f;
    public float yMaxLimit = 80f;

    // === Переменные класса ===
    private float x = 0.0f;
    private float y = 0.0f;

    // Инициализирует стартовые углы камеры
    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;
    }

    // Обновляет орбитальную камеру после движения цели
    void LateUpdate()
    {
        if (target == null)
        {
            return;
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
            distance = Mathf.Clamp(distance - (scroll * 0.01f), minDistance, maxDistance);
        }
    }

    // Выставляет финальные позицию и поворот камеры
    private void UpdateCameraTransform()
    {
        Quaternion rotation = Quaternion.Euler(y, x, 0);
        Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
        Vector3 position = rotation * negDistance + target.position;

        transform.rotation = rotation;
        transform.position = position;
    }

    // Ограничивает угол в заданном диапазоне
    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}
