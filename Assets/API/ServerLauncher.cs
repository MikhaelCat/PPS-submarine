using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;

public class ServerLauncher : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = Settings.port;

    [Header("Dependencies")]
    public AUVAPIController auvController;

    private ServerRuntime _server;
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    void Start()
    {
        AUVControllerManager manualManager = FindAnyObjectByType<AUVControllerManager>();
        if (manualManager != null) manualManager.enabled = false;

        _server = new ServerRuntime(port);
        _server.AddRequest("set_motor_speed", async (values) => {
            return await ExecuteOnMainThread(() => {
                int auvId = Convert.ToInt32(values["auv_id"]);
                int motorId = Convert.ToInt32(values["motor_id"]);
                float force = Convert.ToSingle(values["force"]);
                int status = auvController.SetAUVMotorSpeed(auvId, motorId, force);
                return (200, status == 0 ? "Success" : $"Error Code: {status}", new { status });
            });
        });


        _server.AddRequest("get_auvs", async (values) => {
            return await ExecuteOnMainThread(() => {
                var ids = auvController.GetAUVs();
                return (200, "Success", new { auv_ids = ids });
            });
        });

        _server.AddRequest("get_motor_ids", async (values) => {
            return await ExecuteOnMainThread(() => {
                var ids = auvController.GetAUVMotorIds();
                return (200, "Success", new { motor_ids = ids });
            });
        });

        _server.AddRequest("spawn_object", async (values) => {
            return await ExecuteOnMainThread(() => {
                string type = values["type"].ToString();
                Vector3 pos = ParseVector(values["position"]);
                

                Debug.Log($"[ТЗ РЕДАКТОР] Создание объекта: {type} в позиции {pos}");
                
                return (200, $"Object {type} spawned (simulated)", new { success = true });
            });
        });

        _server.AddRequest("delete_object", async (values) => {
            return await ExecuteOnMainThread(() => {
                string objectId = values["id"].ToString();
                Debug.Log($"[ТЗ РЕДАКТОР] Удаление объекта с ID: {objectId}");
                return (200, "Object deleted (simulated)", new { success = true });
            });
        });

        _server.AddRequest("get_telemetry", async (values) => {
            return await ExecuteOnMainThread(() => {
                int auvId = Convert.ToInt32(values["auv_id"]);
                var telemetry = new {
                    depth = UnityEngine.Random.Range(10f, 100f),
                    pitch = UnityEngine.Random.Range(-5f, 5f),
                    roll = UnityEngine.Random.Range(-2f, 2f),
                    yaw = UnityEngine.Random.Range(0f, 360f)
                };
                Debug.Log($"[ТЗ ТЕЛЕМЕТРИЯ] Отправка данных для AUV {auvId}");
                return (200, "Success", telemetry);
            });
        });

        _server.Start();
        Debug.Log($"[API Server] Сервер запущен на порту {port}. Команды ТЗ активны.");
    }

    private Task<(int, string, object)> ExecuteOnMainThread(Func<(int, string, object)> logic)
    {
        var tcs = new TaskCompletionSource<(int, string, object)>();
        _mainThreadQueue.Enqueue(() => {
            try { tcs.SetResult(logic()); }
            catch (Exception ex) { tcs.SetResult((400, ex.Message, null)); }
        });
        return tcs.Task;
    }

    private Vector3 ParseVector(object data) {
        var dict = data as Dictionary<string, object>;
        return new Vector3(Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]), Convert.ToSingle(dict["z"]));
    }

    void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var action)) action.Invoke();
    }

    void OnDestroy() => _server?.Stop();
    void OnApplicationQuit() => _server?.Stop();
}