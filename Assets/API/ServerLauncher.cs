using UnityEngine;
using System.Collections.Generic;

public class ServerLauncher : MonoBehaviour
{
    [Header("Network Settings")]
    public int port = Settings.port;

    private ServerRuntime _server;

    void Start()
    {
        _server = new ServerRuntime(port);

        _server.AddRequest("set_player_speed", (values) =>
        {

            if (!values.ContainsKey("speed") || !values.ContainsKey("duration"))
            {
                return (400, "Missing required parameters: 'speed' or 'duration'", null);
            }

            double speed = System.Convert.ToDouble(values["speed"]);
            double duration = System.Convert.ToDouble(values["duration"]);

            var result = new Dictionary<string, object>
            {
                { "currentSpeed", speed },
                { "appliedDuration", duration }
            };

            return (200, "Success", result);
        });
        _server.AddRequest("force_error", (values) =>
        {
            throw new System.Exception("This is a simulated crash inside a bound function.");
        });

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