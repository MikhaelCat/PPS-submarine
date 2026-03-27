// Wibe code
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Цель")]
    public Transform target; // Сюда перетащи свой подводный аппарат в инспекторе

    [Header("Настройки дистанции")]
    public float distance = 7.0f;     // На каком расстоянии от объекта камера
    public float minDistance = 2.0f;   // Минимальный зум
    public float maxDistance = 15.0f;  // Максимальный зум

    [Header("Скорость вращения")]
    public float xSpeed = 120.0f; // Скорость по горизонтали
    public float ySpeed = 120.0f; // Скорость по вертикали

    [Header("Ограничения углов")]
    public float yMinLimit = -20f; // Чтобы не заглядывать слишком сильно под дно
    public float yMaxLimit = 80f;

    private float x = 0.0f;
    private float y = 0.0f;

    void Start()
    {
        // Инициализируем текущие углы камеры
        Vector3 angles = transform.eulerAngles;
        x = angles.y;
        y = angles.x;

        // Если цель не назначена, пробуем найти объект с твоим скриптом WaterObject
        if (target == null)
        {
            var obj = FindFirstObjectByType<WaterObject>();
            if (obj != null) target = obj.transform;
        }
    }

    void LateUpdate() // Камера всегда обновляется в LateUpdate
    {
        if (target)
        {
            // Вращение происходит при зажатой правой кнопке мыши (удобно для тестов)
            // Если хочешь всегда — убери условие Input.GetMouseButton(1)
            if (Input.GetMouseButton(1))
            {
                x += Input.GetAxis("Mouse X") * xSpeed * 0.02f;
                y -= Input.GetAxis("Mouse Y") * ySpeed * 0.02f;
            }

            // Ограничиваем вертикальный угол
            y = ClampAngle(y, yMinLimit, yMaxLimit);

            // Обработка Зума (колесико мыши)
            distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 5, minDistance, maxDistance);

            // Рассчитываем вращение и позицию
            Quaternion rotation = Quaternion.Euler(y, x, 0);
            Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
            Vector3 position = rotation * negDistance + target.position;

            // Применяем значения
            transform.rotation = rotation;
            transform.position = position;
        }
    }

    // Вспомогательная функция для ограничения углов
    public static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360F) angle += 360F;
        if (angle > 360F) angle -= 360F;
        return Mathf.Clamp(angle, min, max);
    }
}