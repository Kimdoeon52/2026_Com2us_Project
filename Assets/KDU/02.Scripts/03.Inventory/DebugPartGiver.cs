using System.Collections.Generic;
using UnityEngine;

// 테스트용 파츠 지급 창. 키를 누르면 부위 → 등급 → 파츠 순서로 골라 인벤토리에 넣는다
public class DebugPartGiver : MonoBehaviour
{
    [Tooltip("창 열기 키")]
    [SerializeField] private KeyCode _key = KeyCode.F6;

    private readonly List<PartsDefinition> _matches = new List<PartsDefinition>();
    private readonly List<PartGrade> _grades = new List<PartGrade>();
    private bool _open;
    private bool _hasSlot;
    private bool _hasGrade;
    private Slot _slot;
    private PartGrade _grade;
    private string _message = "";
    private Vector2 _scroll;

    private static readonly Slot[] Slots = { Slot.LeftArm, Slot.RightArm, Slot.LeftLeg, Slot.RightLeg, Slot.Head };
    private static readonly string[] SlotNames = { "왼팔", "오른팔", "왼다리", "오른다리", "머리" };

    // 에디터·개발 빌드에서만 씬마다 따로 배치하지 않아도 생긴다
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (!Debug.isDebugBuild || FindAnyObjectByType<DebugPartGiver>() != null)
            return;

        var go = new GameObject(nameof(DebugPartGiver));
        DontDestroyOnLoad(go);
        go.AddComponent<DebugPartGiver>();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(_key))
            return;

        _open = !_open;
        _hasSlot = false;
        _hasGrade = false;
        _message = "";
    }

    private void OnGUI()
    {
        if (!_open)
            return;

        // 1080p 기준 크기로 맞춘다
        float scale = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        GUI.skin.label.fontSize = 20;
        GUI.skin.button.fontSize = 20;

        GUILayout.BeginArea(new Rect(20f, 20f, 420f, 700f), GUI.skin.box);
        GUILayout.Label($"파츠 지급 ({_key} 닫기)");

        PartsCatalog catalog = InventoryHost.Instance != null ? InventoryHost.Instance.Catalog : null;
        if (catalog == null || catalog.Parts == null)
            GUILayout.Label("InventoryHost나 파츠 카탈로그가 없다.");
        else if (!_hasSlot)
            DrawSlots();
        else if (!_hasGrade)
            DrawGrades(catalog);
        else
            DrawParts(catalog);

        if (!string.IsNullOrEmpty(_message))
            GUILayout.Label(_message);

        GUILayout.EndArea();
    }

    private void DrawSlots()
    {
        GUILayout.Label("1. 부위");
        for (int i = 0; i < Slots.Length; i++)
        {
            if (GUILayout.Button(SlotNames[i], GUILayout.Height(40f)))
            {
                _slot = Slots[i];
                _hasSlot = true;
            }
        }
    }

    private void DrawGrades(PartsCatalog catalog)
    {
        GUILayout.Label($"2. 등급 ({SlotName(_slot)})");

        _grades.Clear();
        foreach (PartsDefinition part in catalog.Parts)
        {
            if (part != null && part.Slot == _slot && !_grades.Contains(part.Grade))
                _grades.Add(part.Grade);
        }

        _grades.Sort();
        if (_grades.Count == 0)
            GUILayout.Label("이 부위 파츠가 없다.");

        foreach (PartGrade grade in _grades)
        {
            if (GUILayout.Button(PartGrades.ToLabel(grade), GUILayout.Height(40f)))
            {
                _grade = grade;
                _hasGrade = true;
                _scroll = Vector2.zero;
            }
        }

        if (GUILayout.Button("< 뒤로", GUILayout.Height(32f)))
            _hasSlot = false;
    }

    private void DrawParts(PartsCatalog catalog)
    {
        GUILayout.Label($"3. 파츠 ({SlotName(_slot)} · {PartGrades.ToLabel(_grade)})");

        _matches.Clear();
        foreach (PartsDefinition part in catalog.Parts)
        {
            if (part != null && part.Slot == _slot && part.Grade == _grade)
                _matches.Add(part);
        }

        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(480f));
        foreach (PartsDefinition part in _matches)
        {
            if (GUILayout.Button($"{part.DisplayName}  ({part.Id})", GUILayout.Height(40f)))
                Give(part);
        }

        GUILayout.EndScrollView();

        if (GUILayout.Button("< 뒤로", GUILayout.Height(32f)))
            _hasGrade = false;
    }

    private void Give(PartsDefinition part)
    {
        bool given = InventoryHost.Instance.GivePart(part);
        _message = given ? $"지급: {part.DisplayName}" : "인벤토리에 빈자리가 없다.";
    }

    private static string SlotName(Slot slot)
    {
        int index = System.Array.IndexOf(Slots, slot);
        return index >= 0 ? SlotNames[index] : slot.ToString();
    }
}
