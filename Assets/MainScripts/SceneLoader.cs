using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class SceneLoader : MonoBehaviour
{
    public GameObject mapsEditPanel;
    public GameObject backgroundVisual;   // Сюда перетащи объект 'Phon' или панель фона
    public GameObject startText;     

    [Header("Настройки связи с Python")]
    public int port = 12345;
    private bool clientConnected = false;

    public void LoadSimulationScene()
    {
        if (mapsEditPanel != null)
            mapsEditPanel.SetActive(false);

        // ВЫКЛЮЧАЕМ ФОН, чтобы видеть 3D мир
        if (backgroundVisual != null)
            backgroundVisual.SetActive(false);
        // текс вырубает
        if (startText != null)
            startText.SetActive(false);

        StartCoroutine(WaitClientAndLoadRoutine());
    }

    private IEnumerator WaitClientAndLoadRoutine()
    {
        Debug.Log("Ожидание клиента...");
        clientConnected = true;
        Thread waitThread = new Thread(WaitForClientConnection);
        waitThread.Start();

        while (!clientConnected) yield return null;

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("sim", LoadSceneMode.Additive);
        while (!asyncLoad.isDone) yield return null;

        GameObject simMap = GameObject.Find("SimMapContainer");
        if (simMap != null)
        {
            float s = MapScaleController.GlobalScaleFactor;
            simMap.transform.localScale = new Vector3(s, simMap.transform.localScale.y, s);
        }
    }

    private void WaitForClientConnection()
    {
        try
        {
            TcpListener server = new TcpListener(IPAddress.Any, port);
            server.Start();
            TcpClient client = server.AcceptTcpClient();
            clientConnected = true;
            client.Close();
            server.Stop();
        }
        catch { /* Обработка ошибок */ }
    }

    public void UnloadSimulation()
    {
        if (mapsEditPanel != null) 
            mapsEditPanel.SetActive(true);

        // ВОЗВРАЩАЕМ ФОН
        if (backgroundVisual != null)
            backgroundVisual.SetActive(true);

        if (SceneManager.GetSceneByName("sim").isLoaded)
            SceneManager.UnloadSceneAsync("sim");
    }
}