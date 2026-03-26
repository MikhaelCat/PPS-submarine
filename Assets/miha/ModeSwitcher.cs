using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TabManager : MonoBehaviour
{
    [Header("Настройки цвета")]
    public Color activeColor = new Color(0.15f, 0.15f, 0.15f); // Темнее для активной
    public Color inactiveColor = new Color(0.25f, 0.25f, 0.25f); // Обычный цвет

    [System.Serializable]
    public struct TabItem
    {
        public Button button;      // Кнопка в хедере
        public GameObject panel;   // Объект с контентом (режим)
    }

    public List<TabItem> tabs;

    void Start()
    {
        // Инициализация: вешаем события на кнопки
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i; // Локальная переменная для замыкания
            tabs[i].button.onClick.AddListener(() => SelectTab(index));
        }

        // По умолчанию выбираем первую вкладку
        if (tabs.Count > 0) SelectTab(0);
    }

    public void SelectTab(int index)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool isActive = (i == index);

            // Включаем/выключаем панель режима
            if (tabs[i].panel != null)
                tabs[i].panel.SetActive(isActive);

            // Меняем цвет кнопки (обращаемся к Image кнопки)
            Image btnImg = tabs[i].button.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.color = isActive ? activeColor : inactiveColor;
            }
        }
    }
}