using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapScaleController : MonoBehaviour
{
    public Transform mapContainer;

    // СТАТИЧЕСКАЯ ПЕРЕМЕННАЯ: сохраняет значение между сценами
    // 1.0f соответствует масштабу 1000x1000
    public static float GlobalScaleFactor = 1.0f;

    [Header("Настройки цветов")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    public List<Button> scaleButtons;

    void Start()
    {
        // При старте подсвечиваем кнопку, которая соответствует текущему GlobalScaleFactor
        float currentScaleValue = GlobalScaleFactor * 1000f;
        foreach (Button btn in scaleButtons)
        {
            if (btn != null && btn.gameObject.name == currentScaleValue.ToString())
            {
                SetMapScale(btn);
                break;
            }
        }
    }

    public void SetMapScale(Button clickedButton)
    {
        if (clickedButton == null) return;

        foreach (Button btn in scaleButtons)
        {
            if (btn != null)
            {
                btn.image.color = normalColor;
                var cb = btn.colors;
                cb.normalColor = normalColor;
                btn.colors = cb;
            }
        }

        clickedButton.image.color = selectedColor;
        var clickedCb = clickedButton.colors;
        clickedCb.normalColor = selectedColor;
        clickedButton.colors = clickedCb;

        // Удаление старых точек
        GameObject[] oldMarkers = GameObject.FindGameObjectsWithTag("Marker");
        foreach (var m in oldMarkers) Destroy(m);

        // МАСШТАБИРОВАНИЕ
        if (float.TryParse(clickedButton.gameObject.name, out float value))
        {
            GlobalScaleFactor = value / 1000f; // Сохраняем масштаб в "память"
            if (mapContainer != null)
            {
                mapContainer.localScale = new Vector3(GlobalScaleFactor, mapContainer.localScale.y, GlobalScaleFactor);
            }
        }
    }
}