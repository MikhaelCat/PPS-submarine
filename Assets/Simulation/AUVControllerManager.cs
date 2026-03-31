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
    [SerializeField] string ActionMapName = "AUV";
    [SerializeField] OrbitCamera OrbitCamera;
    
    [System.Serializable]
    public struct IdAndForce
    {
        [SerializeField] public int MotorId;
        [SerializeField] public float Force; // от 100 до -100

        public int Id => MotorId;
        public float Value => Force;
    }
    
    [Header("Manual Control")]
    [SerializeField] List<IdAndForce> ForwardMove = new List<IdAndForce>
    {
        new IdAndForce { MotorId = 1, Force = 100f },
        new IdAndForce { MotorId = 2, Force = 100f },
    };
    [SerializeField] List<IdAndForce> BackMove = new List<IdAndForce>
    {
        new IdAndForce { MotorId = 1, Force = -100f },
        new IdAndForce { MotorId = 2, Force = -100f },
    };
    [SerializeField] List<IdAndForce> LeftMove = new List<IdAndForce>
    {
        new IdAndForce { MotorId = 1, Force = -100f },
        new IdAndForce { MotorId = 2, Force = 100f },
    };
    [SerializeField] List<IdAndForce> RightMove = new List<IdAndForce>
    {
        new IdAndForce { MotorId = 1, Force = 100f },
        new IdAndForce { MotorId = 2, Force = -100f },
    };
    [SerializeField] List<IdAndForce> UpMove = new List<IdAndForce>
    {
        new IdAndForce { MotorId = 3, Force = 100f },
        new IdAndForce { MotorId = 4, Force = 100f },
    };
    [SerializeField] List<IdAndForce> DownMove = new List<IdAndForce>
    {
        new IdAndForce { MotorId = 3, Force = -100f },
        new IdAndForce { MotorId = 4, Force = -100f },
    };
    [SerializeField] List<IdAndForce> YawMove = new List<IdAndForce>
    {
        new IdAndForce { MotorId = 1, Force = 100f },
        new IdAndForce { MotorId = 2, Force = -100f },
    };
    [SerializeField] List<IdAndForce> RollMove = new List<IdAndForce>();
    [SerializeField] List<IdAndForce> PitchMove = new List<IdAndForce>
    {
        new IdAndForce { MotorId = 3, Force = 100f },
        new IdAndForce { MotorId = 4, Force = -100f },
    };

    // Флаг включения ручного управления
    [System.NonSerialized]
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
    private int currentAuvId = -1;

    private static List<IdAndForce> CreateForwardMove()
    {
        return new List<IdAndForce>
        {
            new IdAndForce { MotorId = 1, Force = 100f },
            new IdAndForce { MotorId = 2, Force = 100f },
        };
    }

    private static List<IdAndForce> CreateBackMove()
    {
        return new List<IdAndForce>
        {
            new IdAndForce { MotorId = 1, Force = -100f },
            new IdAndForce { MotorId = 2, Force = -100f },
        };
    }

    private static List<IdAndForce> CreateUpMove()
    {
        return new List<IdAndForce>
        {
            new IdAndForce { MotorId = 3, Force = 100f },
            new IdAndForce { MotorId = 4, Force = 100f },
        };
    }

    private static List<IdAndForce> CreateDownMove()
    {
        return new List<IdAndForce>
        {
            new IdAndForce { MotorId = 3, Force = -100f },
            new IdAndForce { MotorId = 4, Force = -100f },
        };
    }

    private static List<IdAndForce> CreateLeftMove()
    {
        return new List<IdAndForce>
        {
            new IdAndForce { MotorId = 1, Force = -100f },
            new IdAndForce { MotorId = 2, Force = 100f },
        };
    }

    private static List<IdAndForce> CreateRightMove()
    {
        return new List<IdAndForce>
        {
            new IdAndForce { MotorId = 1, Force = 100f },
            new IdAndForce { MotorId = 2, Force = -100f },
        };
    }

    private static List<IdAndForce> CreateYawMove()
    {
        return new List<IdAndForce>
        {
            new IdAndForce { MotorId = 1, Force = 100f },
            new IdAndForce { MotorId = 2, Force = -100f },
        };
    }

    private static List<IdAndForce> CreatePitchMove()
    {
        return new List<IdAndForce>
        {
            new IdAndForce { MotorId = 3, Force = 100f },
            new IdAndForce { MotorId = 4, Force = -100f },
        };
    }

    private bool AreManualMappingsEmpty()
    {
        return ForwardMove.Count == 0
            && BackMove.Count == 0
            && LeftMove.Count == 0
            && RightMove.Count == 0
            && UpMove.Count == 0
            && DownMove.Count == 0
            && YawMove.Count == 0
            && RollMove.Count == 0
            && PitchMove.Count == 0;
    }

    private void ApplyDefaultManualMappings()
    {
        ForwardMove = CreateForwardMove();
        BackMove = CreateBackMove();
        LeftMove = CreateLeftMove();
        RightMove = CreateRightMove();
        UpMove = CreateUpMove();
        DownMove = CreateDownMove();
        YawMove = CreateYawMove();
        RollMove = new List<IdAndForce>();
        PitchMove = CreatePitchMove();
    }

    private void EnsureDefaultsIfNeeded()
    {
        if (ForwardMove == null) ForwardMove = new List<IdAndForce>();
        if (BackMove == null) BackMove = new List<IdAndForce>();
        if (LeftMove == null) LeftMove = new List<IdAndForce>();
        if (RightMove == null) RightMove = new List<IdAndForce>();
        if (UpMove == null) UpMove = new List<IdAndForce>();
        if (DownMove == null) DownMove = new List<IdAndForce>();
        if (YawMove == null) YawMove = new List<IdAndForce>();
        if (RollMove == null) RollMove = new List<IdAndForce>();
        if (PitchMove == null) PitchMove = new List<IdAndForce>();

        if (AreManualMappingsEmpty())
        {
            ApplyDefaultManualMappings();
        }
    }

    private void Reset()
    {
        DefaultIncluded = false;
        ActionMapName = "AUV";
        ApplyDefaultManualMappings();
    }

    // === Unity жизненный цикл ===

    // Инициализация ссылок и ввода
    void Awake()
    {
        EnsureDefaultsIfNeeded();
        Included = DefaultIncluded;

        if (OrbitCamera == null)
        {
            OrbitCamera = UnityEngine.Object.FindAnyObjectByType<OrbitCamera>();
        }

        InitInput();
    }

    void OnValidate()
    {
        EnsureDefaultsIfNeeded();
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
        bool hadAuvBeforeUpdate = currentAuvId >= 0;
        if (!TryGetCurrentAUV(out AUV currentAuv))
        {
            if (hadAuvBeforeUpdate)
            {
                SetOrbitTargetToCurrentAUV();
            }

            return;
        }

        SyncOrbitTarget(currentAuv);

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
            Debug.LogError("AUVControllerManager: InputActions is not assigned in Inspector.", this);
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
        int previousAuvId = currentAuvId;
        if (previousAuvId < 0
            && controlledAUVs != null
            && currentAuvIndex >= 0
            && currentAuvIndex < controlledAUVs.Length
            && controlledAUVs[currentAuvIndex] != null)
        {
            previousAuvId = controlledAUVs[currentAuvIndex].id;
        }

        controlledAUVs = GetAUVs();
        if (controlledAUVs.Length == 0)
        {
            currentAuvIndex = -1;
            currentAuvId = -1;
            if (OrbitCamera != null)
            {
                OrbitCamera.SetTarget(null, false);
            }
            return;
        }

        int nextIndex = -1;
        if (previousAuvId >= 0)
        {
            for (int i = 0; i < controlledAUVs.Length; i++)
            {
                if (controlledAUVs[i] != null && controlledAUVs[i].id == previousAuvId)
                {
                    nextIndex = i;
                    break;
                }
            }
        }

        if (nextIndex >= 0)
        {
            currentAuvIndex = nextIndex;
        }
        else if (currentAuvIndex < 0 || currentAuvIndex >= controlledAUVs.Length)
        {
            currentAuvIndex = 0;
        }

        currentAuvId = controlledAUVs[currentAuvIndex] != null ? controlledAUVs[currentAuvIndex].id : -1;
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
        currentAuvId = controlledAUVs[currentAuvIndex] != null ? controlledAUVs[currentAuvIndex].id : -1;
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
            OrbitCamera.SetTarget(currentAuv.transform, true);
        }
        else
        {
            OrbitCamera.SetTarget(null, false);
        }
    }

    // Возвращает текущий AUV, если он валиден
    private bool TryGetCurrentAUV(out AUV currentAuv)
    {
        currentAuv = null;
        if (controlledAUVs == null || controlledAUVs.Length == 0)
        {
            RefreshAUVs();
            if (controlledAUVs == null || controlledAUVs.Length == 0)
            {
                return false;
            }
        }

        if (currentAuvIndex < 0 || currentAuvIndex >= controlledAUVs.Length || controlledAUVs[currentAuvIndex] == null)
        {
            RefreshAUVs();
            if (controlledAUVs == null || controlledAUVs.Length == 0)
            {
                return false;
            }
        }

        currentAuv = controlledAUVs[currentAuvIndex];
        if (currentAuv == null)
        {
            RefreshAUVs();
            if (controlledAUVs == null || controlledAUVs.Length == 0)
            {
                return false;
            }

            currentAuv = controlledAUVs[currentAuvIndex];
            if (currentAuv == null)
            {
                return false;
            }
        }

        currentAuvId = currentAuv.id;
        return true;
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

        if (HasForces(LeftMove))
        {
            AddActionForces(LeftMove, targetForces);
            return true;
        }

        if (HasForces(YawMove))
        {
            AddActionForces(YawMove, targetForces, -1f);
            return true;
        }

        return false;
    }

    // Движение вправо
    private bool Right(Dictionary<int, float> targetForces)
    {
        if (!IsPressed(rightAction))
        {
            return false;
        }

        if (HasForces(RightMove))
        {
            AddActionForces(RightMove, targetForces);
            return true;
        }

        if (HasForces(YawMove))
        {
            AddActionForces(YawMove, targetForces);
            return true;
        }

        return false;
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

    private void SyncOrbitTarget(AUV currentAuv)
    {
        if (OrbitCamera == null)
        {
            return;
        }

        if (OrbitCamera.target != currentAuv.transform)
        {
            OrbitCamera.SetTarget(currentAuv.transform, true);
        }
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
        AddActionForces(actionForces, targetForces, 1f);
    }

    // Добавляет силы текущего действия в общий словарь моторов с множителем
    private void AddActionForces(List<IdAndForce> actionForces, Dictionary<int, float> targetForces, float multiplier)
    {
        if (actionForces == null)
        {
            return;
        }

        for (int i = 0; i < actionForces.Count; i++)
        {
            IdAndForce motorForce = actionForces[i];
            float nextForce = motorForce.Value * multiplier;

            if (targetForces.TryGetValue(motorForce.Id, out float currentForce))
            {
                nextForce += currentForce;
            }

            targetForces[motorForce.Id] = Mathf.Clamp(nextForce, -100f, 100f);
        }
    }

    // Проверяет, что у действия есть моторные силы
    private static bool HasForces(List<IdAndForce> actionForces)
    {
        return actionForces != null && actionForces.Count > 0;
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
