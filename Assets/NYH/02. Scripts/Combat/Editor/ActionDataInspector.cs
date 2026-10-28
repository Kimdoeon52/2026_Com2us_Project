using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionData 인스펙터의 "칸 목록" 부분을 그리는 공용 함수 (2026-10-27).
///
/// ActionData 칸이 30개를 넘으면서 펀치 하나를 고치려 해도 투사체·화염·도약·실린더 칸을 다 지나가야 했다.
/// 대부분의 기술은 부가 기능을 하나도 안 쓰므로, 여기서는
///   · 모든 기술이 쓰는 핵심 칸(식별·프레임·피해·연출·판정 박스)은 항상 펼치고
///   · 부가 기능은 접어두되, 값이 들어 있으면 "사용 중"으로 표시하고 자동으로 펼치고
///   · 2026-09-29에 무효가 된 칸은 "사용 안 함"으로 숨기고
///   · 같이 쓰면 이상해지는 조합은 경고를 띄운다.
/// 데이터(에셋)는 하나도 안 바뀐다 — 보이는 방식만 바꾼다.
///
/// ActionDataEditor·FanActionDataEditor가 둘 다 이걸 부른다. 파생 클래스(CSH의 SkillData 계열)가 추가한 칸은
/// 아래 목록에 없으므로 맨 끝 "기타" 묶음에 그대로 그려진다 — 새 칸이 생겨도 인스펙터에서 사라지는 일은 없다.
/// </summary>
public static class ActionDataInspector
{
    private sealed class Group
    {
        public readonly string title;
        public readonly string[] fields;
        public readonly bool alwaysOpen;

        public Group(string title, bool alwaysOpen, params string[] fields)
        {
            this.title = title;
            this.alwaysOpen = alwaysOpen;
            this.fields = fields;
        }
    }

    // 그리는 순서 그대로. 핵심 묶음(alwaysOpen) → 부가 기능 묶음 → 사용 안 함 → 판정 박스
    private static readonly Group[] Groups =
    {
        new Group("식별", true, "actionName", "source", "requiredPart"),
        new Group("프레임 (60fps)", true, "startupFrames", "activeFrames", "recoveryFrames", "isInvincibleDuringActive"),
        new Group("피해", true, "damage", "knockbackDistance", "hitIntervalFrames", "hitStopScale"),
        new Group("연출", true, "animationClipName", "activeClipName", "keepClipSpeed"),

        new Group("이동 · 잔상", false, "advanceDistance", "advanceDuringActiveOnly", "trailAfterImageInterval", "groundOnly"),
        new Group("도약", false, "leapVelocity"),
        new Group("띄우기 (맞은 상대)", false, "launchVelocity"),
        new Group("투사체", false, "projectile", "projectileIntervalFrames", "projectileMuzzle"),
        new Group("화염", false, "flame"),
        new Group("박스당 피해", false, "perBoxDamage"),
        new Group("쿨타임", false, "cooldownFrames", "cooldownOwner"),
        new Group("실린더", false, "cylinderCost", "fallbackAction"),

        // KKH 코드가 실제로 안 읽는 것까지 2026-10-06에 확인. 지우기 전 KKH 최종 확인 대기 (CLAUDE_1 §14)
        new Group("사용 안 함 (2026-09-29 무효 — 값을 넣어도 효과 없음)", false, "isGuardable", "causesKnockdown", "staggerValue"),

        new Group("판정 박스", true, "frameBoxes"),
    };

    private const string DeadGroupTitlePrefix = "사용 안 함";
    private const string FoldoutKeyPrefix = "NYH.ActionDataInspector.";

    private static GUIStyle usedFoldoutStyle;

    /// <summary>칸 목록 + 조합 경고를 그린다. 호출하는 쪽은 그 아래에 박스 편집 UI 등을 이어서 그리면 된다</summary>
    public static void Draw(SerializedObject serializedObject, ActionData action)
    {
        serializedObject.Update();

        DrawWarnings(action);

        var drawn = new HashSet<string> { "m_Script" };
        foreach (var group in Groups)
        {
            foreach (var field in group.fields) drawn.Add(field);
            DrawGroup(serializedObject, group);
        }

        DrawRemaining(serializedObject, drawn);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawGroup(SerializedObject serializedObject, Group group)
    {
        // 이 클래스 버전에 없는 칸 이름이 섞여 있어도(파생·구버전) 있는 칸만 그린다
        var properties = new List<SerializedProperty>();
        foreach (var field in group.fields)
        {
            var property = serializedObject.FindProperty(field);
            if (property != null) properties.Add(property);
        }
        if (properties.Count == 0) return;

        EditorGUILayout.Space(4f);

        if (group.alwaysOpen)
        {
            EditorGUILayout.LabelField(group.title, EditorStyles.boldLabel);
            DrawProperties(properties);
            return;
        }

        bool isDeadGroup = group.title.StartsWith(DeadGroupTitlePrefix);
        bool inUse = !isDeadGroup && IsAnyInUse(properties);

        // 처음 열 때는 "사용 중이면 펼침"이 기본값. 사람이 접고 펼친 건 에디터를 닫을 때까지 기억한다(SessionState)
        string key = FoldoutKeyPrefix + group.title;
        bool open = SessionState.GetBool(key, inUse);

        string title = inUse ? group.title + "   ● 사용 중" : group.title;
        bool newOpen = EditorGUILayout.Foldout(open, title, true, inUse ? UsedFoldoutStyle() : EditorStyles.foldout);
        if (newOpen != open) SessionState.SetBool(key, newOpen);
        if (!newOpen) return;

        EditorGUI.indentLevel++;
        DrawProperties(properties);
        EditorGUI.indentLevel--;
    }

    private static void DrawProperties(List<SerializedProperty> properties)
    {
        foreach (var property in properties)
            EditorGUILayout.PropertyField(property, true);
    }

    // 위 목록에 없는 칸(파생 클래스가 추가한 칸 등)을 맨 끝에 그대로 그린다 — 여기서 빠지면 인스펙터에서 영영 안 보이게 된다
    private static void DrawRemaining(SerializedObject serializedObject, HashSet<string> drawn)
    {
        var remaining = new List<SerializedProperty>();
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (!drawn.Contains(iterator.name)) remaining.Add(iterator.Copy());
        }
        if (remaining.Count == 0) return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("기타 (파생 클래스 칸)", EditorStyles.boldLabel);
        DrawProperties(remaining);
    }

    // "기본값이 아니면 사용 중". 불·숫자·벡터·참조만 보면 부가 기능 칸은 전부 판단된다
    private static bool IsAnyInUse(List<SerializedProperty> properties)
    {
        foreach (var property in properties)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    if (property.boolValue) return true;
                    break;
                case SerializedPropertyType.Integer:
                    // 투사체 간격처럼 기본값이 1 이상인 칸은 그 칸 혼자서는 "사용 중"이 아니다 — 투사체 참조로 판단
                    if (property.name != "projectileIntervalFrames" && property.intValue != 0) return true;
                    break;
                case SerializedPropertyType.Float:
                    if (!Mathf.Approximately(property.floatValue, 0f)) return true;
                    break;
                case SerializedPropertyType.Vector2:
                    // 총구 위치는 투사체·화염과 같이 쓰는 칸이라, 그 자체로는 사용 여부를 정하지 않는다
                    if (property.name != "projectileMuzzle" && property.vector2Value != Vector2.zero) return true;
                    break;
                case SerializedPropertyType.ObjectReference:
                    if (property.objectReferenceValue != null) return true;
                    break;
            }
        }
        return false;
    }

    // 같이 켜면 이상해지거나 아무 효과가 없는 조합. 막지는 않고 알려만 준다 — 의도한 조합일 수도 있어서
    private static void DrawWarnings(ActionData action)
    {
        if (action == null) return;

        bool hasProjectile = action.Projectile != null;
        bool hasFlame = action.Flame != null;
        bool hasLeap = action.LeapVelocity != Vector2.zero;

        if (hasProjectile && hasFlame)
            EditorGUILayout.HelpBox("투사체와 화염이 둘 다 켜져 있습니다. 둘 다 같은 총구(Projectile Muzzle)에서 나옵니다.", MessageType.Warning);

        if ((hasProjectile || hasFlame) && action.ActiveFrames == 0)
            EditorGUILayout.HelpBox("활성 프레임이 0이라 투사체·화염이 한 번도 안 나갑니다. 둘 다 활성 구간에만 나옵니다.", MessageType.Error);

        if (hasProjectile && action.ActiveFrames > 0 && action.ProjectileIntervalFrames >= action.ActiveFrames)
            EditorGUILayout.HelpBox($"발사 간격({action.ProjectileIntervalFrames}f)이 활성 구간({action.ActiveFrames}f) 이상이라 1발만 나갑니다.", MessageType.Info);

        if (hasLeap && !Mathf.Approximately(action.AdvanceDistance, 0f))
            EditorGUILayout.HelpBox("도약과 전진(Advance Distance)이 둘 다 켜져 있습니다. 공중에서 두 이동이 겹칩니다.", MessageType.Warning);

        if (hasLeap && !action.GroundOnly)
            EditorGUILayout.HelpBox("도약 기술인데 '지상 전용'이 꺼져 있어서 공중에서도 또 뛸 수 있습니다. 의도한 게 아니면 이동 · 잔상 → Ground Only를 켜세요.", MessageType.Info);

        if (action.KeepClipSpeed && string.IsNullOrEmpty(action.ActiveClipName))
            EditorGUILayout.HelpBox("'클립 원래 속도'가 켜져 있는데 활성 클립이 없어서, 행동 내내 첫 클립이 원래 속도로 재생됩니다. 클립이 행동보다 짧으면 반복되고 길면 잘립니다.", MessageType.Info);

        if (action.StaggerValue != 0 || action.CausesKnockdown)
            EditorGUILayout.HelpBox("'사용 안 함' 묶음에 값이 남아 있습니다. 2026-09-29 이후 효과가 없는 칸입니다.", MessageType.Info);
    }

    private static GUIStyle UsedFoldoutStyle()
    {
        if (usedFoldoutStyle == null)
        {
            usedFoldoutStyle = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
        }
        return usedFoldoutStyle;
    }
}
