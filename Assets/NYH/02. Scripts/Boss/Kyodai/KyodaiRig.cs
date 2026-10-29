using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 리깅된 3D 모델(스튜디오 안)을 쿄다이 부위로 쓸 수 있게 하는 다리 (2026-10-29).
///
/// 1) 메쉬 쪼개기: 리깅 모델은 보통 메쉬가 한 덩어리라, 팔만 앞 판(플레이어 앞)으로 꺼낼 수가 없다.
///    시작할 때 뼈대 기준으로 삼각형을 나눠 부위별 SkinnedMeshRenderer를 따로 만든다(몸통 + 팔·다리).
///    각 삼각형은 가장 영향이 큰 뼈가 어느 부위 뿌리(예: RightShoulder) 아래에 있는지로 나눈다. 원본 렌더러는 끈다.
///    → FBX 임포트 설정에서 Read/Write가 켜져 있어야 한다.
/// 2) 몸 따라오기: 팔이 닿지 않는 곳을 노리면 몸(Hips)을 그쪽으로 조금 기울여 보낸다. 여러 부위가 동시에 요청하면 합친다.
/// 3) IK 순서: 매 프레임 몸 → 각 부위 IK 순서로 직접 부른다(LateUpdate 순서가 컴포넌트마다 보장되지 않아서).
/// </summary>
public class KyodaiRig : MonoBehaviour
{
    [Serializable]
    public class Part
    {
        [Tooltip("KyodaiLimb의 부위 이름과 같게")]
        public string partId;
        [Tooltip("이 뼈와 그 아래 뼈에 붙은 메쉬가 이 부위가 된다")]
        public string rootBone;
    }

    [Tooltip("쪼갤 원본 메쉬. 비우면 자식에서 첫 SkinnedMeshRenderer")]
    [SerializeField] private SkinnedMeshRenderer source;

    [SerializeField] private List<Part> parts = new List<Part>
    {
        new Part { partId = "오른팔", rootBone = "RightShoulder" },
        new Part { partId = "왼팔", rootBone = "LeftShoulder" },
        new Part { partId = "오른다리", rootBone = "RightUpperLeg" },
        new Part { partId = "왼다리", rootBone = "LeftUpperLeg" },
    };

    [Tooltip("팔이 안 닿을 때 기울여 보낼 몸 뼈")]
    [SerializeField] private string bodyBone = "Hips";

    [Tooltip("몸이 따라가는 빠르기 (클수록 빨리 붙는다)")]
    [Min(0f)] [SerializeField] private float bodyFollowSpeed = 8f;

    private readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    private readonly Dictionary<string, SkinnedMeshRenderer> partRenderers = new Dictionary<string, SkinnedMeshRenderer>();
    private readonly List<KyodaiLimb> limbs = new List<KyodaiLimb>();
    private Transform body;
    private Vector3 bodyOffset;

    // 기본 자세(시작할 때 뼈 전부) — 매 프레임 여기로 되돌린 뒤 애니메이션 → IK. 안 되돌리면 IK가 바꾼 회전이 쌓인다
    private Transform[] poseBones = new Transform[0];
    private Vector3[] restPos;
    private Quaternion[] restRot;
    private Vector3[] restScale;

    // 온몸 애니메이션 (모션 재생 중에만)
    private AnimationClip clip;
    private float clipTime;
    private GameObject clipRoot;

    public SkinnedMeshRenderer BodyRenderer { get; private set; }

    private void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (!bones.ContainsKey(t.name)) bones.Add(t.name, t);

        body = FindBone(bodyBone);

        // 애니메이터가 있으면 끈다 — 애니메이션 창에서 클립을 만들면 컨트롤러가 붙어서 저절로 재생되는데,
        // 여기서는 모션 시간에 맞춰 직접 샘플링한다. 클립 경로는 애니메이터 오브젝트 기준이라 거기에 샘플링
        var animator = GetComponentInChildren<Animator>(true);
        clipRoot = animator != null ? animator.gameObject : gameObject;
        if (animator != null) animator.enabled = false;

        if (source == null) source = GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (source == null) { Debug.LogError("[KyodaiRig] SkinnedMeshRenderer가 없음 — 리깅된 모델인지 확인"); return; }
        CaptureRestPose();
        if (!source.sharedMesh.isReadable)
        {
            Debug.LogError("[KyodaiRig] 메쉬 Read/Write가 꺼져 있어 쪼갤 수 없음 — FBX 선택 → Model 탭 → Read/Write 체크 → Apply");
            return;
        }
        Split();
    }

    public Transform FindBone(string boneName) =>
        !string.IsNullOrEmpty(boneName) && bones.TryGetValue(boneName, out var t) ? t : null;

    /// <summary>부위 렌더러(쪼갠 결과). 없으면 null — 그 부위는 몸통에 붙어서 같이 움직인다</summary>
    public SkinnedMeshRenderer GetPartRenderer(string partId) =>
        partId != null && partRenderers.TryGetValue(partId, out var r) ? r : null;

    // 호출: KyodaiLimb.Start
    public void Register(KyodaiLimb limb) { if (!limbs.Contains(limb)) limbs.Add(limb); }

    /// <summary>온몸 애니메이션 클립과 그 시각 (null이면 대기 자세). KyodaiMotionPlayer가 매 프레임 부른다</summary>
    public void SetClip(AnimationClip c, float t)
    {
        clip = c;
        clipTime = t;
    }

    private void CaptureRestPose()
    {
        var list = new List<Transform>(source.bones);
        if (body != null && !list.Contains(body)) list.Add(body);
        list.RemoveAll(t => t == null);
        poseBones = list.ToArray();
        restPos = new Vector3[poseBones.Length];
        restRot = new Quaternion[poseBones.Length];
        restScale = new Vector3[poseBones.Length];
        for (int i = 0; i < poseBones.Length; i++)
        {
            restPos[i] = poseBones[i].localPosition;
            restRot[i] = poseBones[i].localRotation;
            restScale[i] = poseBones[i].localScale;
        }
    }

    private void LateUpdate()
    {
        // 1) 뼈 전부 기본 자세로 — 매 프레임 같은 출발점에서 풀어야 오차가 안 쌓인다
        for (int i = 0; i < poseBones.Length; i++)
        {
            poseBones[i].localPosition = restPos[i];
            poseBones[i].localRotation = restRot[i];
            poseBones[i].localScale = restScale[i];
        }

        // 2) 온몸 애니메이션 (손가락·팔꿈치·몸통). IK는 이 자세에서 출발한다
        if (clip != null) clip.SampleAnimation(clipRoot, Mathf.Clamp(clipTime, 0f, clip.length));

        // 3) 몸 따라오기: 팔이 모자란 만큼을 합쳐서 몸을 옮긴다
        Vector3 wanted = Vector3.zero;
        foreach (var limb in limbs) wanted += limb.ReachShortfall();
        float k = 1f - Mathf.Exp(-bodyFollowSpeed * Time.deltaTime);
        bodyOffset = Vector3.Lerp(bodyOffset, wanted, k);
        if (body != null) body.position += bodyOffset;

        // 4) 부위별 IK (IK 세기만큼)
        foreach (var limb in limbs) limb.SolveRig();
    }

    // ── 메쉬 쪼개기 ──

    private void Split()
    {
        Mesh mesh = source.sharedMesh;
        Transform[] smrBones = source.bones;
        BoneWeight[] weights = mesh.boneWeights;
        if (weights == null || weights.Length == 0) { Debug.LogError("[KyodaiRig] 뼈 가중치가 없는 메쉬"); return; }

        int bodyIndex = parts.Count;
        int[] bonePart = new int[smrBones.Length];
        for (int i = 0; i < smrBones.Length; i++) bonePart[i] = PartOfBone(smrBones[i], bodyIndex);

        // 정점마다 가장 영향이 큰 뼈(boneIndex0)의 부위
        int[] vertexPart = new int[weights.Length];
        for (int v = 0; v < weights.Length; v++)
        {
            int b = weights[v].boneIndex0;
            vertexPart[v] = b >= 0 && b < bonePart.Length ? bonePart[b] : bodyIndex;
        }

        // 부위 × 서브메쉬별 삼각형 목록. 세 정점 중 둘 이상이 같은 부위면 그 부위, 아니면 몸통
        int subCount = mesh.subMeshCount;
        var tris = new List<int>[bodyIndex + 1, subCount];
        for (int s = 0; s < subCount; s++)
        {
            int[] src = mesh.GetTriangles(s);
            for (int i = 0; i < src.Length; i += 3)
            {
                int p0 = vertexPart[src[i]], p1 = vertexPart[src[i + 1]], p2 = vertexPart[src[i + 2]];
                int p = p0 == p1 || p0 == p2 ? p0 : p1 == p2 ? p1 : bodyIndex;
                (tris[p, s] ??= new List<int>()).Add(src[i]);
                tris[p, s].Add(src[i + 1]);
                tris[p, s].Add(src[i + 2]);
            }
        }

        var cache = new MeshArrays(mesh);
        for (int p = 0; p <= bodyIndex; p++)
        {
            string id = p == bodyIndex ? "몸통" : parts[p].partId;
            Mesh partMesh = BuildPartMesh(cache, mesh, tris, p, subCount, id);
            if (partMesh == null) continue;
            var r = CreateRenderer(partMesh, id);
            if (p == bodyIndex) BodyRenderer = r;
            else partRenderers[id] = r;
        }
        source.enabled = false;

        if (partRenderers.Count == 0)
            Debug.LogWarning("[KyodaiRig] 팔·다리 뼈 이름을 못 찾아서 메쉬가 몸통 하나로만 나왔음 — Parts의 Root Bone 이름 확인");
    }

    private int PartOfBone(Transform bone, int bodyIndex)
    {
        for (Transform t = bone; t != null && t != transform; t = t.parent)
            for (int k = 0; k < parts.Count; k++)
                if (t.name == parts[k].rootBone) return k;
        return bodyIndex;
    }

    // 원본 정점 배열을 한 번만 읽어 두고 부위마다 필요한 정점만 골라 담는다
    private class MeshArrays
    {
        public readonly Vector3[] vertices, normals;
        public readonly Vector4[] tangents;
        public readonly Vector2[] uv0, uv1;
        public readonly Color[] colors;
        public readonly BoneWeight[] weights;
        public readonly Matrix4x4[] bindposes;

        public MeshArrays(Mesh m)
        {
            vertices = m.vertices; normals = m.normals; tangents = m.tangents;
            uv0 = m.uv; uv1 = m.uv2; colors = m.colors;
            weights = m.boneWeights; bindposes = m.bindposes;
        }
    }

    private static Mesh BuildPartMesh(MeshArrays a, Mesh src, List<int>[,] tris, int part, int subCount, string id)
    {
        var remap = new Dictionary<int, int>();
        var order = new List<int>();
        bool any = false;
        for (int s = 0; s < subCount; s++)
        {
            var list = tris[part, s];
            if (list == null) continue;
            any = true;
            for (int i = 0; i < list.Count; i++)
            {
                int old = list[i];
                if (!remap.TryGetValue(old, out int idx)) { idx = order.Count; remap[old] = idx; order.Add(old); }
                list[i] = idx;
            }
        }
        if (!any) return null;

        int n = order.Count;
        var mesh = new Mesh { name = $"{src.name}_{id}", indexFormat = n > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.vertices = Pick(a.vertices, order);
        if (a.normals.Length > 0) mesh.normals = Pick(a.normals, order);
        if (a.tangents.Length > 0) mesh.tangents = Pick(a.tangents, order);
        if (a.uv0.Length > 0) mesh.uv = Pick(a.uv0, order);
        if (a.uv1.Length > 0) mesh.uv2 = Pick(a.uv1, order);
        if (a.colors.Length > 0) mesh.colors = Pick(a.colors, order);
        mesh.boneWeights = Pick(a.weights, order);
        mesh.bindposes = a.bindposes;
        mesh.subMeshCount = subCount;
        for (int s = 0; s < subCount; s++)
            mesh.SetTriangles(tris[part, s] ?? new List<int>(), s, false);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static T[] Pick<T>(T[] src, List<int> order)
    {
        var dst = new T[order.Count];
        for (int i = 0; i < dst.Length; i++) dst[i] = src[order[i]];
        return dst;
    }

    private SkinnedMeshRenderer CreateRenderer(Mesh mesh, string id)
    {
        var go = new GameObject($"{id} (분리)");
        go.layer = source.gameObject.layer;
        go.transform.SetParent(source.transform.parent, false);
        go.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
        go.transform.localScale = source.transform.localScale;

        var r = go.AddComponent<SkinnedMeshRenderer>();
        r.sharedMesh = mesh;
        r.sharedMaterials = source.sharedMaterials;
        r.bones = source.bones;
        r.rootBone = source.rootBone;
        r.updateWhenOffscreen = true; // 팔을 크게 뻗어도 경계 상자 때문에 사라지지 않게
        r.shadowCastingMode = source.shadowCastingMode;
        return r;
    }
}
