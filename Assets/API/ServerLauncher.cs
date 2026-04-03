using System;
using System.Reflection;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

public class ServerLauncher : MonoBehaviour
{
    [Header("Telemetry Stream")]
    public int clientTelemetryPort = 8081;
    private UdpClient _telemetryStreamer;

    [Header("Network Settings")]
    public int port = Settings.port;

    [Header("Dependencies")]
    public AUVAPIController auvController;

    [Header("Geographic Settings")]
    // Базовая позиция сцены в глобальных координатах (по умолчанию - Японское море,near Владивосток)
    [Tooltip("Широта сцены (градусы)")]
    public double sceneBaseLatitude = 43.115;
    [Tooltip("Долгота сцены (градусы)")]
    public double sceneBaseLongitude = 131.885;

    private ServerRuntime _server;

    void Start()
    {
        _server = new ServerRuntime(port);
        _telemetryStreamer = new UdpClient();
        InvokeRepeating(nameof(StreamAllSensorData), 1f, 0.4f);
        InvokeRepeating(nameof(BroadcastAuvList), 1f, 1.0f);

        // --- НИЗКОУРОВНЕВЫЕ КОМАНДЫ ---
        _server.AddRequest("set_motor_speed", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);
                int motorId = Convert.ToInt32(values["motor_id"]);
                float force = Convert.ToSingle(values["force"]);
                int status = auvController.SetAUVMotorSpeed(auvId, motorId, force);
                return Task.FromResult<(int, string, object)>((200, status == 0 ? "Success" : $"Error Code: {status}", (object)new { status }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("get_auvs", (values) => {
            try
            {
                var auvs = auvController.GetAUVs();
                return Task.FromResult<(int, string, object)>((200, "Success", auvs));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((500, ex.Message, null));
            }
        });

        _server.AddRequest("get_motor_ids", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);
                var ids = auvController.GetAUVMotorIds(auvId);
                return Task.FromResult<(int, string, object)>((200, "Success", (object)new { motor_ids = ids }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((500, ex.Message, null));
            }
        });

        // --- НОВАЯ КОМАНДА: Установка глобальной позиции сцены ---
        _server.AddRequest("set_geo_position", (values) => {
            try
            {
                double latitude = Convert.ToDouble(values["latitude"]);
                double longitude = Convert.ToDouble(values["longitude"]);

                sceneBaseLatitude = latitude;
                sceneBaseLongitude = longitude;

                Debug.Log($"[GeoPosition] Scene base position set to: {latitude}, {longitude}");

                return Task.FromResult<(int, string, object)>((200, "Success", (object)new
                {
                    latitude = sceneBaseLatitude,
                    longitude = sceneBaseLongitude
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        // --- КОМАНДЫ ПО ТЕХНИЧЕСКОМУ ЗАДАНИЮ ---
        _server.AddRequest("get_telemetry", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);
                AUV targetAuv = null;

                AUV[] allAuvs = FindObjectsByType<AUV>(FindObjectsInactive.Exclude);
                for (int i = 0; i < allAuvs.Length; i++)
                {
                    if (allAuvs[i].id == auvId)
                    {
                        targetAuv = allAuvs[i];
                        break;
                    }
                }

                if (targetAuv == null) return Task.FromResult<(int, string, object)>((404, $"AUV {auvId} not found", null));

                Rigidbody rb = targetAuv.GetComponent<Rigidbody>();

                // Конвертируем локальные координаты в глобальные
                var globalCoords = LocalToGeo(targetAuv.transform.position);

                var telemetry = new
                {
                    latitude = globalCoords.latitude,
                    longitude = globalCoords.longitude,
                    x = targetAuv.transform.position.x,
                    y = targetAuv.transform.position.z,
                    depth = -targetAuv.transform.position.y,
                    pitch = targetAuv.transform.eulerAngles.x,
                    roll = targetAuv.transform.eulerAngles.z,
                    yaw = targetAuv.transform.eulerAngles.y,
                    speed = rb != null ? rb.linearVelocity.magnitude : 0f
                };

                return Task.FromResult<(int, string, object)>((200, "Success", (object)telemetry));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("get_mbes", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);

                if (auvController.TryGetAUVMBESData(auvId, out var mbesData))
                {
                    List<float> listX = new List<float>();
                    List<float> listY = new List<float>();

                    int step = 2;
                    for (int i = 0; i < mbesData.points.Length; i += step)
                    {
                        var pt = mbesData.points[i];
                        if (pt.hasHit)
                        {
                            listX.Add((float)Math.Round(pt.pointLocal.x, 1));
                            listY.Add((float)Math.Round(pt.pointLocal.y, 1));
                        }
                    }

                    return Task.FromResult<(int, string, object)>((200, "Success", (object)new { points_x = listX, points_y = listY }));
                }
                else
                {
                    return Task.FromResult<(int, string, object)>((404, $"No MBES data for AUV {auvId}", null));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("get_camera", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);

                if (auvController.TryGetAUVCameraImage(auvId, out byte[] rgba32, out int width, out int height))
                {
                    Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(rgba32);
                    tex.Apply();

                    byte[] jpgBytes = tex.EncodeToJPG(50);
                    Destroy(tex);

                    string base64Image = Convert.ToBase64String(jpgBytes);

                    return Task.FromResult<(int, string, object)>((200, "Success", (object)new { camera_image = base64Image }));
                }
                else
                {
                    return Task.FromResult<(int, string, object)>((404, $"No camera data for AUV {auvId}", null));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("get_side_sonar", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);

                if (auvController.TryGetAUVSideSonarData(auvId, out var sonarData))
                {
                    float[] leftIntensities = new float[sonarData.pointsPerSide];
                    float[] rightIntensities = new float[sonarData.pointsPerSide];

                    for (int i = 0; i < sonarData.pointsPerSide; i++)
                    {
                        leftIntensities[i] = sonarData.leftLine[i].intensity;
                        rightIntensities[i] = sonarData.rightLine[i].intensity;
                    }

                    return Task.FromResult<(int, string, object)>((200, "Success", (object)new
                    {
                        left = leftIntensities,
                        right = rightIntensities,
                        max_range = sonarData.maxRange,
                        count = sonarData.pointsPerSide
                    }));
                }
                else
                {
                    return Task.FromResult<(int, string, object)>((404, $"No Sonar data for AUV {auvId}", null));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("spawn_auv", (values) => {
            try
            {
                auvController.GetAPISpawnTransform(out Vector3 basePos, out Vector3 baseRot);
                if (auvController.TrySpawnAUV(out int newAuvId))
                {
                    Vector3 offsetPos = basePos + new Vector3(newAuvId * 5.0f, 0, 0);
                    AUV[] allAuvs = FindObjectsByType<AUV>(FindObjectsInactive.Exclude);
                    foreach (var auv in allAuvs)
                    {
                        if (auv.id == newAuvId)
                        {
                            auv.transform.position = offsetPos;
                            auv.transform.eulerAngles = baseRot;
                            break;
                        }
                    }

                    return Task.FromResult<(int, string, object)>((200, "Success", (object)new { spawned_id = newAuvId }));
                }
                else
                {
                    return Task.FromResult<(int, string, object)>((500, "Failed to spawn AUV. Check AUVControllerManager.", null));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.AddRequest("reset_auv", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);
                AUV targetAuv = null;

                AUV[] allAuvs = FindObjectsByType<AUV>(FindObjectsInactive.Exclude);
                foreach (var a in allAuvs) { if (a.id == auvId) { targetAuv = a; break; } }

                if (targetAuv == null) return Task.FromResult<(int, string, object)>((404, "AUV not found", null));

                auvController.GetAPISpawnTransform(out Vector3 basePos, out Vector3 baseRot);
                Vector3 offsetPos = basePos + new Vector3(auvId * 5.0f, 0, 0);

                Rigidbody rb = targetAuv.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                var motorIds = auvController.GetAUVMotorIds(auvId);
                foreach (var mId in motorIds) auvController.SetAUVMotorSpeed(auvId, mId, 0f);

                targetAuv.transform.position = offsetPos;
                targetAuv.transform.rotation = Quaternion.Euler(baseRot);

                if (rb != null)
                {
                    rb.isKinematic = false;
                }

                return Task.FromResult<(int, string, object)>((200, "Success", (object)new { reset_id = auvId }));
            }
            catch (Exception ex) { return Task.FromResult<(int, string, object)>((400, ex.Message, null)); }
        });

        _server.AddRequest("remove_auv", (values) => {
            try
            {
                int auvId = Convert.ToInt32(values["auv_id"]);
                bool removed = auvController.TryRemoveAUV(auvId);

                if (removed)
                {
                    return Task.FromResult<(int, string, object)>((200, "AUV Removed", new { auv_id = auvId }));
                }
                else
                {
                    return Task.FromResult<(int, string, object)>((404, "AUV not found", null));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((400, ex.Message, null));
            }
        });

        _server.Start();
    }

    // ============================================
    // ГЕОГРАФИЧЕСКИЕ КОНВЕРТАЦИИ
    // ============================================

    /// <summary>
    /// Конвертирует локальные Unity координаты в глобальные (широта/долгота)
    /// Использует упрощенную эвклидову аппроксимацию
    /// </summary>
    private (double latitude, double longitude) LocalToGeo(Vector3 localPos)
    {
        // Приблизительные константы (метров в градусе)
        const double metersPerDegreeLat = 111111.0; // 1 градус широты ≈ 111.111 км
        double metersPerDegreeLon = 111111.0 * Math.Cos(DegToRad(sceneBaseLatitude)); // Зависит от широты

        // Unity: X = East, Z = North, Y = Up(Down для глубины)
        double deltaLat = localPos.z / metersPerDegreeLat;
        double deltaLon = localPos.x / metersPerDegreeLon;

        double latitude = sceneBaseLatitude + deltaLat;
        double longitude = sceneBaseLongitude + deltaLon;

        return (latitude, longitude);
    }

    /// <summary>
    /// Конвертирует градусы в радианы
    /// </summary>
    private double DegToRad(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    /// <summary>
    /// Конвертирует радианы в градусы
    /// </summary>
    private double RadToDeg(double radians)
    {
        return radians * 180.0 / Math.PI;
    }

    private Vector3 ParseVector(object data)
    {
        var dict = data as Dictionary<string, object>;
        return new Vector3(Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]), Convert.ToSingle(dict["z"]));
    }

    private void BroadcastAuvList()
    {
        if (auvController == null) return;

        try
        {
            FieldInfo field = typeof(AUVAPIController).GetField("auvByIdSnapshot",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                var dict = field.GetValue(auvController) as Dictionary<int, AUV>;

                if (dict != null)
                {
                    List<int> ids;
                    lock (dict)
                    {
                        ids = new List<int>(dict.Keys);
                    }

                    SendStreamData(new
                    {
                        type = "auv_list",
                        ids = ids
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BroadcastAuvList] Reflection Error: {e.Message}");
        }
    }

    private void StreamAllSensorData()
    {
        AUV[] allAuvs = FindObjectsByType<AUV>(FindObjectsInactive.Exclude);
        foreach (var auv in allAuvs)
        {
            int auvId = auv.id;

            // Конвертируем локальные координаты в глобальные
            var globalCoords = LocalToGeo(auv.transform.position);

            // 1. Стрим Телеметрии (С ГЛОБАЛЬНЫМИ КООРДИНАТАМИ!)
            Rigidbody rb = auv.GetComponent<Rigidbody>();
            SendStreamData(new
            {
                type = "telemetry",
                auv_id = auvId,
                latitude = globalCoords.latitude,      // НОВОЕ ПО ТЗ 4.1.a
                longitude = globalCoords.longitude,     // НОВОЕ ПО ТЗ 4.1.a
                x = auv.transform.position.x,
                y = auv.transform.position.z,
                depth = -auv.transform.position.y,
                pitch = auv.transform.eulerAngles.x,
                roll = auv.transform.eulerAngles.z,
                yaw = auv.transform.eulerAngles.y,
                speed = rb != null ? rb.linearVelocity.magnitude : 0f
            });

            // 2. Стрим Эхолота (MBES)
            if (auvController.TryGetAUVMBESData(auvId, out var mbesData))
            {
                List<float> listX = new List<float>();
                List<float> listY = new List<float>();
                for (int i = 0; i < mbesData.points.Length; i += 2)
                {
                    if (mbesData.points[i].hasHit)
                    {
                        listX.Add((float)Math.Round(mbesData.points[i].pointLocal.x, 1));
                        listY.Add((float)Math.Round(mbesData.points[i].pointLocal.y, 1));
                    }
                }
                SendStreamData(new { type = "mbes", auv_id = auvId, points_x = listX, points_y = listY });
            }

            // 3. Стрим Сонара
            if (auvController.TryGetAUVSideSonarData(auvId, out var sonarData))
            {
                float[] leftIntensities = new float[sonarData.pointsPerSide];
                float[] rightIntensities = new float[sonarData.pointsPerSide];
                for (int i = 0; i < sonarData.pointsPerSide; i++)
                {
                    leftIntensities[i] = sonarData.leftLine[i].intensity;
                    rightIntensities[i] = sonarData.rightLine[i].intensity;
                }
                SendStreamData(new { type = "sonar", auv_id = auvId, left = leftIntensities, right = rightIntensities, max_range = sonarData.maxRange });
            }

            // 4. Стрим Камеры
            if (auvController.TryGetAUVCameraImage(auvId, out byte[] rgba32, out int width, out int height))
            {
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(rgba32);
                tex.Apply();
                byte[] jpgBytes = tex.EncodeToJPG(50);
                Destroy(tex);
                SendStreamData(new { type = "camera", auv_id = auvId, camera_image = Convert.ToBase64String(jpgBytes) });
            }
        }
    }

    private void SendStreamData(object data)
    {
        try
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            _telemetryStreamer.Send(bytes, bytes.Length, "127.0.0.1", clientTelemetryPort);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Stream send error: {e.Message}");
        }
    }

    void OnDestroy()
    {
        _server?.Stop();
        _telemetryStreamer?.Close();
    }

    void OnApplicationQuit() => _server?.Stop();
}