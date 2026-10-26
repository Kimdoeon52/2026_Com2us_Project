using System.Collections.Generic;
using RealSteel.Terrain;
using UnityEditor;
using UnityEngine;

// 열린 씬의 더미(RSHeap)에 수집 더미·노드를 붙이고 탐지·파동 참조까지 연결한다.
// 손으로 12개를 클릭하면 참조가 어긋나기 쉬워 에디터에서 만든다
public class ScrapHeapFieldWindow : EditorWindow
{
    private const string PulsePrefabPath = "Assets/KDU/04.Prefab/ScrapPulse.prefab";
    private const string NodesRootName = "Nodes";
    private const string PulseEmitterName = "PulseEmitter";

    private string _namePrefix = "더미_";
    private ScrapyardDefinition _definition;
    private int _nodesPerHeap = 3;
    private float _nodeRingRatio = 0.55f;
    private float _detectRadius = 6f;
    private float _interactRadius = 4f;
    private Transform _trackedTarget;
    private bool _buildPulseEmitter = true;

    [MenuItem("KDU/고물상/더미 수집 노드 세팅")]
    private static void Open()
    {
        GetWindow<ScrapHeapFieldWindow>("더미 수집 노드 세팅");
    }

    private void OnGUI()
    {
        _namePrefix = EditorGUILayout.TextField("이름 접두어", _namePrefix);
        _definition = (ScrapyardDefinition)EditorGUILayout.ObjectField("지역 정의", _definition, typeof(ScrapyardDefinition), false);
        _nodesPerHeap = Mathf.Max(1, EditorGUILayout.IntField("더미당 노드 수", _nodesPerHeap));
        _nodeRingRatio = EditorGUILayout.Slider("노드 배치 반경 비율", _nodeRingRatio, 0f, 1f);
        _detectRadius = Mathf.Max(0f, EditorGUILayout.FloatField("탐지 반경", _detectRadius));
        _interactRadius = Mathf.Max(0f, EditorGUILayout.FloatField("상호작용 반경", _interactRadius));

        EditorGUILayout.Space();
        _trackedTarget = (Transform)EditorGUILayout.ObjectField("추적 대상(플레이어)", _trackedTarget, typeof(Transform), true);
        _buildPulseEmitter = EditorGUILayout.Toggle("파동 이미터 생성", _buildPulseEmitter);

        EditorGUILayout.HelpBox("여러 번 눌러도 이미 붙은 것은 건드리지 않는다. Ctrl+Z로 되돌릴 수 있다.", MessageType.None);

        List<RSHeap> heaps = FindHeaps();

        using (new EditorGUI.DisabledScope(heaps.Count == 0))
        {
            if (GUILayout.Button($"세팅 (더미 {heaps.Count}개)"))
                Apply(heaps);
        }
    }

    private void OnFocus() => Repaint();

    // 접두어로 거른다. 둔덕·웅덩이 같은 다른 요소는 제외된다
    private List<RSHeap> FindHeaps()
    {
        List<RSHeap> found = new List<RSHeap>();

        foreach (RSHeap heap in Object.FindObjectsByType<RSHeap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (string.IsNullOrEmpty(_namePrefix) || heap.name.StartsWith(_namePrefix))
                found.Add(heap);
        }

        found.Sort((a, b) => EditorUtility.NaturalCompare(a.name, b.name));
        return found;
    }

    private void Apply(List<RSHeap> heaps)
    {
        Undo.SetCurrentGroupName("더미 수집 노드 세팅");
        int group = Undo.GetCurrentGroup();

        for (int i = 0; i < heaps.Count; i++)
            SetupHeap(heaps[i]);

        WireDetector();

        Undo.CollapseUndoOperations(group);
        Debug.Log($"더미 {heaps.Count}개에 수집 노드를 세팅했다.");
    }

    private void SetupHeap(RSHeap heap)
    {
        ScrapPile pile = GetOrAddComponent<ScrapPile>(heap.gameObject);
        SetField(pile, "_definition", _definition);

        ScrapHeapVisual visual = GetOrAddComponent<ScrapHeapVisual>(heap.gameObject);
        SetField(visual, "_heap", heap);

        Transform nodesRoot = GetOrCreateChild(heap.transform, NodesRootName);

        for (int i = 0; i < _nodesPerHeap; i++)
            SetupNode(heap, nodesRoot, i);
    }

    // 더미 둘레를 따라 고르게 깔고 지면 높이에 맞춘다
    private void SetupNode(RSHeap heap, Transform nodesRoot, int index)
    {
        Transform node = GetOrCreateChild(nodesRoot, $"Node_{index + 1:00}");

        float angle = index * Mathf.PI * 2f / _nodesPerHeap;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (heap.radius * _nodeRingRatio);
        Vector3 world = heap.transform.position + offset;

        Undo.RecordObject(node, "더미 수집 노드 세팅");
        node.position = SnapToTerrain(heap, world);

        ScrapNode scrapNode = GetOrAddComponent<ScrapNode>(node.gameObject);
        SetField(scrapNode, "_detectRadius", _detectRadius);
        SetField(scrapNode, "_interactRadius", _interactRadius);
    }

    private static Vector3 SnapToTerrain(RSHeap heap, Vector3 world)
    {
        RSTerrain terrain = heap.Owner;
        if (terrain == null || !terrain.HasHeights)
            return world;

        Vector3 local = terrain.transform.InverseTransformPoint(world);
        if (!terrain.InsideXZ(local.x, local.z))
            return world;

        return new Vector3(world.x, terrain.SampleHeightWorld(world), world.z);
    }

    private void WireDetector()
    {
        ScrapNodeDetector detector = Object.FindFirstObjectByType<ScrapNodeDetector>();
        if (detector == null)
        {
            Debug.LogWarning("씬에서 ScrapNodeDetector를 찾지 못했다.");
            return;
        }

        if (_trackedTarget != null)
            SetField(detector, "_trackedTarget", _trackedTarget);

        if (!_buildPulseEmitter || _trackedTarget == null)
            return;

        Transform emitterRoot = GetOrCreateChild(_trackedTarget, PulseEmitterName);
        ScrapPulseEmitter emitter = GetOrAddComponent<ScrapPulseEmitter>(emitterRoot.gameObject);

        SetField(emitter, "_detector", detector);

        GameObject pulse = AssetDatabase.LoadAssetAtPath<GameObject>(PulsePrefabPath);
        if (pulse != null && pulse.TryGetComponent(out ScrapPulseInstance instance))
            SetField(emitter, "_pulsePrefab", instance);
    }

    // ---------- 에디터 헬퍼 ----------

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent(out T found) ? found : Undo.AddComponent<T>(go);
    }

    private static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null)
            return found;

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, "더미 수집 노드 세팅");
        created.transform.SetParent(parent, false);
        return created.transform;
    }

    // 비워둔 참조만 채운다. 손으로 바꿔둔 값은 유지한다
    private static void SetField(Object target, string path, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(path);

        if (property != null && property.objectReferenceValue == null)
        {
            property.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }
    }

    private static void SetField(Object target, string path, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(path);

        if (property != null)
        {
            property.floatValue = value;
            so.ApplyModifiedProperties();
        }
    }
}
