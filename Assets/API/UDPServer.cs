using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Text;

public class UDPServer : MonoBehaviour
{
    public int port = 8080;
    public AUVController[] auvUnits;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = false;
    private IPEndPoint lastClientEndPoint;

    private ConcurrentQueue<string> commandQueue = new ConcurrentQueue<string>();

    void Start()
    {
        isRunning = true;
        receiveThread = new Thread(ReceiveData);
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveData()
    {
        udpClient = new UdpClient(port);
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                lastClientEndPoint = anyIP;
                string text = Encoding.UTF8.GetString(data);
                commandQueue.Enqueue(text);
            }
            catch { }
        }
    }

    void Update()
    {
        while (commandQueue.TryDequeue(out string json))
        {
            ProcessCommand(json);
        }
    }

    private void ProcessCommand(string json)
    {
        try
        {
            AUVCommand cmd = JsonUtility.FromJson<AUVCommand>(json);
            foreach (var auv in auvUnits)
            {
                if (auv != null && auv.auvId == cmd.auv_id)
                {
                    auv.ApplyCommand(cmd.command, cmd.value);
                    SendResponse(auv.GetCurrentTelemetry());
                }
            }
        }
        catch { }
    }

    private void SendResponse(AUVTelemetry tele)
    {
        if (lastClientEndPoint == null) return;
        string json = JsonUtility.ToJson(tele);
        byte[] data = Encoding.UTF8.GetBytes(json);
        udpClient.Send(data, data.Length, lastClientEndPoint);
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
    }
}