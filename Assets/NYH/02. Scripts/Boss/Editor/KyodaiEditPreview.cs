using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이하지 않고 모션을 보는 미리보기 (2026-10-29, 모션 창 전용).
/// 2026-10-30: 스튜디오를 없애면서 숨긴 복제본 대신 씬의 실제 로봇을 직접 움직인다 — Scene 뷰에서 돌려보고 확대해서 볼 수 있다.
///
/// 시작할 때 로봇 뼈(상자 모델이면 손·팔 상자)의 자세를 적어 두고, 매번 그 자세로 되돌린 뒤
/// 게임과 같은 계산(온몸 애니메이션 → KyodaiMotionPlayer.EvaluateTrack + KyodaiIK)으로 꺾는다. End에서 원래 자세로 되돌린다.
/// 씬 저장 직전·어셈블리 리로드·플레이 시작 때는 모션 창이 End를 불러 원래 자세로 저장되게 한다.
/// </summary>
public class KyodaiEditPreview
{
    private class LimbProxy
    {
        public string partId;
        public bool rig;
        public Transform upper, lower, end;
        public Vector3 upperRestScale;
        public float baseLength;
        public Vector3 elbowHint;
        public LimbReach reach;
        public float maxGrow;
        public Transform box, segment, shoulder; // 임시 상자 모델
        public Quaternion boxRestRot;
        public Vector3 restWorld;
    }

    private GameObject clipRoot;          // 애니메이션 클립을 샘플링할 오브젝트(애니메이터 자리)
    private Transform[] poseBones = new Transform[0];
    private Vector3[] restPos;
    private Quaternion[] restRot;
    private Vector3[] restScale;
    private readonly List<LimbProxy> limbs = new List<LimbProxy>();

    public bool Active => Boss != null;
    public KyodaiBossController Boss { get; private set; }

    public bool Begin(KyodaiBossController boss)
    {
        End();
        if (boss == null) return false;
        Boss = boss;

        // 되돌릴 트랜스폼: 리깅 모델이면 모델 아래 전부(모델 자체 위치는 사용자가 옮기는 것이라 뺀다), 상자 모델이면 손·팔 상자
        var bones = new List<Transform>();
        KyodaiRig rig = null;
        foreach (var l in boss.Limbs) if (l.Rig != null) { rig = l.Rig; break; }
        var byName = new Dictionary<string, Transform>();
        if (rig != null)
        {
            var anim = rig.GetComponentInChildren<Animator>(true);
            clipRoot = anim != null ? anim.gameObject : rig.gameObject;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;
                if (t != rig.transform) bones.Add(t);
            }
        }

        foreach (var limb in boss.Limbs)
        {
            var p = new LimbProxy { partId = limb.PartId, elbowHint = limb.ElbowHint, reach = limb.ReachMode, maxGrow = limb.MaxGrow };
            if (limb.Rig != null)
            {
                byName.TryGetValue(limb.UpperBone, out p.upper);
                byName.TryGetValue(limb.LowerBone, out p.lower);
                byName.TryGetValue(limb.EndBone, out p.end);
                if (p.upper == null || p.lower == null || p.end == null) continue;
                p.rig = true;
                p.upperRestScale = p.upper.localScale;
                p.baseLength = Vector3.Distance(p.upper.position, p.lower.position) + Vector3.Distance(p.lower.position, p.end.position);
                p.restWorld = p.end.position;
            }
            else
            {
                p.box = limb.EffectorSetting;
                if (p.box == null) continue;
                p.segment = limb.Segment;
                p.shoulder = limb.Shoulder;
                p.boxRestRot = p.box.rotation;
                p.restWorld = p.box.position;
                bones.Add(p.box);
                if (p.segment != null) bones.Add(p.segment);
            }
            limbs.Add(p);
        }

        poseBones = bones.ToArray();
        restPos = new Vector3[poseBones.Length];
        restRot = new Quaternion[poseBones.Length];
        restScale = new Vector3[poseBones.Length];
        for (int i = 0; i < poseBones.Length; i++)
        {
            restPos[i] = poseBones[i].localPosition;
            restRot[i] = poseBones[i].localRotation;
            restScale[i] = poseBones[i].localScale;
        }
        return true;
    }

    /// <summary>부위의 대기 손 자리 (없으면 null)</summary>
    public Vector2? RestWorld(string partId)
    {
        var p = limbs.Find(x => x.partId == partId);
        return p != null ? (Vector2)p.restWorld : (Vector2?)null;
    }

    private void Restore()
    {
        for (int i = 0; i < poseBones.Length; i++)
        {
            if (poseBones[i] == null) continue;
            poseBones[i].localPosition = restPos[i];
            poseBones[i].localRotation = restRot[i];
            poseBones[i].localScale = restScale[i];
        }
    }

    /// <summary>t초 자세로 꺾는다</summary>
    public void Apply(KyodaiMotionData motion, float t, Vector2 target)
    {
        if (!Active || motion == null) return;

        // 1) 처음 자세로
        Restore();

        // 2) 온몸 애니메이션
        if (motion.clip != null && clipRoot != null) motion.clip.SampleAnimation(clipRoot, Mathf.Clamp(t, 0f, motion.clip.length));

        // 3) 손 키 IK (세기만큼)
        foreach (var track in motion.tracks)
        {
            var p = limbs.Find(x => x.partId == track.partId);
            if (p == null || track.keys.Count == 0) continue;
            if (p.rig ? p.upper == null : p.box == null) continue;
            KyodaiMotionPlayer.EvaluateTrack(track, t, target, Boss.PlayPoint, p.restWorld, out Vector3 pos, out Quaternion hand, out float weight);
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
    }

    /// <summary>원래 자세로만 돌린다 (기본 자세 기록은 유지 — 애니메이션 창이 로봇을 쓰는 동안)</summary>
    public void Pause()
    {
        if (Active) Restore();
    }

    /// <summary>원래 자세로 되돌리고 끝낸다</summary>
    public void End()
    {
        if (Active) Restore();
        clipRoot = null;
        poseBones = new Transform[0];
        limbs.Clear();
        Boss = null;
    }
}
