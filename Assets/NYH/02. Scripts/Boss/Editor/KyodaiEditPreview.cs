using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 플레이하지 않고 모션을 보는 미리보기 (2026-10-29, 모션 창 전용).
///
/// 씬을 건드리지 않으려고 스튜디오를 통째로 숨긴 복제본(HideAndDontSave — 저장 안 됨, Hierarchy에 안 보임)으로 만들고,
/// 그 복제본의 팔을 게임과 같은 계산(KyodaiMotionPlayer.EvaluateTrack + KyodaiIK)으로 꺾은 뒤
/// 전용 카메라로 저해상도 렌더텍스처에 찍는다. 모션 창이 그걸 Scene 뷰의 판 자리와 창 안에 그린다.
/// 메쉬 쪼개기·앞/뒤 판·레이어 색은 없다(전부 한 장) — 동작 모양 확인용.
/// </summary>
public class KyodaiEditPreview
{
    private const int PreviewLayer = 29; // 숨긴 복제본만 찍으려고 쓰는 빈 레이어

    private class LimbProxy
    {
        public string partId;
        public bool rig;
        public Transform upper, lower, end;
        public Quaternion upperRest, lowerRest, endRest;
        public Vector3 upperRestScale;
        public float baseLength;
        public Vector3 elbowHint;
        public LimbReach reach;
        public float maxGrow;
        public Transform box, segment, shoulder; // 임시 상자 모델
        public Vector3 boxRestPos;
        public Quaternion boxRestRot;
        public Vector3 restStudio;
    }

    private GameObject clone;
    private GameObject clipRoot;          // 복제본에서 애니메이션 클립을 샘플링할 오브젝트(애니메이터 자리)
    private Transform[] poseBones = new Transform[0];
    private Vector3[] restPos;
    private Quaternion[] restRot;
    private Vector3[] restScale;
    private Camera cam;
    private RenderTexture texture;
    private Rect arena;
    private readonly List<LimbProxy> limbs = new List<LimbProxy>();

    public RenderTexture Texture => texture;
    public Rect Arena => arena;
    public bool Active => clone != null;
    public KyodaiBossController Boss { get; private set; }

    public bool Begin(KyodaiBossController boss)
    {
        End();
        PixelStudio studio = boss != null ? boss.Studio : null;
        if (studio == null || studio.StudioCamera == null) return false;
        Boss = boss;
        arena = studio.ArenaRect;

        // 1) 스튜디오 복제 (숨김·저장 안 됨). 복제된 카메라는 지운다 — 남으면 게임 화면에 그려진다
        clone = Object.Instantiate(studio.gameObject, studio.transform.position, studio.transform.rotation);
        clone.name = "쿄다이 편집 미리보기 (숨김)";
        foreach (var c in clone.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject == clone ? (Object)c : c.gameObject);
        foreach (var t in clone.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            t.gameObject.layer = PreviewLayer;
        }
        foreach (var smr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.forceMatrixRecalculationPerRender = true; // 편집 모드에서 뼈를 바꾼 직후 찍어도 반영되게
            smr.updateWhenOffscreen = true;
        }

        // 2) 전용 카메라
        int w = Mathf.Clamp(Mathf.RoundToInt(arena.width * studio.PixelsPerUnit), 8, 2048);
        int h = Mathf.Clamp(Mathf.RoundToInt(arena.height * studio.PixelsPerUnit), 8, 2048);
        texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
        texture.Create();

        var camGo = new GameObject("쿄다이 편집 미리보기 카메라") { hideFlags = HideFlags.HideAndDontSave };
        cam = camGo.AddComponent<Camera>();
        cam.CopyFrom(studio.StudioCamera); // 위치·화각까지 같게
        cam.enabled = false;               // 직접 찍을 때만
        cam.cullingMask = 1 << PreviewLayer;
        cam.targetTexture = texture;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.aspect = arena.width / arena.height;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        var data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = false;
        data.renderShadows = false;

        // 3) 부위마다 복제본 쪽 뼈·상자 찾기 (원본과 계층이 같으니 경로로)
        var cloneBones = new Dictionary<string, Transform>();
        foreach (var t in clone.GetComponentsInChildren<Transform>(true)) if (!cloneBones.ContainsKey(t.name)) cloneBones[t.name] = t;

        // 리깅 모델: 애니메이션 샘플링 자리와 기본 자세(복제본 쪽 모든 트랜스폼) — 게임의 KyodaiRig와 같은 순서로 쓴다
        KyodaiRig srcRig = null;
        foreach (var l in boss.Limbs) if (l.Rig != null) { srcRig = l.Rig; break; }
        if (srcRig != null)
        {
            Transform rigClone = Map(studio.transform, clone.transform, srcRig.transform);
            if (rigClone != null)
            {
                var anim = rigClone.GetComponentInChildren<Animator>(true);
                clipRoot = anim != null ? anim.gameObject : rigClone.gameObject;
                if (anim != null) anim.enabled = false;
                poseBones = rigClone.GetComponentsInChildren<Transform>(true);
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
        }

        foreach (var limb in boss.Limbs)
        {
            var p = new LimbProxy { partId = limb.PartId, elbowHint = limb.ElbowHint, reach = limb.ReachMode, maxGrow = limb.MaxGrow };
            if (limb.Rig != null)
            {
                cloneBones.TryGetValue(limb.UpperBone, out p.upper);
                cloneBones.TryGetValue(limb.LowerBone, out p.lower);
                cloneBones.TryGetValue(limb.EndBone, out p.end);
                if (p.upper == null || p.lower == null || p.end == null) continue;
                p.rig = true;
                p.upperRest = p.upper.localRotation;
                p.lowerRest = p.lower.localRotation;
                p.endRest = p.end.localRotation;
                p.upperRestScale = p.upper.localScale;
                p.baseLength = Vector3.Distance(p.upper.position, p.lower.position) + Vector3.Distance(p.lower.position, p.end.position);
                p.restStudio = p.end.position;
            }
            else
            {
                p.box = Map(studio.transform, clone.transform, limb.EffectorSetting);
                if (p.box == null) continue;
                p.segment = Map(studio.transform, clone.transform, limb.Segment);
                p.shoulder = Map(studio.transform, clone.transform, limb.Shoulder);
                p.boxRestPos = p.box.position;
                p.boxRestRot = p.box.rotation;
                p.restStudio = p.boxRestPos;
            }
            limbs.Add(p);
        }
        return true;
    }

    // 원본 계층의 transform을 복제본의 같은 자리로 (형제 순서 경로)
    private static Transform Map(Transform srcRoot, Transform dstRoot, Transform src)
    {
        if (src == null || !src.IsChildOf(srcRoot)) return null;
        var path = new List<int>();
        for (Transform t = src; t != srcRoot; t = t.parent) path.Add(t.GetSiblingIndex());
        Transform d = dstRoot;
        for (int i = path.Count - 1; i >= 0; i--)
        {
            if (path[i] >= d.childCount) return null;
            d = d.GetChild(path[i]);
        }
        return d;
    }

    /// <summary>부위의 대기 손 자리가 경기장 어디에 보이는지 (없으면 null)</summary>
    public Vector2? RestWorld(string partId)
    {
        var p = limbs.Find(x => x.partId == partId);
        return p != null && cam != null ? PixelStudio.StudioToWorld(cam, arena, p.restStudio) : (Vector2?)null;
    }

    /// <summary>t초 자세로 꺾고 찍는다</summary>
    public void Render(KyodaiMotionData motion, float t, Vector2 target)
    {
        if (!Active || motion == null) return;

        // 1) 기본 자세로 (뼈 전부 + 상자 손)
        for (int i = 0; i < poseBones.Length; i++)
        {
            poseBones[i].localPosition = restPos[i];
            poseBones[i].localRotation = restRot[i];
            poseBones[i].localScale = restScale[i];
        }
        foreach (var p in limbs)
            if (!p.rig) p.box.SetPositionAndRotation(p.boxRestPos, p.boxRestRot);

        // 2) 온몸 애니메이션
        if (motion.clip != null && clipRoot != null) motion.clip.SampleAnimation(clipRoot, Mathf.Clamp(t, 0f, motion.clip.length));

        // 3) 손 키 IK (세기만큼)
        foreach (var track in motion.tracks)
        {
            var p = limbs.Find(x => x.partId == track.partId);
            if (p == null || track.keys.Count == 0) continue;
            KyodaiMotionPlayer.EvaluateTrack(track, t, target, (w, d) => PixelStudio.WorldToStudio(cam, arena, w, d), p.restStudio,
                                             out Vector3 pos, out Quaternion hand, out float weight);
            if (p.rig)
            {
                Quaternion upperBase = p.upper.localRotation, lowerBase = p.lower.localRotation, endBase = p.end.localRotation;
                Vector3 scaleBase = p.upper.localScale;
                if (weight > 0f)
                {
                    if (p.reach == LimbReach.Grow) KyodaiIK.Grow(p.upper, p.upperRestScale, p.baseLength, pos, p.maxGrow);
                    KyodaiIK.SolveTwoBone(p.upper, p.lower, p.end, pos, p.elbowHint);
                    if (weight < 1f)
                    {
                        p.upper.localRotation = Quaternion.Slerp(upperBase, p.upper.localRotation, weight);
                        p.lower.localRotation = Quaternion.Slerp(lowerBase, p.lower.localRotation, weight);
                        p.upper.localScale = Vector3.Lerp(scaleBase, p.upper.localScale, weight);
                    }
                }
                p.end.localRotation = endBase * hand;
            }
            else
            {
                p.box.SetPositionAndRotation(pos, p.boxRestRot * hand);
                if (p.segment != null && p.shoulder != null) KyodaiIK.Stretch(p.segment, p.shoulder.position, pos);
            }
        }

        var request = new RenderPipeline.StandardRequest { destination = texture };
        if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
        else cam.Render();
    }

    public void End()
    {
        if (clone != null) Object.DestroyImmediate(clone);
        if (cam != null) Object.DestroyImmediate(cam.gameObject);
        if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
        clone = null; cam = null; texture = null; clipRoot = null;
        poseBones = new Transform[0];
        limbs.Clear();
        Boss = null;
    }
}
