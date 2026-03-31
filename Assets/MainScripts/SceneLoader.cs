using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    [Header("Объект редактора карт")]
    public GameObject mapsEditPanel; // Сюда перетащи MapsEdit из иерархии

    // Метод для кнопки "Симуляция"
    public void LoadSimulationScene()
    {
        // 1. Скрываем редактор (шапка останется, так как она не в этом объекте)
        if (mapsEditPanel != null)
            mapsEditPanel.SetActive(false);

        // 2. Загружаем сцену аддитивно, если она еще не загружена
        if (!SceneManager.GetSceneByName("sim").isLoaded)
        {
            StartCoroutine(LoadAndScaleRoutine());
        }
    }

    private IEnumerator LoadAndScaleRoutine()
    {
        // Загрузка сцены
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("sim", LoadSceneMode.Additive);

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 3. После загрузки ищем объект карты в новой сцене
        // ВАЖНО: В сцене 'sim' назови объект карты "SimMapContainer"
        GameObject simMap = GameObject.Find("SimMapContainer");

        if (simMap != null)
        {
            float s = MapScaleController.GlobalScaleFactor;
            simMap.transform.localScale = new Vector3(s, simMap.transform.localScale.y, s);
            Debug.Log("Масштаб симуляции применен: " + s);
        }
        else
        {
            Debug.LogWarning("Объект 'SimMapContainer' не найден в сцене sim!");
        }
    }

    // Метод для кнопки "Редактор карт" (чтобы вернуться)
    public void UnloadSimulation()
    {
        // Показываем редактор обратно
        if (mapsEditPanel != null)
            mapsEditPanel.SetActive(true);

        // Включаем обратно старую карту (если нужно обновить масштаб при возврате)
        // Выгружаем симуляцию
        if (SceneManager.GetSceneByName("sim").isLoaded)
        {
            SceneManager.UnloadSceneAsync("sim");
        }
    }
}