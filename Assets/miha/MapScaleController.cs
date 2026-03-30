using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapScaleController : MonoBehaviour
{
    public Transform mapContainer;

    [Header("Настройки цветов")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.green;

    public List<Button> scaleButtons;

    void Start()
    {
        // По умолчанию 1000
        foreach (Button btn in scaleButtons)
        {
            if (btn != null && btn.gameObject.name == "1000")
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
                // Сбрасываем и цвет картинки, и настройки состояний кнопки
                btn.image.color = normalColor;
                var cb = btn.colors;
                cb.normalColor = normalColor;
                cb.selectedColor = normalColor; // Чтобы при потере фокуса не горела
                btn.colors = cb;
            }
        }

        // Фиксируем выделение на нажатой кнопке
        clickedButton.image.color = selectedColor;
        var clickedCb = clickedButton.colors;
        clickedCb.normalColor = selectedColor;
        clickedCb.selectedColor = selectedColor;
        clickedButton.colors = clickedCb;

        // 1. УДАЛЕНИЕ СТАРЫХ ТОЧЕК
        // Убедитесь, что у вашего префаба RedDot стоит тег "Marker"
        GameObject[] oldMarkers = GameObject.FindGameObjectsWithTag("Marker");
        foreach (var m in oldMarkers) Destroy(m);

        // 2. МАСШТАБИРОВАНИЕ 3D (X и Z)
        if (float.TryParse(clickedButton.gameObject.name, out float value))
        {
            float scaleFactor = value / 1000f;
            if (mapContainer != null)
            {
                // Y оставляем без изменений (например, 1.0f)
                mapContainer.localScale = new Vector3(scaleFactor, mapContainer.localScale.y, scaleFactor);
            }
        }
    }
}