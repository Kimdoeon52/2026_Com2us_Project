using UnityEngine;

/// <summary>
/// 쿄다이 레이저 수치 (2026-11-04 2D 개편). 본체 눈이 플레이어를 노리고 쏜다 — 손에 맞으면 그 손이 정지한다(냉각탑 유도 파괴 대체).
/// 기획 수치: 조준선이 좁아지는 2초(원본 레드 록온), 팔 정지 4초(11-04 개편). 나머지는 임시값.
/// 데미지·부위 피해는 패턴 데이터(KKH BossPatternData).
/// </summary>
[CreateAssetMenu(menuName = "NYH/Boss/Kyodai Laser Data", fileName = "KyodaiLaserData")]
public class KyodaiLaserData : ScriptableObject
{
    [Header("단계 길이 (60fps 프레임)")]
    [Tooltip("조준선이 플레이어를 따라가며 좁아지는 시간. 기획: 2초")]
    [Min(1)] public int aimFrames = 120;

    [Tooltip("TODO: 기획 수치 없음. 조준이 멈추고 깜빡이는 시간 — 플레이어가 피할 틈")]
    [Min(0)] public int lockFrames = 20;

    [Tooltip("TODO: 기획 수치 없음. 레이저가 나가 있는 시간")]
    [Min(1)] public int fireFrames = 24;

    [Tooltip("레이저가 가늘어지며 사라지는 시간(연출)")]
    [Min(0)] public int fadeFrames = 12;

    [Header("조준")]
    [Tooltip("조준점이 플레이어를 따라가는 스프링 빠르기(Hz). 작을수록 늦게 따라와 유도하기 쉽다")]
    [Min(0.1f)] public float aimFrequency = 1.2f;

    [Range(0.1f, 1.5f)] public float aimDamping = 0.9f;

    [Tooltip("조준점 높이 — 바닥에서 플레이어 가슴쯤(월드)")]
    public float aimHeight = 1f;

    [Header("판정")]
    [Tooltip("레이저 굵기(월드) — 판정·그림 공용")]
    [Min(0.05f)] public float beamWidth = 0.9f;

    [Tooltip("레이저 최대 길이(월드). 보통은 바닥에서 끝난다")]
    [Min(1f)] public float maxLength = 40f;

    [Tooltip("손에 맞으면 그 손이 하던 공격을 멈추고 바닥으로 떨어져 누워 있는 프레임. 기획: 4초 (2026-11-04: 제자리 정지 → 떨어짐)")]
    [Min(0)] public int handFreezeFrames = 240;

    [Tooltip("켜면 손에 맞은 레이저가 손에서 끝난다 — 손 뒤의 플레이어는 안 맞는다")]
    public bool blockedByHand = true;

    [Header("그림")]
    [Tooltip("조준선 굵기: 시작 → 끝 (좁아짐이 곧 예고)")]
    public float aimStartWidth = 0.6f;
    public float aimEndWidth = 0.06f;

    public Color aimColor = new Color(1f, 0.15f, 0.1f, 0.55f);
    public Color beamColor = new Color(1f, 0.2f, 0.12f, 1f);
    public Color coreColor = new Color(1f, 0.95f, 0.85f, 1f);

    [Tooltip("발사 순간 화면 흔들림(월드)")]
    [Min(0f)] public float fireShake = 0.25f;

    [Header("이펙트 (2026-11-05 pixelfx 도트 플립북 — 비우면 없음)")]
    [Tooltip("조준·고정 동안 눈에서 반복 — 힘을 모으는 구슬")]
    public PixelFxData chargeFx;

    [Tooltip("발사 순간 눈에서 한 번 — 번쩍임·고리")]
    public PixelFxData fireFx;

    [Tooltip("발사 중 레이저 끝에 붙어 다니는 반복 이펙트 — 흰 심이 바닥·손에 닿아 끓는 자리")]
    public PixelFxData endFx;

    [Tooltip("발사 중 레이저 끝(바닥·막은 손)에서 contactFxInterval마다 — 불티")]
    public PixelFxData contactFx;

    [Tooltip("불티 간격(프레임)")]
    [Min(1)] public int contactFxInterval = 4;

    [Header("쓸기 모드 (2026-11-04 레이저 쓸기 — 0이면 일반 조준 레이저)")]
    [Tooltip("바닥을 가로로 쓸고 가는 길이(월드). 0보다 크면 쓸기 모드 — 조준은 플레이어 뒤쪽 바닥을 따라오고, 발사 때 쓸고 간다")]
    [Min(0f)] public float sweepLength;

    [Tooltip("쓸기 시작점이 플레이어보다 얼마나 뒤인지 (쓸기 길이에 대한 비율) — 쓸고 가며 플레이어 자리를 지나간다")]
    [Range(0f, 1f)] public float sweepStartBehind = 0.25f;

    [Tooltip("켜면 경기장 바닥 전체를 끝에서 끝까지 쓴다(플레이어 쪽 끝 → 반대쪽 끝) — sweepLength·sweepStartBehind는 무시 (2026-11-05 사용자)")]
    public bool sweepFullArena;

    [Tooltip("TODO: 기획 수치 없음. 쓸고 가는 시간(프레임) — 쓸기 모드에선 fireFrames 대신 이것")]
    [Min(1)] public int sweepFrames = 50;

    [Header("쓸고 난 뒤 바닥 폭발")]
    [Tooltip("쓸기가 끝나고 첫 폭발까지 (사용자: 약 1.2초 = 72f)")]
    [Min(0)] public int explosionDelayFrames = 72;

    [Tooltip("TODO: 기획 수치 없음. 폭발 간격(월드) — 쓸고 간 바닥에 이 간격마다 하나")]
    [Min(0.1f)] public float explosionSpacing = 1f;

    [Tooltip("TODO: 기획 수치 없음. 다음 폭발까지 프레임 — 쓸고 간 방향을 따라 천천히 펑 펑 펑")]
    [Min(1)] public int explosionIntervalFrames = 12;

    [Tooltip("폭발 판정 크기(보스 단위) — 가로 × 높이, 바닥에서 위로")]
    public Vector2 explosionSize = new Vector2(1.6f, 2f);

    [Tooltip("폭발이 판정을 갖는 프레임")]
    [Min(1)] public int explosionHitFrames = 6;

    [Tooltip("폭발 그림이 보이는 프레임")]
    [Min(1)] public int explosionVisualFrames = 20;

    [Tooltip("폭발마다 화면 흔들림(월드)")]
    [Min(0f)] public float explosionShake = 0.18f;

    [Tooltip("바닥 그을음 두께(보스 단위)")]
    [Min(0.01f)] public float scorchThickness = 0.25f;

    [Tooltip("폭발 그림 (2026-11-05 pixelfx 도트 플립북). 비우면 예전 임시 그림(돌아가는 네모)")]
    public PixelFxData explosionFx;

    public Color scorchColor = new Color(0.55f, 0.12f, 0.08f, 0.9f);
    public Color explosionColor = new Color(1f, 0.55f, 0.15f, 1f);

    public int TotalFrames => aimFrames + lockFrames + fireFrames + fadeFrames;
}
