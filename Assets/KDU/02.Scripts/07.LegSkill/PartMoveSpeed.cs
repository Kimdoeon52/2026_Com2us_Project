using UnityEngine;

// 장착 다리의 이동속도 지수를 RobotMover 이동 배율로 건다. 기준 지수면 배율 1
[RequireComponent(typeof(RobotMover))]
public class PartMoveSpeed : MonoBehaviour
{
    private const string ModifierKey = "PartLegSpeed";

    [Tooltip("배율 1이 되는 다리 지수")]
    [Min(1f)][SerializeField] private float _baseIndex = 100f;

    private RobotMover _mover;
    private InventoryHost _host;

    private void Awake()
    {
        _mover = GetComponent<RobotMover>();
    }

    private void OnEnable()
    {
        Hook();
    }

    // 씬 시작 시 OnEnable이 InventoryHost.Awake보다 먼저 불릴 수 있다
    private void Start()
    {
        Hook();
    }

    private void OnDisable()
    {
        if (_host != null)
            _host.Changed -= Apply;

        _host = null;

        if (_mover != null)
            _mover.ClearSpeedModifier(ModifierKey);
    }

    private void Hook()
    {
        if (_host != null || InventoryHost.Instance == null)
            return;

        _host = InventoryHost.Instance;
        _host.Changed += Apply;
        Apply();
    }

    private void Apply()
    {
        if (_mover == null || _host == null)
            return;

        float index = CombatCalculator.CalculateMoveSpeed(LegIndex(Slot.LeftLeg), LegIndex(Slot.RightLeg));

        // 다리 지수가 비어 있으면 기본 속도 유지
        if (index <= 0f)
        {
            _mover.ClearSpeedModifier(ModifierKey);
            return;
        }

        _mover.SetSpeedModifier(ModifierKey, index / _baseIndex);
    }

    private float LegIndex(Slot slot)
    {
        PartsDefinition part = _host.GetEquippedOrStarting(slot);
        return part != null ? part.MoveSpeedIndex : 0f;
    }
}
