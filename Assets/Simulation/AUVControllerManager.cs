using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

// Управляет ручным вводом для AUV и переключением цели камеры
public class AUVControllerManager : MonoBehaviour
{
    // === Unity параметры ===
    [Header("AUV Manger")]
    [SerializeField] bool DefaultIncluded = false;
    [SerializeField] InputActionAsset InputActions;
    [SerializeField] string InputActionsName = "AUVController";
    [SerializeField] string ActionMapName = "AUV";
    [SerializeField] OrbitCamera OrbitCamera;
    
    [System.Serializable]
    public struct IdAndForce
    {
        [SerializeField] int MotorId;
        [SerializeField] float Force; // от 100 до -100

        public int Id => MotorId;
        public float Value => Force;
    }
    
    [Header("Manual Control")]
    [SerializeField] List<IdAndForce> ForwardMove;
    [SerializeField] List<IdAndForce> BackMove;
    [SerializeField] List<IdAndForce> LeftMove;
    [SerializeField] List<IdAndForce> RightMove;
    [SerializeField] List<IdAndForce> UpMove;
    [SerializeField] List<IdAndForce> DownMove;
    [SerializeField] List<IdAndForce> YawMove;
    [SerializeField] List<IdAndForce> RollMove;
    [SerializeField] List<IdAndForce> PitchMove;
    
    // Флаг включения ручного управления
    public bool Included;

    // === Переменные класса ===
    private InputActionMap auvMap;
    private InputAction forwardAction;
    private InputAction backAction;
    private InputAction leftAction;
    private InputAction rightAction;
    private InputAction upAction;
    private InputAction downAction;
    private InputAction yawAction;
    private InputAction rollAction;
    private InputAction pitchAction;
    private InputAction switchAction;

    private AUV[] controlledAUVs = System.Array.Empty<AUV>();
    private int currentAuvIndex = -1;

    // === Unity жизненный цикл ===

    // Инициализация ссылок и ввода
    void Awake()
    {
        Included = DefaultIncluded;

        InputActions = ResolveInputActionsAsset(InputActions);

        if (OrbitCamera == null)
        {
            OrbitCamera = UnityEngine.Object.FindAnyObjectByType<OrbitCamera>();
        }

        InitInput();
    }

    // Стартовая привязка камеры к текущему AUV
    void Start()
    {
        RefreshAUVs();
        SetOrbitTargetToCurrentAUV();
    }

    // Включает карту действий
    void OnEnable()
    {
        auvMap?.Enable();
    }

    // Отключает карту действий
    void OnDisable()
    {
        auvMap?.Disable();
    }

    // Отписывается от событий ввода
    void OnDestroy()
    {
        if (switchAction != null)
        {
            switchAction.performed -= OnSwitchPerformed;
        }
    }

    // === Публичный API ===

    // Возвращает список AUV, отсортированный по id
    public AUV[] GetAUVs()
    {
        AUV[] objects = UnityEngine.Object.FindObjectsByType<AUV>(FindObjectsInactive.Exclude).OrderBy(AUV => AUV.id).ToArray();
        return objects;
    }

    // Главный цикл ручного управления
    void Update()
    {
        if (!TryGetCurrentAUV(out AUV currentAuv))
        {
            return;
        }

        if (!Included)
        {
            currentAuv.SetAllMotorForces(0f);
            return;
        }

        Dictionary<int, float> targetForces = new Dictionary<int, float>();
        bool hasInput = false;

        hasInput |= Forward(targetForces);
        hasInput |= Back(targetForces);
        hasInput |= Left(targetForces);
        hasInput |= Right(targetForces);
        hasInput |= Up(targetForces);
        hasInput |= Down(targetForces);
        hasInput |= Yaw(targetForces);
        hasInput |= Roll(targetForces);
        hasInput |= Pitch(targetForces);

        if (hasInput)
        {
            ApplyMotorForces(currentAuv, targetForces);
        }
        else
        {
            currentAuv.SetAllMotorForces(0f);
        }
    }

    // === Инициализация input ===

    // Инициализирует карту действий и ссылки на action
    private void InitInput()
    {
        if (InputActions == null)
        {
            Debug.LogError($"AUVControllerManager: InputActions with name '{InputActionsName}' was not found.", this);
            return;
        }

        auvMap = InputActions.FindActionMap(ActionMapName, false);
        if (auvMap == null)
        {
            Debug.LogError($"AUVControllerManager: Action map '{ActionMapName}' was not found in InputActions '{InputActions.name}'.", this);
            return;
        }

        forwardAction = FindAction("Forward");
        backAction = FindAction("Back");
        leftAction = FindAction("Left");
        rightAction = FindAction("Right");
        upAction = FindAction("Up");
        downAction = FindAction("Down");
        yawAction = FindAction("Yaw");
        rollAction = FindAction("Roll");
        pitchAction = FindAction("Pitch");
        switchAction = FindAction("Switch");

        if (switchAction != null)
        {
            switchAction.performed += OnSwitchPerformed;
        }
    }

    // Ищет action по имени в карте
    private InputAction FindAction(string actionName)
    {
        InputAction action = auvMap.FindAction(actionName, false);
        if (action == null)
        {
            Debug.LogWarning($"AUVControllerManager: Action '{actionName}' was not found.", this);
        }
        return action;
    }

    // === Поиск InputActionAsset ===

    // Подбирает InputActionAsset по имени из Inspector
    private InputActionAsset ResolveInputActionsAsset(InputActionAsset preferredAsset)
    {
        if (MatchesInputActionsName(preferredAsset))
        {
            return preferredAsset;
        }

        if (preferredAsset != null)
        {
            Debug.LogWarning($"AUVControllerManager: Assigned InputActions '{preferredAsset.name}' does not match required name '{InputActionsName}'. Trying auto search.", this);
        }

        PlayerInput[] playerInputs = UnityEngine.Object.FindObjectsByType<PlayerInput>(FindObjectsInactive.Include);
        for (int i = 0; i < playerInputs.Length; i++)
        {
            InputActionAsset candidate = playerInputs[i].actions;
            if (MatchesInputActionsName(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    // Проверяет совпадение имени ассета действий
    private bool MatchesInputActionsName(InputActionAsset asset)
    {
        if (asset == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(InputActionsName))
        {
            return true;
        }

        return string.Equals(asset.name, InputActionsName, StringComparison.Ordinal);
    }

    // === Переключение активного AUV ===

    // Обрабатывает переключение активного AUV
    private void OnSwitchPerformed(InputAction.CallbackContext context)
    {
        if (!Included)
        {
            return;
        }

        if (TryGetCurrentAUV(out AUV currentAuv))
        {
            currentAuv.SetAllMotorForces(0f);
        }

        SwitchToNextAUV();
    }

    // Обновляет кэш доступных AUV
    private void RefreshAUVs()
    {
        controlledAUVs = GetAUVs();
        if (controlledAUVs.Length == 0)
        {
            currentAuvIndex = -1;
            if (OrbitCamera != null)
            {
                OrbitCamera.target = null;
            }
            return;
        }

        if (currentAuvIndex < 0 || currentAuvIndex >= controlledAUVs.Length)
        {
            currentAuvIndex = 0;
        }
    }

    // Переключает управление на следующий AUV
    private void SwitchToNextAUV()
    {
        RefreshAUVs();
        if (controlledAUVs.Length == 0)
        {
            return;
        }

        currentAuvIndex = (currentAuvIndex + 1) % controlledAUVs.Length;
        SetOrbitTargetToCurrentAUV();
    }

    // Ставит OrbitCamera на текущий AUV
    private void SetOrbitTargetToCurrentAUV()
    {
        if (OrbitCamera == null)
        {
            return;
        }

        if (TryGetCurrentAUV(out AUV currentAuv))
        {
            OrbitCamera.target = currentAuv.transform;
        }
        else
        {
            OrbitCamera.target = null;
        }
    }

    // Возвращает текущий AUV, если он валиден
    private bool TryGetCurrentAUV(out AUV currentAuv)
    {
        currentAuv = null;
        if (controlledAUVs == null || controlledAUVs.Length == 0)
        {
            return false;
        }

        if (currentAuvIndex < 0 || currentAuvIndex >= controlledAUVs.Length)
        {
            currentAuvIndex = 0;
        }

        currentAuv = controlledAUVs[currentAuvIndex];
        return currentAuv != null;
    }

    // === Обработка действий управления ===

    // Движение вперед
    private bool Forward(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(forwardAction))
        {
            return false;
        }

        AddActionForces(ForwardMove, targetForces);
        return true;
    }

    // Движение назад
    private bool Back(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(backAction))
        {
            return false;
        }

        AddActionForces(BackMove, targetForces);
        return true;
    }

    // Движение влево
    private bool Left(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(leftAction))
        {
            return false;
        }

        AddActionForces(LeftMove, targetForces);
        return true;
    }

    // Движение вправо
    private bool Right(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(rightAction))
        {
            return false;
        }

        AddActionForces(RightMove, targetForces);
        return true;
    }

    // Движение вверх
    private bool Up(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(upAction))
        {
            return false;
        }

        AddActionForces(UpMove, targetForces);
        return true;
    }

    // Движение вниз
    private bool Down(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(downAction))
        {
            return false;
        }

        AddActionForces(DownMove, targetForces);
        return true;
    }

    // Рыскание
    private bool Yaw(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(yawAction))
        {
            return false;
        }

        AddActionForces(YawMove, targetForces);
        return true;
    }

    // Крен
    private bool Roll(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(rollAction))
        {
            return false;
        }

        AddActionForces(RollMove, targetForces);
        return true;
    }

    // Тангаж
    private bool Pitch(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(pitchAction))
        {
            return false;
        }

        AddActionForces(PitchMove, targetForces);
        return true;
    }

    // === Вспомогательные функции ===

    // Проверяет, зажата ли кнопка действия
    private static bool IsPressed(InputAction action)
    {
        return action != null && action.IsPressed();
    }

    // Добавляет силы текущего действия в общий словарь моторов
    private void AddActionForces(List<IdAndForce> actionForces, Dictionary<int, float> targetForces)
    {
        if (actionForces == null)
        {
            return;
        }

        for (int i = 0; i < actionForces.Count; i++)
        {
            IdAndForce motorForce = actionForces[i];
            float nextForce = motorForce.Value;

            if (targetForces.TryGetValue(motorForce.Id, out float currentForce))
            {
                nextForce += currentForce;
            }

            targetForces[motorForce.Id] = Mathf.Clamp(nextForce, -100f, 100f);
        }
    }

    // Применяет рассчитанные силы к моторам активного AUV
    private static void ApplyMotorForces(AUV auv, Dictionary<int, float> targetForces)
    {
        auv.SetAllMotorForces(0f);

        foreach (KeyValuePair<int, float> pair in targetForces)
        {
            auv.SetMotorForce(pair.Key, pair.Value);
        }
    }
}
