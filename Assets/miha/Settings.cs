using UnityEngine;

public class SettingsController : MonoBehaviour
{
    [Header("Объект панели настроек")]
    public GameObject settingsPanel;

    // Метод для кнопки-шестеренки
    public void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            // Инвертируем текущее состояние (включено -> выключено и наоборот)
            bool isActive = settingsPanel.activeSelf;
            settingsPanel.SetActive(!isActive);
        }
        else
        {
            Debug.LogError("Перетащи панель настроек в поле Settings Panel в инспекторе!");
        }
    }

    // Метод для отдельной кнопки "Закрыть" (если она будет внутри окна)
    public void CloseSettings()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
    }
}