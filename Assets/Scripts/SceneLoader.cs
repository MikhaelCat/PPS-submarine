using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;

public class SceneLoader : MonoBehaviour
{
    public GameObject mapsEditPanel;
    public GameObject backgroundVisual;
    public GameObject startText;

    [Header("Настройки связи с Python")]
    public int port = 12345;
    private bool clientConnected = false;

    // ПЕРЕМЕННЫЕ ДЛЯ ЗАЩИТЫ ОТ БАГА
    private bool isLoading = false;

    public void LoadSimulationScene()
    {
        // Если сцена уже грузится или уже загружена — ничего не делаем
        if (isLoading || SceneManager.GetSceneByName("sim").isLoaded)
        {
            Debug.Log("Симуляция уже запущена или в процессе загрузки.");
            return;
        }

        isLoading = true; // Ставим замок

        if (mapsEditPanel != null) mapsEditPanel.SetActive(false);
        if (backgroundVisual != null) backgroundVisual.SetActive(false);
        if (startText != null) startText.SetActive(false);

        StartCoroutine(WaitClientAndLoadRoutine());
    }

    private IEnumerator WaitClientAndLoadRoutine()
    {
        Debug.Log("Ожидание клиента...");

        // Сбрасываем флаг подключения перед началом
        clientConnected = true;

        Thread waitThread = new Thread(WaitForClientConnection);
        waitThread.Start();

        // Ждем подключения Python
        while (!clientConnected) yield return null;

        // Загружаем сцену только один раз
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("sim", LoadSceneMode.Additive);
        while (!asyncLoad.isDone) yield return null;

        // Применяем масштаб
        GameObject simMap = GameObject.Find("SimMapContainer");
        if (simMap != null)
        {
            float s = MapScaleController.GlobalScaleFactor;
            simMap.transform.localScale = new Vector3(s, simMap.transform.localScale.y, s);
            Debug.Log("Масштаб применен успешно.");
        }

        isLoading = false; // Снимаем замок, загрузка окончена
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
        catch (System.Exception e)
        {
            Debug.LogError("Ошибка сети: " + e.Message);
            clientConnected = false;
            isLoading = false;
        }
    }

    public void UnloadSimulation()
    {
        isLoading = false; // Сбрасываем на всякий случай

        if (mapsEditPanel != null) mapsEditPanel.SetActive(true);
        if (backgroundVisual != null) backgroundVisual.SetActive(true);

        if (SceneManager.GetSceneByName("sim").isLoaded)
        {
            SceneManager.UnloadSceneAsync("sim");
        }
    }
}