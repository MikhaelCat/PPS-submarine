using UnityEngine;

public class MainCartReader: MonoBehaviour
{
    [Header("Экраны")]
    public GameObject mainMenu;    // Объект Main
    public GameObject mapEditor;   // Объект Cart Reader

    // Метод для перехода в редактор карт
    public void OpenMapEditor()
    {
        mainMenu.SetActive(false);  // Выключаем главное меню
        mapEditor.SetActive(true);  // Включаем редактор
    }

    // Метод для возврата в главное меню
    public void OpenMainMenu()
    {
        mapEditor.SetActive(false);
        mainMenu.SetActive(true);
    }
}