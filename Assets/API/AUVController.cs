using UnityEngine;

public class AUVController : MonoBehaviour
{
    [Header("Настройки аппарата")]
    [Tooltip("ID должен совпадать с ID в Python-клиенте (1, 2 или 3)")]
    public int auvId;

    [Header("Отладка")]
    public bool showDebugLogs = true;

    // Ссылка на компоненты (физик добавит сюда Rigidbody и т.д.)
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// Главный входной узел для команд из сети.
    /// Этот метод распределяет строковые команды по конкретным функциям.
    /// </summary>
    public void ApplyCommand(string cmd, float val)
    {
        if (showDebugLogs)
            Debug.Log($"<color=cyan>[АНПА {auvId}]</color> Получена команда: <b>{cmd}</b> со значением: <b>{val}</b>");

        switch (cmd.ToLower())
        {
            case "move_forward": OnMoveForward(val); break;
            case "move_backward": OnMoveBackward(val); break;
            case "rotate": OnRotate(val); break;
            case "set_depth": OnSetDepth(val); break;
            case "stop": OnStop(); break;
            default:
                Debug.LogWarning($"[АНПА {auvId}] Неизвестная команда: {cmd}");
                break;
        }
    }

    // --- СЕКЦИЯ ДЛЯ РАЗРАБОТЧИКА ФИЗИКИ (МЕСТО ДЛЯ ЛОГИКИ) ---

    private void OnMoveForward(float power)
    {
        // Разработчик физики вставит здесь: rb.AddForce(transform.forward * power);
        if (showDebugLogs) Debug.Log($"<color=green>Логика:</color> Движение вперед на мощности {power}");
    }

    private void OnMoveBackward(float power)
    {
        // Логика движения назад
        if (showDebugLogs) Debug.Log($"<color=green>Логика:</color> Движение назад на мощности {power}");
    }

    private void OnRotate(float angle)
    {
        // Разработчик физики вставит здесь: rb.AddTorque(transform.up * angle);
        if (showDebugLogs) Debug.Log($"<color=green>Логика:</color> Поворот на угол/силу {angle}");
    }

    private void OnSetDepth(float targetDepth)
    {
        // Логика погружения
        if (showDebugLogs) Debug.Log($"<color=green>Логика:</color> Установка глубины: {targetDepth} м.");
    }

    private void OnStop()
    {
        // Логика полной остановки (например, обнуление скоростей)
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (showDebugLogs) Debug.Log("<color=red>Логика:</color> ПОЛНАЯ ОСТАНОВКА");
    }

    // --- СЕКЦИЯ СБОРА ТЕЛЕМЕТРИИ ---

    public AUVTelemetry GetCurrentTelemetry()
    {
        AUVTelemetry tele = new AUVTelemetry();

        tele.auv_id = auvId;

        // Здесь можно реализовать перевод Unity-координат в Lat/Lon
        tele.lat = 43.1200 + (transform.position.z * 0.00001);
        tele.lon = 131.9200 + (transform.position.x * 0.00001);

        // Расчет глубины и высоты (в Unity Y обычно вверх)
        tele.depth = Mathf.Max(0, -transform.position.y);
        tele.alt = CalculateDistanceToBottom();

        // Углы Эйлера
        tele.pitch = transform.rotation.eulerAngles.x;
        tele.yaw = transform.rotation.eulerAngles.y;
        tele.roll = transform.rotation.eulerAngles.z;

        // Скорость берем напрямую из физики
        tele.velocity = rb != null ? rb.linearVelocity.magnitude : 0.0f;

        return tele;
    }

    private float CalculateDistanceToBottom()
    {
        // Простой луч вниз для определения высоты над дном (эхолот)
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 1000f))
        {
            return hit.distance;
        }
        return 100f; // Значение по умолчанию, если дно слишком далеко
    }
}