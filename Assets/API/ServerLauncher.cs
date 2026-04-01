using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class ServerLauncher : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = Settings.port;

    [Header("Dependencies")]
    public AUVAPIController auvController;

    private ServerRuntime _server;

    void Start()
    {
        // Отключаем ручное управление напарника
        AUVControllerManager manualManager = FindAnyObjectByType<AUVControllerManager>();
        if (manualManager != null) manualManager.enabled = false;

        _server = new ServerRuntime(port);

        // --- НИЗКОУРОВНЕВЫЕ КОМАНДЫ ---

        _server.AddRequest("set_motor_speed", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);
                int motorId = Convert.ToInt32(values["motor_id"]);
                float force = Convert.ToSingle(values["force"]);

                int status = auvController.SetAUVMotorSpeed(auvId, motorId, force);
                return Task.FromResult<(int, string, object)>((200, status == 0 ? "Success" : $"Error Code: {status}", new { status }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("get_auvs", (values) => {
            try
            {
                var ids = auvController.GetAUVs();
                return Task.FromResult<(int, string, object)>((200, "Success", new { auv_ids = ids }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((500, ex.Message, null));
            }
        });

        _server.AddRequest("get_motor_ids", (values) => {
            try
            {
                var ids = auvController.GetAUVMotorIds();
                return Task.FromResult<(int, string, object)>((200, "Success", new { motor_ids = ids }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((500, ex.Message, null));
            }
        });

        // --- КОМАНДЫ ПО ТЕХНИЧЕСКОМУ ЗАДАНИЮ ---

        _server.AddRequest("spawn_object", (values) => {
            try
            {
                string type = values["type"].ToString();
                Vector3 pos = ParseVector(values["position"]);

                Debug.Log($"[ТЗ РЕДАКТОР] Создание объекта: {type} в позиции {pos}");
                return Task.FromResult<(int, string, object)>((200, $"Object {type} spawned (simulated)", new { success = true }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("delete_object", (values) => {
            try
            {
                string objectId = values["id"].ToString();
                Debug.Log($"[ТЗ РЕДАКТОР] Удаление объекта с ID: {objectId}");
                return Task.FromResult<(int, string, object)>((200, "Object deleted (simulated)", new { success = true }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("get_telemetry", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);
                var telemetry = new
                {
                    depth = UnityEngine.Random.Range(10f, 100f),
                    pitch = UnityEngine.Random.Range(-5f, 5f),
                    roll = UnityEngine.Random.Range(-2f, 2f),
                    yaw = UnityEngine.Random.Range(0f, 360f)
                };
                Debug.Log($"[ТЗ ТЕЛЕМЕТРИЯ] Отправка данных для AUV {auvId}");
                return Task.FromResult<(int, string, object)>((200, "Success", telemetry));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.Start();
        Debug.Log($"[API Server] Сервер запущен на порту {port}. Логика Update отключена.");
    }

    private Vector3 ParseVector(object data)
    {
        var dict = data as Dictionary<string, object>;
        return new Vector3(Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]), Convert.ToSingle(dict["z"]));
    }

    void OnDestroy() => _server?.Stop();
    void OnApplicationQuit() => _server?.Stop();
}