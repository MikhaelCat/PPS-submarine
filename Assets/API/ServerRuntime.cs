using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

// DTO for input request
public class ApiRequest
{
    public string request;
    public Dictionary<string, object> values;
}

// DTO for output request
public class ApiResponse
{
    public int status;
    public string message;
    public object result;
}

public class ServerRuntime
{
    private readonly int _port;
    private UdpClient _udpClient;
    private CancellationTokenSource _cts;

    // Command registry (router)
    private readonly Dictionary<string, Func<Dictionary<string, object>, (int code, string msg, object res)>> _commands
        = new Dictionary<string, Func<Dictionary<string, object>, (int code, string msg, object res)>>();

    public ServerRuntime(int port)
    {
        _port = port;
    }

    public void AddRequest(string name, Func<Dictionary<string, object>, (int code, string msg, object res)> logic)
    {
        _commands[name] = logic;
    }

    public void Start()
    {
        _udpClient = new UdpClient(_port);
        _cts = new CancellationTokenSource();

        _ = ReceiveLoopAsync(_cts.Token);
    }
    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Close();
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult receiveResult = await _udpClient.ReceiveAsync();

                _ = Task.Run(() => ProcessPacketAsync(receiveResult), token);
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception e) { UnityEngine.Debug.LogError($"UDP Server Loop Error: {e.Message}"); }
    }

    private async Task ProcessPacketAsync(UdpReceiveResult receiveResult)
    {
        string jsonString = Encoding.UTF8.GetString(receiveResult.Buffer);
        ApiResponse response = new ApiResponse();

        try
        {
            ApiRequest req = JsonConvert.DeserializeObject<ApiRequest>(jsonString);

            if (req == null || string.IsNullOrEmpty(req.request) || req.values == null)
            {
                response.status = 400;
                response.message = "Bad Request: Missing 'request' or 'values'";
            }
            else
            {
                if (_commands.TryGetValue(req.request, out var executeLogic))
                {
                    try
                    {
                        var (code, msg, res) = executeLogic.Invoke(req.values);

                        response.status = code;
                        response.message = msg;
                        response.result = res;
                    }
                    catch (Exception ex)
                    {
                        response.status = 500;
                        response.message = $"Internal Execution Error: {ex.Message}";
                    }
                }
                else
                {
                    response.status = 404;
                    response.message = $"Not Found: Command '{req.request}' is not mapped.";
                }
            }
        }
        catch (JsonException ex)
        {
            response.status = 400;
            response.message = $"JSON Parse Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            response.status = 500;
            response.message = $"Critical Pipeline Error: {ex.Message}";
        }

        await SendResponseAsync(response, receiveResult.RemoteEndPoint);
    }

    private async Task SendResponseAsync(ApiResponse response, System.Net.IPEndPoint endpoint)
    {
        try
        {
            string jsonResponse = JsonConvert.SerializeObject(response);
            byte[] bytes = Encoding.UTF8.GetBytes(jsonResponse);
            await _udpClient.SendAsync(bytes, bytes.Length, endpoint);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to send response: {e.Message}");
        }
    }
}