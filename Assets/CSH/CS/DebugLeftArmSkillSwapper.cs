using UnityEngine;

/// <summary>
/// 테스트용: Shift+Alt+1~6을 누르면 배열의 해당 스킬을 왼팔(Q) 슬롯에 넣는다.
/// 플레이어 오브젝트(PlayerSkillLoadout이 붙은 곳)에 붙이고 skills 배열을 인스펙터에서 채운다.
/// 주의: 같은 오브젝트의 PartSkillLoadout이 장착 파츠로 왼팔 스킬을 채우고 있으면 그쪽이 우선한다.
/// </summary>
public class DebugLeftArmSkillSwapper : MonoBehaviour
{
    private static readonly KeyCode[] NumberKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
        KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
    };

    [Tooltip("Shift+Alt+1~6 순서대로 왼팔에 넣을 스킬")]
    [SerializeField] private ActionData[] skills = new ActionData[6];

    [Tooltip("비우면 같은 오브젝트에서 찾는다")]
    [SerializeField] private PlayerSkillLoadout loadout;

    private void Awake()
    {
        if (loadout == null)
            loadout = GetComponent<PlayerSkillLoadout>();

        if (loadout == null)
            Debug.LogWarning($"[DebugLeftArmSkillSwapper] {name}: PlayerSkillLoadout 없음", this);
    }

    private void Update()
    {
        if (loadout == null || skills == null)
            return;

        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (!shift || !alt)
            return;

        int count = Mathf.Min(skills.Length, NumberKeys.Length);
        for (int i = 0; i < count; i++)
        {
            if (!Input.GetKeyDown(NumberKeys[i]))
                continue;

            ActionData skill = skills[i];
            if (skill == null)
            {
                Debug.LogWarning($"[DebugLeftArmSkillSwapper] {i + 1}번 칸 비어 있음", this);
                return;
            }

            loadout.SetSlot(ActionSlot.LeftArm, skill);
            Debug.Log($"[DebugLeftArmSkillSwapper] 왼팔 ← {i + 1}번 {skill.name}", this);
            return;
        }
    }
}
