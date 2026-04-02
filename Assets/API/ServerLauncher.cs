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
                // Явно приводим анонимный тип к object
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
                var ids = auvController.GetAUVs();
                return Task.FromResult<(int, string, object)>((200, "Success", (object)new { auv_ids = ids }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((500, ex.Message, null));
            }
        });

        _server.AddRequest("get_motor_ids", (values) => {
            try
            {
                // ИСПРАВЛЕНО: Добавлен auv_id, так как контроллер требует его для поиска моторов
                int auvId = Convert.ToInt32(values["auv_id"]);
                var ids = auvController.GetAUVMotorIds(auvId);
                return Task.FromResult<(int, string, object)>((200, "Success", (object)new { motor_ids = ids }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<(int, string, object)>((500, ex.Message, null));
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

                var telemetry = new
                {
                    x = targetAuv.transform.position.x,
                    y = targetAuv.transform.position.z,
                    depth = -targetAuv.transform.position.y,
                    pitch = targetAuv.transform.eulerAngles.x,
                    roll = targetAuv.transform.eulerAngles.z,
                    yaw = targetAuv.transform.eulerAngles.y,
                    // ИСПРАВЛЕНО: используем linearVelocity для новых версий Unity
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
                    // 1. Создаем текстуру и загружаем сырые пиксели
                    Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(rgba32);
                    tex.Apply();

                    // 2. Сжимаем в JPG с качеством 50%. 
                    // Это визуально почти не изменит картинку 256x256, но сожмет ее до 10-15 КБ.
                    byte[] jpgBytes = tex.EncodeToJPG(50);

                    // Обязательно выгружаем текстуру из памяти, иначе получим жесткую утечку (Memory Leak)
                    Destroy(tex);

                    // 3. Кодируем бинарные данные JPG в текстовый формат Base64 для передачи через JSON
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
                    // Для Side Scan Sonar нам важнее всего интенсивность (отражающая способность дна)
                    // Формируем два массива интенсивностей для левого и правого борта
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
                    // КРИТИЧЕСКИЙ МОМЕНТ: временно отключаем физику для телепортации
                    rb.isKinematic = true;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                // Сброс моторов
                var motorIds = auvController.GetAUVMotorIds(auvId);
                foreach (var mId in motorIds) auvController.SetAUVMotorSpeed(auvId, mId, 0f);

                // Перемещение
                targetAuv.transform.position = offsetPos;
                targetAuv.transform.rotation = Quaternion.Euler(baseRot);

                if (rb != null)
                {
                    // Возвращаем физику обратно
                    rb.isKinematic = false;
                }

                return Task.FromResult<(int, string, object)>((200, "Success", (object)new { reset_id = auvId }));
            }
            catch (Exception ex) { return Task.FromResult<(int, string, object)>((400, ex.Message, null)); }
        });

        _server.Start();
    }

    private Vector3 ParseVector(object data)
    {
        var dict = data as Dictionary<string, object>;
        return new Vector3(Convert.ToSingle(dict["x"]), Convert.ToSingle(dict["y"]), Convert.ToSingle(dict["z"]));
    }

    void OnDestroy() => _server?.Stop();
    void OnApplicationQuit() => _server?.Stop();
}