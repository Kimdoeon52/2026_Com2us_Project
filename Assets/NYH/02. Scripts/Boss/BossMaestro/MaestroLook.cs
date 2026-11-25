using System;
using System.Collections.Generic;
using RealSteel.NanoBoss;
using UnityEngine;

/// <summary>패턴이 거는 몸짓 (MaestroLook.Play). 새 값은 맨 뒤에만 추가할 것</summary>
public enum MaestroGesture
{
    None,        // 평소 지휘 (4박)
    Swing,       // 크게 한 번 휘두르기 — 음표 공격
    RaiseBoth,   // 두 팔을 들어 올림 — 달빛 커튼, 거대 지휘봉 ①
    ConductFast, // 몸 전체로 격렬하게 지휘 — 거대 지휘봉 ④ (배경에서)
    Spread,      // 두 팔을 활짝 펼침 — 원형 음탄 (시트 '음표 폭풍')
    Strike,      // 지휘봉을 치켜들었다 바닥으로 내리침 — 크레셴도 파도마다
    SlashLeft,   // 검처럼 오른쪽→왼쪽으로 크게 벰 — 지휘봉 베기 (PlayTimed로 베는 순간을 맞춘다)
    SlashRight,  // 왼쪽→오른쪽으로 벰
    Custom,      // 모션 에셋 재생 (MaestroLook.PlayMotion — 모션 편집기로 만든 것)
}

/// <summary>
/// 마에스트로 외형 = 나노봇 퍼펫 (2026-11-24 개편). 참고 그림 픽셀 하나 = 봇 하나를 이 스크립트가 직접 놓는다(엔진 리그 안 씀).
///   · 뼈: 상체·머리·달, 지휘팔(위팔·아래팔·손·지휘봉·손가락), 반대팔(위팔·아래팔·손·손가락 3개). 관절 각도는 2차 스프링(넘쳤다 돌아옴) +
///     팔로스루(부모가 빨리 돌면 자식이 늦게) → 어깨 → 팔꿈치 → 손목 → 지휘봉 순서로 채찍처럼 휜다
///   · 천: 끈·소매·자락 픽셀은 천 뼈대 선에 붙어 있고, 뼈대는 물리(원래 모양 복원력 + 중력 + 바람 + 관성)로 매 프레임 계산 → 가만히 있어도 펄럭이고 팔을 따라 휘날림
///   · 몸짓: 예비 동작 → 본동작 → 여운 키 포즈 (Swing / RaiseBoth / ConductFast) + Flick, 손가락도 몸짓마다 쥐고·펴고·떨림. 평소엔 4박 지휘(변주) + 숨쉬기·8자 떠다니기·노이즈
///   · 아랫단 조각은 계속 흘러내리고, 반짝이는 떠다닌다
///   · 남는 봇(패턴이 빌려 쓰는 봇)은 하체 그림 실루엣(군집 덩어리 가이드)으로 하체에 겹쳐 숨긴다 — 하체는 돌리지 않는다
///   · 봇을 잃으면(무너짐·패턴) 덩어리에서 다시 가져와 아래부터 채운다 (흘러와서 자리 잡음)
/// 그림 전용(Time.deltaTime) — 판정과 무관. 판정 범위(HurtRect)는 컨트롤러가 읽는다.
/// </summary>
public class MaestroLook : MonoBehaviour
{
    [Header("연결 (빌더가 채움)")]
    public RSNanoSwarm swarm;
    public MaestroPuppetData data;
    [Tooltip("하체 그림 실루엣 — 남는 봇을 여기 겹쳐 숨긴다")]
    public RSNanoSpriteShape reserveShape;

    [Tooltip("모션 시트 키 포즈 (빌더가 채움 — MaestroPoseBaker). 비어 있으면 베기는 예전 퍼펫 몸짓으로")]
    public MaestroPoseSet poseSet;

    [Tooltip("몸짓 → 모션 연결표 (모션 편집기). 비우면 Resources/Maestro_MotionSet을 찾는다")]
    public MaestroMotionSet motionSet;

    [Tooltip("시안 단독 실행 — 보스 컨트롤러 없이 시작할 때 군집을 켠다")]
    public bool standalone;

    [Header("키 포즈 (11-25 B-2 — 봇이 시트 키 포즈로 흘러가 바뀐다)")]
    [Tooltip("봇마다 출발을 늦추는 비율 — 클수록 물결처럼 차례로 흘러간다")]
    [Range(0f, 0.8f)] public float poseStagger = 0.3f;
    [Tooltip("포즈로 흘러갈 때 궤적이 옆으로 휘는 정도 (그림 픽셀)")]
    [Min(0f)] public float poseArcPx = 5f;
    [Tooltip("포즈를 잡고 있는 동안 봇 떨림 (그림 픽셀)")]
    [Min(0f)] public float poseJitterPx = 0.35f;
    [Tooltip("포즈 중 숨쉬기 (위아래로 늘었다 줄었다)")]
    [Range(0f, 0.05f)] public float poseBreath = 0.012f;

    [Header("그림")]
    [Tooltip("판정 범위 (그림 픽셀, 그림 아래 끝 가운데 기준, 위가 +): x, y = 왼쪽 아래, z, w = 너비·높이. TODO 임시값 — 몸통·머리. " +
             "아래 끝을 바닥 근처까지 내려 플레이어 기본 공격이 닿게 (11-24 사용자)")]
    public Vector4 hurtRectPx = new Vector4(-12f, 16f, 30f, 96f);
    [Tooltip("그림을 바닥 아래로 내리는 정도 (그림 픽셀) — 아래로 흩어지는 꼬리 끝이 바닥에 묻히고 몸통이 바닥에 가까워진다")]
    public float sinkPx = 14f;
    [Tooltip("그림이 원래 바라보는 쪽 (-1 = 왼쪽 — 지휘봉 든 팔이 화면 왼쪽)")]
    public float artFacing = -1f;
    [Tooltip("켜면 플레이어 쪽으로 돌아선다 (좌우가 뒤집힘). 그림이 정면이라 기본 꺼짐")]
    public bool turnToPlayer;
    [Tooltip("지휘봉 끝 (그림 픽셀)")]
    public Vector2 batonTipPx = new Vector2(31f, 3f);

    [Header("크기 맞추기 (시작할 때)")]
    public bool autoFit = true;
    [Tooltip("보스 키 = 플레이어 키 × 이 값 (TODO 임시값)")]
    [Min(0.5f)] public float heightPerPlayer = 2.0f;
    [Tooltip("보스 키가 화면에 보이는 높이의 이 비율을 넘지 않게")]
    [Range(0.2f, 0.9f)] public float maxScreenRatio = 0.5f;
    [Min(0.5f)] public float fallbackHeight = 3f;

    [Header("평소")]
    [Tooltip("평소 지휘 빠르기·크기는 아래 '악장별 연출'이 정한다")]
    [HideInInspector] public float bpm = 54f;
    [Tooltip("평소 지휘 크기 (도)")]
    [HideInInspector] public float idleBeat = 16f;
    [Tooltip("떠다니기 크기 (키 대비) — x 좌우, y 위아래. 주기가 달라 8자로 돈다")]
    public Vector2 floatRatio = new Vector2(0.008f, 0.014f);
    public Vector2 floatPeriods = new Vector2(4.3f, 2.7f);
    [Tooltip("손가락이 오므라드는 최대 각도 (도)")]
    public float fingerCurlDeg = 65f;

    [Header("아랫단 조각 · 반짝이")]
    [Tooltip("조각이 흘러내리는 거리 (그림 픽셀)")]
    public float dripRangePx = 22f;
    [Tooltip("흘러내리는 빠르기 (그림 픽셀/초)")]
    public float dripSpeedPx = 7f;

    [Serializable]
    public class MovementLook
    {
        [Tooltip("평소 지휘 빠르기 (분당 박)")] [Min(10f)] public float bpm = 54f;
        [Tooltip("평소 지휘 크기 (도)")] [Min(0f)] public float idleBeat = 16f;
        [Tooltip("끈·소매 바람 배율")] [Min(0f)] public float windScale = 1f;
        [Tooltip("아랫단 조각 흘러내림 빠르기 배율")] [Min(0f)] public float dripScale = 1f;
        [Tooltip("떠다니기 크기 배율")] [Min(0f)] public float floatScale = 1f;
        [Min(0f)] public float sparkleGlow = 2.2f;
        [Min(0f)] public float moonGlow = 0.6f;
        [Tooltip("몸 분해 — 초당 떨어져 나갔다 돌아오는 입자 수 (시트 3악장 '신체 분해')")] [Min(0f)] public float shedPerSecond;
        [Tooltip("주변을 떠도는 음표 수 (시트 2·3악장)")] [Min(0)] public int ambientNotes;
        [Tooltip("주변에서 깜박이는 빛기둥 수 (시트 3악장)")] [Min(0)] public int beams;

        public void LerpTo(MovementLook b, float k)
        {
            bpm = Mathf.Lerp(bpm, b.bpm, k); idleBeat = Mathf.Lerp(idleBeat, b.idleBeat, k);
            windScale = Mathf.Lerp(windScale, b.windScale, k); dripScale = Mathf.Lerp(dripScale, b.dripScale, k);
            floatScale = Mathf.Lerp(floatScale, b.floatScale, k);
            sparkleGlow = Mathf.Lerp(sparkleGlow, b.sparkleGlow, k); moonGlow = Mathf.Lerp(moonGlow, b.moonGlow, k);
            shedPerSecond = Mathf.Lerp(shedPerSecond, b.shedPerSecond, k);
            ambientNotes = b.ambientNotes; beams = b.beams;
        }
    }

    [Header("악장별 연출 (시트 '페이즈별 변화' — 그림만, 패턴 수치는 안 바꿈)")]
    [Tooltip("1악장 고요 · 2악장 경쾌 · 3악장 격렬. 악장이 바뀌면 1.5초에 걸쳐 넘어간다. 전부 시안값(TODO)")]
    public MovementLook[] movementLooks =
    {
        new MovementLook { bpm = 54f, idleBeat = 16f },
        new MovementLook { bpm = 76f, idleBeat = 22f, windScale = 1.4f, dripScale = 1.4f, floatScale = 1.2f, sparkleGlow = 2.8f, moonGlow = 0.9f, ambientNotes = 6 },
        new MovementLook { bpm = 100f, idleBeat = 28f, windScale = 2.2f, dripScale = 2.5f, floatScale = 1.5f, sparkleGlow = 3.5f, moonGlow = 1.4f, shedPerSecond = 24f, ambientNotes = 10, beams = 6 },
    };

    [Header("이동")]
    [Min(0.01f)] public float moveSmooth = 0.3f;
    [Tooltip("움직이는 속도(m/s)당 상체 기울기 (도)")]
    public float leanPerSpeed = 8f;
    [Header("몸 전체 기울기·휨 (11-25 사용자: 통짜로 둥둥 떠다니기만 함)")]
    [Tooltip("몸짓 기울기 배율 (베기 준비 −14° → 벰 +26° 등에 곱함)")]
    [Range(0f, 2f)] public float bodyLeanScale = 1f;
    [Tooltip("휨 곡선 — 1 = 곧게 기움, 작을수록 아래부터 고르게, 클수록 위쪽만 크게 휨")]
    [Range(0.3f, 3f)] public float bendCurve = 1.3f;
    [Tooltip("몸 기울기가 따라가는 빠르기 (Hz)")]
    [Range(0.3f, 6f)] public float bodyLeanFrequency = 2.2f;
    [Tooltip("평소 몸 전체가 좌우로 흔들리는 크기 (도)")]
    [Range(0f, 10f)] public float idleSway = 2.5f;
    [Tooltip("상체 관절 몸짓 배율 (몸 전체 휨과 겹치므로 조절)")]
    [Range(0f, 2f)] public float chestScale = 1f;
    [Tooltip("눌림·늘어남 배율 (준비에 눌렸다 튀어나갈 때 늘어남)")]
    [Range(0f, 2f)] public float squashScale = 1f;
    [Header("이음새 (11-25 사용자: 허리 빈 공간 · 어깨 떨어짐)")]
    [Tooltip("허리 이음새 띠 반폭 (그림 픽셀) — 상체가 꺾일 때 이 띠 안의 픽셀은 상체·하체 움직임을 섞어 벌어지지 않게. 클수록 부드럽게 휨")]
    [Range(2f, 40f)] public float waistBlendPx = 16f;
    [Tooltip("어깨 주변 반경 (그림 픽셀) — 이 안의 상체 픽셀이 어깨 들썩임을 따라간다")]
    [Range(4f, 40f)] public float shoulderBlendPx = 20f;
    [Tooltip("어깨 들썩임 최대 거리 (키 대비) — 더 멀리 가면 어깨가 몸에서 떨어져 보인다")]
    [Range(0f, 0.2f)] public float maxShoulderShift = 0.07f;
    [Tooltip("몸 뒤(뒤판)에 숨기는 남는 봇 실루엣 크기 — 몸이 기울어도 앞에서 안 보이게 작게")]
    [Range(0.3f, 1f)] public float reserveScale = 0.7f;

    [Header("베기 몸 전체 움직임 (키 대비)")]
    [Tooltip("준비할 때 웅크려 내려가는 정도")]
    [Range(0f, 0.3f)] public float slashCrouch = 0.08f;
    [Tooltip("준비할 때 진행 반대쪽으로 당기는 정도")]
    [Range(0f, 0.3f)] public float slashRootBack = 0.05f;
    [Tooltip("벨 때 진행 방향으로 튀어나가는 정도 (발 위치 이동과 별개로 몸이 앞으로 쏠림)")]
    [Range(0f, 0.4f)] public float slashRootLunge = 0.1f;
    [Tooltip("이동할 때 상체가 기우는 최대 각도 (도) — 시트 '이동(좌/우)'·런지")]
    [Min(0f)] public float maxLean = 18f;
    [Tooltip("거대 지휘봉 중 절도 있는 지휘 — 초당 박 수 (박마다 자세로 짧게 들어가 멈춤)")]
    [Min(0.3f)] public float conductBeatsPerSecond = 1.3f;
    [Header("피격 반응")]
    [Tooltip("한 번 맞을 때 튀어 나가는 입자 수")]
    [Min(0)] public int hitBurst = 14;
    [Tooltip("움찔하는 크기 (도)")]
    [Min(0f)] public float hitFlinch = 10f;
    [Min(0.01f)] public float turnSmooth = 0.2f;

    [Header("봇 채우기 · 흩어지기")]
    [Tooltip("잃은 봇을 프레임마다 몇 개까지 다시 가져올지 (다시 쌓이는 빠르기)")]
    [Min(1)] public int refillPerFrame = 90;
    [Tooltip("봇 하나가 제자리로 흘러오는 시간 (초) — 최소·최대. 휘어진 궤적으로 천천히 출발해 천천히 자리 잡는다")]
    public Vector2 gatherSeconds = new Vector2(0.45f, 0.9f);
    [Tooltip("모여 올 때 궤적이 휘는 정도 (키 대비)")]
    [Min(0f)] public float gatherCurve = 0.12f;
    [Tooltip("흩어질 때 봇이 바깥으로 퍼지는 속도 (m/s)")]
    [Min(0f)] public float scatterSpeed = 0.8f;
    [Tooltip("끄면 잃은 봇을 다시 채우지 않는다 (격파 뒤)")]
    public bool refillEnabled = true;

    // ── 몸짓 채널 ──
    private enum Ch { Chest, Head, BShoulder, BElbow, BWrist, Baton, BGrip, OShoulder, OElbow, OWrist, OCurl, Count }

    private struct Key
    {
        public float t;
        public float[] v;
        public Key(float t, params float[] v) { this.t = t; this.v = v; }
    }

    //                         상체  머리 지휘어깨 팔꿈치 손목 지휘봉 쥠  반대어깨 팔꿈치 손목 손가락
    private static readonly Key[] SwingKeys =
    {
        new Key(0f,     0f,   0f,    0f,   0f,   0f,   0f, 0.5f,    0f,   0f,   0f,  0.3f),
        new Key(0.15f, -4f,  -3f,  -15f,  25f, -20f,  -5f, 0.8f,   15f,  10f,   0f,  0.6f), // 예비: 뒤로 당김
        new Key(0.35f, -6f,  -5f,  -45f,  35f, -35f, -10f, 1f,     30f,  20f,  -5f, -0.2f), // 크게 젖힘
        new Key(0.65f,  8f,   6f,   90f, -10f,  40f,  20f, 1f,     40f, -10f,  20f, -0.6f), // 휘두름
        new Key(0.85f,  4f,   3f,   75f,   5f,  15f,   8f, 0.9f,   25f,   0f,  10f, -0.3f), // 지나침
        new Key(1f,     2f,   1f,   55f,   5f,   5f,   0f, 0.7f,   15f,   0f,   0f,  0f),   // 여운
    };

    //                         상체  머리 지휘어깨 팔꿈치 손목 지휘봉 쥠  반대어깨 팔꿈치 손목 손가락
    private static readonly Key[] SpreadKeys =
    {
        new Key(0f,     0f,   0f,    0f,   0f,   0f,   0f, 0.5f,    0f,   0f,   0f,  0.3f),
        new Key(0.2f,   4f,   3f,  -10f,  20f,   5f,   0f, 0.9f,  -10f,  20f,   0f,  0.9f), // 예비: 팔을 안으로 모음, 주먹
        new Key(0.5f,  -4f,  -4f,   45f, -25f,  10f,   5f, 0.8f,   45f, -20f,  10f, -0.7f), // 활짝 펼침, 손가락 쫙
        new Key(1f,    -3f,  -3f,   42f, -22f,   8f,   3f, 0.8f,   42f, -18f,   8f, -0.6f), // 유지
    };

    private static readonly Key[] StrikeKeys =
    {
        new Key(0f,     0f,   0f,    0f,   0f,   0f,   0f, 0.5f,    0f,   0f,   0f,  0.3f),
        new Key(0.35f, -8f,  -6f,  -50f,  30f, -30f, -10f, 1f,     30f,  10f,   0f,  0.9f), // 치켜듦
        new Key(0.55f, 10f,   8f,  100f,  -5f,  40f,  15f, 1f,    -10f,   0f,   0f,  1f),   // 내리침
        new Key(1f,     4f,   2f,   80f,   5f,  15f,   5f, 0.8f,    0f,   0f,   0f,  0.5f), // 여운
    };

    // 베기 (사용자 시트 '3연속 횡베기') — 0~0.4 준비 자세(웅크리고 두 팔을 앞으로 모음) · 0.4~0.55 벰(진행 방향으로 팔을 쭉 뻗는 런지,
    // 반대팔은 뒤로 젖힘) · 0.55~1 전환 동작(똑바로 서며 팔을 거둠). 그림이 왼쪽(앞)을 보므로 오른쪽→왼쪽 = 앞으로(+)
    //                         상체  머리 지휘어깨 팔꿈치 손목 지휘봉 쥠  반대어깨 팔꿈치 손목 손가락
    private static readonly Key[] SlashLeftKeys =
    {
        new Key(0f,     0f,   0f,    0f,   0f,   0f,   0f, 0.5f,    0f,   0f,   0f,  0.3f),
        new Key(0.4f,   9f,   7f,   30f,  60f, -25f,   0f, 1f,    -15f,  45f,   0f,  0.9f), // 준비: 웅크림, 두 팔을 앞으로 모음
        new Key(0.55f, 14f,   8f,   88f, -12f,  12f,   0f, 1f,     70f, -20f,  10f, -0.4f), // 벰: 왼쪽으로 쭉 뻗고 반대팔 뒤로
        new Key(1f,     4f,   2f,   40f,  25f,   0f,   0f, 0.8f,   20f,  15f,   0f,  0.5f), // 전환: 서며 팔을 거둠
    };

    private static readonly Key[] SlashRightKeys =
    {
        new Key(0f,     0f,   0f,    0f,   0f,   0f,   0f, 0.5f,    0f,   0f,   0f,  0.3f),
        new Key(0.4f,  10f,   6f,   70f,  40f,  10f,   0f, 1f,    -10f,  40f,   0f,  0.9f), // 준비: 팔을 앞(왼쪽)으로 감아 모음
        new Key(0.55f,-12f,  -6f,  -85f,  -5f, -20f,   0f, 1f,     50f, -10f,   0f, -0.4f), // 벰: 오른쪽으로 쭉 뻗는 역베기
        new Key(1f,    -4f,  -2f,  -20f,  20f,   0f,   0f, 0.8f,   20f,  15f,   0f,  0.5f), // 전환
    };

    private static readonly Key[] RaiseKeys =
    {
        new Key(0f,     0f,   0f,    0f,   0f,   0f,   0f, 0.5f,    0f,   0f,   0f,  0.3f),
        new Key(0.12f,  6f,   4f,   20f,  30f,  10f,   0f, 0.9f,  -20f,  25f,   0f,  0.9f), // 예비: 웅크림, 주먹
        new Key(0.4f,  -5f,  -6f,  -35f, -15f, -25f, -10f, 1f,    110f, -10f, -20f, -0.7f), // 들어 올림, 손가락 쫙
        new Key(1f,    -4f,  -5f,  -32f, -12f, -20f,  -8f, 1f,    105f,  -8f, -15f, -0.6f), // 유지
    };

    // ── 상태 ──
    private bool started;
    private float cell = 0.03125f;
    private Vector2[] restOffset = Array.Empty<Vector2>();   // 뼈: 부모 관절 → 이 관절 (그림 공간 m)
    private float[] ang = Array.Empty<float>(), angVel = Array.Empty<float>(), angPrevTarget = Array.Empty<float>();
    private Vector2[] bonePos = Array.Empty<Vector2>();
    private float[] boneAng = Array.Empty<float>();
    private Vector2[] pixLocal = Array.Empty<Vector2>();     // 뼈 픽셀: 관절 기준, 하체: 발밑 기준 (그림 공간 m)
    private int[] clothSeg = Array.Empty<int>();
    private float[] clothT = Array.Empty<float>(), clothD = Array.Empty<float>();
    private float[] pixHash = Array.Empty<float>();
    private int[] slotBot = Array.Empty<int>();
    private bool[] slotArriving = Array.Empty<bool>();
    private int[] fillOrder = Array.Empty<int>();
    private readonly List<int> tmp = new List<int>(512);

    private class ClothSim
    {
        public Vector2[] restLocal;   // 붙은 뼈 기준 (그림 공간 m)
        public float[] restLen;       // 그림 공간 m
        public Vector2[] pos, prev;   // 월드 2D
        public bool init;
    }
    private ClothSim[] cloth = Array.Empty<ClothSim>();

    private int iChest, iHead, iMoon, iBS, iBE, iBW, iBaton, iBGrip, iOS, iOE, iOW, iF1, iF2, iF3;
    private MaestroGesture gesture = MaestroGesture.None;
    /// <summary>지금 몸짓 (디버그 표시)</summary>
    public MaestroGesture CurrentGesture => gesture;
    public int CurrentMovement => movement;
    private float gestureTime, gestureLength, gestureWeight, gestureWeightVel, flick;
    private Vector3 feetTarget, feet, feetVel;
    private Vector2 poseOffset, rootArt, lastRootWorld;
    private float growTarget = 1f, grow = 1f, growVel;
    private float backTarget, back, backVel;
    private float faceTarget = -1f, face = -1f, faceVel;
    private float leanAng, leanVel;
    private float batonForm = 1f, batonFormDur = 0.5f;
    private float[] batonU = Array.Empty<float>();      // 지휘봉 픽셀: 손잡이 0 → 끝 1
    private float hitPulse, nextHitTime;

    // 악장별 연출
    private MovementLook cur = new MovementLook();
    private int movement = 1;
    private float beatPhase, dripPhase, shedAcc, appliedSparkle = -1f, appliedMoon = -1f;

    /// <summary>지금 악장 (1~3) — 그림 연출만 바뀐다 (1.5초에 걸쳐)</summary>
    public void SetMovement(int m, bool instant = false)
    {
        movement = Mathf.Clamp(m, 1, Mathf.Max(1, movementLooks.Length));
        if (instant && movementLooks.Length > 0) cur.LerpTo(movementLooks[movement - 1], 1f);
    }

    // 악장 프로필 쪽으로 서서히 + 박·조각 위상 누적 + 몸 분해 + 발광 다시 칠하기
    private void TickMovement(float dt)
    {
        if (movementLooks.Length > 0) cur.LerpTo(movementLooks[Mathf.Clamp(movement - 1, 0, movementLooks.Length - 1)], Mathf.Clamp01(dt / 1.5f) * 3f);
        beatPhase += dt * cur.bpm / 60f;
        dripPhase += dt * dripSpeedPx * cur.dripScale;

        if (cur.shedPerSecond > 0.5f && refillEnabled && dissolving.Count == 0)
        {
            shedAcc += cur.shedPerSecond * dt;
            int n = Mathf.FloorToInt(shedAcc);
            if (n > 0) { shedAcc -= n; BurstParticles(n, soft: true); }
        }

        if (Mathf.Abs(appliedSparkle - cur.sparkleGlow) > 0.05f || Mathf.Abs(appliedMoon - cur.moonGlow) > 0.05f)
        {
            appliedSparkle = cur.sparkleGlow;
            appliedMoon = cur.moonGlow;
            for (int k = 0; k < slotBot.Length; k++)
            {
                if (slotBot[k] < 0) continue;
                byte kind = data.pixKind[k];
                if (kind == (byte)MaestroPuppetData.PixelKind.Sparkle) swarm.SetGlow(slotBot[k], cur.sparkleGlow);
                else if (kind == (byte)MaestroPuppetData.PixelKind.Glow) swarm.SetGlow(slotBot[k], cur.moonGlow);
            }
        }
    }
    private int pixelCount;

    public bool Ready => started;
    public float Cell => cell;
    public float Facing => Mathf.Sign(face == 0f ? faceTarget : face);
    public int ReserveCount { get; private set; }
    public float Height => (data != null ? data.artHeightPx : 150f) * cell * grow;
    /// <summary>시작할 때 잰 플레이어 키 (m, 렌더러 높이) — 패턴이 플레이어 크기에 맞출 때. 못 재면 보스 키 / heightPerPlayer</summary>
    public float PlayerHeight { get; private set; } = 1.5f;
    public float ArtHalfWidth => (data != null ? data.artWidthPx * 0.5f : 46f) * cell * grow;

    /// <summary>판정 범위 (월드) — 지금 위치·크기·방향 기준</summary>
    public Rect HurtRect
    {
        get
        {
            float c = cell * grow;
            Vector2 f = RootWorld();
            float x0 = hurtRectPx.x * c, x1 = (hurtRectPx.x + hurtRectPx.z) * c;
            if (FlipX < 0f) { float t = x0; x0 = -x1; x1 = -t; }
            return new Rect(f.x + x0, f.y + hurtRectPx.y * c, x1 - x0, hurtRectPx.w * c);
        }
    }

    public Vector3 BodyCenter { get { var r = HurtRect; return new Vector3(r.center.x, r.center.y, PlaneZ); } }

    /// <summary>지휘봉 끝 (월드) — 거대 지휘봉이 여기서 자라난다</summary>
    public Vector3 BatonTip
    {
        get
        {
            if (!started || iBaton < 0) return BodyCenter;
            Vector2 local = ArtPx(batonTipPx) - ArtPx(data.bones[iBaton].pivotPx);
            Vector2 art = bonePos[iBaton] + Rot(local, boneAng[iBaton]);
            Vector2 w = ArtToWorld(art);
            if (kpOn && kpAvgW > 0.01f && kpTarget >= 0) w = Vector2.Lerp(w, KeyPoseArtWorld(KeyPoseTip(kpTarget, kpMirror)), kpAvgW); // 키 포즈: 베는 쪽 손끝
            return new Vector3(w.x, w.y, PlaneZ);
        }
    }

    private float FlipX => Mathf.Abs(face * artFacing) < 0.02f ? 0.02f * Mathf.Sign(faceTarget * artFacing) : face * artFacing;
    private float PlaneZ => swarm != null ? (back > 0.5f ? swarm.BackZ : swarm.FrontZ) : transform.position.z;

    // ───────────────────────── 시작 ─────────────────────────

    /// <summary>크기를 맞추고 군집을 세우고 그림대로 봇을 놓는다. feetPos = 발밑(바닥)</summary>
    public void Init(Vector3 feetPos, float facing)
    {
        if (swarm == null || data == null || data.PixelCount == 0) { Debug.LogError("[Maestro] 외형 데이터가 없음 — 빌더(마에스트로 만들기·갱신)를 다시 실행"); return; }
        swarm.floorFromTransform = false;
        swarm.floorY = feetPos.y;
        feet = feetTarget = feetPos;
        face = faceTarget = turnToPlayer && Mathf.Abs(facing) > 0.01f ? Mathf.Sign(facing) : artFacing;

        float height = FitHeight(feetPos);
        swarm.botWorldSize = height / data.artHeightPx * 8f / Mathf.Max(1, swarm.grain);
        cell = swarm.Cell;

        BuildRig();
        pixelCount = data.PixelCount;
        ReserveCount = Mathf.Max(0, swarm.botCount - pixelCount);

        // 남는 봇 = 하체 실루엣으로 하체에 겹쳐 숨김
        swarm.Blob.shape = reserveShape;
        swarm.Blob.shapeScale = reserveScale; // 몸 뒤(뒤판)에 작게
        swarm.Blob.shapeArtColor = true;
        swarm.Blob.noise = 0f;
        swarm.Blob.wobble = 0f;
        swarm.Blob.flow = 0f;
        swarm.Blob.center = new Vector3(feetPos.x, feetPos.y - sinkPx * cell, swarm.BackZ);
        swarm.Build(swarm.Blob.center);
        swarm.ResetBotLooks();

        // 그림 자리에 바로 놓는다 (처음엔 흘러오지 않음)
        Pose(Time.time, 0f);
        SimulateCloth(Time.time, 0f); // 천 뼈대를 쉬는 모양으로 먼저 세운다
        swarm.Collect(RSNanoSwarm.G_Blob, swarm.Blob.center, pixelCount, tmp);
        for (int k = 0; k < pixelCount && k < tmp.Count; k++) Assign(k, tmp[k], snap: true);
        InitPoses();
        if (motionSet == null) motionSet = Resources.Load<MaestroMotionSet>(MaestroMotionSet.ResourcePath);
        started = true;
        Debug.Log($"[Maestro] 외형 시작 — 키 {height:0.00}m (칸 {cell * 100f:0.00}cm), 그림 봇 {pixelCount}, 남는 봇 {ReserveCount}");
    }

    private void BuildRig()
    {
        int nb = data.bones.Length;
        restOffset = new Vector2[nb];
        ang = new float[nb]; angVel = new float[nb]; angPrevTarget = new float[nb];
        bonePos = new Vector2[nb]; boneAng = new float[nb];
        for (int b = 0; b < nb; b++)
        {
            var bone = data.bones[b];
            Vector2 parentPivot = bone.parent >= 0 ? ArtPx(data.bones[bone.parent].pivotPx) : Vector2.zero;
            restOffset[b] = ArtPx(bone.pivotPx) - parentPivot;
        }
        iChest = data.BoneIndex("상체"); iHead = data.BoneIndex("머리"); iMoon = data.BoneIndex("달");
        iBS = data.BoneIndex("지휘_위팔"); iBE = data.BoneIndex("지휘_아래팔"); iBW = data.BoneIndex("지휘_손");
        iBaton = data.BoneIndex("지휘봉"); iBGrip = data.BoneIndex("지휘_손가락");
        iOS = data.BoneIndex("반대_위팔"); iOE = data.BoneIndex("반대_아래팔"); iOW = data.BoneIndex("반대_손");
        iF1 = data.BoneIndex("손가락1"); iF2 = data.BoneIndex("손가락2"); iF3 = data.BoneIndex("손가락3");
        moonFromHead = iMoon >= 0 && iHead >= 0 ? ArtPx(data.bones[iMoon].pivotPx) - ArtPx(data.bones[iHead].pivotPx) : Vector2.zero;
        aimReady = false;

        // 천: 뼈대 쉬는 모양(붙은 뼈 기준)
        cloth = new ClothSim[data.cloths.Length];
        for (int c = 0; c < cloth.Length; c++)
        {
            var def = data.cloths[c];
            int n = def.spinePx.Length;
            var sim = new ClothSim { restLocal = new Vector2[n], restLen = new float[Mathf.Max(0, n - 1)], pos = new Vector2[n], prev = new Vector2[n] };
            Vector2 pivot = def.parentBone >= 0 ? ArtPx(data.bones[def.parentBone].pivotPx) : Vector2.zero;
            for (int i = 0; i < n; i++) sim.restLocal[i] = ArtPx(def.spinePx[i]) - pivot;
            for (int i = 0; i < n - 1; i++) sim.restLen[i] = Vector2.Distance(ArtPx(def.spinePx[i]), ArtPx(def.spinePx[i + 1]));
            cloth[c] = sim;
        }

        // 픽셀: 뼈 기준 자리 / 천 뼈대 묶기
        int np = data.PixelCount;
        pixLocal = new Vector2[np];
        clothSeg = new int[np]; clothT = new float[np]; clothD = new float[np];
        pixHash = new float[np];
        slotBot = new int[np];
        slotArriving = new bool[np];
        arriveFrom = new Vector3[np];
        arriveT0 = new float[np];
        arriveDur = new float[np];
        for (int k = 0; k < np; k++)
        {
            slotBot[k] = -1;
            pixHash[k] = Mathf.Repeat(Mathf.Sin(k * 12.9898f) * 43758.5453f, 1f);
            Vector2 p = ArtPx(data.pixPos[k] + new Vector2(0.5f, 0.5f));
            int owner = data.pixOwner[k];
            if (owner >= 0) pixLocal[k] = p - ArtPx(data.bones[owner].pivotPx);
            else if (owner == -1) pixLocal[k] = p;
            else BindCloth(k, -2 - owner, p);
        }

        // 이음새 무게 (11-25 사용자: 상체가 꺾이면 허리에 빈 공간 / 어깨를 옮기면 어깨가 몸에서 떨어짐)
        //  · skinW: 상체(1) ↔ 하체(0) — 허리 관절 높이 ± waistBlendPx 띠 안에서 부드럽게 섞는다 (상체·하체 픽셀 둘 다)
        //  · shBW/shOW: 상체 픽셀이 어깨 관절에 가까울수록 어깨 들썩임을 따라감
        pixRest = new Vector2[np]; skinW = new float[np]; shBW = new float[np]; shOW = new float[np];
        chestPivArt = iChest >= 0 ? ArtPx(data.bones[iChest].pivotPx) : Vector2.zero;
        float waist = chestPivArt.y, band = Mathf.Max(1f, waistBlendPx) * cell, shR = Mathf.Max(1f, shoulderBlendPx) * cell;
        Vector2 shB = iBS >= 0 ? ArtPx(data.bones[iBS].pivotPx) : Vector2.one * 1e4f, shO = iOS >= 0 ? ArtPx(data.bones[iOS].pivotPx) : Vector2.one * 1e4f;
        for (int k = 0; k < np; k++)
        {
            Vector2 p = ArtPx(data.pixPos[k] + new Vector2(0.5f, 0.5f));
            pixRest[k] = p;
            int owner = data.pixOwner[k];
            if (iChest < 0 || (owner != iChest && owner != -1)) continue;
            bool drip = owner == -1 && data.pixKind[k] == (byte)MaestroPuppetData.PixelKind.Drip;
            float u = Mathf.Clamp01((p.y - (waist - band)) / (2f * band));
            skinW[k] = drip ? 0f : u * u * (3f - 2f * u);
            if (owner == iChest)
            {
                float a = Mathf.Clamp01(1f - Vector2.Distance(p, shB) / shR), b = Mathf.Clamp01(1f - Vector2.Distance(p, shO) / shR);
                shBW[k] = a * a * (3f - 2f * a);
                shOW[k] = b * b * (3f - 2f * b);
            }
        }

        // 지휘봉 픽셀: 손잡이(관절)에서 끝까지 비율 — 지휘봉이 손에서부터 생기게
        batonU = new float[np];
        if (iBaton >= 0)
        {
            Vector2 grip = ArtPx(data.bones[iBaton].pivotPx), tip = ArtPx(batonTipPx);
            Vector2 axis = tip - grip;
            float len2 = Mathf.Max(1e-8f, axis.sqrMagnitude);
            for (int k = 0; k < np; k++)
                if (data.pixOwner[k] == iBaton)
                    batonU[k] = Mathf.Clamp01(Vector2.Dot(ArtPx(data.pixPos[k] + new Vector2(0.5f, 0.5f)) - grip, axis) / len2);
        }

        // 다시 채우는 순서 = 아래 픽셀부터 (모래성 쌓기)
        fillOrder = new int[np];
        for (int k = 0; k < np; k++) fillOrder[k] = k;
        Array.Sort(fillOrder, (a, b) => data.pixPos[b].y.CompareTo(data.pixPos[a].y));
    }

    // 천 픽셀 → 가장 가까운 뼈대 마디 (마디 번호, 마디 위 비율, 마디 옆 거리)
    private void BindCloth(int k, int c, Vector2 p)
    {
        var def = data.cloths[c];
        float best = float.MaxValue;
        for (int i = 0; i < def.spinePx.Length - 1; i++)
        {
            Vector2 a = ArtPx(def.spinePx[i]), b = ArtPx(def.spinePx[i + 1]);
            Vector2 ab = b - a;
            float len2 = Mathf.Max(1e-8f, ab.sqrMagnitude);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            Vector2 q = a + ab * t;
            float d2 = (p - q).sqrMagnitude;
            if (d2 >= best) continue;
            best = d2;
            Vector2 dir = ab / Mathf.Sqrt(len2);
            Vector2 perp = new Vector2(-dir.y, dir.x);
            clothSeg[k] = i;
            clothT[k] = t;
            clothD[k] = Vector2.Dot(p - q, perp);
        }
        pixLocal[k] = new Vector2(c, 0f); // 천 번호를 기억 (x)
    }

    // 보스 키 = min(플레이어 키 × heightPerPlayer, 화면 높이 × maxScreenRatio)
    private float FitHeight(Vector3 feetPos)
    {
        if (!autoFit) return fallbackHeight;
        float h = fallbackHeight;
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            bool any = false;
            Bounds b = default;
            foreach (var r in player.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any && b.size.y > 0.3f && b.size.y < 6f) { PlayerHeight = b.size.y; h = b.size.y * heightPerPlayer; }
        }
        var cam = Camera.main;
        if (cam != null && PlaneY(cam, 0f, feetPos.z, out float y0) && PlaneY(cam, 1f, feetPos.z, out float y1))
            h = Mathf.Min(h, Mathf.Abs(y1 - y0) * maxScreenRatio);
        return Mathf.Max(0.8f, h);
    }

    private static bool PlaneY(Camera cam, float vy, float z, out float y)
    {
        y = 0f;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, vy, 0f));
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return false;
        float t = (z - ray.origin.z) / ray.direction.z;
        if (t <= 0f) return false;
        y = ray.origin.y + ray.direction.y * t;
        return true;
    }

    private void Start()
    {
        if (standalone && !started) Init(transform.position, artFacing);
    }

    private void OnDisable() => started = false;

    // ───────────────────────── 바깥에서 거는 것 ─────────────────────────

    public void SetFeet(Vector3 worldFeet) => feetTarget = worldFeet;

    /// <summary>발 위치를 따라가는 시간을 잠깐 바꾼다 (돌진처럼 빠른 이동 — 그림이 판정 위치보다 늦지 않게). 0 이하면 원래대로</summary>
    public void SetMoveSmooth(float seconds) => moveSmoothOverride = seconds;
    private float moveSmoothOverride = -1f;

    public void SetFacing(float dir) { if (turnToPlayer && Mathf.Abs(dir) > 0.01f) faceTarget = Mathf.Sign(dir); }

    /// <summary>자세: 발밑에서 offset(m) 만큼, 크기 grow 배, back이면 뒤판(배경). 부드럽게 넘어간다</summary>
    public void SetPose(Vector2 offset, float grow, bool back)
    {
        poseOffset = offset;
        growTarget = Mathf.Max(0.1f, grow);
        backTarget = back ? 1f : 0f;
    }

    /// <summary>몸짓을 seconds 동안. 끝나면 평소 지휘로 섞여 돌아간다</summary>
    /// <summary>
    /// 시간을 나눠 거는 몸짓 (베기 등) — 감아올림 windup초(키 0~0.4) · 벰 strike초(0.4~0.55) · 버팀 hold초(0.55~1).
    /// 패턴의 실제 베는 순간과 몸짓의 베는 순간을 맞출 때
    /// </summary>
    public void PlayTimed(MaestroGesture g, float windup, float strike, float hold)
    {
        Play(g, windup + strike + hold);
        timed = true;
        timedWindup = Mathf.Max(0.01f, windup);
        timedStrike = Mathf.Max(0.01f, strike);
        timedHold = Mathf.Max(0.01f, hold);
    }

    private bool timed;
    private float timedWindup, timedStrike, timedHold;

    // 나눠 건 몸짓의 진행률 → 키 시간 (0.4 / 0.55 경계)
    private float TimedU()
    {
        float s = gestureTime;
        if (s < timedWindup) return 0.4f * s / timedWindup;
        s -= timedWindup;
        if (s < timedStrike) return 0.4f + 0.15f * s / timedStrike;
        s -= timedStrike;
        return Mathf.Min(1f, 0.55f + 0.45f * s / timedHold);
    }

    /// <summary>모션 에셋을 재생 (모션 편집기로 만든 것). seconds ≤ 0이면 모션 길이대로(반복 모션이면 Stop까지 계속)</summary>
    public void PlayMotion(MaestroMotion m, float seconds = -1f)
    {
        if (m == null) return;
        Play(MaestroGesture.Custom, seconds > 0f ? seconds : m.loop ? 0f : m.length);
        customMotion = m;
    }

    public void Play(MaestroGesture g, float seconds)
    {
        timed = false;
        gesture = g;
        gestureTime = 0f;
        gestureLength = seconds;
        // 시트 '지휘봉 준비': 팔이 오르며 지휘봉이 입자로 모여 생긴다
        if ((g == MaestroGesture.Swing || g == MaestroGesture.RaiseBoth || g == MaestroGesture.Spread) && batonForm >= 1f) FormBaton(Mathf.Clamp(seconds * 0.35f, 0.25f, 0.6f));
    }

    public void Stop() => gesture = MaestroGesture.None;

    /// <summary>손목 스냅 + 지휘봉 튕김 + 반대손 펴기 (음 하나마다)</summary>
    public void Flick() => flick = 1f;

    /// <summary>지휘봉이 손에서부터 입자로 모여 생긴다 (seconds 동안). 아직 안 생긴 부분은 손 주위를 맴돈다</summary>
    public void FormBaton(float seconds)
    {
        batonForm = 0f;
        batonFormDur = Mathf.Max(0.05f, seconds);
    }

    /// <summary>
    /// 피격 반응 (시트 '피격/반응') — 몸이 뒤로 움찔, 손가락이 펴지고, 가장자리 입자 몇 개가 튀어 나갔다가 다시 흘러 들어온다.
    /// strength 0~1. 너무 잦으면 겹치지 않게 짧은 간격을 둔다
    /// </summary>
    public void Hit(float strength = 1f)
    {
        if (!started || Time.time < nextHitTime) return;
        nextHitTime = Time.time + 0.12f;
        hitPulse = Mathf.Max(hitPulse, Mathf.Clamp01(strength));
        BurstParticles(Mathf.RoundToInt(hitBurst * Mathf.Clamp01(strength)));
    }

    // 그림 봇 n개를 바깥으로 튕겨 내고 잠시 뒤 덩어리로 되돌린다 (빈자리는 남는 봇이 부드럽게 흘러와 채움)
    private void BurstParticles(int n, bool soft = false)
    {
        if (swarm == null || n <= 0 || slotBot.Length == 0) return;
        var bots = swarm.Bots;
        Vector2 center = BodyCenter;
        float now = Time.time;
        for (int tries = 0; tries < n * 4 && n > 0; tries++)
        {
            int k = UnityEngine.Random.Range(0, slotBot.Length);
            int i = slotBot[k];
            if (i < 0 || i >= bots.Length || bots[i].mode != BotMode.Free) continue;
            Vector2 away = (Vector2)bots[i].pos - center;
            away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
            slotBot[k] = -1;
            // soft = 몸 분해(3악장): 위로 천천히 떠오르듯 떨어져 나가 조금 더 오래 머문다
            var v = soft ? new Vector3(away.x * 0.4f, 0.6f + 0.6f * UnityEngine.Random.value, 0f) * UnityEngine.Random.Range(0.6f, 1.2f)
                         : new Vector3(away.x, away.y + 0.5f, 0f) * UnityEngine.Random.Range(1f, 2.5f);
            swarm.Drop(i, v);
            swarm.SetGlow(i, soft ? cur.sparkleGlow * 0.5f : 0f);
            reabsorb.Add(new Pending { bot = i, at = now + (soft ? UnityEngine.Random.Range(0.7f, 1.4f) : UnityEngine.Random.Range(0.4f, 0.9f)) });
            n--;
        }
    }

    /// <summary>
    /// 몸을 모래로 부드럽게 무너뜨린다 — duration초에 걸쳐 위에서부터(약간 무작위) 차례로 부스러져 바깥으로 살짝 퍼지며 내려앉는다.
    /// includeReserve면 하체에 숨긴 남는 봇도. reabsorbAfter ≥ 0이면 떨어진 뒤 그만큼(+ 0~reabsorbSpread초) 지나 덩어리로 되돌려 다시 쌓이게 한다
    /// (악장 전환처럼 다른 곳이 되돌리면 -1). 무너지는 동안은 다시 채우지 않는다
    /// </summary>
    public void DropAll(float duration, float reabsorbAfter = -1f, float reabsorbSpread = 0f, bool includeReserve = true)
    {
        if (swarm == null) return;
        var bots = swarm.Bots;
        float now = Time.time;
        float top = (data != null ? data.artHeightPx : 150f) * cell * grow;
        float baseY = FeetWorld().y;
        void Queue(int i)
        {
            if (i < 0 || i >= bots.Length) return;
            float h01 = Mathf.Clamp01((bots[i].pos.y - baseY) / Mathf.Max(0.01f, top));
            float at = now + duration * ((1f - h01) * 0.75f + UnityEngine.Random.value * 0.25f); // 위에서부터
            float returnAfter = reabsorbAfter < 0f ? -1f : reabsorbAfter + UnityEngine.Random.value * reabsorbSpread;
            dissolving.Add(new Pending { bot = i, at = at, extra = returnAfter });
        }
        for (int k = 0; k < slotBot.Length; k++)
        {
            int i = slotBot[k];
            slotBot[k] = -1; // 자리에서 놓아 준다 — 떨어질 때까지 그 자리에 머문다(엔진이 마지막 자리를 붙잡음)
            if (i >= 0 && i < bots.Length && bots[i].mode == BotMode.Free) Queue(i);
        }
        // 키 포즈에 빌려 쓰던 추가 봇도 같이 무너진다
        for (int e = 0; e < extraBot.Length; e++)
        {
            int i = extraBot[e];
            extraBot[e] = -1;
            if (i >= 0 && i < bots.Length && bots[i].mode == BotMode.Free) Queue(i);
        }
        ResetKeyPose();
        if (includeReserve)
            foreach (int i in swarm.AllIn(BotMode.Bound, tmp, RSNanoSwarm.G_Blob)) Queue(i);
    }

    private struct Pending { public int bot; public float at, extra; }
    private readonly List<Pending> dissolving = new List<Pending>(4096);
    private readonly List<Pending> reabsorb = new List<Pending>(4096);
    private Vector3[] arriveFrom = Array.Empty<Vector3>();
    private float[] arriveT0 = Array.Empty<float>(), arriveDur = Array.Empty<float>();

    // 무너지는 봇을 시간이 되면 떨어뜨리고, 되돌릴 시간이 된 모래는 덩어리로
    private void TickDissolve(float now)
    {
        if (dissolving.Count > 0)
        {
            var bots = swarm.Bots;
            Vector2 center = BodyCenter;
            for (int n = dissolving.Count - 1; n >= 0; n--)
            {
                var d = dissolving[n];
                if (now < d.at) continue;
                dissolving.RemoveAt(n);
                int i = d.bot;
                if (i < 0 || i >= bots.Length || bots[i].mode == BotMode.Debris || bots[i].mode == BotMode.Dead) continue;
                Vector2 away = (Vector2)bots[i].pos - center;
                away = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up;
                var v = new Vector3(away.x * scatterSpeed * UnityEngine.Random.Range(0.4f, 1f),
                                    UnityEngine.Random.Range(0.1f, 0.9f) * scatterSpeed, 0f);
                swarm.Drop(i, v);
                swarm.SetGlow(i, 0f);
                if (d.extra >= 0f) reabsorb.Add(new Pending { bot = i, at = now + d.extra });
            }
        }
        if (reabsorb.Count > 0)
        {
            var bots = swarm.Bots;
            for (int n = reabsorb.Count - 1; n >= 0; n--)
            {
                if (now < reabsorb[n].at) continue;
                int i = reabsorb[n].bot;
                reabsorb.RemoveAt(n);
                if (i >= 0 && i < bots.Length && bots[i].mode == BotMode.Debris) swarm.Bind(i, RSNanoSwarm.G_Blob);
            }
        }
    }

    // ───────────────────────── 매 프레임 ─────────────────────────

    private void LateUpdate()
    {
        if (!started || swarm == null) return;
        float dt = Mathf.Clamp(Time.deltaTime, 1e-4f, 0.05f);
        float t = Time.time;

        feet = Vector3.SmoothDamp(feet, feetTarget, ref feetVel, moveSmoothOverride > 0f ? moveSmoothOverride : moveSmooth, Mathf.Infinity, dt);
        grow = Mathf.SmoothDamp(grow, growTarget, ref growVel, 0.35f, Mathf.Infinity, dt);
        back = Mathf.SmoothDamp(back, backTarget, ref backVel, 0.3f, Mathf.Infinity, dt);
        face = Mathf.SmoothDamp(face, faceTarget, ref faceVel, turnSmooth, Mathf.Infinity, dt);

        if (editOn)
        {
            // 모션 편집기: 몸짓을 편집 중인 모션의 editTime 자세로 붙잡아 둔다
            gesture = MaestroGesture.Custom; customMotion = editMotion; timed = false;
            gestureTime = 0f; gestureLength = 0f;
        }
        else if (gesture != MaestroGesture.None)
        {
            gestureTime += dt;
            if (gestureLength > 0f && gestureTime >= gestureLength) gesture = MaestroGesture.None;
        }
        gestureWeight = Mathf.SmoothDamp(gestureWeight, gesture == MaestroGesture.None ? 0f : 1f, ref gestureWeightVel, 0.12f, Mathf.Infinity, dt);
        if (editOn && editSnap) { gestureWeight = 1f; gestureWeightVel = 0f; }
        flick = Mathf.MoveTowards(flick, 0f, dt * 3.5f);
        hitPulse = Mathf.MoveTowards(hitPulse, 0f, dt * 4f);
        batonForm = Mathf.MoveTowards(batonForm, 1f, dt / Mathf.Max(0.05f, batonFormDur));
        TickMovement(dt);

        Pose(t, dt);
        SimulateCloth(t, dt);
        TickDissolve(t);
        TickKeyPose(t);
        PlaceBots(t);
        PlaceExtras(t);

        // 남는 봇 = 몸 뒤(뒤판)에 작게 숨긴다 — 몸이 기울고 휘어도 앞에서 어긋나 보이지 않게 (11-25: 하체도 움직이도록)
        // 11-25 사용자 '뒤에 나노봇 모이는 곳이 이상함': 원근 카메라라 뒤판(+z)에 같은 x,y로 두면 작아지며 화면 가운데 쪽으로 밀려 몸 옆에 보였다
        // → 카메라에서 발밑(앞판)을 지나는 선 위 뒤판 자리에 두고, 멀어진 만큼 키워서 화면에선 몸과 정확히 겹치게(몸 뒤에 가려짐)
        // 11-25 사용자 '몸을 기울이면 아래에 덩어리가 남음': 덩어리는 돌릴 수 없으니, 하체 가운데가 기울며 옮겨 간 만큼 같이 옮기고
        // 많이 기울수록 작게 줄여 몸 뒤에 숨긴다
        float midY = WaistY * 0.5f;
        Vector2 lowerShift = BendRoot(new Vector2(0f, midY)) + LowerOffset(midY) - new Vector2(0f, midY);
        Vector2 rootW2 = ArtToWorld(rootArt + lowerShift);
        Vector3 rootFront = new Vector3(rootW2.x, rootW2.y, PlaneZ);
        float leanShrink = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(Mathf.Abs(rootLean) / 35f));
        float depthRatio = 1f;
        Vector3 reserveAt = new Vector3(rootFront.x, rootFront.y, swarm.BackZ);
        var cam = Camera.main;
        if (cam != null && !cam.orthographic)
        {
            Vector3 c = cam.transform.position;
            float d0 = rootFront.z - c.z, d1 = swarm.BackZ - c.z;
            if (d0 > 0.01f && d1 > 0.01f)
            {
                depthRatio = d1 / d0;
                reserveAt = c + (rootFront - c) * depthRatio;
            }
        }
        swarm.Blob.center = reserveAt;
        swarm.Blob.shapeScale = grow * reserveScale * depthRatio * leanShrink;
        // 하체에 숨긴 남는 봇: 몸이 움직이는 동안은 즉시 따라오게 (11-25 사용자: 런지 때 하체 덩어리가 뒤에 남음),
        // 멈춰 있을 땐 기본 느낌(돌아오는 모래가 부드럽게 흡수)
        bool moving = feetVel.sqrMagnitude > 0.04f || Mathf.Abs(growVel) > 0.05f || Mathf.Abs(backVel) > 0.05f || gestureRootW > 0.05f;
        if (moving) { swarm.Blob.stiffness = 40000f; swarm.Blob.damping = 2f * 200f * 0.95f; swarm.Blob.maxSpeed = 800f; }
        else { swarm.Blob.stiffness = 26f; swarm.Blob.damping = 6.5f; swarm.Blob.maxSpeed = 18f; }
        UpdateFx(t);
    }

    // ── 이펙트 (시트: 바닥 그림자 타원·초승달 번짐·지휘봉 끝 발광) ──
    private MaestroFx fx;
    private SpriteRenderer fxShadow, fxFloorRing, fxMoonGlow, fxTipGlow;

    private void UpdateFx(float t)
    {
        if (fx == null)
        {
            fx = MaestroFx.Get(this);
            fxShadow = fx.Acquire(MaestroFx.Disc);
            fxFloorRing = fx.Acquire(MaestroFx.RingTex);
            fxMoonGlow = fx.Acquire(MaestroFx.Disc);
            fxTipGlow = fx.Acquire(MaestroFx.Disc);
        }
        bool visible = slotBot.Length > 0 && dissolving.Count == 0 && refillEnabled;
        float alive = visible ? 1f : 0f;
        float h = Height;
        float frontness = 1f - back;

        // 바닥 그림자 — 발밑(바닥 높이)에 납작한 어두운 타원 + 옅은 보라 고리
        var ground = new Vector3(feet.x + poseOffset.x, feet.y, PlaneZ);
        fx.Place(fxShadow, ground, new Vector2(ArtHalfWidth * 1.6f, h * 0.07f), new Color(0.05f, 0.04f, 0.1f, 0.55f * alive * frontness));
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 1.3f);
        fx.Place(fxFloorRing, ground, new Vector2(ArtHalfWidth * 1.9f, h * 0.09f), new Color(MaestroFx.Lavender.r, MaestroFx.Lavender.g, MaestroFx.Lavender.b, (0.12f + 0.08f * pulse) * alive * frontness));

        // 초승달 뒤 은은한 번짐
        if (iMoon >= 0)
        {
            Vector2 m = ArtToWorld(bonePos[iMoon] + new Vector2(-2f, 6f) * cell);
            fx.Place(fxMoonGlow, new Vector3(m.x, m.y, PlaneZ), Vector2.one * h * 0.22f, new Color(MaestroFx.Lavender.r, MaestroFx.Lavender.g, MaestroFx.Lavender.b, (0.18f + 0.06f * pulse) * alive * (1f - kpAvgW))); // 키 포즈 중엔 달 자리가 달라서 끔
        }

        // 지휘봉 끝 — 몸짓 중·튕길 때 밝게
        float tipA = (0.15f + 0.45f * gestureWeight + 0.6f * Mathf.Sin(Mathf.Clamp01(flick) * Mathf.PI)) * batonForm * alive;
        fx.Place(fxTipGlow, BatonTip, Vector2.one * h * (0.06f + 0.05f * gestureWeight), new Color(MaestroFx.MoonWhite.r, MaestroFx.MoonWhite.g, MaestroFx.MoonWhite.b, tipA));

        UpdateAmbient(t, alive * frontness);
    }

    // ── 주변 음표(2·3악장) · 빛기둥(3악장) ──
    private readonly List<SpriteRenderer> ambientNotes = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> ambientBeams = new List<SpriteRenderer>();
    private float ambientFade;

    private void UpdateAmbient(float t, float visible)
    {
        int wantNotes = cur.ambientNotes, wantBeams = cur.beams;
        while (ambientNotes.Count < wantNotes) ambientNotes.Add(fx.Acquire(MaestroFx.Note));
        while (ambientBeams.Count < wantBeams) ambientBeams.Add(fx.Acquire(MaestroFx.Beam));
        ambientFade = Mathf.MoveTowards(ambientFade, visible, Time.deltaTime);
        float h = Height;
        Vector3 c = BodyCenter;

        // 음표: 몸 둘레 기운 타원을 천천히 돌며 위아래로 떠다님, 각자 반짝
        for (int n = 0; n < ambientNotes.Count; n++)
        {
            var r = ambientNotes[n];
            if (n >= wantNotes) { r.color = Color.clear; continue; }
            float ph = n / (float)Mathf.Max(1, wantNotes) * Mathf.PI * 2f + t * (0.35f + 0.05f * (n % 3)) * (cur.bpm / 54f);
            var p = c + new Vector3(Mathf.Cos(ph) * ArtHalfWidth * 1.5f, Mathf.Sin(ph) * h * 0.18f + Mathf.Sin(t * 1.7f + n) * h * 0.04f + h * 0.08f, 0f);
            float tw = 0.55f + 0.45f * Mathf.Sin(t * 3f + n * 1.9f);
            Color col = n % 2 == 0 ? MaestroFx.MoonWhite : MaestroFx.Lavender;
            col.a = tw * ambientFade * 0.9f;
            fx.Place(r, p, Vector2.one * h * 0.075f, col, 8f * Mathf.Sin(t * 1.3f + n));
        }

        // 빛기둥: 몸 양옆에서 솟았다 사라지는 세로 빛 (시트 3악장 광폭화)
        for (int n = 0; n < ambientBeams.Count; n++)
        {
            var r = ambientBeams[n];
            if (n >= wantBeams) { r.color = Color.clear; continue; }
            float cycle = 1.6f + 0.4f * (n % 3);
            float u = Mathf.Repeat(t / cycle + n * 0.37f, 1f);
            float side = (n % 2 == 0 ? -1f : 1f) * (0.5f + 0.9f * Mathf.Repeat(n * 0.618f, 1f));
            var p = new Vector3(c.x + side * ArtHalfWidth * 1.3f, feet.y + h * 0.6f, c.z);
            Color col = MaestroFx.Violet;
            col.a = Mathf.Sin(u * Mathf.PI) * 0.45f * ambientFade;
            fx.Place(r, p, new Vector2(h * 0.08f, h * (1.1f + 0.3f * u)), col);
        }
    }

    // 뼈 각도 (몸짓 목표 → 2차 스프링) → 그림 공간 관절 자리·각도
    private void Pose(float t, float dt)
    {
        int nb = data.bones.Length;
        float h = data.artHeightPx * cell;
        EvaluateMotion();
        TickExtraWeight(dt);
        bool snap = dt <= 0f || (editOn && editSnap);
        float still = editOn && editStill ? 0f : 1f; // 편집기 '흔들림 끄기' — 떠다니기·평소 흔들림 없이 자세만

        // 몸 전체 떠다니기 — 주기가 다른 두 흔들림(8자) + 느린 노이즈 (쿄다이 떠다니기와 같은 원리)
        float nx = Mathf.PerlinNoise(t * 0.3f, 0.17f) - 0.5f, ny = Mathf.PerlinNoise(0.63f, t * 0.3f) - 0.5f;
        rootArt = still * new Vector2(floatRatio.x * cur.floatScale * h * (Mathf.Sin(t * 2f * Mathf.PI / floatPeriods.x) + nx),
                                      floatRatio.y * cur.floatScale * h * (Mathf.Sin(t * 2f * Mathf.PI / floatPeriods.y) + ny));
        rootArt += GestureRoot(h); // 몸 전체 움직임 (베기: 준비에 웅크림 → 벨 때 앞으로 튀어나감)

        Vector2 rootW = RootWorld();
        float speedX = dt > 0f ? (rootW.x - lastRootWorld.x) / dt : 0f;
        lastRootWorld = rootW;
        leanAng = Mathf.SmoothDamp(leanAng, Mathf.Clamp(-speedX * leanPerSpeed * Mathf.Sign(FlipX), -maxLean, maxLean), ref leanVel, 0.25f, Mathf.Infinity, dt);

        // 몸 전체 기울기·휨 (11-25 사용자: 몸이 통짜로 둥둥 떠다니기만 함) — 이동 기울기 + 몸짓 기울기 + 평소 흔들림을 스프링으로.
        // 발밑을 축으로, 위로 갈수록 더 기울어 척추처럼 휜다(BendRoot). + = 앞(그림 왼쪽)으로
        float sway = idleSway * Mathf.Sin(t * 0.8f) + idleSway * 0.6f * (Mathf.PerlinNoise(t * 0.35f, 3.3f) - 0.5f) * 2f;
        float leanTarget = leanAng * still + GestureLean() + sway * still * (1f - gestureWeight * 0.7f);
        if (snap) { rootLean = leanTarget; rootLeanVel = 0f; rootLeanPrev = leanTarget; }
        else SecondOrder(ref rootLean, ref rootLeanVel, ref rootLeanPrev, leanTarget, dt, bodyLeanFrequency, Mathf.Lerp(0.5f, 0.85f, CrispWeight), 0.3f);
        if (snap && dt > 0f) { stretch = GestureStretch(); stretchVel = 0f; }
        else stretch = Mathf.SmoothDamp(stretch, GestureStretch(), ref stretchVel, 0.08f, Mathf.Infinity, dt);

        var target = new float[nb];
        Targets(t, target);

        for (int b = 0; b < nb; b++)
        {
            var bone = data.bones[b];
            float x = target[b];
            // 팔로스루: 부모가 빨리 돌면 자식이 반대로 늦게
            if (bone.parent >= 0 && bone.followThrough > 0f) x -= angVel[bone.parent] * bone.followThrough;
            // 지휘봉은 단단한 막대 — 손에 그대로 붙인다 (스프링·팔로스루 없음. 11-24 사용자: 고무처럼 휜다)
            if (b == iBaton) { ang[b] = 0f; angVel[b] = 0f; angPrevTarget[b] = 0f; }
            else if (snap) { ang[b] = x; angVel[b] = 0f; angPrevTarget[b] = x; }
            else
            {
                // 절도 있는 몸짓 중엔 단단하게: 감쇠 ≥ 0.9(넘치지 않음), 1.5배 빠르게, 예비 반응 없음 — 딱 들어가 딱 멈춘다
                float crisp = CrispWeight;
                float f = bone.frequency * Mathf.Lerp(1f, 1.5f, crisp);
                float z = Mathf.Lerp(bone.damping, Mathf.Max(bone.damping, 0.9f), crisp);
                float r = Mathf.Lerp(bone.response, 0f, crisp);
                SecondOrder(ref ang[b], ref angVel[b], ref angPrevTarget[b], x, dt, f, z, r);
            }

            if (b == iMoon && iHead >= 0)
            {
                // 머리 장식(달)은 머리를 따라 돈다 (11-25 사용자: 목을 움직이면 뒤 장식도 같이 움직여야)
                bonePos[b] = bonePos[iHead] + Rot(moonFromHead, boneAng[iHead]);
                boneAng[b] = boneAng[iHead] + ang[b] * Mathf.Deg2Rad;
            }
            else if (bone.parent >= 0)
            {
                float parentAng = boneAng[bone.parent];
                Vector2 off = restOffset[b];
                // 어깨 들썩임 (상체 기준 — 몸이 기울면 같이 기움)
                if (b == iBS) off += ShoulderArt(mExtra.bShoulder);
                else if (b == iOS) off += ShoulderArt(mExtra.oShoulder);
                bonePos[b] = bonePos[bone.parent] + Rot(off, parentAng);
                boneAng[b] = parentAng + ang[b] * Mathf.Deg2Rad;
            }
            else
            {
                // 발밑 원점에 붙은 뼈(상체): 몸 휨을 따라 자리·각도가 같이 기운다 + 허리 이동
                bonePos[b] = rootArt + BendRoot(restOffset[b]) + HipArt;
                boneAng[b] = BendAngle(restOffset[b].y) + ang[b] * Mathf.Deg2Rad;
            }
        }
    }

    // 2차 동역학 (진동수 f, 감쇠 z, 반응 r) — 넘쳤다 돌아오고, r > 1이면 예비 동작
    private static void SecondOrder(ref float y, ref float yd, ref float xp, float x, float dt, float f, float z, float r)
    {
        float k1 = z / (Mathf.PI * f);
        float k2 = 1f / ((2f * Mathf.PI * f) * (2f * Mathf.PI * f));
        float k3 = r * z / (2f * Mathf.PI * f);
        float xd = (x - xp) / dt;
        xp = x;
        float k2s = Mathf.Max(k2, Mathf.Max(dt * dt / 2f + dt * k1 / 2f, dt * k1));
        y += dt * yd;
        yd += dt * (x + k3 * xd - y - k1 * yd) / k2s;
    }

    // 뼈마다 목표 각도 (도, 그림 기준 + = 반시계)
    private void Targets(float t, float[] target)
    {
        var idle = new float[(int)Ch.Count];
        IdleChannels(t, idle);
        var g = new float[(int)Ch.Count];
        GestureChannels(t, g, idle);
        var c = new float[(int)Ch.Count];
        for (int i = 0; i < c.Length; i++) c[i] = Mathf.Lerp(idle[i], g[i], gestureWeight);

        // Flick: 손목 스냅 + 반대손 펴기 — 짧고 단호하게 들어갔다(처음 25%) 천천히 풀림. 상체는 흔들지 않는다 (11-25 절도)
        float fl = Mathf.Clamp01(flick);
        float fk = fl > 0.75f ? Mathf.SmoothStep(0f, 1f, (1f - fl) / 0.25f) : Mathf.SmoothStep(0f, 1f, fl / 0.75f);
        c[(int)Ch.BWrist] += 30f * fk;
        c[(int)Ch.OCurl] -= 0.4f * fk;

        // 피격: 상체·머리가 뒤로 젖혀지고(그림 기준 앞 = +), 팔이 움찔, 손가락이 펴진다 — 스프링이 넘쳤다 돌아오며 복귀
        float hp = hitPulse;
        c[(int)Ch.Chest] -= hitFlinch * hp;
        // 시트 '피격': 몸이 뒤로 크게 젖혀지고 지휘팔이 위로 튄다
        c[(int)Ch.Chest] -= hitFlinch * 0.8f * hp;
        c[(int)Ch.Head] -= hitFlinch * 1.6f * hp;
        c[(int)Ch.BShoulder] -= hitFlinch * 3f * hp;
        c[(int)Ch.BElbow] += hitFlinch * 0.8f * hp;
        c[(int)Ch.OShoulder] += hitFlinch * 2.5f * hp;
        c[(int)Ch.OCurl] -= 0.6f * hp;

        // 지휘봉이 생기는 동안: 손이 살짝 위로 들리고 손가락이 그 주위를 감싸듯
        float forming = 1f - batonForm;
        c[(int)Ch.BWrist] -= 15f * forming;
        c[(int)Ch.BGrip] += 0.4f * forming;

        Set(target, iChest, c[(int)Ch.Chest] * chestScale); // 이동 기울기는 이제 몸 전체(BendRoot)가 맡는다
        Set(target, iHead, c[(int)Ch.Head]);
        // 장식은 이제 머리를 따라 돌므로(Pose) 반대로 돌리던 −머리×0.5는 뺐다. 살짝 흔들림 + 모션의 장식 각도
        Set(target, iMoon, 4f * Mathf.Sin(t * 0.5f) * (editOn && editStill ? 0f : 1f) + mExtra.moon * extraW);
        Set(target, iBS, c[(int)Ch.BShoulder]);
        Set(target, iBE, c[(int)Ch.BElbow]);
        Set(target, iBW, c[(int)Ch.BWrist]);
        Set(target, iBaton, c[(int)Ch.Baton]);
        Set(target, iBGrip, -30f * (c[(int)Ch.BGrip] - 0.5f)); // 그림 = 반쯤 쥔 손 (0.5)
        Set(target, iOS, c[(int)Ch.OShoulder]);
        Set(target, iOE, c[(int)Ch.OElbow]);
        Set(target, iOW, c[(int)Ch.OWrist]);
        // 손가락: 손가락마다 조금씩 늦게·다르게 (오므림 = 시계방향)
        float curl = c[(int)Ch.OCurl] - 0.25f; // 그림 = 살짝 편 손 (0.25)
        float wiggle = 6f * (1f - CrispWeight) * (editOn && editStill ? 0f : 1f); // 절도 있는 몸짓 중엔 손가락도 고정
        Set(target, iF1, -fingerCurlDeg * curl + wiggle * Mathf.Sin(t * 1.3f));
        Set(target, iF2, -fingerCurlDeg * curl * 1.1f + wiggle * Mathf.Sin(t * 1.3f - 0.6f));
        Set(target, iF3, -fingerCurlDeg * curl * 1.2f + wiggle * Mathf.Sin(t * 1.3f - 1.2f));
    }

    private static void Set(float[] a, int i, float v) { if (i >= 0) a[i] = v; }

    // 평소: 4박 지휘(마디마다 변주) + 숨쉬기 + 노이즈
    private void IdleChannels(float t, float[] c)
    {
        float beat = beatPhase; // 박 위상은 누적 — 악장이 바뀌어 빠르기가 변해도 튀지 않게
        int bar = Mathf.FloorToInt(beat / 4f);
        int b = Mathf.FloorToInt(beat) & 3;
        float f = beat - Mathf.Floor(beat);
        // 마디마다 크기·강세가 조금씩 다르다 (같은 반복으로 안 보이게)
        float barAmp = 0.8f + 0.4f * Mathf.PerlinNoise(bar * 0.71f, 0.3f);
        bool flourish = Mathf.PerlinNoise(bar * 0.37f, 5.1f) > 0.68f; // 가끔 크게 한 번
        float amp = cur.idleBeat * barAmp * (flourish && b == 0 ? 1.8f : 1f);
        float beatCurve = CatmullRom(BeatAngles[(b + 3) & 3], BeatAngles[b], BeatAngles[(b + 1) & 3], BeatAngles[(b + 2) & 3], Ease(f));
        float n1 = Mathf.PerlinNoise(t * 0.4f, 1.7f) - 0.5f, n2 = Mathf.PerlinNoise(t * 0.5f, 7.3f) - 0.5f;

        c[(int)Ch.Chest] = 1.5f * Mathf.Sin(t * 1.15f) + 3f * n1;
        c[(int)Ch.Head] = (b == 0 ? 4f * Mathf.Sin(f * Mathf.PI) : 0f) + 5f * n2;
        c[(int)Ch.BShoulder] = amp * beatCurve;
        c[(int)Ch.BElbow] = amp * 0.6f * beatCurve;
        c[(int)Ch.BWrist] = 10f * Mathf.Sin((beat + 0.25f) * Mathf.PI * 2f) * barAmp;
        c[(int)Ch.Baton] = 4f * Mathf.Sin((beat + 0.4f) * Mathf.PI * 2f);
        c[(int)Ch.BGrip] = 0.5f + 0.1f * Mathf.Sin(t * 0.8f);
        c[(int)Ch.OShoulder] = 7f * Mathf.Sin(t * 0.55f) + 4f * n2;
        c[(int)Ch.OElbow] = 10f * Mathf.Sin(t * 0.55f + 0.8f);
        c[(int)Ch.OWrist] = 12f * Mathf.Sin(t * 0.7f + 1.6f);
        c[(int)Ch.OCurl] = 0.25f + 0.2f * Mathf.Sin(t * 0.9f);
    }

    // 절도 있는 지휘 자세 — down = 내리친 자세(아니면 치켜든 자세), accent = 강박(더 크게)
    private static void ConductPose(bool down, bool accent, float[] c)
    {
        float k = accent ? 1.25f : 1f;
        c[(int)Ch.Chest] = down ? 3f * k : -2f;
        c[(int)Ch.Head] = down ? 2f * k : -2f;
        c[(int)Ch.BShoulder] = down ? 70f * k : -35f;
        c[(int)Ch.BElbow] = down ? -10f : 25f;
        c[(int)Ch.BWrist] = down ? 25f * k : -20f;
        c[(int)Ch.Baton] = 0f;
        c[(int)Ch.BGrip] = 1f;
        c[(int)Ch.OShoulder] = down ? 25f : 40f;
        c[(int)Ch.OElbow] = down ? 10f : -10f;
        c[(int)Ch.OWrist] = down ? 10f : 0f;
        c[(int)Ch.OCurl] = down ? 1f : 0.85f;
    }

    // ── 몸 전체 기울기·휨·눌림 (11-25) ──
    private float rootLean, rootLeanVel, rootLeanPrev;  // 도
    private float stretch = 1f, stretchVel;

    // 발밑을 축으로 v를 돌린다 — 위로 갈수록 더 (척추처럼 휜다)
    private Vector2 BendRoot(Vector2 v) => Rot(v, BendAngle(v.y));
    private float BendAngle(float y)
    {
        float h = Mathf.Max(0.01f, (data != null ? data.artHeightPx : 150f) * cell);
        return rootLean * Mathf.Deg2Rad * Mathf.Pow(Mathf.Clamp01(y / h), bendCurve);
    }

    // 키 (u, 값) 사이를 부드럽게
    private static float Piece(float u, Vector2[] k)
    {
        if (u <= k[0].x) return k[0].y;
        for (int i = 0; i < k.Length - 1; i++)
            if (u <= k[i + 1].x) return Mathf.Lerp(k[i].y, k[i + 1].y, Mathf.SmoothStep(0f, 1f, (u - k[i].x) / Mathf.Max(1e-4f, k[i + 1].x - k[i].x)));
        return k[k.Length - 1].y;
    }

    private static readonly Vector2[] LeanSlashLeft = { new Vector2(0f, 0f), new Vector2(0.4f, -14f), new Vector2(0.55f, 26f), new Vector2(1f, 6f) };
    private static readonly Vector2[] LeanSlashRight = { new Vector2(0f, 0f), new Vector2(0.4f, 14f), new Vector2(0.55f, -24f), new Vector2(1f, -5f) };
    private static readonly Vector2[] LeanStrike = { new Vector2(0f, 0f), new Vector2(0.35f, -10f), new Vector2(0.55f, 18f), new Vector2(1f, 5f) };
    private static readonly Vector2[] LeanSwing = { new Vector2(0f, 0f), new Vector2(0.15f, -6f), new Vector2(0.35f, -12f), new Vector2(0.65f, 16f), new Vector2(1f, 5f) };
    private static readonly Vector2[] LeanRaise = { new Vector2(0f, 0f), new Vector2(0.12f, 6f), new Vector2(0.4f, -7f), new Vector2(1f, -6f) };
    private static readonly Vector2[] LeanSpread = { new Vector2(0f, 0f), new Vector2(0.2f, 5f), new Vector2(0.5f, -5f), new Vector2(1f, -4f) };
    private static readonly Vector2[] SquashSlash = { new Vector2(0f, 1f), new Vector2(0.4f, 0.92f), new Vector2(0.5f, 1.06f), new Vector2(0.7f, 1f), new Vector2(1f, 1f) };
    private static readonly Vector2[] SquashStrike = { new Vector2(0f, 1f), new Vector2(0.35f, 1.04f), new Vector2(0.55f, 0.93f), new Vector2(0.8f, 1f), new Vector2(1f, 1f) };

    private float GestureU => timed ? TimedU() : (gestureLength > 0f ? Mathf.Clamp01(gestureTime / gestureLength) : 0f);

    // 몸짓마다 몸 전체 기울기 (도) — 준비에 뒤로 젖히고 본동작에 앞으로 크게 쏠린다
    private float GestureLean()
    {
        float u = GestureU, v;
        if (motionOn) return mLean * gestureWeight; // 모션 에셋: 편집기에서 본 그대로 (배율 없음)
        switch (gesture)
        {
            case MaestroGesture.SlashLeft: v = Piece(u, LeanSlashLeft); break;
            case MaestroGesture.SlashRight: v = Piece(u, LeanSlashRight); break;
            case MaestroGesture.Strike: v = Piece(u, LeanStrike); break;
            case MaestroGesture.Swing: v = Piece(u, LeanSwing); break;
            case MaestroGesture.RaiseBoth: v = Piece(Mathf.Clamp01(gestureTime / Mathf.Min(1.2f, Mathf.Max(0.3f, gestureLength))), LeanRaise); break;
            case MaestroGesture.Spread: v = Piece(Mathf.Clamp01(gestureTime / Mathf.Min(1f, Mathf.Max(0.3f, gestureLength))), LeanSpread); break;
            case MaestroGesture.ConductFast:
            {
                float beats = gestureTime * conductBeatsPerSecond;
                float f = beats - Mathf.Floor(beats);
                bool down = (Mathf.FloorToInt(beats) & 1) != 0;
                float e = f < 0.22f ? Mathf.SmoothStep(0f, 1f, f / 0.22f) : 1f;
                v = Mathf.Lerp(down ? -4f : 8f, down ? 8f : -4f, e);
                break;
            }
            default: v = 0f; break;
        }
        return v * bodyLeanScale * gestureWeight;
    }

    // 눌림·늘어남 (1 = 그대로) — 준비에 눌렸다가 튀어나갈 때 늘어난다
    private float GestureStretch()
    {
        float u = GestureU, v = 1f;
        if (motionOn) return 1f + (mStretch - 1f) * gestureWeight;
        if (gesture == MaestroGesture.SlashLeft || gesture == MaestroGesture.SlashRight) v = Piece(u, SquashSlash);
        else if (gesture == MaestroGesture.Strike) v = Piece(u, SquashStrike);
        return 1f + (v - 1f) * squashScale * gestureWeight;
    }

    // 몸 전체 움직임 (그림 공간 m, 키 h 기준) — 베기: 준비(0~0.4) 웅크림·뒤로 당김 → 벰(0.4~0.55) 진행 방향으로 튀어나감 → 전환(0.55~1) 제자리.
    // 팔 각도만으론 전조가 작아 보여서 (11-25 사용자) 몸 전체를 쓴다. 그림이 왼쪽(앞)을 보므로 앞 = −x
    private float gestureRootW;
    private Vector2 GestureRoot(float h)
    {
        if (motionOn)
        {
            // 모션 에셋: root = 키 대비, x + = 앞 (그림이 왼쪽을 보므로 그림 공간 −x)
            gestureRootW = mRoot.sqrMagnitude > 1e-6f ? gestureWeight : 0f;
            return new Vector2(-mRoot.x * h, mRoot.y * h) * gestureWeight;
        }
        bool slash = gesture == MaestroGesture.SlashLeft || gesture == MaestroGesture.SlashRight;
        gestureRootW = slash ? gestureWeight : 0f;
        if (!slash) return Vector2.zero;
        float fwd = gesture == MaestroGesture.SlashLeft ? -1f : 1f; // 진행 방향 (그림 공간 x)
        float u = timed ? TimedU() : Mathf.Clamp01(gestureTime / Mathf.Max(0.01f, gestureLength));
        Vector2 ready = new Vector2(-fwd * slashRootBack * h, -slashCrouch * h);
        Vector2 lunge = new Vector2(fwd * slashRootLunge * h, -slashCrouch * 0.3f * h);
        Vector2 v;
        if (u < 0.4f) v = Vector2.Lerp(Vector2.zero, ready, Mathf.SmoothStep(0f, 1f, u / 0.4f));
        else if (u < 0.55f) v = Vector2.Lerp(ready, lunge, Mathf.SmoothStep(0f, 1f, (u - 0.4f) / 0.15f));
        else v = Vector2.Lerp(lunge, Vector2.zero, Mathf.SmoothStep(0f, 1f, (u - 0.55f) / 0.45f));
        return v * gestureWeight;
    }

    // 절도 있게 움직이는 몸짓인지 (관절 스프링을 단단하게 — 출렁임 없이 딱 멈춤)
    private float CrispWeight =>
        motionOn ? (activeMotion.crisp ? gestureWeight : 0f) :
        gesture == MaestroGesture.ConductFast || gesture == MaestroGesture.Spread || gesture == MaestroGesture.Strike ||
        gesture == MaestroGesture.SlashLeft || gesture == MaestroGesture.SlashRight ? gestureWeight : 0f;

    private void GestureChannels(float t, float[] c, float[] idle)
    {
        float u = gestureLength > 0f ? Mathf.Clamp01(gestureTime / gestureLength) : 0f;
        if (motionOn) { Array.Copy(mCh, c, Mathf.Min(c.Length, mCh.Length)); return; }
        switch (gesture)
        {
            case MaestroGesture.Swing:
                EvalKeys(SwingKeys, u, c);
                return;
            case MaestroGesture.Spread:
                // 펼친 자세를 흔들림 없이 버틴다 (11-25 사용자: 경박하지 않게 절도 있게) — 움직이는 건 옷자락뿐
                EvalKeys(SpreadKeys, Mathf.Clamp01(gestureTime / Mathf.Min(1f, Mathf.Max(0.3f, gestureLength))), c);
                return;
            case MaestroGesture.Strike:
                EvalKeys(StrikeKeys, u, c);
                return;
            case MaestroGesture.SlashLeft:
                EvalKeys(SlashLeftKeys, timed ? TimedU() : u, c);
                return;
            case MaestroGesture.SlashRight:
                EvalKeys(SlashRightKeys, timed ? TimedU() : u, c);
                return;
            case MaestroGesture.RaiseBoth:
                EvalKeys(RaiseKeys, Mathf.Clamp01(gestureTime / Mathf.Min(1.2f, Mathf.Max(0.3f, gestureLength))), c);
                // 들고 있는 동안 떨림 (힘이 들어간 손가락·팔)
                c[(int)Ch.OShoulder] += 2.5f * Mathf.Sin(t * 9f);
                c[(int)Ch.BShoulder] += 2f * Mathf.Sin(t * 8f + 1f);
                c[(int)Ch.OCurl] += 0.08f * Mathf.Sin(t * 13f);
                return;
            case MaestroGesture.ConductFast:
            {
                // 절도 있는 지휘 (11-25 사용자): 박마다 정해진 자세로 짧게(박의 22%) 들어가 멈춰 버틴다 — 올림 ↔ 내리침, 4박마다 강세.
                // 상체·머리는 거의 고정, 움직임은 팔에 모으고 옷자락이 그 뒤를 흐른다
                float beats = gestureTime * conductBeatsPerSecond;
                int bi = Mathf.FloorToInt(beats);
                float f = beats - bi;
                float e = f < 0.22f ? Mathf.SmoothStep(0f, 1f, f / 0.22f) : 1f;
                bool accentTo = ((bi + 1) & 3) == 0;
                ConductPose((bi & 1) == 0, false, c);          // 지난 박 자세
                var to = new float[c.Length];
                ConductPose((bi & 1) != 0, accentTo, to);       // 이번 박 자세
                for (int i = 0; i < c.Length; i++) c[i] = Mathf.Lerp(c[i], to[i], e);
                return;
            }
            default:
                Array.Copy(idle, c, c.Length);
                return;
        }
    }

    // 키 사이를 가속·감속 곡선으로
    private static void EvalKeys(Key[] keys, float u, float[] c)
    {
        int k = 0;
        while (k < keys.Length - 2 && u > keys[k + 1].t) k++;
        float a = keys[k].t, b = keys[k + 1].t;
        float s = b > a ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - a) / (b - a))) : 1f;
        for (int i = 0; i < c.Length && i < keys[k].v.Length; i++) c[i] = Mathf.Lerp(keys[k].v[i], keys[k + 1].v[i], s);
    }

    // 천: 붙은 뼈 기준 원래 모양으로 끌림(뿌리 강·끝 약) + 중력 + 바람 + 관성(베를레) + 길이 유지
    private void SimulateCloth(float t, float dt)
    {
        float s = grow;
        for (int c = 0; c < cloth.Length; c++)
        {
            var def = data.cloths[c];
            var sim = cloth[c];
            int n = sim.pos.Length;
            if (n < 2) continue;
            float pAng = def.parentBone >= 0 ? boneAng[def.parentBone] : 0f;
            Vector2 pPos = def.parentBone >= 0 ? bonePos[def.parentBone] : rootArt;
            bool onRoot = def.parentBone < 0; // 발밑에 붙은 천은 몸 휨을 따라

            if (!sim.init)
            {
                for (int i = 0; i < n; i++) sim.pos[i] = sim.prev[i] = ArtToWorld(onRoot ? rootArt + BendRoot(sim.restLocal[i]) + LowerOffset(sim.restLocal[i].y) : pPos + Rot(sim.restLocal[i], pAng));
                sim.init = true;
            }

            for (int i = 0; i < n; i++)
            {
                Vector2 rest = ArtToWorld(onRoot ? rootArt + BendRoot(sim.restLocal[i]) + LowerOffset(sim.restLocal[i].y) : pPos + Rot(sim.restLocal[i], pAng));
                if (i == 0) { sim.prev[0] = sim.pos[0]; sim.pos[0] = rest; continue; } // 뿌리 고정
                float u = i / (float)(n - 1);
                float k = Mathf.Lerp(def.stiffnessRoot, def.stiffnessTip, u);
                float wph = t * def.windSpeed + c * 3.1f + i * 0.35f;
                Vector2 wind = new Vector2(Mathf.PerlinNoise(wph, c * 1.7f) - 0.5f, Mathf.PerlinNoise(c * 2.3f, wph) - 0.5f) * (2f * def.wind * cur.windScale * u);
                Vector2 acc = (rest - sim.pos[i]) * k + new Vector2(0f, -def.gravity * u) + wind * s;
                Vector2 vel = (sim.pos[i] - sim.prev[i]) * (1f - def.drag);
                sim.prev[i] = sim.pos[i];
                sim.pos[i] += vel + acc * dt * dt;
            }
            // 길이 유지 (뿌리부터)
            for (int it = 0; it < 3; it++)
                for (int i = 0; i < n - 1; i++)
                {
                    Vector2 d = sim.pos[i + 1] - sim.pos[i];
                    float len = d.magnitude;
                    if (len < 1e-6f) continue;
                    float want = sim.restLen[i] * s;
                    Vector2 fix = d * ((len - want) / len);
                    if (i == 0) sim.pos[i + 1] -= fix;
                    else { sim.pos[i] += fix * 0.5f; sim.pos[i + 1] -= fix * 0.5f; }
                }
        }
    }

    // 픽셀 자리(월드) → 봇. 잃은 자리는 덩어리에서 다시 가져와 아래부터 채운다
    private void PlaceBots(float t)
    {
        var bots = swarm.Bots;
        float z = PlaneZ;
        int missing = 0;
        for (int k = 0; k < slotBot.Length; k++)
        {
            int i = slotBot[k];
            if (i >= 0 && (i >= bots.Length || bots[i].mode != BotMode.Free)) { slotBot[k] = -1; i = -1; }
            if (i < 0) { missing++; continue; }
            Vector2 w = PixelWorld(k, t);
            if (kpOn && kpW[k] > 0.001f)
            {
                // 키 포즈 쪽으로 (자리·색) — 퍼펫 자리와 포즈 자리를 봇마다 섞는다
                w = Vector2.Lerp(w, KeyPoseWorld(k, t), kpW[k]);
                swarm.SetTint(i, Color32.Lerp(data.pixColor[k], kpCol[k], kpW[k]));
                swarm.SetGlow(i, BaseGlow(k) * (1f - kpW[k]));
            }
            else if (kpRestore)
            {
                swarm.SetTint(i, data.pixColor[k]);
                swarm.SetGlow(i, BaseGlow(k));
            }
            var p = new Vector3(w.x, w.y, z);
            bots[i].hold = p;
            bots[i].wait = 9999f;
            if (slotArriving[k])
            {
                // 흘러오는 중 — 휘어진 궤적(옆으로 살짝 + 위로 볼록)을 천천히 출발해 천천히 도착 (엔진 스프링에 맡기면 순간이동처럼 빠르다)
                float u = Mathf.Clamp01((t - arriveT0[k]) / Mathf.Max(0.05f, arriveDur[k]));
                if (u >= 1f) slotArriving[k] = false;
                else
                {
                    float e = u * u * (3f - 2f * u);
                    Vector3 a = arriveFrom[k];
                    Vector3 mid = (a + p) * 0.5f;
                    Vector3 d = p - a;
                    float bend = gatherCurve * Height * (pixHash[k] - 0.5f) * 2f;
                    Vector3 ctrl = mid + new Vector3(-d.y, d.x, 0f).normalized * bend + Vector3.up * Mathf.Abs(bend) * 0.5f;
                    Vector3 q = Vector3.Lerp(Vector3.Lerp(a, ctrl, e), Vector3.Lerp(ctrl, p, e), e);
                    bots[i].pos = q;
                    bots[i].hold = q;
                    bots[i].vel = Vector3.zero;
                    continue;
                }
            }
            bots[i].pos = p;
            bots[i].vel = Vector3.zero;
        }
        kpRestore = false;

        if (missing == 0 || !refillEnabled || dissolving.Count > 0) return; // 무너지는 동안은 다시 채우지 않는다
        swarm.Collect(RSNanoSwarm.G_Blob, new Vector3(RootWorld().x, RootWorld().y, z), Mathf.Min(missing, refillPerFrame), tmp);
        int next = 0;
        for (int o = 0; o < fillOrder.Length && next < tmp.Count; o++)
        {
            int k = fillOrder[o];
            if (slotBot[k] >= 0) continue;
            Assign(k, tmp[next++], snap: false);
        }
    }

    private void Assign(int k, int bot, bool snap)
    {
        Vector2 w = PixelWorld(k, Time.time);
        var p = new Vector3(w.x, w.y, PlaneZ);
        swarm.Fire(bot, p, 9999f, Vector3.zero, 9999f, RSNanoSwarm.G_Blob, snap: snap, clump: 1);
        swarm.SetTint(bot, data.pixColor[k]);
        swarm.SetGlow(bot, BaseGlow(k));
        slotBot[k] = bot;
        slotArriving[k] = !snap;
        if (!snap)
        {
            arriveFrom[k] = swarm.Bots[bot].pos;
            arriveT0[k] = Time.time;
            arriveDur[k] = Mathf.Lerp(gatherSeconds.x, gatherSeconds.y, pixHash[k]);
        }
    }

    private float BaseGlow(int k)
    {
        byte kind = data.pixKind[k];
        return kind == (byte)MaestroPuppetData.PixelKind.Sparkle ? cur.sparkleGlow : kind == (byte)MaestroPuppetData.PixelKind.Glow ? cur.moonGlow : 0f;
    }

    private Vector2 PixelWorld(int k, float t)
    {
        int owner = data.pixOwner[k];
        if (owner >= 0)
        {
            Vector2 local = pixLocal[k];
            if (owner == iChest && extraW > 0f) local += ShoulderArt(mExtra.bShoulder) * shBW[k] + ShoulderArt(mExtra.oShoulder) * shOW[k]; // 어깨 주변 살이 어깨를 따라
            Vector2 art = bonePos[owner] + Rot(local, boneAng[owner]);
            if (owner == iChest && skinW[k] < 1f) art = Vector2.Lerp(LowerAt(pixRest[k]), art, skinW[k]); // 허리 띠: 하체 쪽과 섞음
            if (owner == iBaton && batonForm < 1f)
            {
                // 아직 안 생긴 부분은 손잡이 주위를 맴돌다가, 생기는 앞자리가 지나가면 제자리로 날아간다
                float front = batonForm * 1.25f;
                float w = Mathf.Clamp01((front - batonU[k]) / 0.25f);
                float ph = pixHash[k];
                float orbit = t * (6f + 4f * ph) + ph * 6.283f;
                Vector2 swirl = bonePos[owner] + new Vector2(Mathf.Cos(orbit), Mathf.Sin(orbit)) * (3f + 3f * ph) * cell;
                art = Vector2.Lerp(swirl, art, w * w * (3f - 2f * w));
            }
            return ArtToWorld(art);
        }
        if (owner == -1)
        {
            Vector2 p = LowerAt(pixLocal[k]); // 하체도 몸 휨을 따라 (위로 갈수록 더) + 허리·자락
            if (skinW[k] > 0f && iChest >= 0) // 허리 띠: 상체 쪽과 섞음 (상체가 꺾여도 이음새가 벌어지지 않게)
                p = Vector2.Lerp(p, bonePos[iChest] + Rot(pixRest[k] - chestPivArt, boneAng[iChest]), skinW[k]);
            byte kind = data.pixKind[k];
            float h = pixHash[k];
            if (kind == (byte)MaestroPuppetData.PixelKind.Drip)
            {
                // 아랫단 조각: 계속 흘러내리고 다시 위에서
                float fall = Mathf.Repeat(dripPhase * (0.6f + 0.8f * h) + h * dripRangePx, dripRangePx);
                p += new Vector2(Mathf.Sin(t * 1.7f + h * 20f) * 0.8f * cell, -fall * cell);
            }
            else if (kind == (byte)MaestroPuppetData.PixelKind.Sparkle)
            {
                p += new Vector2(Mathf.Sin(t * (0.6f + h) + h * 30f) * 2f, Mathf.Cos(t * (0.5f + 0.7f * h) + h * 17f) * 3f) * cell;
            }
            return ArtToWorld(p);
        }
        // 천
        var sim = cloth[(int)pixLocal[k].x];
        int j = Mathf.Clamp(clothSeg[k], 0, sim.pos.Length - 2);
        Vector2 a = sim.pos[j], b = sim.pos[j + 1];
        Vector2 dir = b - a;
        float len = dir.magnitude;
        dir = len > 1e-6f ? dir / len : Vector2.down;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        return Vector2.Lerp(a, b, clothT[k]) + perp * (clothD[k] * grow * Mathf.Sign(FlipX));
    }

    // ───────────────────────── 모션 에셋 · 모션 편집기 (11-25) ─────────────────────────
    // 몸짓 = 코드 키(SwingKeys 등) 또는 모션 에셋(MaestroMotion). 모션 에셋이 걸리면 채널·몸 기울기·이동·눌림을 에셋에서 읽는다.
    //  · PlayMotion(m) → 몸짓 Custom
    //  · motionSet에 연결된 몸짓(예: Swing → 내가 만든 휘두르기)은 패턴이 Play(Swing)를 걸면 모션이 대신 나온다
    //  · 편집기: EditBegin → 편집 중인 모션의 editTime 자세로 붙잡고, 손잡이(EditHandle)를 끌면 키 값을 거꾸로 풀어 넣는다(EditDrag)
    private MaestroMotion customMotion, activeMotion;
    private bool motionOn;
    private readonly float[] mCh = MaestroMotion.NewChannels();
    private float mLean, mStretch = 1f;
    private Vector2 mRoot;

    private bool editOn, editSnap = true, editStill = true;
    private MaestroMotion editMotion;
    private float editTime;

    private void EvaluateMotion()
    {
        activeMotion = null;
        float time = 0f;
        if (gesture == MaestroGesture.Custom)
        {
            activeMotion = customMotion;
            if (activeMotion != null)
                time = editOn ? editTime : activeMotion.loop || gestureLength <= 0f ? gestureTime : gestureTime / gestureLength * activeMotion.length;
        }
        else if (gesture != MaestroGesture.None && motionSet != null)
        {
            activeMotion = motionSet.Find(gesture);
            // 패턴이 건 길이에 비율대로 맞춘다 (나눠 건 몸짓은 감아올림·벰·버팀 경계 0.4 / 0.55도 그대로). 반복 모션은 실제 초로 돈다
            if (activeMotion != null) time = activeMotion.loop || (!timed && gestureLength <= 0f) ? gestureTime : GestureU * activeMotion.length;
        }
        motionOn = activeMotion != null;
        if (motionOn) activeMotion.Evaluate(time, mCh, out mLean, out mRoot, out mStretch, out mExtra);
    }

    // ── 추가 조종점 (어깨·허리·자락·머리 장식, 11-25 2차) — 모션이 끝나도 툭 끊기지 않게 무게를 천천히 뺀다 ──
    private MaestroMotion.Extra mExtra;
    private float extraW;
    private Vector2 moonFromHead;   // 머리 관절 → 장식 관절 (그림 공간 m)

    private void TickExtraWeight(float dt)
    {
        if (motionOn) extraW = gestureWeight;
        else extraW = Mathf.MoveTowards(extraW, 0f, dt * 6f);
        if (dt <= 0f && !motionOn) extraW = 0f;
    }

    // 키 대비 비율(x + = 앞) → 그림 공간 m (그림이 왼쪽을 보므로 앞 = −x)
    private Vector2 FwdArt(Vector2 r) => new Vector2(-r.x * ArtH, r.y * ArtH) * extraW;
    private Vector2 HipArt => FwdArt(mExtra.hip);
    // 어깨 들썩임 (그림 공간 m) — 너무 멀리 가면 몸에서 떨어져 보이므로 키의 maxShoulderShift까지
    private Vector2 ShoulderArt(Vector2 r) => Vector2.ClampMagnitude(FwdArt(r), maxShoulderShift * ArtH);
    // 하체 자리 (발밑 기준 그림 좌표 p → 몸 휨 + 허리·자락)
    private Vector2 LowerAt(Vector2 p) => rootArt + BendRoot(p) + LowerOffset(p.y);
    private Vector2[] pixRest = Array.Empty<Vector2>();
    private float[] skinW = Array.Empty<float>(), shBW = Array.Empty<float>(), shOW = Array.Empty<float>();
    private Vector2 chestPivArt;
    private float WaistY => iChest >= 0 ? Mathf.Max(0.05f, restOffset[iChest].y) : ArtH * 0.45f;

    /// <summary>하체(발밑에 붙은 픽셀·천) 높이 y에서의 추가 이동 — 허리 쪽은 상체를 따라, 아랫단 쪽은 자락을 따라</summary>
    private Vector2 LowerOffset(float y)
    {
        if (extraW <= 0f) return Vector2.zero;
        float u = Mathf.Clamp01(y / WaistY);
        return HipArt * (u * u * (3f - 2f * u)) + FwdArt(mExtra.hem) * Mathf.Pow(1f - u, 1.5f);
    }

    /// <summary>지금 걸린 모션 (없으면 null) — 디버그 표시용</summary>
    public MaestroMotion CurrentMotion => motionOn ? activeMotion : null;

    // ── 편집기 ──
    public bool EditActive => editOn;
    public MaestroMotion EditingMotion { get => editMotion; set => editMotion = value; }
    /// <summary>편집 중 보이는 시간 (초)</summary>
    public float EditTime { get => editTime; set => editTime = Mathf.Max(0f, value); }
    /// <summary>켜면 관절 스프링 없이 키 자세 그대로 (손잡이가 정확히 따라옴). 끄면 물리 반응을 보며 편집</summary>
    public bool EditSnap { get => editSnap; set => editSnap = value; }
    /// <summary>켜면 떠다니기·평소 흔들림·손가락 꼼지락 끔</summary>
    public bool EditStill { get => editStill; set => editStill = value; }

    public void EditBegin(MaestroMotion m)
    {
        editOn = true;
        editMotion = m;
        if (kpOn) PoseRelease(0.15f);
    }

    public void EditEnd()
    {
        editOn = false;
        gesture = MaestroGesture.None;
    }

    /// <summary>편집기 손잡이 (몸 → 팔 순서)</summary>
    public enum EditHandle { Root, Lean, Chest, Head, BatonElbow, BatonHand, BatonTip, OtherElbow, OtherHand, OtherFingers, BatonShoulder, OtherShoulder, Hip, Hem, Ornament }

    public static readonly string[] EditHandleNames =
    {
        "몸 이동", "몸 기울기", "상체", "머리", "지휘 팔꿈치", "지휘 손", "지휘봉 끝", "반대 팔꿈치", "반대 손", "반대 손끝",
        "지휘 어깨", "반대 어깨", "허리", "하체 자락", "머리 장식",
    };

    private Vector2 headAim, fingerAim, batonAim, moonAim;
    private bool aimReady;

    private void EnsureAims()
    {
        if (aimReady || data == null) return;
        headAim = RestCentroid(iHead, -99, -99) - ArtPx(data.bones[iHead].pivotPx);
        fingerAim = RestCentroid(iOW, iF1, iF2, iF3) - ArtPx(data.bones[iOW].pivotPx);
        batonAim = ArtPx(batonTipPx) - ArtPx(data.bones[iBW].pivotPx);
        moonAim = iMoon >= 0 ? RestCentroid(iMoon, -99, -99) - ArtPx(data.bones[iMoon].pivotPx) : Vector2.up * 0.1f;
        if (moonAim.sqrMagnitude < 1e-6f) moonAim = Vector2.up * 0.1f;
        aimReady = true;
    }

    private Vector2 RestCentroid(params int[] owners)
    {
        Vector2 sum = Vector2.zero;
        int n = 0;
        for (int k = 0; k < data.PixelCount; k++)
            if (Array.IndexOf(owners, (int)data.pixOwner[k]) >= 0) { sum += ArtPx(data.pixPos[k] + new Vector2(0.5f, 0.5f)); n++; }
        return n > 0 ? sum / n : ArtPx(data.bones[owners[0]].pivotPx);
    }

    private Vector2 WorldToArt(Vector2 w)
    {
        Vector2 f = FeetWorld();
        float sx = FlipX * grow / Mathf.Sqrt(stretch), sy = grow * stretch;
        return new Vector2((w.x - f.x) / sx, (w.y - f.y) / sy);
    }

    private Vector3 W3(Vector2 art) { Vector2 w = ArtToWorld(art); return new Vector3(w.x, w.y, PlaneZ); }
    private Vector2 BonePoint(int b, Vector2 local) => bonePos[b] + Rot(local, boneAng[b]);
    private float ArtH => data.artHeightPx * cell;

    /// <summary>손잡이 자리(월드)와 선을 그을 기준점(월드)</summary>
    public bool EditHandlePos(EditHandle h, out Vector3 pos, out Vector3 anchor)
    {
        pos = anchor = transform.position;
        if (!started || data == null || iChest < 0) return false;
        EnsureAims();
        switch (h)
        {
            case EditHandle.Root: pos = anchor = W3(rootArt + new Vector2(0f, RootHandleY)); return true; // 바닥 높이(묻힌 발밑보다 위)
            case EditHandle.Lean: anchor = W3(rootArt); pos = W3(rootArt + BendRoot(new Vector2(0f, ArtH * 1.08f))); return true;
            case EditHandle.Chest: anchor = W3(bonePos[iChest]); pos = W3(bonePos[iHead]); return true;
            case EditHandle.Head: anchor = W3(bonePos[iHead]); pos = W3(BonePoint(iHead, headAim)); return true;
            case EditHandle.BatonElbow: anchor = W3(bonePos[iBS]); pos = W3(bonePos[iBE]); return true;
            case EditHandle.BatonHand: anchor = W3(bonePos[iBE]); pos = W3(bonePos[iBW]); return true;
            case EditHandle.BatonTip: anchor = W3(bonePos[iBW]); pos = W3(BonePoint(iBW, batonAim)); return true;
            case EditHandle.OtherElbow: anchor = W3(bonePos[iOS]); pos = W3(bonePos[iOE]); return true;
            case EditHandle.OtherHand: anchor = W3(bonePos[iOE]); pos = W3(bonePos[iOW]); return true;
            case EditHandle.OtherFingers: anchor = W3(bonePos[iOW]); pos = W3(BonePoint(iOW, fingerAim)); return true;
            case EditHandle.BatonShoulder: anchor = W3(bonePos[iChest]); pos = W3(bonePos[iBS]); return true;
            case EditHandle.OtherShoulder: anchor = W3(bonePos[iChest]); pos = W3(bonePos[iOS]); return true;
            case EditHandle.Hip: anchor = W3(rootArt + BendRoot(new Vector2(0f, WaistY))); pos = W3(bonePos[iChest]); return true;
            case EditHandle.Hem: anchor = W3(rootArt + BendRoot(new Vector2(0f, WaistY)) + LowerOffset(WaistY)); pos = W3(HemPoint()); return true;
            case EditHandle.Ornament: if (iMoon < 0) return false; anchor = W3(bonePos[iMoon]); pos = W3(BonePoint(iMoon, moonAim)); return true;
        }
        return false;
    }

    /// <summary>
    /// 손잡이를 world로 끌었을 때 key 값을 고친다 — 지금 보이는 자세에서 바뀐 만큼만 더한다(스프링·흔들림이 켜져 있어도 맞게 수렴).
    /// 팔꿈치 = 어깨를 돌림, 손 = 어깨·팔꿈치 2관절 IK(팔꿈치 굽는 쪽은 지금 쪽 유지), 손끝·지휘봉 끝 = 손목을 돌림
    /// </summary>
    public bool EditDrag(EditHandle h, Vector3 world, MaestroMotion.Key key)
    {
        if (!started || key == null || data == null || iChest < 0) return false;
        EnsureAims();
        key.EnsureSize();
        Vector2 T = WorldToArt(world);
        switch (h)
        {
            case EditHandle.Root:
            {
                Vector2 d = T - rootArt;
                key.root += new Vector2(-d.x / ArtH, d.y / ArtH);
                return true;
            }
            case EditHandle.Lean:
            {
                Vector2 v = T - rootArt;
                if (v.sqrMagnitude < 1e-6f) return false;
                float want = Mathf.Atan2(-v.x, v.y) * Mathf.Rad2Deg; // + = 앞(그림 −x)으로 기움
                key.lean += Mathf.DeltaAngle(rootLean, want);
                return true;
            }
            case EditHandle.Chest: return Aim(iChest, restOffset[iHead], T, key, 0, chestScale);
            case EditHandle.Head: return Aim(iHead, headAim, T, key, 1, 1f);
            case EditHandle.BatonElbow: return Aim(iBS, restOffset[iBE], T, key, 2, 1f);
            case EditHandle.BatonHand: return TwoBone(iBS, iBE, iBW, T, key, 2, 3);
            case EditHandle.BatonTip: return Aim(iBW, batonAim, T, key, 4, 1f);
            case EditHandle.OtherElbow: return Aim(iOS, restOffset[iOE], T, key, 7, 1f);
            case EditHandle.OtherHand: return TwoBone(iOS, iOE, iOW, T, key, 7, 8);
            case EditHandle.OtherFingers: return Aim(iOW, fingerAim, T, key, 9, 1f);
            case EditHandle.BatonShoulder: key.bShoulder = Vector2.ClampMagnitude(key.bShoulder + ChestLocalDelta(T - bonePos[iBS]) / Mathf.Max(0.05f, extraW), maxShoulderShift); return true;
            case EditHandle.OtherShoulder: key.oShoulder = Vector2.ClampMagnitude(key.oShoulder + ChestLocalDelta(T - bonePos[iOS]) / Mathf.Max(0.05f, extraW), maxShoulderShift); return true;
            case EditHandle.Hip:
            {
                Vector2 d = T - bonePos[iChest];
                key.hip += new Vector2(-d.x / ArtH, d.y / ArtH) / Mathf.Max(0.05f, extraW);
                return true;
            }
            case EditHandle.Hem:
            {
                Vector2 d = T - HemPoint();
                float w = Mathf.Pow(1f - Mathf.Clamp01(HemY / WaistY), 1.5f) * Mathf.Max(0.05f, extraW); // 자락 손잡이 높이에서 hem이 먹는 비율
                key.hem += new Vector2(-d.x / ArtH, d.y / ArtH) / Mathf.Max(0.05f, w);
                return true;
            }
            case EditHandle.Ornament:
            {
                if (iMoon < 0) return false;
                if (!AimDelta(iMoon, moonAim, T, out float delta)) return false;
                key.moon += delta / Mathf.Max(0.05f, extraW);
                return true;
            }
        }
        return false;
    }

    // 뼈 b를 돌려 local 점이 T를 향하게 — 채널 ch에 바뀐 각도만큼 (scale = 채널 → 각도 배율)
    private bool Aim(int b, Vector2 local, Vector2 T, MaestroMotion.Key key, int ch, float scale)
    {
        if (!AimDelta(b, local, T, out float delta)) return false;
        key.ch[ch] += delta / Mathf.Max(0.01f, scale);
        return true;
    }

    private bool AimDelta(int b, Vector2 local, Vector2 T, out float delta)
    {
        delta = 0f;
        if (b < 0) return false;
        Vector2 d = T - bonePos[b];
        if (d.sqrMagnitude < 1e-8f || local.sqrMagnitude < 1e-8f) return false;
        float want = (Mathf.Atan2(d.y, d.x) - Mathf.Atan2(local.y, local.x)) * Mathf.Rad2Deg;
        delta = Mathf.DeltaAngle(boneAng[b] * Mathf.Rad2Deg, want);
        return true;
    }

    // 그림 공간 이동 d → 상체 기준(상체가 기운 만큼 되돌림) 키 대비 비율 (x + = 앞)
    private Vector2 ChestLocalDelta(Vector2 d)
    {
        Vector2 l = Rot(d, -boneAng[iChest]);
        return new Vector2(-l.x / ArtH, l.y / ArtH);
    }

    // 몸 이동 손잡이 높이 = 바닥(그림을 묻은 만큼 위), 하체 자락 손잡이 높이 = 바닥 조금 위
    private float RootHandleY => sinkPx * cell;
    private float HemY => Mathf.Min(sinkPx * cell + ArtH * 0.06f, WaistY * 0.5f);
    private Vector2 HemPoint() => rootArt + BendRoot(new Vector2(0f, HemY)) + LowerOffset(HemY);

    // 2관절 IK: 위팔 a(자식 관절 b) · 아래팔 b(자식 관절 c)로 c 관절이 T에 오게
    private bool TwoBone(int a, int b, int c, Vector2 T, MaestroMotion.Key key, int chA, int chB)
    {
        if (a < 0 || b < 0 || c < 0) return false;
        Vector2 S = bonePos[a];
        float l1 = restOffset[b].magnitude, l2 = restOffset[c].magnitude;
        if (l1 < 1e-6f || l2 < 1e-6f) return false;
        Vector2 toT = T - S;
        float dist = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
        float baseAng = Mathf.Atan2(toT.y, toT.x);
        float cosA = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
        float alpha = Mathf.Acos(cosA);
        // 팔꿈치 후보 두 곳 중 지금 팔꿈치에 가까운 쪽 (굽는 방향이 갑자기 뒤집히지 않게)
        Vector2 e1 = S + new Vector2(Mathf.Cos(baseAng + alpha), Mathf.Sin(baseAng + alpha)) * l1;
        Vector2 e2 = S + new Vector2(Mathf.Cos(baseAng - alpha), Mathf.Sin(baseAng - alpha)) * l1;
        Vector2 E = (e1 - bonePos[b]).sqrMagnitude <= (e2 - bonePos[b]).sqrMagnitude ? e1 : e2;
        Vector2 Tc = S + toT.normalized * dist;

        Vector2 ob = restOffset[b], oc = restOffset[c];
        float wantA = (Mathf.Atan2(E.y - S.y, E.x - S.x) - Mathf.Atan2(ob.y, ob.x)) * Mathf.Rad2Deg;
        float wantB = (Mathf.Atan2(Tc.y - E.y, Tc.x - E.x) - Mathf.Atan2(oc.y, oc.x)) * Mathf.Rad2Deg;
        float curA = boneAng[a] * Mathf.Rad2Deg, curB = boneAng[b] * Mathf.Rad2Deg;
        key.ch[chA] += Mathf.DeltaAngle(curA, wantA);
        key.ch[chB] += Mathf.DeltaAngle(curB - curA, wantB - wantA);
        return true;
    }

    /// <summary>모션 이벤트 기준 자리 (월드, 지금 보이는 자세)</summary>
    public Vector3 MotionAnchorWorld(MaestroMotion.Anchor a)
    {
        if (!started || data == null || iChest < 0) return transform.position;
        EnsureAims();
        switch (a)
        {
            case MaestroMotion.Anchor.BatonTip: return BatonTip;
            case MaestroMotion.Anchor.BatonHand: return W3(bonePos[iBW]);
            case MaestroMotion.Anchor.OtherHand: return W3(BonePoint(iOW, fingerAim));
            case MaestroMotion.Anchor.Head: return W3(BonePoint(iHead, headAim));
            case MaestroMotion.Anchor.Feet: return W3(rootArt + new Vector2(0f, RootHandleY));
            default: return BodyCenter;
        }
    }

    /// <summary>모션 좌표(키 대비, x + = 앞) → 월드 이동량. 보스가 보는 쪽·크기 반영</summary>
    public Vector3 MotionToWorldDelta(Vector2 r)
    {
        float h = (data != null ? data.artHeightPx : 150f) * cell * grow;
        float front = -Mathf.Sign(FlipX == 0f ? 1f : FlipX); // 그림 −x = 앞
        return new Vector3(r.x * h * front, r.y * h, 0f);
    }

    /// <summary>월드 이동량 → 모션 좌표 (MotionToWorldDelta의 반대)</summary>
    public Vector2 WorldDeltaToMotion(Vector3 d)
    {
        float h = Mathf.Max(1e-4f, (data != null ? data.artHeightPx : 150f) * cell * grow);
        float front = -Mathf.Sign(FlipX == 0f ? 1f : FlipX);
        return new Vector2(d.x / (h * front), d.y / h);
    }

    /// <summary>뼈대 그리기용 — 뼈 b 관절과 부모 관절 (월드)</summary>
    public int EditBoneCount => started ? bonePos.Length : 0;
    public bool EditBone(int b, out Vector3 pos, out Vector3 parentPos)
    {
        pos = parentPos = transform.position;
        if (!started || b < 0 || b >= bonePos.Length) return false;
        pos = W3(bonePos[b]);
        int p = data.bones[b].parent;
        parentPos = p >= 0 ? W3(bonePos[p]) : W3(rootArt);
        return true;
    }

    /// <summary>
    /// 코드에 있던 몸짓을 모션 에셋으로 꺼낸다 (편집 출발점). 채널 키·몸 기울기·눌림·베기 몸 이동의 키 시간을 합쳐 그 시간마다 값을 찍는다
    /// → 원래 키 시간에서는 똑같고, 그 사이는 거의 같다. 평소 지휘(None)·격렬한 지휘(ConductFast)는 박자 기반이라 못 꺼낸다
    /// </summary>
    public bool ExportGesture(MaestroGesture g, MaestroMotion m, float seconds)
    {
        Key[] keys; Vector2[] lean; Vector2[] squash = null;
        switch (g)
        {
            case MaestroGesture.Swing: keys = SwingKeys; lean = LeanSwing; break;
            case MaestroGesture.Spread: keys = SpreadKeys; lean = LeanSpread; break;
            case MaestroGesture.Strike: keys = StrikeKeys; lean = LeanStrike; squash = SquashStrike; break;
            case MaestroGesture.SlashLeft: keys = SlashLeftKeys; lean = LeanSlashLeft; squash = SquashSlash; break;
            case MaestroGesture.SlashRight: keys = SlashRightKeys; lean = LeanSlashRight; squash = SquashSlash; break;
            case MaestroGesture.RaiseBoth: keys = RaiseKeys; lean = LeanRaise; break;
            default: return false;
        }
        bool slash = g == MaestroGesture.SlashLeft || g == MaestroGesture.SlashRight;
        var times = new SortedSet<float>();
        foreach (var k in keys) times.Add(k.t);
        foreach (var v in lean) times.Add(v.x);
        if (squash != null) foreach (var v in squash) times.Add(v.x);
        if (slash) { times.Add(0.4f); times.Add(0.55f); }

        m.keys.Clear();
        m.length = Mathf.Max(0.05f, seconds);
        m.loop = false;
        m.crisp = g == MaestroGesture.Spread || g == MaestroGesture.Strike || slash;
        float fwd = g == MaestroGesture.SlashLeft ? -1f : 1f;
        foreach (float u in times)
        {
            var key = new MaestroMotion.Key { time = u * m.length };
            EvalKeys(keys, u, key.ch);
            key.lean = Piece(u, lean) * bodyLeanScale;
            key.stretch = squash != null ? 1f + (Piece(u, squash) - 1f) * squashScale : 1f;
            if (slash)
            {
                // GestureRoot와 같은 식 (키 대비) → 모션 root (x + = 앞 = 그림 −x)
                Vector2 ready = new Vector2(-fwd * slashRootBack, -slashCrouch), lunge = new Vector2(fwd * slashRootLunge, -slashCrouch * 0.3f), v;
                if (u < 0.4f) v = Vector2.Lerp(Vector2.zero, ready, Mathf.SmoothStep(0f, 1f, u / 0.4f));
                else if (u < 0.55f) v = Vector2.Lerp(ready, lunge, Mathf.SmoothStep(0f, 1f, (u - 0.4f) / 0.15f));
                else v = Vector2.Lerp(lunge, Vector2.zero, Mathf.SmoothStep(0f, 1f, (u - 0.55f) / 0.45f));
                key.root = new Vector2(-v.x, v.y);
            }
            m.keys.Add(key);
        }
        m.memo = $"코드 몸짓 '{g}'에서 가져옴 ({System.DateTime.Now:yyyy-MM-dd})";
        return true;
    }

    // ───────────────────────── 키 포즈 (11-25 사용자 결정 B-2) ─────────────────────────
    // 정면 그림 한 장(퍼펫)으로는 몸을 비틀고 쭉 뻗는 자세가 안 나와서, 모션 시트의 키 포즈 그림 자리로 봇을 흘려 보낸다.
    // 가상 봇 i < N = 그림 봇(퍼펫 픽셀 i), i ≥ N = 추가 봇(남는 봇을 잠깐 빌림 — 퍼펫 픽셀 extraSource에서 나와 그리로 돌아감).
    // 봇마다 무게 kpW(0 = 퍼펫 자리, 1 = 포즈 자리)와 포즈 자리 kpPos를 매 프레임 정한다. 바뀌는 도중에 새 포즈가 와도
    // 지금 보이는 자리에서 출발하므로 튀지 않는다.
    private bool kpReady, kpOn, kpRestore, kpSnappy, kpMirror;
    private int kpTarget = -1;
    private float kpT0, kpDur = 1f, kpWTo, kpAvgW;
    private Vector2[] kpFrom = Array.Empty<Vector2>(), kpPos = Array.Empty<Vector2>();
    private Color32[] kpFromCol = Array.Empty<Color32>(), kpCol = Array.Empty<Color32>();
    private float[] kpWFrom = Array.Empty<float>(), kpW = Array.Empty<float>(), kpHash = Array.Empty<float>();
    private int[] extraBot = Array.Empty<int>();
    private Vector2[] kpTip = Array.Empty<Vector2>();   // 포즈마다 베는 쪽 손끝 (그림 픽셀)
    private readonly Dictionary<int, Sprite> kpSprite = new Dictionary<int, Sprite>();

    /// <summary>키 포즈를 쓸 수 있나 (없으면 베기는 예전 퍼펫 몸짓)</summary>
    public bool HasKeyPoses => kpReady;
    /// <summary>지금 키 포즈 이름 (디버그)</summary>
    public string CurrentKeyPose => kpOn && kpTarget >= 0 ? poseSet.poses[kpTarget].name + (kpMirror ? " (좌우반전)" : "") : kpOn ? "풀리는 중" : "-";
    /// <summary>키 포즈 이름 목록 (디버그)</summary>
    public int KeyPoseCount => kpReady ? poseSet.poses.Length : 0;
    public string KeyPoseName(int p) => kpReady && p >= 0 && p < poseSet.poses.Length ? poseSet.poses[p].name : "";

    private void InitPoses()
    {
        kpReady = poseSet != null && poseSet.Valid(pixelCount);
        if (poseSet != null && !kpReady) Debug.LogWarning("[Maestro] 키 포즈가 퍼펫 그림과 안 맞음 — 빌더 다시 실행 (그동안 베기는 퍼펫 몸짓으로)");
        if (!kpReady) return;
        int V = poseSet.virtualCount;
        kpFrom = new Vector2[V]; kpPos = new Vector2[V];
        kpFromCol = new Color32[V]; kpCol = new Color32[V];
        kpWFrom = new float[V]; kpW = new float[V]; kpHash = new float[V];
        for (int i = 0; i < V; i++) kpHash[i] = Mathf.Repeat(Mathf.Sin(i * 78.233f + 1.7f) * 43758.5453f, 1f);
        extraBot = new int[poseSet.ExtraCount];
        for (int e = 0; e < extraBot.Length; e++) extraBot[e] = -1;

        // 베는 쪽 손끝 = 포즈 방향 쪽으로 가장 먼 픽셀 (아래 30%는 빼고) — 전조 번쩍임 자리
        kpTip = new Vector2[poseSet.poses.Length];
        for (int p = 0; p < kpTip.Length; p++)
        {
            var pose = poseSet.poses[p];
            float dir = pose.dir != 0 ? Mathf.Sign(pose.dir) : -1f;
            Vector2 best = new Vector2(poseSet.mirrorXPx, data.artHeightPx * 0.4f);
            float bestS = float.NegativeInfinity;
            foreach (var q in pose.pos)
            {
                if (q.y > data.artHeightPx * 0.7f) continue;
                if (q.x * dir > bestS) { bestS = q.x * dir; best = q; }
            }
            kpTip[p] = best;
        }
        ResetKeyPose();
        Debug.Log($"[Maestro] 키 포즈 {poseSet.poses.Length}개 (가상 봇 {V}, 빌릴 봇 {extraBot.Length})");
    }

    private void ResetKeyPose()
    {
        kpOn = false; kpTarget = -1; kpWTo = 0f; kpAvgW = 0f;
        for (int i = 0; i < kpW.Length; i++) { kpW[i] = 0f; kpWFrom[i] = 0f; }
        kpRestore = true;
    }

    private Vector2 KeyTarget(int pose, int i, bool mirror)
    {
        var q = poseSet.poses[pose].pos[i];
        if (mirror) q.x = 2f * poseSet.mirrorXPx - q.x;
        return q;
    }

    private Vector2 KeyPoseTip(int pose, bool mirror)
    {
        var q = kpTip[pose];
        if (mirror) q.x = 2f * poseSet.mirrorXPx - q.x;
        return q;
    }

    // 포즈 그림 방향과 베는 방향(화면)이 다르면 좌우를 뒤집는다
    private bool KeyPoseMirror(int p, int dir)
    {
        if (p < 0 || dir == 0) return false;
        int pd = poseSet.poses[p].dir;
        return pd != 0 && Mathf.Sign(pd * FlipX) != Mathf.Sign(dir);
    }

    /// <summary>
    /// 키 포즈 pose로 seconds초에 흘러간다 (봇마다 조금씩 늦게 출발). snappy = 확 출발해 감속하며 꽂힘(타격), 아니면 부드럽게.
    /// mirror = 좌우 반전. 퍼펫 상태에서 부르면 퍼펫 → 포즈, 포즈 중이면 지금 보이는 자리 → 새 포즈
    /// </summary>
    public void PoseTo(int pose, float seconds, bool mirror, bool snappy = false)
    {
        if (!kpReady || !started || pose < 0 || pose >= poseSet.poses.Length) return;
        int V = poseSet.virtualCount;
        var tp = poseSet.poses[pose];
        for (int i = 0; i < V; i++)
        {
            if (kpOn) { kpFrom[i] = kpPos[i]; kpFromCol[i] = kpCol[i]; }
            else { kpFrom[i] = KeyTarget(pose, i, mirror); kpFromCol[i] = tp.col[i]; kpPos[i] = kpFrom[i]; kpCol[i] = kpFromCol[i]; }
            kpWFrom[i] = kpW[i];
        }
        kpTarget = pose; kpMirror = mirror; kpSnappy = snappy;
        kpWTo = 1f; kpT0 = Time.time; kpDur = Mathf.Max(0.03f, seconds);
        kpOn = true;
        BorrowExtras();
    }

    /// <summary>키 포즈를 풀고 seconds초에 퍼펫(평소 모습)으로 돌아간다</summary>
    public void PoseRelease(float seconds)
    {
        if (!kpOn) return;
        for (int i = 0; i < kpW.Length; i++) { kpFrom[i] = kpPos[i]; kpFromCol[i] = kpCol[i]; kpWFrom[i] = kpW[i]; }
        kpTarget = -1; kpWTo = 0f; kpSnappy = false;
        kpT0 = Time.time; kpDur = Mathf.Max(0.03f, seconds);
    }

    /// <summary>베기 hit번째(0부터)의 준비·전환 포즈로 (감아올리는 동안). dir = 화면 기준 베는 방향</summary>
    public void PoseSlashPrep(int hit, int dir, float seconds)
    {
        if (!kpReady) return;
        int p = poseSet.SlashPose(hit, false);
        PoseTo(p, seconds, KeyPoseMirror(p, dir));
    }

    /// <summary>베기 hit번째의 타격 포즈로 확 꽂힌다</summary>
    public void PoseSlashHit(int hit, int dir, float seconds)
    {
        if (!kpReady) return;
        int p = poseSet.SlashPose(hit, true);
        PoseTo(p, seconds, KeyPoseMirror(p, dir), snappy: true);
    }

    /// <summary>디버그 — 포즈 p를 잡아 본다 (-1 = 풀기)</summary>
    public void DebugPose(int p, bool mirror)
    {
        if (p < 0) PoseRelease(0.4f);
        else PoseTo(p, 0.35f, mirror);
    }

    private void TickKeyPose(float t)
    {
        if (!kpOn) return;
        float u = Mathf.Clamp01((t - kpT0) / kpDur);
        int V = poseSet.virtualCount, N = pixelCount;
        float st = Mathf.Clamp(poseStagger, 0f, 0.8f);
        var tp = kpTarget >= 0 ? poseSet.poses[kpTarget] : null;
        float sum = 0f;
        bool any = kpWTo > 0f;
        for (int i = 0; i < V; i++)
        {
            float e = Mathf.Clamp01((u - st * kpHash[i]) / (1f - st));
            e = kpSnappy ? 1f - (1f - e) * (1f - e) * (1f - e) : e * e * (3f - 2f * e);
            Vector2 to = tp != null ? KeyTarget(kpTarget, i, kpMirror) : kpFrom[i];
            Vector2 p = Vector2.LerpUnclamped(kpFrom[i], to, e);
            if (poseArcPx > 0f && e > 0f && e < 1f)
            {
                // 곧게 말고 옆으로 살짝 휘어 흘러간다 (봇마다 휘는 쪽·크기 다르게)
                Vector2 d = to - kpFrom[i];
                float len = d.magnitude;
                if (len > 0.5f) p += new Vector2(-d.y, d.x) / len * (Mathf.Sin(e * Mathf.PI) * poseArcPx * (kpHash[i] - 0.5f) * 2f * Mathf.Min(1f, len / 20f));
            }
            kpPos[i] = p;
            kpCol[i] = tp != null ? Color32.Lerp(kpFromCol[i], tp.col[i], e) : kpFromCol[i];
            float w = Mathf.Lerp(kpWFrom[i], kpWTo, e);
            kpW[i] = w;
            if (i < N) sum += w;
            if (w > 0.001f) any = true;
        }
        kpAvgW = sum / Mathf.Max(1, N);
        if (!any && u >= 1f)
        {
            kpOn = false;
            kpTarget = -1;
            kpAvgW = 0f;
            kpRestore = true;
            ReleaseExtras();
        }
    }

    private Vector2 KeyPoseWorld(int i, float t)
    {
        float h = kpHash[i];
        var px = kpPos[i] + new Vector2(0.5f + Mathf.Sin(t * (5f + 3f * h) + h * 40f) * poseJitterPx,
                                        0.5f + Mathf.Cos(t * (4f + 3f * h) + h * 25f) * poseJitterPx);
        return KeyPoseArtWorld(px);
    }

    // 포즈 그림 픽셀 → 월드 (떠다니기·크기·방향·눌림은 퍼펫과 같게 + 숨쉬기)
    private Vector2 KeyPoseArtWorld(Vector2 px)
    {
        Vector2 art = ArtPx(px);
        art.y *= 1f + poseBreath * Mathf.Sin(Time.time * 2.4f);
        return ArtToWorld(rootArt + art);
    }

    // 포즈 그림이 퍼펫보다 픽셀이 많아 모자란 만큼 남는 봇을 빌린다 — 퍼펫 픽셀 위(같은 색)에서 나와 눈에 안 띄게 갈라져 나간다
    private void BorrowExtras()
    {
        if (extraBot.Length == 0 || !refillEnabled || dissolving.Count > 0) return;
        int need = 0;
        for (int e = 0; e < extraBot.Length; e++) if (extraBot[e] < 0) need++;
        if (need == 0) return;
        float t = Time.time;
        swarm.Collect(RSNanoSwarm.G_Blob, new Vector3(RootWorld().x, RootWorld().y, PlaneZ), need, tmp);
        int n = 0;
        for (int e = 0; e < extraBot.Length && n < tmp.Count; e++)
        {
            if (extraBot[e] >= 0) continue;
            int b = tmp[n++];
            int k = poseSet.extraSource[e];
            Vector2 w = PixelWorld(k, t);
            swarm.Fire(b, new Vector3(w.x, w.y, PlaneZ), 9999f, Vector3.zero, 9999f, RSNanoSwarm.G_Blob, snap: true, clump: 1);
            swarm.SetTint(b, data.pixColor[k]);
            swarm.SetGlow(b, 0f);
            extraBot[e] = b;
        }
    }

    private void ReleaseExtras()
    {
        var bots = swarm.Bots;
        for (int e = 0; e < extraBot.Length; e++)
        {
            int b = extraBot[e];
            extraBot[e] = -1;
            if (b >= 0 && b < bots.Length && bots[b].mode == BotMode.Free) swarm.Bind(b, RSNanoSwarm.G_Blob);
        }
    }

    private void PlaceExtras(float t)
    {
        if (!kpOn || extraBot.Length == 0) return;
        var bots = swarm.Bots;
        float z = PlaneZ;
        int N = pixelCount;
        for (int e = 0; e < extraBot.Length; e++)
        {
            int b = extraBot[e];
            if (b < 0) continue;
            if (b >= bots.Length || bots[b].mode != BotMode.Free) { extraBot[e] = -1; continue; }
            int i = N + e, k = poseSet.extraSource[e];
            float w = kpW[i];
            Vector2 pos = Vector2.Lerp(PixelWorld(k, t), KeyPoseWorld(i, t), w);
            var p = new Vector3(pos.x, pos.y, z);
            bots[b].hold = p;
            bots[b].pos = p;
            bots[b].vel = Vector3.zero;
            bots[b].wait = 9999f;
            swarm.SetTint(b, Color32.Lerp(data.pixColor[k], kpCol[i], w));
        }
    }

    /// <summary>
    /// 지금 키 포즈 모양의 잔상 (시트 '잔상' — 돌진하며 베면 뒤에 실루엣이 남는다). color 실루엣이 life초에 흐려진다.
    /// 지금 자리에 남으므로, 보스가 움직이는 동안 몇 번 부르면 지나온 길에 겹겹이 남는다
    /// </summary>
    public void PoseAfterimage(float life, Color color)
    {
        if (!kpOn || kpTarget < 0 || swarm == null) return;
        var f = fx != null ? fx : MaestroFx.Get(this); // fx 필드는 UpdateFx가 처음 채운다 (그림자 등 같이 빌림)
        var spr = KeyPoseSprite(kpTarget);
        if (spr == null || f == null) return;
        Vector2 o = ArtToWorld(rootArt); // 그림 원점(발밑)
        float sq = Mathf.Sqrt(stretch);
        float sx = cell * grow * FlipX / sq, sy = cell * grow * stretch;
        if (kpMirror)
        {
            sx = -sx; // 발밑 기준으로 뒤집고, 뒤집는 기준(가슴)과의 차이만큼 옮긴다
            o.x += FlipX * grow * 2f * (poseSet.mirrorXPx - data.feetXPx) * cell / sq;
        }
        f.Flash(spr, new Vector3(o.x, o.y, PlaneZ), new Vector2(sx, sy), 0f, color, life);
    }

    // 포즈 실루엣 스프라이트 (흰색, 픽셀 하나 = 1단위, 기준점 = 그림 원점) — 처음 쓸 때 만든다
    private Sprite KeyPoseSprite(int p)
    {
        if (kpSprite.TryGetValue(p, out var s) && s != null) return s;
        var pose = poseSet.poses[p];
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var q in pose.pos)
        {
            int x = Mathf.FloorToInt(q.x), y = Mathf.FloorToInt(q.y);
            if (x < minX) minX = x; if (x > maxX) maxX = x;
            if (y < minY) minY = y; if (y > maxY) maxY = y;
        }
        if (maxX < minX) return null;
        int w = maxX - minX + 1, h = maxY - minY + 1;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var px = new Color32[w * h];
        var white = new Color32(255, 255, 255, 255);
        foreach (var q in pose.pos)
        {
            int x = Mathf.FloorToInt(q.x) - minX, yTop = Mathf.FloorToInt(q.y) - minY;
            px[(h - 1 - yTop) * w + x] = white;
        }
        tex.SetPixels32(px);
        tex.Apply();
        var pivot = new Vector2((data.feetXPx - minX) / w, (maxY + 1 - data.artHeightPx) / (float)h);
        s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, 1f);
        s.hideFlags = HideFlags.DontSave;
        kpSprite[p] = s;
        return s;
    }

    // ── 좌표 ──
    private Vector2 ArtPx(Vector2 px) => new Vector2((px.x - data.feetXPx) * cell, (data.artHeightPx - px.y) * cell);
    private static Vector2 Rot(Vector2 v, float a) { float c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y); }
    private Vector2 FeetWorld() => new Vector2(feet.x + poseOffset.x, feet.y + poseOffset.y - sinkPx * cell * grow);
    private Vector2 RootWorld() => ArtToWorld(rootArt);
    private Vector2 ArtToWorld(Vector2 art) { Vector2 f = FeetWorld(); return new Vector2(f.x + art.x * FlipX * grow / Mathf.Sqrt(stretch), f.y + art.y * grow * stretch); } // stretch: 눌림·늘어남 (부피 유지)

    // 4박 지휘 각도 (평소 크기 1 기준) — 1 아래(+) · 2 안 · 3 밖 · 4 위(-)
    private static readonly float[] BeatAngles = { 1.2f, -0.3f, 0.4f, -1f };

    private static float Ease(float f) => f < 0.7f ? Mathf.SmoothStep(0f, 0.55f, f / 0.7f) : Mathf.Lerp(0.55f, 1f, Mathf.Pow((f - 0.7f) / 0.3f, 0.6f));

    private static float CatmullRom(float p0, float p1, float p2, float p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
