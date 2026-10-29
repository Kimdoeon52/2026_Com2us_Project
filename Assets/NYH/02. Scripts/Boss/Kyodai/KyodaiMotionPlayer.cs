using System;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using UnityEngine;

/// <summary>
/// KyodaiMotionData를 t초 자세로 바꿔 부위에 적용한다 (2026-10-29).
/// 게임(KyodaiMotionPattern)과 모션 창 미리보기가 같은 함수를 쓴다 — 미리보기에서 본 그대로 게임에서 나온다.
/// </summary>
public static class KyodaiMotionPlayer
{
    /// <summary>t초의 온몸 애니메이션·손 위치·손목·레이어를 적용. target = 목표 지점(경기장 월드)</summary>
    public static void ApplyPose(KyodaiMotionData motion, float t, Vector2 target, KyodaiBossController boss)
    {
        if (motion == null || boss == null || boss.Studio == null) return;

        // 온몸 애니메이션: 실제 샘플링은 KyodaiRig.LateUpdate가 IK 전에 한다.
        // 클립이 있으면 트랙 없는 부위는 IK를 끈다 — 안 끄면 대기 자리로 당겨져서 애니메이션을 덮는다
        KyodaiRig rig = FindRig(boss);
        if (rig != null) rig.SetClip(motion.clip, t);
        if (motion.clip != null)
            foreach (var l in boss.Limbs) if (l.Ready && motion.tracks.Find(x => x.partId == l.PartId) == null) l.SetIKWeight(0f);

        PixelStudio studio = boss.Studio;
        foreach (var track in motion.tracks)
        {
            KyodaiLimb limb = boss.FindLimb(track.partId);
            if (limb == null || !limb.Ready) continue;

            LimbLayer layer = track.LayerAt(t);
            if (limb.Layer != layer)
            {
                if (layer == LimbLayer.Linger && limb.HurtZone != null) limb.HurtZone.SetWorldSize(track.hurtSize);
                limb.SetLayer(layer);
            }

            if (track.keys.Count == 0) { limb.SetIKWeight(motion.clip != null ? 0f : 1f); continue; }
            EvaluateTrack(track, t, target, (w, d) => studio.WorldToStudio(w, d), limb.RestPosition,
                          out Vector3 studioPos, out Quaternion hand, out float weight);
            limb.SetPose(studioPos, hand, weight);
        }
    }

    /// <summary>모션이 끝났을 때: 온몸 애니메이션을 풀고 대기 자세로 (패턴 정리·미리보기 끝)</summary>
    public static void Clear(KyodaiBossController boss)
    {
        if (boss == null) return;
        FindRig(boss)?.SetClip(null, 0f);
        foreach (var l in boss.Limbs) if (l.Ready) l.SnapToRest();
    }

    private static KyodaiRig FindRig(KyodaiBossController boss)
    {
        foreach (var l in boss.Limbs) if (l.Rig != null) return l.Rig;
        return null;
    }

    /// <summary>(prev, now] 사이에 있는 이벤트를 부른다. 처음엔 prev = -1로 0초 이벤트까지 포함</summary>
    public static void FireEvents(KyodaiMotionData motion, float prev, float now, Action<KyodaiMotionEvent> fire)
    {
        if (motion == null) return;
        foreach (var e in motion.events)
            if (e.time > prev && e.time <= now) fire(e);
    }

    /// <summary>키 하나가 경기장 어디에 보이는지. 대기 키면 restWorld(부위의 대기 자리, 모르면 null → 목표+offset)</summary>
    public static Vector2 KeyWorld(KyodaiMotionKey key, Vector2 target, Vector2? restWorld)
    {
        if (key.rest && restWorld.HasValue) return restWorld.Value;
        return target + key.offset;
    }

    private static Vector3 KeyStudio(KyodaiMotionKey key, Vector2 target, Func<Vector2, float, Vector3> toStudio, Vector3 restStudio) =>
        key.rest ? restStudio : toStudio(target + key.offset, key.depth);

    /// <summary>
    /// 트랙의 t초 손 위치(스튜디오 좌표)와 손목 회전. toStudio = 경기장 좌표+깊이 → 스튜디오 좌표, restStudio = 대기 손 자리.
    /// 게임은 실제 스튜디오로, 편집 미리보기는 숨긴 복제본 카메라로 같은 계산을 한다
    /// </summary>
    public static void EvaluateTrack(KyodaiMotionTrack track, float t, Vector2 target, Func<Vector2, float, Vector3> toStudio,
                                     Vector3 restStudio, out Vector3 pos, out Quaternion hand, out float weight)
    {
        var keys = track.keys;
        if (t <= keys[0].time || keys.Count == 1)
        {
            pos = KeyStudio(keys[0], target, toStudio, restStudio);
            hand = Quaternion.Euler(keys[0].handRotation);
            weight = keys[0].ikWeight;
            return;
        }

        for (int i = 0; i < keys.Count - 1; i++)
        {
            KyodaiMotionKey a = keys[i], b = keys[i + 1];
            if (t > b.time) continue;

            float span = Mathf.Max(b.time - a.time, 1e-4f);
            float u = Mathf.Clamp01((t - a.time) / span);
            float e = Ease01(b.ease, u);
            // 스튜디오 공간에서 잇는다 — 깊이(크기)도 같이 자연스럽게 변한다
            pos = Vector3.LerpUnclamped(KeyStudio(a, target, toStudio, restStudio), KeyStudio(b, target, toStudio, restStudio), e);
            hand = Quaternion.SlerpUnclamped(Quaternion.Euler(a.handRotation), Quaternion.Euler(b.handRotation), e);
            weight = Mathf.Clamp01(Mathf.LerpUnclamped(a.ikWeight, b.ikWeight, e));
            return;
        }

        var last = keys[keys.Count - 1];
        pos = KeyStudio(last, target, toStudio, restStudio);
        hand = Quaternion.Euler(last.handRotation);
        weight = last.ikWeight;
    }

    public static float Ease01(Ease ease, float u)
    {
        if (ease == Ease.Unset || ease == Ease.INTERNAL_Zero || ease == Ease.INTERNAL_Custom) ease = Ease.Linear;
        return EaseManager.Evaluate(ease, null, u, 1f, 1.70158f, 0f);
    }
}
