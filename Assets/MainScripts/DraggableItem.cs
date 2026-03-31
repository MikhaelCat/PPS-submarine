using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private GameObject ghostIcon;
    public GameObject markerPrefab;
    private Canvas canvas;

    void Start() => canvas = GetComponentInParent<Canvas>();

    public void OnBeginDrag(PointerEventData eventData)
    {
        ghostIcon = new GameObject("IconGhost");
        ghostIcon.transform.SetParent(canvas.transform, false);
        var img = ghostIcon.AddComponent<Image>();
        img.sprite = GetComponent<Image>().sprite;
        img.raycastTarget = false;
        img.color = new Color(1, 1, 1, 0.6f);
        // Делаем размер призрака таким же, как у оригинала
        ghostIcon.GetComponent<RectTransform>().sizeDelta = GetComponent<RectTransform>().sizeDelta;
    }

    public void OnDrag(PointerEventData eventData)
    {
        ghostIcon.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Destroy(ghostIcon);

        // Проверяем, попали ли мы на карту
        if (eventData.pointerCurrentRaycast.gameObject != null &&
            eventData.pointerCurrentRaycast.gameObject.name == "Image")
        {
            // ВАЖНО: Берем worldPosition (точку в 3D мире), а не координаты экрана
            Vector3 hitPoint = eventData.pointerCurrentRaycast.worldPosition;
            CreateMarker(eventData.pointerCurrentRaycast.gameObject.transform, hitPoint);
        }
    }

    void CreateMarker(Transform parent, Vector3 worldPos)
    {
        GameObject marker = Instantiate(markerPrefab, parent);

        // Устанавливаем позицию в мире. 
        // worldPos.y + 0.1f — чтобы точка не «тонула» в текстуре карты (Z-fighting)
        marker.transform.position = new Vector3(worldPos.x, worldPos.y + 0.1f, worldPos.z);

        // Если маркер — это 2D спрайт в 3D мире, разверните его к камере или положите плашмя
        marker.transform.rotation = Quaternion.Euler(90, 0, 0);

        Debug.Log($"Точка создана в координатах: {marker.transform.position}");
    }
}