using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class ApiRequest
{
    public string request;
    public Dictionary<string, object> values;
}

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

    private readonly Dictionary<string, Func<Dictionary<string, object>, Task<(int code, string msg, object res)>>> _commands
        = new Dictionary<string, Func<Dictionary<string, object>, Task<(int code, string msg, object res)>>>();

    public ServerRuntime(int port)
    {
        _port = port;
    }

    public void AddRequest(string name, Func<Dictionary<string, object>, Task<(int code, string msg, object res)>> logic)
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
        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                _ = ProcessPacketAsync(result);
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException ex)
            {
                // Игнорируем ошибку 10054 (клиент закрыл соединение)
                if (ex.NativeErrorCode != 10054)
                    UnityEngine.Debug.LogError($"UDP Receive Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"UDP Loop Error: {ex.Message}");
            }
        }
    }

    private async Task ProcessPacketAsync(UdpReceiveResult receiveResult)
    {
        var response = new ApiResponse();
        try
        {
            string json = Encoding.UTF8.GetString(receiveResult.Buffer);
            var req = JsonConvert.DeserializeObject<ApiRequest>(json);

            if (req == null || string.IsNullOrEmpty(req.request))
            {
                response.status = 400;
                response.message = "Invalid Request Format";
            }
            else
            {
                if (_commands.TryGetValue(req.request, out var executeLogic))
                {
                    try
                    {
                        // ОБНОВЛЕНО: Теперь мы ждем (await) выполнения команды
                        var (code, msg, res) = await executeLogic.Invoke(req.values);
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