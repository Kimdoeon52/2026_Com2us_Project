using UnityEngine;

// 다리 스킬 테스트용. 숫자키 1~9로 지정 슬롯 스킬 교체
public class TEMP_LegSkillTester : MonoBehaviour
{
    [Tooltip("테스트할 스킬 목록")]
    [SerializeField] private ActionData[] skills;

    [Tooltip("교체할 슬롯")]
    [SerializeField] private ActionSlot slot = ActionSlot.LeftLeg;

    private PlayerSkillLoadout loadout;

    private void Awake()
    {
        loadout = GetComponent<PlayerSkillLoadout>();
        if (loadout == null)
            Debug.LogWarning("[LegSkillTester] PlayerSkillLoadout 없음");
    }

    private void Update()
    {
        if (loadout == null || skills == null)
            return;

        for (int i = 0; i < skills.Length && i < 9; i++)
        {
            if (!Input.GetKeyDown(KeyCode.Alpha1 + i))
                continue;

            loadout.SetSlot(slot, skills[i]);
            Debug.Log($"[LegSkillTester] {slot} ← {(skills[i] != null ? skills[i].ActionName : "없음")}");
        }
    }

    private void OnGUI()
    {
        if (loadout == null || skills == null)
            return;

        ActionData current = loadout.GetSlotAction(slot);
        GUILayout.BeginArea(new Rect(10, 10, 320, 240), GUI.skin.box);
        GUILayout.Label($"{slot}: {(current != null ? current.ActionName : "없음")}");
        for (int i = 0; i < skills.Length && i < 9; i++)
            GUILayout.Label($"{i + 1}. {(skills[i] != null ? skills[i].ActionName : "-")}");
        GUILayout.EndArea();
    }
}
