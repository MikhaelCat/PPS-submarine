using UnityEngine;
using System.Collections.Generic;

public class ServerLauncher : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = Settings.port;

    private ServerRuntime _server;

    void Start()
    {
        // Инициализация ядра
        _server = new ServerRuntime(port);

        // --- БИНДИНГ ФУНКЦИЙ (Пример из ТЗ) ---
        _server.AddRequest("set_player_speed", (values) =>
        {
            // Важно: Newtonsoft парсит числа как Int64 или Double. 
            // Используем Convert.ToDouble для безопасного извлечения.

            if (!values.ContainsKey("speed") || !values.ContainsKey("duration"))
            {
                return (400, "Missing required parameters: 'speed' or 'duration'", null);
            }

            double speed = System.Convert.ToDouble(values["speed"]);
            double duration = System.Convert.ToDouble(values["duration"]);

            // ВАЖНО: Так как это выполняется не в Main Thread, здесь нельзя напрямую
            // вызывать методы Unity API (например, transform.position). 
            // Если нужно передать данные в Unity, их нужно сложить в ConcurrentQueue.

            // Формируем результат для возврата клиенту
            var result = new Dictionary<string, object>
            {
                { "currentSpeed", speed },
                { "appliedDuration", duration }
            };

            return (200, "Success", result);
        });

        // Пример: Команда, которая всегда выдает ошибку (для проверки 500)
        _server.AddRequest("force_error", (values) =>
        {
            throw new System.Exception("This is a simulated crash inside a bound function.");
        });

        // Запуск
        _server.Start();
        Debug.Log($"UDP API Server started on port {port} (Autonomous Mode)");
    }

    void OnDestroy()
    {
        _server?.Stop();
    }

    void OnApplicationQuit()
    {
        _server?.Stop();
    }
}