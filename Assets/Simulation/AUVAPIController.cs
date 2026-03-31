using UnityEngine;
using System.Collections.Generic;
using System.Linq;


// ДОЛГАЯ ПОДДЕРЖКА ФУНКЦИЙ ячозвфршыв-ршфрш-ыв-=гфо=вы
public class AUVAPIController : MonoBehaviour
{
    // === Переменные класса ===
    private readonly Dictionary<int, AUV> auvById = new Dictionary<int, AUV>();

    // === Unity жизненный цикл ===

    // Собирает кэш всех AUV при старте
    private void Awake()
    {
        RefreshAUVCache();
    }

    // === Публичный API ===

    // Устанавливает силу конкретному мотору конкретного AUV
    // Возврат: -1 если AUV не найден; иначе коды из AUV.SetMotorForce (0/1/2)
    public int SetAUVMotorSpeed(int auv_id, int motorid, float force)
    {
        if (!TryGetAUV(auv_id, out AUV auv))
        {
            return -1;
        }

        return auv.SetMotorForce(motorid, force);
    }

    // Возвращает список id всех AUV в сцене
    public List<int> GetAUVs()
    {
        RefreshAUVCache();
        return auvById.Keys.OrderBy(id => id).ToList();
    }

    // Возвращает список id моторов (общий для всех AUV из AUVSettings)
    public List<int> GetAUVMotorIds()
    {
        return GetMotorIdsFromSettings();
    }

    // Возвращает список id моторов конкретного AUV
    // Если AUV с таким id не найден, возвращает пустой список
    public List<int> GetAUVMotorIds(int auv_id)
    {
        if (!TryGetAUV(auv_id, out _))
        {
            return new List<int>();
        }

        return GetMotorIdsFromSettings();
    }

    // === Вспомогательные функции ===

    // Забирает id моторов из общих настроек AUV
    private static List<int> GetMotorIdsFromSettings()
    {
        AUVSettings settings = AUVSettings.GetOrFind();
        if (settings == null)
        {
            return new List<int>();
        }

        return settings.ForcePoints
            .Select(point => point.id)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    // Пересобирает словарь id->AUV
    private void RefreshAUVCache()
    {
        auvById.Clear();

        AUV[] auvs = UnityEngine.Object.FindObjectsByType<AUV>(FindObjectsInactive.Exclude);
        for (int i = 0; i < auvs.Length; i++)
        {
            AUV auv = auvs[i];
            if (auv == null)
            {
                continue;
            }

            if (!auvById.ContainsKey(auv.id))
            {
                auvById.Add(auv.id, auv);
            }
            else
            {
                Debug.LogWarning($"Duplicate AUV id detected: {auv.id}. Only the first instance will be used by AUVAPIController.");
            }
        }
    }

    // Находит AUV по id, при необходимости обновляет кэш
    private bool TryGetAUV(int auvId, out AUV auv)
    {
        if (auvById.TryGetValue(auvId, out auv) && auv != null)
        {
            return true;
        }

        RefreshAUVCache();
        return auvById.TryGetValue(auvId, out auv) && auv != null;
    }
}
