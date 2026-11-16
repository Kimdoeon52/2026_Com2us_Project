using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 회피 성공 연출 (2026-11-16 사용자: 회피에 성공하면 MISS + 줌인 + 잠깐 시간이 느려지게, DOTween으로 자연스럽게).
///
/// "회피 성공" = 공격이 회피 무적 중인 플레이어에게 닿은 순간. HitDetection.IsInvincibleNow가 플레이어를 무적으로 판정할 때 부른다 —
/// KKH 보스 공격(BossPatternGimmickBase.IsPlayerInvincible → ApplyDamageToPlayer, 쿄다이 공격 전부 포함)과 일반 적 로봇 공격이 모두 이 길을 지난다.
/// (KKH 일부 기물은 무적 플래그를 직접 봐서 이 길을 안 지난다 — 그건 막히긴 하지만 연출은 안 나온다)
///
/// 연출 (값은 DodgeSuccessSettings):
///   · MISS — 플레이어 머리 위 (DamageNumberEmitter.ShowMiss, 공용 스타일의 MISS 색·글자)
///   · 슬로우모션 — Time.timeScale을 실제 시간으로 트윈(느려짐 → 버팀 → 부드럽게 복귀). 전투 틱(CombatClock)도 deltaTime이라 같이 느려진다
///   · 줌인 — CameraFx(그리기 순간에만 — 경기장 계산·판정은 그대로)로 확대 + 플레이어 쪽으로 살짝
/// 한 번 회피에 공격이 여러 번 닿아도(레이저는 매 틱 닿는다) cooldownSeconds 안에는 한 번만.
/// 연출이 끝나거나 중간에 끊겨도(씬 이동 등) 시간 배율과 줌은 원래대로 되돌린다.
/// </summary>
public static class DodgeSuccessDirector
{
    private static float lastTriggerTime = -999f;
    private static Sequence timeSeq, zoomSeq;
    private static float baseTimeScale = 1f;
    private static bool restarting;

    /// <summary>회피 성공 순간 (소리·UI 등 다른 연출이 구독)</summary>
    public static event Action<ActionExecutor> OnDodgeSuccess;

    /// <summary>호출: HitDetection.IsInvincibleNow (플레이어가 무적일 때 공격이 닿음)</summary>
    public static void Trigger(ActionExecutor player)
    {
        if (!Application.isPlaying || player == null) return;
        DodgeSuccessSettings s = DodgeSuccessSettings.Current;
        float now = Time.realtimeSinceStartup;
        if (now - lastTriggerTime < s.cooldownSeconds) return;
        lastTriggerTime = now;

        var emitter = player.GetComponent<DamageNumberEmitter>();
        if (emitter == null) emitter = player.gameObject.AddComponent<DamageNumberEmitter>();
        emitter.ShowMiss();
        OnDodgeSuccess?.Invoke(player);

        if (!s.enabled || Time.timeScale <= 0f) return; // 일시정지 중엔 연출하지 않는다
        PlaySlowMotion(s);
        PlayZoom(s, player.transform.position + Vector3.up * 0.5f);
    }

    private static void PlaySlowMotion(DodgeSuccessSettings s)
    {
        restarting = true;
        timeSeq?.Kill(); // 이전 연출이 남아 있으면 끝내고(원래 속도로 되돌린 뒤) 새로
        restarting = false;
        baseTimeScale = Time.timeScale;
        float slow = baseTimeScale * s.slowTimeScale;

        timeSeq = DOTween.Sequence()
            .Append(DOTween.To(() => Time.timeScale, SetTimeScale, slow, s.slowInSeconds).SetEase(Ease.OutQuad))
            .AppendInterval(s.slowHoldSeconds)
            .Append(DOTween.To(() => Time.timeScale, SetTimeScale, baseTimeScale, s.slowOutSeconds).SetEase(Ease.InOutSine))
            .SetUpdate(true) // 실제 시간 — 느려진 시간으로 돌면 복귀도 느려진다
            .OnKill(() => { SetTimeScale(baseTimeScale); timeSeq = null; });
    }

    // 누가 일시정지(0)를 걸었으면 덮어쓰지 않는다
    private static void SetTimeScale(float v)
    {
        if (Time.timeScale > 0f) Time.timeScale = v;
    }

    private static void PlayZoom(DodgeSuccessSettings s, Vector3 target)
    {
        restarting = true;
        zoomSeq?.Kill();
        restarting = false;
        CameraFx.SetZoom(s.zoomAmount, s.followPlayer, target);

        zoomSeq = DOTween.Sequence()
            .Append(DOTween.To(() => CameraFx.Zoom01, v => CameraFx.Zoom01 = v, 1f, s.zoomInSeconds).SetEase(Ease.OutCubic))
            .AppendInterval(s.zoomHoldSeconds)
            .Append(DOTween.To(() => CameraFx.Zoom01, v => CameraFx.Zoom01 = v, 0f, s.zoomOutSeconds).SetEase(Ease.InOutSine))
            .SetUpdate(true)
            .OnKill(() => { if (!restarting) CameraFx.Zoom01 = 0f; zoomSeq = null; });
    }
}
