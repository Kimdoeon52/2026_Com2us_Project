using System;
using UnityEngine;

/// <summary>효과음 한 칸 — 여러 개 넣으면 매번 무작위로 하나 (같은 소리 반복이 덜 들리게)</summary>
[Serializable]
public class KyodaiSfxCue
{
    public AudioClip[] clips = new AudioClip[0];

    [Range(0f, 1f)] public float volume = 1f;

    [Tooltip("재생할 때마다 음높이를 ± 이만큼 흔든다 (0.05 = ±5%)")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.05f;

    public bool IsEmpty => clips == null || clips.Length == 0;
}

/// <summary>
/// 쿄다이 효과음 묶음 (2026-11-05). 소리가 나는 순간은 코드가 정한다(전투 틱의 그 사건 — 찍는 순간, 발사 1프레임 등)라
/// 패턴 길이·수치가 바뀌어도 따로 맞출 필요 없다. 길이가 변하는 구간(차지·발사 중·기물 내려옴)은 반복 소리를 켜 두고 단계가 끝나면 끈다.
/// 처음 소리는 코드로 합성한 것(Boss/Editor/Tools~/kyodai-sfx.mjs) — 칸마다 다른 파일로 바꿔 끼우면 된다.
/// </summary>
[CreateAssetMenu(menuName = "NYH/Boss/Kyodai Sfx Data", fileName = "KyodaiSfxData")]
public class KyodaiSfxData : ScriptableObject
{
    [Range(0f, 1f)] public float masterVolume = 0.8f;

    [Tooltip("소리 위치에 따라 좌우로 나누는 정도 (0 = 가운데, 1 = 화면 끝이면 한쪽 끝)")]
    [Range(0f, 1f)] public float stereoSpread = 0.5f;

    [Header("손")]
    [Tooltip("주먹이 바닥을 찍는 순간")]
    public KyodaiSfxCue fistImpact = new KyodaiSfxCue();
    [Tooltip("손바닥 충격 (양손 손뼉·압착)")]
    public KyodaiSfxCue palmImpact = new KyodaiSfxCue();
    [Tooltip("공격 판정이 시작되는 키 — 휘두르기·밀기")]
    public KyodaiSfxCue strikeWhoosh = new KyodaiSfxCue();
    [Tooltip("플레이어에게 맞음")]
    public KyodaiSfxCue handHit = new KyodaiSfxCue();
    [Tooltip("레이저에 맞아 떨어지기 시작")]
    public KyodaiSfxCue handZapped = new KyodaiSfxCue();
    [Tooltip("떨어져 바닥에 부딪힘")]
    public KyodaiSfxCue handThud = new KyodaiSfxCue();
    [Tooltip("충격파 내려치기 — 바닥을 따라 퍼짐")]
    public KyodaiSfxCue shockwave = new KyodaiSfxCue();

    [Header("어깨 레이저")]
    [Tooltip("조준·고정 동안 반복 — 차지할수록 음이 올라간다(laserChargePitch)")]
    public KyodaiSfxCue laserCharge = new KyodaiSfxCue();
    [Tooltip("차지 반복음 높이: 조준 시작 → 발사 직전")]
    public Vector2 laserChargePitch = new Vector2(0.8f, 1.5f);
    [Tooltip("발사 순간")]
    public KyodaiSfxCue laserFire = new KyodaiSfxCue();
    [Tooltip("발사 중 반복 (쓸기 포함)")]
    public KyodaiSfxCue laserBeam = new KyodaiSfxCue();
    [Tooltip("쓸고 난 바닥 폭발 하나")]
    public KyodaiSfxCue laserExplosion = new KyodaiSfxCue();

    [Header("내려찍는 기물")]
    [Tooltip("화면 위에서 내려오는 동안 반복")]
    public KyodaiSfxCue crusherDescend = new KyodaiSfxCue();
    [Tooltip("멈추고 떨기 시작 — 피할 신호")]
    public KyodaiSfxCue crusherLock = new KyodaiSfxCue();
    [Tooltip("찍는 순간")]
    public KyodaiSfxCue crusherSlam = new KyodaiSfxCue();
    [Tooltip("다시 올라가기 시작")]
    public KyodaiSfxCue crusherRise = new KyodaiSfxCue();

    [Header("촉수")]
    public KyodaiSfxCue tentacleEmerge = new KyodaiSfxCue();
    [Tooltip("휘두르기 시작")]
    public KyodaiSfxCue tentacleSwing = new KyodaiSfxCue();
    public KyodaiSfxCue tentacleHit = new KyodaiSfxCue();
    public KyodaiSfxCue tentacleDie = new KyodaiSfxCue();

    [Header("보스")]
    [Tooltip("2페이즈 전환 연출 시작")]
    public KyodaiSfxCue phaseTransition = new KyodaiSfxCue();
    [Tooltip("그로기 시작")]
    public KyodaiSfxCue groggy = new KyodaiSfxCue();
    [Tooltip("격파")]
    public KyodaiSfxCue defeat = new KyodaiSfxCue();
}
