using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

// DTO для входящего запроса
public class ApiRequest
{
    public string request;
    public Dictionary<string, object> values;
}

// DTO для исходящего ответа
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

    // Реестр команд (маршрутизатор)
    private readonly Dictionary<string, Func<Dictionary<string, object>, (int code, string msg, object res)>> _commands
        = new Dictionary<string, Func<Dictionary<string, object>, (int code, string msg, object res)>>();

    public ServerRuntime(int port)
    {
        _port = port;
    }

    /// <summary>
    /// Биндинг (привязка) функции к текстовой команде.
    /// </summary>
    public void AddRequest(string name, Func<Dictionary<string, object>, (int code, string msg, object res)> logic)
    {
        _commands[name] = logic;
    }

    /// <summary>
    /// Запуск сервера в фоновом потоке.
    /// </summary>
    public void Start()
    {
        _udpClient = new UdpClient(_port);
        _cts = new CancellationTokenSource();

        // Запускаем бесконечный цикл приема данных автономно от Unity
        _ = ReceiveLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Остановка сервера и освобождение ресурсов.
    /// </summary>
    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Close();
    }

    // --- PIPELINE ОБРАБОТКИ ---

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                // 1. Прием пакета (асинхронно, не блокирует поток)
                UdpReceiveResult receiveResult = await _udpClient.ReceiveAsync();

                // Передаем обработку в ThreadPool, чтобы сразу быть готовым к новому пакету
                _ = Task.Run(() => ProcessPacketAsync(receiveResult), token);
            }
        }
        catch (ObjectDisposedException) { /* Нормальное поведение при остановке сервера */ }
        catch (Exception e) { UnityEngine.Debug.LogError($"UDP Server Loop Error: {e.Message}"); }
    }

    private async Task ProcessPacketAsync(UdpReceiveResult receiveResult)
    {
        string jsonString = Encoding.UTF8.GetString(receiveResult.Buffer);
        ApiResponse response = new ApiResponse();

        try
        {
            // 2. JsonDecode: Десериализация
            ApiRequest req = JsonConvert.DeserializeObject<ApiRequest>(jsonString);

            if (req == null || string.IsNullOrEmpty(req.request) || req.values == null)
            {
                // Ошибка 400: Некорректный JSON или нет обязательных полей
                response.status = 400;
                response.message = "Bad Request: Missing 'request' or 'values'";
            }
            else
            {
                // 3. Perform (Маршрутизация без if/else)
                if (_commands.TryGetValue(req.request, out var executeLogic))
                {
                    try
                    {
                        // 4. Execute: Выполнение привязанной функции
                        var (code, msg, res) = executeLogic.Invoke(req.values);

                        response.status = code;
                        response.message = msg;
                        response.result = res;
                    }
                    catch (Exception ex)
                    {
                        // Ошибка 500: Исключение внутри пользовательской функции
                        response.status = 500;
                        response.message = $"Internal Execution Error: {ex.Message}";
                    }
                }
                else
                {
                    // Ошибка 404: Команда не зарегистрирована в Dictionary
                    response.status = 404;
                    response.message = $"Not Found: Command '{req.request}' is not mapped.";
                }
            }
        }
        catch (JsonException ex)
        {
            // Ошибка 400: Мусор вместо JSON
            response.status = 400;
            response.message = $"JSON Parse Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            // Глобальный перехват на крайний случай
            response.status = 500;
            response.message = $"Critical Pipeline Error: {ex.Message}";
        }

        // 5. Answer (Ответ)
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